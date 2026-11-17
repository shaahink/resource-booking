using Api.Domain;
using Xunit;

namespace Api.Tests.Unit;

public sealed class SlugsTests
{
    [Theory]
    [InlineData("Boardroom", "boardroom")]
    [InlineData("Pool Car 1 (EV)", "pool-car-1-ev")]
    [InlineData("  Huddle   A  ", "huddle-a")]
    [InlineData("Café Space", "caf-space")]
    [InlineData("!!!", "resource")]
    public void Names_become_lower_case_hyphenated_slugs(string name, string expected) =>
        Assert.Equal(expected, Slugs.From(name));
}
