namespace WitnessSharp.Testing;

public sealed class TestWitnessFactory : IWitnessFactory
{
    private readonly Dictionary<Type, object> _witnesses = [];

    public IWitness<T> Create<T>()
    {
        if (!_witnesses.TryGetValue(typeof(T), out var witness))
        {
            witness = new TestWitness<T>();
            _witnesses[typeof(T)] = witness;
        }

        return (IWitness<T>)witness;
    }

    public TestWitness<T> Get<T>() =>
        _witnesses.TryGetValue(typeof(T), out var witness)
            ? (TestWitness<T>)witness
            : throw new InvalidOperationException($"No TestWitness<{typeof(T).Name}> has been created. Call Create<{typeof(T).Name}>() first.");
}
