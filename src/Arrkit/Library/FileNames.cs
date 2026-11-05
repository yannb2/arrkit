using System.Globalization;
using System.Text.RegularExpressions;

namespace Arrkit.Library;

/// <summary>File and folder names as the library uses them.</summary>
public static partial class FileNames
{
    /// <summary>
    /// A name that is safe on Linux and on a Windows share: "Title: Sub" becomes "Title - Sub", slashes become
    /// spaces, ? * " &lt; &gt; | go, and no trailing dot.
    /// </summary>
    public static string Safe(string name)
    {
        var cleaned = name.Replace(": ", " - ").Replace(':', '-');
        cleaned = cleaned.Replace('/', ' ').Replace('\\', ' ');
        foreach (var c in new[] { '?', '*', '"', '<', '>', '|' })
        {
            cleaned = cleaned.Replace(c.ToString(), "");
        }

        return SpacesRegex().Replace(cleaned, " ").Trim().TrimEnd('.').Trim();
    }

    /// <summary>"Season 01", or "Specials" for season 0, as Sonarr and Jellyfin lay out a series.</summary>
    public static string SeasonFolder(int season) =>
        season == 0 ? "Specials" : $"Season {season.ToString("00", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// A readable extra name from a file name: "The.Making.Of_1080p" gives "The Making Of 1080p". Dots and
    /// underscores are taken as spaces only when the name has no spaces of its own.
    /// </summary>
    public static string ExtraName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.Contains(' '))
        {
            name = name.Replace('.', ' ').Replace('_', ' ');
        }

        return Safe(name);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRegex();
}
