using Api.Data;
using Xunit;

namespace Api.Tests.Unit;

public sealed class DatabaseUrlTests
{
    [Fact]
    public void Converts_a_full_url_with_port_and_escaped_password()
    {
        var connectionString = DatabaseUrl.ToConnectionString("postgres://booking:s3cr%40t@db.example.com:6543/bookable");

        Assert.Contains("Host=db.example.com", connectionString);
        Assert.Contains("Port=6543", connectionString);
        Assert.Contains("Username=booking", connectionString);
        Assert.Contains("Password=s3cr@t", connectionString);
        Assert.Contains("Database=bookable", connectionString);
    }

    [Fact]
    public void Defaults_the_port_when_the_url_has_none()
    {
        // Render's internal URLs carry no port.
        var connectionString = DatabaseUrl.ToConnectionString("postgres://booking:pass@dpg-abc123-a/booking");
        Assert.Contains("Port=5432", connectionString);
    }
}
