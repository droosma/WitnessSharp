namespace WitnessSharp;

/// <summary>
/// A singleton factory for creating <see cref="IWitness{T}"/> instances at runtime, for use when a class
/// constructs sub-objects that each need their own typed witness.
/// </summary>
/// <remarks>
/// Implementations are not required to cache or return the same instance for a given category type;
/// callers must not rely on reference identity of the returned witnesses. (The production factory returns a
/// fresh instance per call; test doubles may cache so created witnesses can be retrieved for assertions.)
/// </remarks>
public interface IWitnessFactory
{
    /// <summary>Creates an <see cref="IWitness{T}"/> for the given category type.</summary>
    /// <typeparam name="T">The type used as the logger category.</typeparam>
    /// <returns>A witness for <typeparamref name="T"/>.</returns>
    IWitness<T> Create<T>();
}
