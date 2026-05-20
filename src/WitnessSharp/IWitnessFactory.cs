namespace WitnessSharp;

public interface IWitnessFactory
{
    IWitness<T> Create<T>();
}
