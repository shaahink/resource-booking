using Api.Domain;
using Xunit;

namespace Api.Tests.Unit;

public sealed class ConstraintParamsTests
{
    [Theory]
    [InlineData(ConstraintType.MaxDurationMinutes, """{"minutes":120}""")]
    [InlineData(ConstraintType.MinNoticeMinutes, """{"minutes":525600}""")]
    [InlineData(ConstraintType.MaxAdvanceDays, """{"days":60}""")]
    [InlineData(ConstraintType.MaxConcurrentPerUser, """{"count":1}""")]
    [InlineData(ConstraintType.MaxActiveBookingsPerUser, """{"count":2}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":{"mon":[["08:00","20:00"]]}}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":{"MON":[["08:00","12:00"],["13:00","24:00"]],"sat":[]}}""")]
    public void Accepts_the_documented_shapes(ConstraintType type, string paramsJson)
    {
        Assert.True(ConstraintParams.IsValid(type, paramsJson));
    }

    [Theory]
    [InlineData(ConstraintType.MaxDurationMinutes, "")]
    [InlineData(ConstraintType.MaxDurationMinutes, "not json")]
    [InlineData(ConstraintType.MaxDurationMinutes, "[]")]
    [InlineData(ConstraintType.MaxDurationMinutes, "{}")]
    [InlineData(ConstraintType.MaxDurationMinutes, """{"minutes":0}""")]
    [InlineData(ConstraintType.MaxDurationMinutes, """{"minutes":"60"}""")]
    [InlineData(ConstraintType.MaxDurationMinutes, """{"minutes":525601}""")]
    [InlineData(ConstraintType.MaxAdvanceDays, """{"days":3000000}""")]
    [InlineData(ConstraintType.MaxConcurrentPerUser, """{"count":1001}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":{"mon":"08:00-18:00"}}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":{"monday":[["08:00","18:00"]]}}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":{"mon":[["9","17"]]}}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":{"mon":[["18:00","08:00"]]}}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":{"mon":[["08:00","25:00"]]}}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":{"mon":[["08:00"]]}}""")]
    [InlineData(ConstraintType.OpeningHours, """{"days":[]}""")]
    public void Rejects_anything_else(ConstraintType type, string paramsJson)
    {
        Assert.False(ConstraintParams.IsValid(type, paramsJson));
    }

    [Fact]
    public void Twenty_four_hundred_reads_as_midnight()
    {
        var days = ConstraintParams.OpeningHours("""{"days":{"fri":[["18:00","24:00"]]}}""");
        Assert.NotNull(days);
        var window = Assert.Single(days[DayOfWeek.Friday]);
        Assert.Equal(TimeSpan.FromHours(24), window.Close);
        Assert.Equal("24:00", ConstraintParams.Format(window.Close));
    }
}
