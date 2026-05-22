using static WitnessSharp.Analyzers.Tests.AnalyzerTestSupport;

namespace WitnessSharp.Analyzers.Tests;

public class PreferLoggerMessageCodeFixProviderTests
{
    [Fact]
    public async Task Scaffolds_logger_message_method_for_extension_logging()
    {
        await VerifyCodeFixAsync(
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
        {|WS0001:witness.Logger.LogInformation("Order {OrderId} placed", orderId)|};
    }
}
""",
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
        LogOrderPlacedCore(witness.Logger, orderId);
    }

    [global::Microsoft.Extensions.Logging.LoggerMessage(Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "Order {OrderId} placed")]
    private static void LogOrderPlacedCore(global::Microsoft.Extensions.Logging.ILogger logger, int orderId)
    {
        global::Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger, "Order {OrderId} placed", orderId);
    }
}
""");
    }

    [Fact]
    public async Task Scaffolds_logger_message_for_generic_log_invocation()
    {
        await VerifyCodeFixAsync(
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
        {|WS0001:witness.Logger.Log(LogLevel.Warning, "Order {OrderId} placed", orderId)|};
    }
}
""",
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
        LogOrderPlacedCore(witness.Logger, orderId);
    }

    [global::Microsoft.Extensions.Logging.LoggerMessage(Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Order {OrderId} placed")]
    private static void LogOrderPlacedCore(global::Microsoft.Extensions.Logging.ILogger logger, int orderId)
    {
        global::Microsoft.Extensions.Logging.LoggerExtensions.Log(logger, global::Microsoft.Extensions.Logging.LogLevel.Warning, "Order {OrderId} placed", orderId);
    }
}
""");
    }
}
