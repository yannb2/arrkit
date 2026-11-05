using System.Security.Cryptography;

namespace Arrkit.Library;

/// <summary>What a copy did.</summary>
public sealed record ExtrasCopyResult(int Copied, int AlreadyThere, long Bytes, IReadOnlyList<(ExtraStep Step, string Error)> Failed)
{
    /// <summary>Each extra now in the library (copied, or already there) and the file it is.</summary>
    public IReadOnlyList<(ExtraStep Step, string Target)> Placed { get; init; } = [];

    public IEnumerable<string> Targets => Placed.Select(p => p.Target);

    public bool Ok => Failed.Count == 0;

    /// <summary>"Copied 3 extras, 1 already there." in plain words.</summary>
    public string Summary =>
        (Copied == 1 ? "Copied 1 extra" : $"Copied {Copied} extras")
        + (AlreadyThere > 0 ? $", {AlreadyThere} already there" : "")
        + (Failed.Count > 0 ? $", {Failed.Count} failed ({Failed[0].Error})" : "") + ".";
}

/// <summary>
/// Copies planned extras into the library. Each file is written as ".Name.mkv.copying" in its folder (Jellyfin and the
/// arr apps skip dot files), read back and compared when asked, then given its real name. Nothing in the library is
/// overwritten and the source is only read. Running it again after a stop carries on: what is there is skipped.
/// </summary>
public static class ExtrasCopier
{
    public const string PartialSuffix = ".copying";

    private const int Buffer = 4 * 1024 * 1024;

    /// <param name="progress">Bytes done and bytes in all (twice the size when verifying: it reads everything back).</param>
    public static async Task<ExtrasCopyResult> CopyAsync(IReadOnlyList<ExtraStep> steps, bool verify, Action<long, long>? progress,
        Action<string>? log, CancellationToken cancellationToken)
    {
        var toCopy = steps.Where(s => s.ToCopy).ToList();
        var total = toCopy.Sum(s => s.Size) * (verify ? 2 : 1);
        long done = 0;
        var copied = 0;
        long bytes = 0;
        var failed = new List<(ExtraStep, string)>();
        var placed = steps.Where(s => s.State == ExtraStepState.AlreadyThere).Select(s => (s, s.Target)).ToList();
        foreach (var step in steps.Where(s => s.State == ExtraStepState.Problem))
        {
            failed.Add((step, step.Problem ?? "Can't be copied."));
        }

        foreach (var step in toCopy)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = done;
            try
            {
                var target = await CopyOneAsync(step, verify, n => progress?.Invoke(done = start + n, total), log, cancellationToken);
                copied++;
                bytes += step.Size;
                placed.Add((step, target));
                done = start + step.Size * (verify ? 2 : 1);
                progress?.Invoke(done, total);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                log?.Invoke($"{step.File.Source}: {e.Message}");
                failed.Add((step, Reason(e)));
                done = start + step.Size * (verify ? 2 : 1);
            }
        }

        return new ExtrasCopyResult(copied, steps.Count(s => s.State == ExtraStepState.AlreadyThere), bytes, failed) { Placed = placed };
    }

    /// <summary>Copies one file; returns where it ended up.</summary>
    private static async Task<string> CopyOneAsync(ExtraStep step, bool verify, Action<long> progress, Action<string>? log,
        CancellationToken cancellationToken)
    {
        var folder = Path.GetDirectoryName(step.Target)!;
        Directory.CreateDirectory(folder);
        var partial = PartialPath(step.Target);
        log?.Invoke($"Copy {step.File.Source} -> {step.Target}{(verify ? ", then read back and compared" : "")}");
        try
        {
            byte[] sourceHash;
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using var input = new FileStream(step.File.Source, FileMode.Open, FileAccess.Read, FileShare.Read, Buffer,
                    FileOptions.SequentialScan | FileOptions.Asynchronous);
                await using var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, Buffer,
                    FileOptions.Asynchronous);
                var buffer = new byte[Buffer];
                long n = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    n += read;
                    progress(n);
                }

                output.Flush(flushToDisk: true);
                sourceHash = hash.GetHashAndReset();
                if (n != step.Size)
                {
                    throw new InvalidDataException($"read {n} bytes, expected {step.Size}: the source changed while it was copied.");
                }
            }

            if (verify)
            {
                var copyHash = await HashAsync(partial, n => progress(step.Size + n), cancellationToken);
                if (!copyHash.AsSpan().SequenceEqual(sourceHash))
                {
                    throw new InvalidDataException("the copy read back differently from the source.");
                }
            }

            File.SetLastWriteTimeUtc(partial, File.GetLastWriteTimeUtc(step.File.Source));
            // Something may have appeared under the name meanwhile: never overwrite it.
            var target = step.Target;
            for (var i = 2; File.Exists(target); i++)
            {
                target = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(step.Target)} ({i}){Path.GetExtension(step.Target)}");
            }

            File.Move(partial, target, overwrite: false);
            return target;
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    /// <summary>"/movies/x/Heat (1995)/featurettes/.Making Of.mkv.copying".</summary>
    public static string PartialPath(string target) =>
        Path.Combine(Path.GetDirectoryName(target)!, "." + Path.GetFileName(target) + PartialSuffix);

    private static async Task<byte[]> HashAsync(string path, Action<long> progress, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, Buffer,
            FileOptions.SequentialScan | FileOptions.Asynchronous);
        var buffer = new byte[Buffer];
        long n = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            n += read;
            progress(n);
        }

        return hash.GetHashAndReset();
    }

    /// <summary>A failed copy in plain words.</summary>
    public static string Reason(Exception e) => e.Message switch
    {
        var m when m.Contains("No space left", StringComparison.OrdinalIgnoreCase) => "The library pool is full.",
        var m when m.Contains("Read-only file system", StringComparison.OrdinalIgnoreCase) =>
            "The library is mounted read-only here. Mount /movies and /tv read-write (without :ro) to copy extras.",
        var m when e is UnauthorizedAccessException || m.Contains("Permission denied", StringComparison.OrdinalIgnoreCase) =>
            "No permission to write in the library folder (it should belong to uid 1000).",
        var m when m.Contains("Input/output error", StringComparison.OrdinalIgnoreCase) =>
            "A disk gave a read error (Input/output error).",
        _ => e.Message
    };

    private static void TryDelete(string partial)
    {
        try
        {
            if (partial.EndsWith(PartialSuffix, StringComparison.Ordinal) && File.Exists(partial))
            {
                File.Delete(partial);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left behind: the next run writes over it, and dot files are skipped by Jellyfin.
        }
    }
}
