using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp;

/// <summary>
/// The non-generic observability facade bundling the shared <see cref="System.Diagnostics.Metrics.Meter"/>,
/// <see cref="System.Diagnostics.ActivitySource"/> and an <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// When resolved from dependency injection, the non-generic <see cref="IWitness"/> is backed by
/// <c>IWitness&lt;object&gt;</c>, so its <see cref="Logger"/> uses the <c>System.Object</c> log category.
/// Prefer injecting <see cref="IWitness{T}"/> at call sites that want a meaningful, type-based category.
/// </remarks>
public interface IWitness
{
    /// <summary>Gets the shared meter used to create instruments.</summary>
    Meter Meter { get; }

    /// <summary>Gets the shared activity source used to start activities.</summary>
    ActivitySource ActivitySource { get; }

    /// <summary>Gets the logger.</summary>
    ILogger Logger { get; }
}

/// <summary>
/// The central per-call-site observability facade. Mirrors the shape of <see cref="ILogger{TCategoryName}"/>
/// while also exposing the shared <see cref="System.Diagnostics.Metrics.Meter"/> and
/// <see cref="System.Diagnostics.ActivitySource"/>.
/// </summary>
/// <typeparam name="T">The type used as the logger category.</typeparam>
public interface IWitness<out T> : IWitness
{
    /// <summary>Gets the typed logger whose category is derived from <typeparamref name="T"/>.</summary>
    new ILogger<T> Logger { get; }
}
