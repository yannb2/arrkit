using System.Text.RegularExpressions;

namespace Arrkit.Library;

/// <summary>A library root: /movies or /tv.</summary>
public sealed record LibraryRoot(string Path, bool Tv);

/// <summary>A film or series folder in the library: /movies/live-action/Heat (1995).</summary>
/// <param name="Category">The folder between the root and the title ("live-action"); empty when the title sits in the root.</param>
public sealed record LibraryFolder(string Path, string Name, string Category, bool Tv)
{
    /// <summary>"Heat" from "Heat (1995)".</summary>
    public string Title => LibraryFolders.TitleYear(Name).Title;

    public int? Year => LibraryFolders.TitleYear(Name).Year;

    /// <summary>"Heat (1995) · live-action".</summary>
    public string Display => Category.Length == 0 ? Name : $"{Name} · {Category}";
}

/// <summary>
/// Finds the film and series folders under the library roots: root/category/Title (Year), or root/Title (Year)
/// when a root has no categories. Folders starting with a dot are skipped.
/// </summary>
public static partial class LibraryFolders
{
    /// <summary>The usual roots, as Radarr, Sonarr and Jellyfin see them.</summary>
    public static readonly IReadOnlyList<LibraryRoot> Default = [new("/movies", false), new("/tv", true)];

    /// <summary>Every film and series folder, by name. A root that is missing or can't be read gives nothing.</summary>
    public static List<LibraryFolder> List(IEnumerable<LibraryRoot> roots)
    {
        var list = new List<LibraryFolder>();
        foreach (var root in roots)
        {
            foreach (var child in Folders(root.Path))
            {
                var name = System.IO.Path.GetFileName(child);
                if (TitleYear(name).Year != null)
                {
                    list.Add(new LibraryFolder(child, name, "", root.Tv));
                    continue;
                }

                list.AddRange(Folders(child).Select(f => new LibraryFolder(f, System.IO.Path.GetFileName(f), name, root.Tv)));
            }
        }

        return list.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Category, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The folder of a title: an exact "Title (Year)" first, then the same name with the punctuation left out
    /// ("Spider-Man: No Way Home" finds "Spider-Man - No Way Home (2021)"). Null when there is none or more than one.
    /// </summary>
    public static LibraryFolder? Find(IEnumerable<LibraryFolder> folders, string title, int? year, bool tv)
    {
        var candidates = folders.Where(f => f.Tv == tv && (year == null || f.Year == year)).ToList();
        var exact = candidates.Where(f => string.Equals(f.Title, FileNames.Safe(title), StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
        {
            return exact[0];
        }

        var key = Key(title);
        var loose = candidates.Where(f => Key(f.Title) == key).ToList();
        return loose.Count == 1 ? loose[0] : null;
    }

    /// <summary>Is this path a film or series folder under one of the roots (not a root or a category)?</summary>
    public static bool IsTitleFolder(IEnumerable<LibraryRoot> roots, string path)
    {
        var full = Clean(path);
        foreach (var root in roots)
        {
            var rootPath = Clean(root.Path);
            if (!full.StartsWith(rootPath + "/", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = full[(rootPath.Length + 1)..].Split('/');
            return parts.Length switch
            {
                1 => TitleYear(parts[0]).Year != null,
                2 => !parts[0].StartsWith('.') && !parts[1].StartsWith('.'),
                _ => false
            };
        }

        return false;
    }

    /// <summary>("Heat", 1995) from "Heat (1995)"; the year is null when the name doesn't end in one.</summary>
    public static (string Title, int? Year) TitleYear(string name)
    {
        var match = TitleYearRegex().Match(name);
        return match.Success ? (match.Groups["title"].Value.Trim(), int.Parse(match.Groups["year"].Value)) : (name, null);
    }

    /// <summary>A full path with no trailing slash and no "..".</summary>
    public static string Clean(string path) => System.IO.Path.GetFullPath(path).TrimEnd('/') is { Length: > 0 } p ? p : "/";

    private static string Key(string title) => NotLettersRegex().Replace(title.ToLowerInvariant(), "");

    private static IEnumerable<string> Folders(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? Directory.GetDirectories(path).Where(d => !System.IO.Path.GetFileName(d).StartsWith('.')).ToList()
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    [GeneratedRegex(@"^(?<title>.+?)\s*\((?<year>\d{4})\)$")]
    private static partial Regex TitleYearRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NotLettersRegex();
}
