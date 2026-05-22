using static WitnessSharp.Analyzers.Tests.AnalyzerTestSupport;

namespace WitnessSharp.Analyzers.Tests;

public class PreferLoggerMessageAnalyzerTests
{
    [Fact]
    public async Task Static_method_without_this_modifier_does_not_report()
    {
        await VerifyNoDiagnosticAsync("""
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public static class OrderWitnessExtensions
{
    public static void LogOrderPlaced(IWitness<OrderService> witness)
    {
        witness.Logger.LogInformation("Order placed");
    }
}
""");
    }

    [Fact]
    public async Task Extension_method_without_logger_call_does_not_report()
    {
        await VerifyNoDiagnosticAsync("""
using WitnessSharp;

public sealed class OrderService
{
}

public static class OrderWitnessExtensions
{
    public static string Describe(this IWitness<OrderService> witness)
    {
        return witness.GetType().Name;
    }
}
""");
    }

    [Fact]
    public async Task Generic_witness_extension_logging_reports_diagnostic()
    {
        await VerifyDiagnosticAsync("""
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public static class OrderWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId)
    {
        {|WS0001:witness.Logger.LogInformation("Order {OrderId} placed", orderId)|};
    }
}
""");
    }

    [Fact]
    public async Task Non_generic_witness_extension_logging_reports_diagnostic()
    {
        await VerifyDiagnosticAsync("""
using Microsoft.Extensions.Logging;
using WitnessSharp;

public static class OrderWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness witness)
    {
        {|WS0001:witness.Logger.LogWarning("Order placed")|};
    }
}
""");
    }

    [Fact]
    public async Task Generic_logger_log_method_reports_diagnostic()
    {
        await VerifyDiagnosticAsync("""
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public static class OrderWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId)
    {
        {|WS0001:witness.Logger.Log(LogLevel.Information, "Order {OrderId} placed", orderId)|};
    }
}
""");
    }

    [Fact]
    public async Task Existing_logger_message_pattern_does_not_report()
    {
        await VerifyNoDiagnosticAsync("""
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public static class OrderWitnessExtensions
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} placed")]
    private static void LogOrderPlacedCore(ILogger logger, int orderId)
    {
    }

    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId)
    {
        LogOrderPlacedCore(witness.Logger, orderId);
    }
}
""");
    }

    [Fact]
    public async Task Non_static_instance_method_does_not_report()
    {
        await VerifyNoDiagnosticAsync("""
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public sealed class OrderWitnessExtensions
{
    public void LogOrderPlaced(IWitness<OrderService> witness)
    {
        witness.Logger.LogInformation("Order placed");
    }
}
""");
    }

    [Fact]
    public async Task Implementing_witness_type_reports_diagnostic()
    {
        await VerifyDiagnosticAsync("""
using Microsoft.Extensions.Logging;
using WitnessSharp;

public sealed class OrderService
{
}

public sealed class CustomWitness : IWitness<OrderService>
{
    public ILogger<OrderService> Logger { get; } = null!;
    ILogger IWitness.Logger => Logger;
}

public static class OrderWitnessExtensions
{
    public static void LogOrderPlaced(this CustomWitness witness, int orderId)
    {
        {|WS0001:witness.Logger.LogInformation("Order {OrderId} placed", orderId)|};
    }
}
""");
    }
}
