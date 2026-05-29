using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WitnessSharp.Analyzers;

internal static class LoggerAnalysisHelpers
{
    internal const string WitnessNamespace = "WitnessSharp";
    internal const string WitnessInterfaceName = "IWitness";
    internal const string LoggerPropertyName = "Logger";

    private static readonly char[] PlaceholderDelimiters = { ',', ':' };

    internal static IMethodSymbol? GetInvokedMethod(SemanticModel semanticModel, InvocationExpressionSyntax invocation, System.Threading.CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        return symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
    }

    internal static bool IsSupportedLoggerMethod(IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.Name.StartsWith("Log", StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = methodSymbol.ContainingType;
        return containingType is not null && (IsLoggerExtensionsType(containingType) || IsLoggerInterfaceType(containingType));
    }

    internal static bool IsWitnessType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType && IsWitnessInterface(namedType))
        {
            return true;
        }

        return typeSymbol.AllInterfaces.OfType<INamedTypeSymbol>().Any(IsWitnessInterface);
    }

    internal static bool IsWitnessInterface(INamedTypeSymbol typeSymbol)
    {
        var originalDefinition = typeSymbol.OriginalDefinition;
        return originalDefinition.Name == WitnessInterfaceName &&
               originalDefinition.ContainingNamespace.ToDisplayString() == WitnessNamespace &&
               (originalDefinition.Arity == 0 || originalDefinition.Arity == 1);
    }

    internal static bool IsLoggerType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType && IsLoggerInterfaceType(namedType))
        {
            return true;
        }

        return typeSymbol.AllInterfaces.OfType<INamedTypeSymbol>().Any(IsLoggerInterfaceType);
    }

    internal static bool IsLoggerInterfaceType(INamedTypeSymbol typeSymbol)
    {
        var originalDefinition = typeSymbol.OriginalDefinition;
        return originalDefinition.Name == "ILogger" &&
               originalDefinition.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.Logging";
    }

    internal static bool IsLoggerExtensionsType(INamedTypeSymbol typeSymbol) =>
        typeSymbol.Name == "LoggerExtensions" &&
        typeSymbol.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.Logging";

    internal static bool TryGetLogLevelExpression(
        SemanticModel semanticModel,
        IMethodSymbol methodSymbol,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        CancellationToken cancellationToken,
        out string logLevelExpression,
        out int searchStartIndex)
    {
        searchStartIndex = 0;
        if (methodSymbol.Name != "Log")
        {
            logLevelExpression = methodSymbol.Name switch
            {
                "LogTrace" => "global::Microsoft.Extensions.Logging.LogLevel.Trace",
                "LogDebug" => "global::Microsoft.Extensions.Logging.LogLevel.Debug",
                "LogInformation" => "global::Microsoft.Extensions.Logging.LogLevel.Information",
                "LogWarning" => "global::Microsoft.Extensions.Logging.LogLevel.Warning",
                "LogError" => "global::Microsoft.Extensions.Logging.LogLevel.Error",
                "LogCritical" => "global::Microsoft.Extensions.Logging.LogLevel.Critical",
                _ => string.Empty,
            };

            return logLevelExpression.Length > 0;
        }

        if (arguments.Count == 0)
        {
            logLevelExpression = string.Empty;
            return false;
        }

        var levelSymbol = semanticModel.GetSymbolInfo(arguments[0].Expression, cancellationToken).Symbol as IFieldSymbol;
        if (levelSymbol?.ContainingType is not { Name: "LogLevel" } levelType || levelType.ContainingNamespace.ToDisplayString() != "Microsoft.Extensions.Logging")
        {
            logLevelExpression = string.Empty;
            return false;
        }

        logLevelExpression = levelSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + levelSymbol.Name;
        searchStartIndex = 1;
        return true;
    }

    internal static bool TryFindMessageTemplate(
        SemanticModel semanticModel,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int startIndex,
        CancellationToken cancellationToken,
        out int templateIndex,
        out string template)
    {
        templateIndex = -1;
        template = string.Empty;

        for (var index = startIndex; index < arguments.Count; index++)
        {
            var constantValue = semanticModel.GetConstantValue(arguments[index].Expression, cancellationToken);
            if (constantValue.HasValue && constantValue.Value is string templateValue)
            {
                templateIndex = index;
                template = templateValue;
                return true;
            }
        }

        return false;
    }

    internal static bool IsExceptionType(ITypeSymbol? typeSymbol)
    {
        while (typeSymbol is not null)
        {
            if (typeSymbol.Name == nameof(Exception) && typeSymbol.ContainingNamespace.ToDisplayString() == nameof(System))
            {
                return true;
            }

            typeSymbol = (typeSymbol as INamedTypeSymbol)?.BaseType;
        }

        return false;
    }

    internal static List<string> ExtractPlaceholders(string template)
    {
        var placeholders = new List<string>();
        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] != '{')
            {
                continue;
            }

            if (index + 1 < template.Length && template[index + 1] == '{')
            {
                index++;
                continue;
            }

            var endIndex = template.IndexOf('}', index + 1);
            if (endIndex < 0)
            {
                break;
            }

            var token = template.Substring(index + 1, endIndex - index - 1).Trim();
            if (token.Length > 0 && (token[0] == '@' || token[0] == '$'))
            {
                token = token.Substring(1);
            }

            var delimiterIndex = token.IndexOfAny(PlaceholderDelimiters);
            if (delimiterIndex >= 0)
            {
                token = token.Substring(0, delimiterIndex);
            }

            if (token.Length > 0)
            {
                placeholders.Add(token);
            }

            index = endIndex;
        }

        return placeholders;
    }

    internal static string CreateParameterName(ExpressionSyntax expression, string placeholderName, ISet<string> usedNames, int index)
    {
        var baseName = expression is IdentifierNameSyntax identifier
            ? SanitizeIdentifier(identifier.Identifier.ValueText)
            : ToCamelCase(SanitizeIdentifier(placeholderName));

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = "arg" + index.ToString(CultureInfo.InvariantCulture);
        }

        var candidate = baseName;
        var suffix = 1;
        while (!usedNames.Add(candidate))
        {
            candidate = baseName + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return candidate;
    }

    internal static string SanitizeIdentifier(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            if (builder.Length == 0)
            {
                if (SyntaxFacts.IsIdentifierStartCharacter(character))
                {
                    builder.Append(character);
                }
                else if (SyntaxFacts.IsIdentifierPartCharacter(character))
                {
                    builder.Append('_');
                    builder.Append(character);
                }
            }
            else if (SyntaxFacts.IsIdentifierPartCharacter(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    internal static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length == 1
            ? char.ToLowerInvariant(value[0]).ToString()
            : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
