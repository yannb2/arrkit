namespace Arrkit.Links;

/// <summary>
/// Links to Specarr's page for a file or folder, from the other apps ("Open in Specarr"). Specarr's <c>/open</c>
/// page finds where the path is (a library file, a title folder, a file or folder under /downloads) and goes
/// there, so the apps need nothing but its address and the path as their container sees it (same mounts).
/// </summary>
public static class SpecarrLink
{
    /// <summary>Audio files Specarr can show the tracks of (video files: <see cref="Library.VideoFiles"/>).</summary>
    public static readonly IReadOnlySet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".flac", ".mka", ".m4a", ".mp3", ".wav", ".ogg", ".opus", ".aac", ".ac3", ".eac3", ".dts", ".thd", ".wv", ".ape", ".dsf"
    };

    /// <summary>
    /// Specarr's page for the path, or null when no Specarr address is set.
    /// <paramref name="specarrUrl"/> is the address as the browser reaches it, e.g. http://10.1.72.11:8185.
    /// </summary>
    public static string? For(string? specarrUrl, string? path) =>
        string.IsNullOrWhiteSpace(specarrUrl) || string.IsNullOrWhiteSpace(path)
            ? null
            : $"{specarrUrl.Trim().TrimEnd('/')}/open?path={Uri.EscapeDataString(path)}";

    /// <summary>A file whose tracks Specarr shows: a video (not a disc image) or an audio file.</summary>
    public static bool HasDetails(string name) =>
        Library.VideoFiles.IsVideo(name) || AudioExtensions.Contains(Path.GetExtension(name));
}
