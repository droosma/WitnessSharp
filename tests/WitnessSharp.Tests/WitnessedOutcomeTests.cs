namespace WitnessSharp.Tests;

public class WitnessedOutcomeTests
{
    [Fact]
    public void Has_three_named_values()
    {
        var values = Enum.GetValues<WitnessedOutcome>();
        Assert.Equal(3, values.Length);
        Assert.Contains(WitnessedOutcome.Success, values);
        Assert.Contains(WitnessedOutcome.Failure, values);
        Assert.Contains(WitnessedOutcome.Cancelled, values);
    }

    [Fact]
    public void Default_value_is_Success()
    {
        WitnessedOutcome value = default;
        Assert.Equal(WitnessedOutcome.Success, value);
    }
}
