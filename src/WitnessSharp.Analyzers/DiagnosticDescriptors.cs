using Microsoft.CodeAnalysis;

namespace WitnessSharp.Analyzers;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor WS0001_PreferLoggerMessage = new(
        id: "WS0001",
        title: "Prefer [LoggerMessage] for IWitness extension method logging",
        messageFormat: "Consider using [LoggerMessage] for better performance in extension method '{0}'",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: "https://github.com/droosma/WitnessSharp/blob/main/docs/rules/WS0001.md");
}
