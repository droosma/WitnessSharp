using System.Linq;
using Microsoft.CodeAnalysis;
using static WitnessSharp.Analyzers.Tests.AnalyzerTestSupport;

namespace WitnessSharp.Analyzers.Tests;

public class LoggerMessageInterceptorGeneratorTests
{
    [Fact]
    public void Net10_with_opt_in_generates_interceptor_and_logger_message_companion()
    {
        var result = RunInterceptorGenerator(
            """
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public static class OrderWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId)
    {
        witness.Logger.LogInformation("Order {OrderId} placed", orderId);
    }
}

public static class Program
{
    public static void Execute(IWitness<OrderService> witness)
    {
        witness.LogOrderPlaced(42);
    }
}
""");

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var generatedSource = Assert.Single(
                Assert.Single(result.RunResult.Results).GeneratedSources,
                static source => source.HintName == "LoggerMessageInterceptors.g.cs")
            .SourceText
            .ToString();
        Assert.Contains("namespace WitnessSharp.Generated;", generatedSource);
        Assert.Contains("[global::System.Runtime.CompilerServices.InterceptsLocation(", generatedSource);
        Assert.Contains("[global::Microsoft.Extensions.Logging.LoggerMessage(Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = \"Order {OrderId} placed\")]", generatedSource);
        Assert.Contains("public static void Intercept_0(", generatedSource);
        Assert.Contains("IWitness<global::OrderService>", generatedSource);
        Assert.Contains("Log_0(witness.Logger, orderId);", generatedSource);
        Assert.Contains("static partial void Log_0(", generatedSource);
        Assert.Contains("ILogger logger", generatedSource);
    }

    [Fact]
    public void Net8_target_does_not_generate_interceptors()
    {
        var result = RunInterceptorGenerator(
            """
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public static class OrderWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId)
    {
        witness.Logger.LogInformation("Order {OrderId} placed", orderId);
    }
}

public static class Program
{
    public static void Execute(IWitness<OrderService> witness)
    {
        witness.LogOrderPlaced(42);
    }
}
""",
            targetFramework: "net8.0");

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(Assert.Single(result.RunResult.Results).GeneratedSources);
    }

    [Fact]
    public void Missing_namespace_opt_in_does_not_generate_interceptors()
    {
        var result = RunInterceptorGenerator(
            """
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public static class OrderWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId)
    {
        witness.Logger.LogInformation("Order {OrderId} placed", orderId);
    }
}

public static class Program
{
    public static void Execute(IWitness<OrderService> witness)
    {
        witness.LogOrderPlaced(42);
    }
}
""",
            interceptorsNamespaces: "Some.Other.Namespace");

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(Assert.Single(result.RunResult.Results).GeneratedSources);
    }
}
