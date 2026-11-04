using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Arrkit.Processes;

/// <summary>What a command line tool printed and how it ended.</summary>
public sealed record ToolResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;

    /// <summary>True when the tool could not be started at all (most often: not installed).</summary>
    public bool NotStarted => ExitCode == ToolRunner.NotStartedCode;

    /// <summary>True when the tool ran longer than its timeout and was stopped.</summary>
    public bool TimedOut => ExitCode == ToolRunner.TimedOutCode;

    /// <summary>The last non-empty line of the error output, or of the output: usually the reason.</summary>
    public string LastLine
    {
        get
        {
            var text = Error.Trim().Length > 0 ? Error : Output;
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        }
    }
}

/// <summary>
/// Runs a command line tool (ffmpeg, tesseract) and collects what it prints. Small on purpose:
/// each app keeps its own runner for long jobs with progress; this one is for the shared code.
/// </summary>
public static class ToolRunner
{
    public const int NotStartedCode = -1;
    public const int TimedOutCode = -2;

    /// <param name="environment">Variables added to the tool's environment.</param>
    public static async Task<ToolResult> RunAsync(string fileName, IEnumerable<string> arguments, TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environment = null, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // ArgumentList quotes each argument, so file names with spaces or quotes pass untouched.
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[key] = value;
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception e)
        {
            return new ToolResult(NotStartedCode, "", e.Message);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var output = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var error = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        try
        {
            await Task.WhenAll(output, error);
            await process.WaitForExitAsync(timeoutSource.Token);
            return new ToolResult(process.ExitCode, output.Result, error.Result);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new ToolResult(TimedOutCode, "", $"{Path.GetFileName(fileName)} took longer than {timeout} and was stopped.");
        }
    }
}
