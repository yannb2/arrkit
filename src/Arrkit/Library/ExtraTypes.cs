using System.Text.RegularExpressions;

namespace Arrkit.Library;

/// <summary>A kind of extra, by the folder it goes in next to the film or series.</summary>
/// <param name="Folder">The folder name Jellyfin knows ("featurettes").</param>
/// <param name="Label">For a dropdown ("Featurettes").</param>
/// <param name="Singular">For a default name ("Featurette 2").</param>
public sealed record ExtraType(string Folder, string Label, string Singular);

/// <summary>
/// The kinds of extras: folder names Jellyfin shows as extras and Radarr/Sonarr leave alone when they
/// scan a film or series folder. <c>clips</c> is left out on purpose: Radarr and Sonarr don't skip it,
/// so a clip there could be taken for the film itself.
/// </summary>
public static partial class ExtraTypes
{
    public static readonly IReadOnlyList<ExtraType> All =
    [
        new("featurettes", "Featurettes", "Featurette"),
        new("behind the scenes", "Behind the scenes", "Behind the Scenes"),
        new("deleted scenes", "Deleted scenes", "Deleted Scene"),
        new("interviews", "Interviews", "Interview"),
        new("scenes", "Scenes", "Scene"),
        new("shorts", "Shorts", "Short"),
        new("trailers", "Trailers", "Trailer"),
        new("other", "Other", "Other"),
        new("extras", "Other extras", "Extra")
    ];

    /// <summary>The type a new extra starts with when nothing in its name says otherwise.</summary>
    public const string Default = "featurettes";

    public static bool Known(string? folder) => All.Any(t => t.Folder == folder);

    public static string Label(string? folder) => All.FirstOrDefault(t => t.Folder == folder)?.Label ?? folder ?? "";

    /// <summary>"Featurette 2": the name an extra gets when none is typed.</summary>
    public static string DefaultName(string folder, int number) =>
        $"{All.FirstOrDefault(t => t.Folder == folder)?.Singular ?? "Extra"} {number}";

    /// <summary>
    /// The type a file's name points to ("Theatrical Trailer.mkv" is a trailer, "Making Of" behind the
    /// scenes), or null when nothing in it says. The folder it sits in counts too ("Deleted Scenes/01.mkv").
    /// </summary>
    public static string? Guess(string path)
    {
        var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
        if (Known(parent?.ToLowerInvariant()))
        {
            return parent!.ToLowerInvariant();
        }

        var name = Words(Path.GetFileNameWithoutExtension(path));
        foreach (var (folder, pattern) in Clues)
        {
            if (pattern.IsMatch(name))
            {
                return folder;
            }
        }

        return null;
    }

    /// <summary>Lower case, with dots, underscores and dashes as spaces: "the.making-of" is "the making of".</summary>
    private static string Words(string name) => " " + SeparatorsRegex().Replace(name.ToLowerInvariant(), " ") + " ";

    // First match wins: "deleted" before "scene", "making of" before "featurette".
    private static readonly (string Folder, Regex Pattern)[] Clues =
    [
        ("trailers", new Regex(@" (trailers?|teasers?|tv spots?|promos?) ", RegexOptions.Compiled)),
        ("deleted scenes", new Regex(@" (deleted|alternate (ending|opening|scene)s?|alternative (ending|opening)s?|extended scenes?|outtakes?) ", RegexOptions.Compiled)),
        ("behind the scenes", new Regex(@" (behind the scenes|making of|bts|on (the )?set|production diary) ", RegexOptions.Compiled)),
        ("interviews", new Regex(@" (interviews?|q ?a|in conversation|conversation with) ", RegexOptions.Compiled)),
        ("shorts", new Regex(@" short (film|films) ", RegexOptions.Compiled)),
        ("featurettes", new Regex(@" (featurettes?|documentary|gag reel|bloopers?) ", RegexOptions.Compiled)),
        ("scenes", new Regex(@" scenes? ", RegexOptions.Compiled))
    ];

    [GeneratedRegex(@"[\s._\-&]+")]
    private static partial Regex SeparatorsRegex();
}
