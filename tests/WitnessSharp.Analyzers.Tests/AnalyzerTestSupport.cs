using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace WitnessSharp.Analyzers.Tests;

internal static class AnalyzerTestSupport
{
    private const string LoggingStubs = """
namespace Microsoft.Extensions.Logging
{
    public interface ILogger
    {
    }

    public interface ILogger<out T> : ILogger
    {
    }

    public enum LogLevel
    {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical,
        None,
    }

    [global::System.AttributeUsage(global::System.AttributeTargets.Method)]
    public sealed class LoggerMessageAttribute : global::System.Attribute
    {
        public LogLevel Level { get; set; }

        public string Message { get; set; } = string.Empty;

        public int EventId { get; set; }
    }

    public static class LoggerExtensions
    {
        public static void LogTrace(this ILogger logger, string message, params object[] args)
        {
        }

        public static void LogDebug(this ILogger logger, string message, params object[] args)
        {
        }

        public static void LogInformation(this ILogger logger, string message, params object[] args)
        {
        }

        public static void LogWarning(this ILogger logger, string message, params object[] args)
        {
        }

        public static void LogError(this ILogger logger, string message, params object[] args)
        {
        }

        public static void LogCritical(this ILogger logger, string message, params object[] args)
        {
        }

        public static void Log(this ILogger logger, LogLevel level, string message, params object[] args)
        {
        }

        public static void Log(this ILogger logger, LogLevel level, global::System.Exception exception, string message, params object[] args)
        {
        }
    }
}
""";

    private const string WitnessStubs = """
namespace WitnessSharp
{
    public interface IWitness
    {
        Microsoft.Extensions.Logging.ILogger Logger { get; }
    }

    public interface IWitness<out T> : IWitness
    {
        new Microsoft.Extensions.Logging.ILogger<T> Logger { get; }
    }
}
""";

    public static Task VerifyNoDiagnosticAsync(string source)
    {
        var test = CreateAnalyzerTest(source);
        return test.RunAsync();
    }

    public static Task VerifyDiagnosticAsync(string source)
    {
        var test = CreateAnalyzerTest(source);
        return test.RunAsync();
    }

    public static Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<PreferLoggerMessageAnalyzer, PreferLoggerMessageCodeFixProvider, DefaultVerifier>();
        Configure(test, source);
        test.FixedCode = fixedSource;
        test.FixedState.Sources.Add(("LoggingStubs.cs", LoggingStubs));
        test.FixedState.Sources.Add(("WitnessStubs.cs", WitnessStubs));
        return test.RunAsync();
    }

    public static GeneratorTestResult RunInterceptorGenerator(string source, string targetFramework = "net10.0", string? interceptorsNamespaces = "WitnessSharp.Generated")
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview)
            .WithFeatures(new[]
            {
                new KeyValuePair<string, string>("InterceptorsNamespaces", "WitnessSharp.Generated"),
                new KeyValuePair<string, string>("InterceptorsPreviewNamespaces", "WitnessSharp.Generated"),
            });
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(source, parseOptions, path: "Scenario.cs"),
            CSharpSyntaxTree.ParseText(LoggingStubs, parseOptions, path: "LoggingStubs.cs"),
            CSharpSyntaxTree.ParseText(WitnessStubs, parseOptions, path: "WitnessStubs.cs"),
        };

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "WitnessSharp.Analyzers.GeneratorTests",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var options = ImmutableDictionary<string, string>.Empty.Add("build_property.TargetFramework", targetFramework);
        if (!string.IsNullOrWhiteSpace(interceptorsNamespaces))
        {
            options = options.Add("build_property.InterceptorsPreviewNamespaces", interceptorsNamespaces);
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new LoggerMessageInterceptorGenerator().AsSourceGenerator() },
            parseOptions: parseOptions,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(options));

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        return new GeneratorTestResult(driver.GetRunResult(), outputCompilation.GetDiagnostics());
    }

    private static CSharpAnalyzerTest<PreferLoggerMessageAnalyzer, DefaultVerifier> CreateAnalyzerTest(string source)
    {
        var test = new CSharpAnalyzerTest<PreferLoggerMessageAnalyzer, DefaultVerifier>();
        Configure(test, source);
        return test;
    }

    private static void Configure(CSharpAnalyzerTest<PreferLoggerMessageAnalyzer, DefaultVerifier> test, string source)
    {
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        test.TestCode = source;
        test.TestState.Sources.Add(("LoggingStubs.cs", LoggingStubs));
        test.TestState.Sources.Add(("WitnessStubs.cs", WitnessStubs));
    }

    private static void Configure(CSharpCodeFixTest<PreferLoggerMessageAnalyzer, PreferLoggerMessageCodeFixProvider, DefaultVerifier> test, string source)
    {
        test.ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        test.TestCode = source;
        test.TestState.Sources.Add(("LoggingStubs.cs", LoggingStubs));
        test.TestState.Sources.Add(("WitnessStubs.cs", WitnessStubs));
    }

    public sealed class GeneratorTestResult
    {
        public GeneratorTestResult(GeneratorDriverRunResult runResult, ImmutableArray<Diagnostic> diagnostics)
        {
            RunResult = runResult;
            Diagnostics = diagnostics;
        }

        public GeneratorDriverRunResult RunResult { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions globalOptions;
        private readonly AnalyzerConfigOptions emptyOptions = new DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

        public TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions)
        {
            this.globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);
        }

        public override AnalyzerConfigOptions GlobalOptions => globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => emptyOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => emptyOptions;
    }

    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> options;

        public DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> options)
        {
            this.options = options;
        }

        public override bool TryGetValue(string key, out string value) => options.TryGetValue(key, out value!);
    }
}
