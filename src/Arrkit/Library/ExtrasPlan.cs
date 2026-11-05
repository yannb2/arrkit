namespace Arrkit.Library;

/// <summary>One file to put in the library as an extra.</summary>
/// <param name="Source">The file now (in /downloads or /mnt/pc).</param>
/// <param name="Type">Its folder, from <see cref="ExtraTypes"/> ("featurettes").</param>
/// <param name="Name">Its name without extension ("Making Of"): Jellyfin shows the file name.</param>
/// <param name="Season">For a series: the season folder it goes in; null = next to the seasons.</param>
public sealed record ExtraFile(string Source, string Type, string Name, int? Season = null);

public enum ExtraStepState
{
    /// <summary>Not in the library yet: it gets copied.</summary>
    Copy,

    /// <summary>A file with this name and size is already there: nothing to do.</summary>
    AlreadyThere,

    /// <summary>A different file has the name: this one is copied as "Name (2)", nothing is overwritten.</summary>
    Renamed,

    /// <summary>Can't be copied: see the problem.</summary>
    Problem
}

/// <summary>What happens to one extra.</summary>
public sealed record ExtraStep(ExtraFile File, string Target, ExtraStepState State, long Size, string? Problem = null)
{
    public bool ToCopy => State is ExtraStepState.Copy or ExtraStepState.Renamed;

    /// <summary>"featurettes/Making Of.mkv" or "Season 01/featurettes/Making Of.mkv": where it goes, from the title folder.</summary>
    public string Relative { get; init; } = "";
}

/// <summary>
/// Works out where each extra goes in a film or series folder: Title (Year)/featurettes/Making Of.mkv, or
/// Season 01/featurettes/ for a season's extra. Never plans to overwrite: a name already taken by another file
/// gets " (2)". Reads the disk, writes nothing.
/// </summary>
public static class ExtrasPlan
{
    public static List<ExtraStep> Make(IEnumerable<LibraryRoot> roots, string titleFolder, IEnumerable<ExtraFile> files)
    {
        var rootList = roots.ToList();
        var folderProblem = !LibraryFolders.IsTitleFolder(rootList, titleFolder)
            ? $"{titleFolder} is not a film or series folder in {string.Join(" or ", rootList.Select(r => r.Path))}."
            : !Directory.Exists(titleFolder)
                ? $"{titleFolder} does not exist (import the film or series first)."
                : null;

        var steps = new List<ExtraStep>();
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var size = SizeOf(file.Source);
            var problem = folderProblem ?? Problem(file, size);
            if (problem != null)
            {
                steps.Add(new ExtraStep(file, "", ExtraStepState.Problem, size, problem));
                continue;
            }

            var folder = file.Season is { } season
                ? Path.Combine(titleFolder, FileNames.SeasonFolder(season), file.Type)
                : Path.Combine(titleFolder, file.Type);
            var name = FileNames.Safe(file.Name);
            var extension = Path.GetExtension(file.Source).ToLowerInvariant();
            var state = ExtraStepState.Copy;
            var target = Path.Combine(folder, name + extension);
            for (var n = 2; ; n++)
            {
                if (taken.Contains(target))
                {
                    // Two extras of this batch with one name: the second one is numbered.
                    state = ExtraStepState.Renamed;
                }
                else if (!File.Exists(target))
                {
                    break;
                }
                else if (SizeOf(target) == size)
                {
                    state = ExtraStepState.AlreadyThere;
                    break;
                }
                else
                {
                    state = ExtraStepState.Renamed;
                }

                target = Path.Combine(folder, $"{name} ({n}){extension}");
            }

            taken.Add(target);
            steps.Add(new ExtraStep(file, target, state, size) { Relative = Path.GetRelativePath(titleFolder, target) });
        }

        return steps;
    }

    private static string? Problem(ExtraFile file, long size)
    {
        if (!ExtraTypes.Known(file.Type))
        {
            return $"\"{file.Type}\" is not an extras folder Jellyfin knows.";
        }

        if (FileNames.Safe(file.Name).Length == 0)
        {
            return "It needs a name.";
        }

        if (file.Season is < 0 or > 999)
        {
            return $"Season {file.Season} is not a season.";
        }

        return !File.Exists(file.Source) ? $"{file.Source} is not there any more." : size == 0 ? $"{file.Source} is empty." : null;
    }

    private static long SizeOf(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
