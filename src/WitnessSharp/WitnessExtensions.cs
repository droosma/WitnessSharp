using System.Diagnostics;

namespace WitnessSharp;

public static class WitnessExtensions
{
    public static WitnessedAction StartAction(this IWitness witness, string name) =>
        new(witness.ActivitySource.StartActivity(name));
}
