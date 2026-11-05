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
    /// A readable extra name from a file name: "Heat.1995.Making.Of.1080p.BluRay-GRP" gives "Making Of". Dots and
    /// underscores are taken as spaces only when the name has no spaces of its own; the film's title and year in front
    /// and the release details from the resolution on are left out.
    /// </summary>
    public static string ExtraName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.Contains(' '))
        {
            name = name.Replace('.', ' ').Replace('_', ' ');
        }

        name = ReleaseRegex().Replace(name, "");
        var afterYear = TitleYearRegex().Match(name);
        if (afterYear.Success && Safe(afterYear.Groups["rest"].Value).Length > 0)
        {
            name = afterYear.Groups["rest"].Value;
        }

        return Safe(name.Trim(' ', '-'));
    }

    // "1080p BluRay x264-GRP" and everything after.
    [GeneratedRegex(@"[\s\[(]+(480|576|720|1080|2160)[pi]\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex ReleaseRegex();

    // "Heat 1995 Making Of", "Heat (1995) - Making Of": the part after the year.
    [GeneratedRegex(@"^.+?[\s(]+(19|20)\d{2}\)?[\s\-]+(?<rest>.+)$")]
    private static partial Regex TitleYearRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRegex();
}
