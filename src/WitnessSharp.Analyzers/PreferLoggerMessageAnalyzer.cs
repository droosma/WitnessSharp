using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WitnessSharp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferLoggerMessageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.WS0001_PreferLoggerMessage);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodSymbol = LoggerAnalysisHelpers.GetInvokedMethod(context.SemanticModel, invocation, context.CancellationToken);
        if (methodSymbol is null || !LoggerAnalysisHelpers.IsSupportedLoggerMethod(methodSymbol))
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax methodAccess ||
            methodAccess.Expression is not MemberAccessExpressionSyntax loggerAccess ||
            loggerAccess.Name.Identifier.ValueText != LoggerAnalysisHelpers.LoggerPropertyName)
        {
            return;
        }

        var containingMethodDeclaration = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (containingMethodDeclaration is null)
        {
            return;
        }

        var containingMethodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethodDeclaration, context.CancellationToken);
        if (containingMethodSymbol is null || !containingMethodSymbol.IsStatic || !containingMethodSymbol.IsExtensionMethod)
        {
            return;
        }

        var witnessParameter = containingMethodSymbol.Parameters.FirstOrDefault();
        if (witnessParameter is null || !LoggerAnalysisHelpers.IsWitnessType(witnessParameter.Type))
        {
            return;
        }

        var witnessReceiverSymbol = context.SemanticModel.GetSymbolInfo(loggerAccess.Expression, context.CancellationToken).Symbol;
        if (!SymbolEqualityComparer.Default.Equals(witnessReceiverSymbol, witnessParameter))
        {
            return;
        }

        var loggerProperty = context.SemanticModel.GetSymbolInfo(loggerAccess.Name, context.CancellationToken).Symbol as IPropertySymbol;
        if (loggerProperty is null ||
            loggerProperty.Name != LoggerAnalysisHelpers.LoggerPropertyName ||
            !LoggerAnalysisHelpers.IsLoggerType(loggerProperty.Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.WS0001_PreferLoggerMessage,
            invocation.GetLocation(),
            containingMethodSymbol.Name));
    }
}
