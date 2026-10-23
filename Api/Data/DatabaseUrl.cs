using Npgsql;

namespace Api.Data;

/// <summary>
/// Managed-Postgres hosts (Render, Heroku, Fly) hand the app one DATABASE_URL in
/// postgres://user:pass@host:port/db form; Npgsql only speaks key=value. One pure
/// conversion, applied at startup when the variable is present.
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
            Database = uri.AbsolutePath.TrimStart('/'),
            // Same-region internal connections skip TLS; external ones require it.
            SslMode = SslMode.Prefer,
        }.ConnectionString;
    }
}
