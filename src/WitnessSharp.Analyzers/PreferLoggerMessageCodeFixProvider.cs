using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace WitnessSharp.Analyzers;

/// <summary>
/// Provides a code fix for <c>WS0001</c> that converts ad-hoc <c>ILogger</c>
/// calls into the high-performance <c>[LoggerMessage]</c> source-generated pattern.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferLoggerMessageCodeFixProvider)), Shared]
public sealed class PreferLoggerMessageCodeFixProvider : CodeFixProvider
{
    private const string Title = "Convert to [LoggerMessage] pattern";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("WS0001");

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var invocation = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true) as InvocationExpressionSyntax;
        if (invocation is null)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null || !TryCreateScaffold(semanticModel, invocation, context.CancellationToken, out _))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                cancellationToken => ApplyFixAsync(context.Document, invocation, cancellationToken),
                equivalenceKey: Title),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ApplyFixAsync(Document document, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null || !TryCreateScaffold(semanticModel, invocation, cancellationToken, out var scaffold))
        {
            return document;
        }

        var containingType = invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (containingType is null)
        {
            return document;
        }

        var trackedRoot = root.TrackNodes(invocation, containingType);
        var currentInvocation = trackedRoot.GetCurrentNode(invocation);
        if (currentInvocation is null)
        {
            return document;
        }

        var updatedRoot = trackedRoot.ReplaceNode(currentInvocation, scaffold.ReplacementInvocation.WithTriviaFrom(currentInvocation));
        var currentType = updatedRoot.GetCurrentNode(containingType);
        if (currentType is null)
        {
            return document;
        }

        var updatedType = currentType
            .AddMembers(scaffold.GeneratedMethod.WithAdditionalAnnotations(Formatter.Annotation))
            .WithAdditionalAnnotations(Formatter.Annotation);

        updatedRoot = updatedRoot.ReplaceNode(currentType, updatedType);
        return document.WithSyntaxRoot(updatedRoot);
    }

    private static bool TryCreateScaffold(SemanticModel semanticModel, InvocationExpressionSyntax invocation, CancellationToken cancellationToken, out LoggerMessageScaffold scaffold)
    {
        scaffold = default;

        if (invocation.Expression is not MemberAccessExpressionSyntax methodAccess ||
            methodAccess.Expression is not MemberAccessExpressionSyntax loggerAccess)
        {
            return false;
        }

        var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        var containingType = invocation.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        var methodSymbol = LoggerAnalysisHelpers.GetInvokedMethod(semanticModel, invocation, cancellationToken);
        if (containingMethod is null || containingType is null || methodSymbol is null || !LoggerAnalysisHelpers.IsSupportedLoggerMethod(methodSymbol))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (!LoggerAnalysisHelpers.TryGetLogLevelExpression(semanticModel, methodSymbol, arguments, cancellationToken, out var logLevelExpression, out var searchStartIndex))
        {
            return false;
        }

        if (!LoggerAnalysisHelpers.TryFindMessageTemplate(semanticModel, arguments, searchStartIndex, cancellationToken, out var templateIndex, out var template))
        {
            return false;
        }

        var prefixArguments = arguments.Skip(searchStartIndex).Take(templateIndex - searchStartIndex).ToImmutableArray();
        var exceptionArguments = prefixArguments
            .Where(argument => LoggerAnalysisHelpers.IsExceptionType(semanticModel.GetTypeInfo(argument.Expression, cancellationToken).ConvertedType ?? semanticModel.GetTypeInfo(argument.Expression, cancellationToken).Type))
            .ToImmutableArray();
        if (prefixArguments.Length != exceptionArguments.Length || exceptionArguments.Length > 1)
        {
            return false;
        }

        ArgumentSyntax? exceptionArgument = exceptionArguments.Length == 1 ? exceptionArguments[0] : null;
        var valueArguments = arguments.Skip(templateIndex + 1).ToImmutableArray();
        var placeholderNames = LoggerAnalysisHelpers.ExtractPlaceholders(template);
        if (placeholderNames.Count != valueArguments.Length)
        {
            return false;
        }

        var methodName = CreateMethodName(containingType, containingMethod.Identifier.ValueText);
        var usedNames = new HashSet<string>(StringComparer.Ordinal) { "logger" };
        var generatedParameters = new List<GeneratedParameter>
        {
            new("logger", "global::Microsoft.Extensions.Logging.ILogger")
        };
        var replacementArguments = new List<ArgumentSyntax>
        {
            SyntaxFactory.Argument(loggerAccess.WithoutTrivia())
        };

        if (exceptionArgument is not null)
        {
            generatedParameters.Add(new(
                "exception",
                GetExpressionTypeDisplayString(semanticModel, exceptionArgument.Expression, cancellationToken)));
            replacementArguments.Add(SyntaxFactory.Argument(exceptionArgument.Expression.WithoutTrivia()));
            usedNames.Add("exception");
        }

        for (var index = 0; index < valueArguments.Length; index++)
        {
            var argument = valueArguments[index];
            var parameterName = LoggerAnalysisHelpers.CreateParameterName(argument.Expression, placeholderNames[index], usedNames, index + 1);
            generatedParameters.Add(new(
                parameterName,
                GetExpressionTypeDisplayString(semanticModel, argument.Expression, cancellationToken)));
            replacementArguments.Add(SyntaxFactory.Argument(argument.Expression.WithoutTrivia()));
        }

        scaffold = new LoggerMessageScaffold(
            CreateReplacementInvocation(methodName, replacementArguments),
            CreateGeneratedMethod(methodName, methodSymbol.Name, logLevelExpression, template, generatedParameters, exceptionArgument is not null));
        return true;
    }

    private static string CreateMethodName(TypeDeclarationSyntax containingType, string containingMethodName)
    {
        var baseName = containingMethodName + "Core";
        var existingNames = new HashSet<string>(
            containingType.Members.OfType<MethodDeclarationSyntax>().Select(member => member.Identifier.ValueText),
            StringComparer.Ordinal);
        var name = baseName;
        var suffix = 1;
        while (!existingNames.Add(name))
        {
            name = baseName + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            suffix++;
        }

        return name;
    }

    private static InvocationExpressionSyntax CreateReplacementInvocation(string methodName, IEnumerable<ArgumentSyntax> arguments) =>
        SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(methodName),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));

    private static MethodDeclarationSyntax CreateGeneratedMethod(
        string methodName,
        string loggerMethodName,
        string logLevelExpression,
        string template,
        IReadOnlyList<GeneratedParameter> parameters,
        bool hasException)
    {
        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName("global::Microsoft.Extensions.Logging.LoggerMessage"),
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.AttributeArgument(
                        nameEquals: SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("Level")),
                        nameColon: null,
                        expression: SyntaxFactory.ParseExpression(logLevelExpression)),
                    SyntaxFactory.AttributeArgument(
                        nameEquals: SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("Message")),
                        nameColon: null,
                        expression: SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(template))),
                })));

        return SyntaxFactory.MethodDeclaration(
                attributeLists: SyntaxFactory.SingletonList(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))),
                modifiers: SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)),
                returnType: SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                explicitInterfaceSpecifier: null,
                identifier: SyntaxFactory.Identifier(methodName),
                typeParameterList: null,
                parameterList: SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters.Select(CreateParameter))),
                constraintClauses: default,
                body: SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(CreateLoggerInvocation(loggerMethodName, logLevelExpression, template, parameters, hasException))),
                expressionBody: null,
                semicolonToken: default)
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    private static InvocationExpressionSyntax CreateLoggerInvocation(
        string loggerMethodName,
        string logLevelExpression,
        string template,
        IReadOnlyList<GeneratedParameter> parameters,
        bool hasException)
    {
        var arguments = new List<ArgumentSyntax>
        {
            SyntaxFactory.Argument(SyntaxFactory.IdentifierName("logger"))
        };

        if (loggerMethodName == "Log")
        {
            arguments.Add(SyntaxFactory.Argument(SyntaxFactory.ParseExpression(logLevelExpression)));
        }

        if (hasException)
        {
            arguments.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("exception")));
        }

        arguments.Add(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(template))));

        foreach (var parameter in parameters.Skip(hasException ? 2 : 1))
        {
            arguments.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Name)));
        }

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.ParseExpression($"global::Microsoft.Extensions.Logging.LoggerExtensions.{loggerMethodName}"),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
    }

    private static ParameterSyntax CreateParameter(GeneratedParameter parameter) =>
        SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameter.Name))
            .WithType(SyntaxFactory.ParseTypeName(parameter.TypeName));

    private static string GetExpressionTypeDisplayString(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken cancellationToken)
    {
        var typeInfo = semanticModel.GetTypeInfo(expression, cancellationToken);
        return GetTypeDisplayString(typeInfo.Type ?? typeInfo.ConvertedType);
    }

    private static string GetTypeDisplayString(ITypeSymbol? typeSymbol) =>
        typeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "global::System.Object";

    private readonly struct LoggerMessageScaffold
    {
        public LoggerMessageScaffold(InvocationExpressionSyntax replacementInvocation, MethodDeclarationSyntax generatedMethod)
        {
            ReplacementInvocation = replacementInvocation;
            GeneratedMethod = generatedMethod;
        }

        public InvocationExpressionSyntax ReplacementInvocation { get; }

        public MethodDeclarationSyntax GeneratedMethod { get; }
    }

    private readonly struct GeneratedParameter
    {
        public GeneratedParameter(string name, string typeName)
        {
            Name = name;
            TypeName = typeName;
        }

        public string Name { get; }

        public string TypeName { get; }
    }
}
