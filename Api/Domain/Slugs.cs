using System.Text;

namespace Api.Domain;

public static class Slugs
{
    /// <summary>"Pool Car 1 (EV)" → "pool-car-1-ev": lower-case ASCII letters and digits, any run
    /// of anything else collapsed to one hyphen.</summary>
    public static string From(string name)
    {
        var slug = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsAsciiLetterOrDigit(c))
                slug.Append(char.ToLowerInvariant(c));
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
        }

        var result = slug.ToString().TrimEnd('-');
        return result.Length > 0 ? result : "resource";
    }
}
