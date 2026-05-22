namespace WitnessSharp.Testing.Tests;

public class TestWitnessFactoryTests
{
    private sealed record SubjectA;
    private sealed record SubjectB;

    [Fact]
    public void Create_and_Get_return_cached_witnesses_per_type()
    {
        var factory = new TestWitnessFactory();

        var createdA = factory.Create<SubjectA>();
        var cachedA = factory.Get<SubjectA>();
        var createdAAgain = factory.Create<SubjectA>();
        var createdB = factory.Create<SubjectB>();
        var cachedB = factory.Get<SubjectB>();

        Assert.Same(createdA, cachedA);
        Assert.Same(createdA, createdAAgain);
        Assert.Same(createdB, cachedB);
        Assert.NotSame(createdA, createdB);

        cachedA.Dispose();
        cachedB.Dispose();
    }

    [Fact]
    public void Get_throws_when_witness_has_not_been_created()
    {
        var factory = new TestWitnessFactory();

        var exception = Assert.Throws<InvalidOperationException>(factory.Get<SubjectA>);

        Assert.Equal("No TestWitness<SubjectA> has been created. Call Create<SubjectA>() first.", exception.Message);
    }
}
