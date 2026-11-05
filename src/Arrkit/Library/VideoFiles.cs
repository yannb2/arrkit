namespace Arrkit.Library;

/// <summary>Which files are videos, and the videos in a file or folder.</summary>
public static class VideoFiles
{
    public static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".avi", ".ts", ".m2ts", ".mov", ".wmv", ".mpg", ".mpeg", ".webm"
    };

    public static bool IsVideo(string name) => Extensions.Contains(Path.GetExtension(name));

    /// <summary>
    /// The video files of a file (itself, when it is one) or a folder (all levels, by path), skipping dot files and
    /// folders. A folder that can't be read gives what could be read.
    /// </summary>
    public static List<string> In(string path)
    {
        if (File.Exists(path))
        {
            return IsVideo(path) ? [path] : [];
        }

        var list = new List<string>();
        var folders = new Stack<string>();
        if (Directory.Exists(path))
        {
            folders.Push(path);
        }

        while (folders.Count > 0)
        {
            var folder = folders.Pop();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
                {
                    var name = Path.GetFileName(entry);
                    if (name.StartsWith('.'))
                    {
                        continue;
                    }

                    if (Directory.Exists(entry))
                    {
                        folders.Push(entry);
                    }
                    else if (IsVideo(name))
                    {
                        list.Add(entry);
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Unreadable: skipped.
            }
        }

        return list.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Looks like a release's sample file ("x.sample.mkv", "Sample/x.mkv").</summary>
    public static bool IsSample(string path) =>
        Path.GetFileNameWithoutExtension(path).Split('.', '-', '_', ' ').Any(w => w.Equals("sample", StringComparison.OrdinalIgnoreCase))
        || string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "sample", StringComparison.OrdinalIgnoreCase);
}
