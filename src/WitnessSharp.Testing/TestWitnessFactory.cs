namespace WitnessSharp.Testing;

/// <summary>
/// An <see cref="IWitnessFactory"/> test double that caches the <see cref="TestWitness{T}"/> it creates
/// per category type, so created witnesses can be retrieved later (via <see cref="Get{T}"/>) for assertions.
/// </summary>
public sealed class TestWitnessFactory : IWitnessFactory
{
    private readonly Dictionary<Type, object> _witnesses = [];

    /// <summary>Creates (or returns the previously created) <see cref="TestWitness{T}"/> for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type used as the logger category.</typeparam>
    /// <returns>The cached witness for <typeparamref name="T"/>.</returns>
    public IWitness<T> Create<T>()
    {
        if (!_witnesses.TryGetValue(typeof(T), out var witness))
        {
            witness = new TestWitness<T>();
            _witnesses[typeof(T)] = witness;
        }

        return (IWitness<T>)witness;
    }

    /// <summary>Retrieves the <see cref="TestWitness{T}"/> previously created for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type used as the logger category.</typeparam>
    /// <returns>The cached test witness for <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no witness has been created for <typeparamref name="T"/>.</exception>
    public TestWitness<T> Get<T>() =>
        _witnesses.TryGetValue(typeof(T), out var witness)
            ? (TestWitness<T>)witness
            : throw new InvalidOperationException($"No TestWitness<{typeof(T).Name}> has been created. Call Create<{typeof(T).Name}>() first.");
}
