using Api.Data;
using Xunit;

namespace Api.Tests.Unit;

public sealed class DatabaseUrlTests
{
    [Fact]
    public void Converts_a_full_url_with_port_and_escaped_password()
    {
        var connectionString = DatabaseUrl.ToConnectionString("postgres://booking:s3cr%40t@db.example.com:6543/bookable");

        Assert.Contains("Host=db.example.com", connectionString, StringComparison.Ordinal);
        Assert.Contains("Port=6543", connectionString, StringComparison.Ordinal);
        Assert.Contains("Username=booking", connectionString, StringComparison.Ordinal);
        Assert.Contains("Password=s3cr@t", connectionString, StringComparison.Ordinal);
        Assert.Contains("Database=bookable", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_the_port_when_the_url_has_none()
    {
        // Render's internal URLs carry no port.
        var connectionString = DatabaseUrl.ToConnectionString("postgres://booking:pass@dpg-abc123-a/booking");
        Assert.Contains("Port=5432", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void Honours_an_explicit_sslmode()
    {
        var connectionString = DatabaseUrl.ToConnectionString("postgres://u:p@db.example.com/booking?sslmode=verify-full");
        Assert.Contains("SSL Mode=VerifyFull", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void Prefers_tls_when_no_sslmode_is_given()
    {
        var connectionString = DatabaseUrl.ToConnectionString("postgres://u:p@dpg-abc123-a/booking");
        Assert.Contains("SSL Mode=Prefer", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void Unescapes_the_database_name()
    {
        var connectionString = DatabaseUrl.ToConnectionString("postgres://u:p@db.example.com/my%20db");
        Assert.Contains("Database=\"my db\"", connectionString, StringComparison.Ordinal);
    }
}
