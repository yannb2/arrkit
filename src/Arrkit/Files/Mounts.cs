namespace Arrkit.Files;

/// <summary>
/// The mount points the container sees (from /proc/self/mountinfo), and which disk folders have a disk in them.
/// A USB disk or share is mounted on a folder such as /mnt/pc/usb01. While it is unplugged, that folder is still
/// there but empty and on the same mount as its parent: it is listed as "not plugged in" and the apps hide it.
/// </summary>
public static class Mounts
{
    public const string MountInfo = "/proc/self/mountinfo";

    /// <summary>The mount a path sits on: the longest mount point that holds it. Null when the mount table can't be read.</summary>
    public static string? Of(string path, string mountInfo = MountInfo) =>
        Points(mountInfo) is { } points ? Holding(points, path) : null;

    /// <summary>The mount point of every mount, from a mountinfo file. Null when it can't be read.</summary>
    public static IReadOnlyList<string>? Points(string mountInfo = MountInfo)
    {
        try
        {
            return File.ReadLines(mountInfo)
                .Select(l => l.Split(' '))
                .Where(f => f.Length > 4)
                .Select(f => Unescape(f[4]))
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The longest mount point in <paramref name="points"/> that holds <paramref name="path"/>.</summary>
    public static string? Holding(IEnumerable<string> points, string path)
    {
        var clean = Clean(path);
        return points.Select(Clean).Where(m => m == "/" || m == clean || clean.StartsWith(m + "/", StringComparison.Ordinal)).MaxBy(m => m.Length);
    }

    /// <summary>
    /// True when the folder has a disk or share in it: it is a mount of its own, it holds something, or it can't be
    /// told (no mount table, or the folder can't be listed: the page then says why). False only for an empty folder on
    /// the same mount as its parent, which is what an unplugged disk's mount point looks like.
    /// </summary>
    public static bool HasDisk(string folder) => HasDisk(folder, Points());

    /// <inheritdoc cref="HasDisk(string)"/>
    /// <param name="points">The mount points (<see cref="Points"/>); null when the mount table can't be read.</param>
    public static bool HasDisk(string folder, IReadOnlyList<string>? points)
    {
        if (points == null)
        {
            return true;
        }

        var clean = Clean(folder);
        if (points.Select(Clean).Contains(clean))
        {
            return true;
        }

        try
        {
            return Directory.EnumerateFileSystemEntries(clean).Any();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>The folders that have a disk in them (see <see cref="HasDisk(string)"/>), reading the mount table once.</summary>
    public static IEnumerable<string> WithDisk(IEnumerable<string> folders) => WithDisk(folders, Points());

    /// <inheritdoc cref="WithDisk(IEnumerable{string})"/>
    public static IEnumerable<string> WithDisk(IEnumerable<string> folders, IReadOnlyList<string>? points) =>
        folders.Where(f => HasDisk(f, points)).ToList();

    /// <summary>mountinfo writes space, tab, newline and backslash in paths as \040, \011, \012 and \134.</summary>
    public static string Unescape(string field)
    {
        if (!field.Contains('\\'))
        {
            return field;
        }

        var text = new System.Text.StringBuilder(field.Length);
        for (var i = 0; i < field.Length; i++)
        {
            if (field[i] == '\\' && IsOctal(field, i + 1) && IsOctal(field, i + 2) && IsOctal(field, i + 3))
            {
                text.Append((char)Convert.ToInt32(field.Substring(i + 1, 3), 8));
                i += 3;
            }
            else
            {
                text.Append(field[i]);
            }
        }

        return text.ToString();
    }

    private static bool IsOctal(string text, int at) => at < text.Length && text[at] is >= '0' and <= '7';

    private static string Clean(string path)
    {
        var full = Path.GetFullPath(path);
        return full.Length > 1 ? full.TrimEnd('/') : full;
    }
}
