using Npgsql;

namespace Api.Data;

/// <summary>
/// Managed-Postgres hosts (Render, Heroku, Fly) hand the app one DATABASE_URL in
/// postgres://user:pass@host:port/db form; Npgsql only speaks key=value. One pure
/// conversion, applied at startup when the variable is present. An <c>sslmode</c> in the
/// query string is honoured; without one the connection prefers TLS but does not insist,
/// which is what Render's internal (same-region) URLs need.
/// </summary>
public static class DatabaseUrl
{
    public static string ToConnectionString(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            SslMode = SslModeFrom(uri.Query) ?? SslMode.Prefer,
        }.ConnectionString;
    }

    private static SslMode? SslModeFrom(string query)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2
                && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(parts[1].Replace("-", "", StringComparison.Ordinal), ignoreCase: true, out var mode))
                return mode;
        }
        return null;
    }
}
