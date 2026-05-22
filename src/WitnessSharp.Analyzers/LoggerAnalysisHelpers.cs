using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WitnessSharp.Analyzers;

internal static class LoggerAnalysisHelpers
{
    internal const string WitnessNamespace = "WitnessSharp";
    internal const string WitnessInterfaceName = "IWitness";
    internal const string LoggerPropertyName = "Logger";

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
}
