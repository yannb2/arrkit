using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Arrkit.Logging;

/// <param name="EarlierRun">True for a line read back from the log file: it was written before this start.</param>
public sealed record LogLine(long Id, DateTime Time, LogLevel Level, string Source, string Message, string? Exception,
    bool EarlierRun = false);

/// <summary>
/// The app's own log for its Logs page, in two lists:
/// <list type="bullet">
/// <item><see cref="Lines"/>: what was logged since the app started (in memory).</item>
/// <item><see cref="History"/>: the last <see cref="KeepLines"/> lines, earlier runs included, when
/// <see cref="KeepFile"/> gave it a file under /config. Without a file it is the same as Lines.</item>
/// </list>
/// Register it with <c>builder.Logging.AddProvider(new MemoryLoggerProvider(log))</c> and
/// <c>AddSingleton(log)</c>. The container log (docker logs) still has everything.
/// </summary>
public sealed class MemoryLog
{
    /// <summary>Lines kept in memory since start, and the default for <see cref="KeepLines"/>.</summary>
    public const int Capacity = 2000;

    /// <summary>The smallest and largest <see cref="KeepLines"/> allowed.</summary>
    public const int MinKeepLines = 100, MaxKeepLines = 100_000;

    /// <summary>
    /// The configuration key every app reads for <see cref="KeepLines"/>: env var
    /// <c>Logs__KeepLines</c> in compose.
    /// </summary>
    public const string KeepLinesKey = "Logs:KeepLines";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly LinkedList<LogLine> _lines = new();
    private readonly LinkedList<LogLine> _history = new();
    private readonly object _lock = new();
    private long _next;
    private StreamWriter? _writer;
    private int _fileLines;

    /// <summary>Raised after each new line (from any thread).</summary>
    public event Action? Changed;

    /// <summary>How many lines <see cref="History"/> and the log file keep (set by <see cref="KeepFile"/>).</summary>
    public int KeepLines { get; private set; } = Capacity;

    /// <summary>The file is rewritten with the last <see cref="KeepLines"/> lines once it holds twice as many.</summary>
    private int FileLimit => KeepLines * 2;

    /// <summary>The log file, or null when none is kept (no writable /config).</summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// Keep the log in <paramref name="path"/> too (one JSON object per line): the last lines from
    /// earlier runs are read back into <see cref="History"/>, which keeps the last
    /// <paramref name="keepLines"/> lines (clamped to <see cref="MinKeepLines"/>..<see cref="MaxKeepLines"/>).
    /// The file holds at most twice that. Returns false, and keeps memory only, when the file can't
    /// be read or written.
    /// </summary>
    public bool KeepFile(string path, int keepLines = Capacity)
    {
        lock (_lock)
        {
            KeepLines = Math.Clamp(keepLines, MinKeepLines, MaxKeepLines);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                var earlier = File.Exists(path) ? Read(path) : [];
                foreach (var line in earlier.TakeLast(KeepLines))
                {
                    _history.AddLast(line with { Id = -(++_next), EarlierRun = true });
                }

                // Earlier lines first, then the lines of this run that came before this call.
                foreach (var line in _lines)
                {
                    _history.AddLast(line);
                }

                Trim(_history, KeepLines);
                _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
                _fileLines = earlier.Count;
                foreach (var line in _lines)
                {
                    Write(line);
                }

                FilePath = path;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _writer?.Dispose();
                _writer = null;
                return false;
            }
        }
    }

    public void Add(LogLevel level, string source, string message, Exception? exception)
    {
        lock (_lock)
        {
            var line = new LogLine(++_next, DateTime.UtcNow, level, source, message, exception?.ToString());
            _lines.AddLast(line);
            Trim(_lines, Capacity);
            if (_writer != null)
            {
                _history.AddLast(line);
                Trim(_history, KeepLines);
                Write(line);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Since the app started, newest first.</summary>
    public List<LogLine> Lines()
    {
        lock (_lock)
        {
            return _lines.Reverse().ToList();
        }
    }

    /// <summary>The last <see cref="KeepLines"/> lines, earlier runs included, newest first.</summary>
    public List<LogLine> History()
    {
        lock (_lock)
        {
            return (_writer != null ? _history : _lines).Reverse().ToList();
        }
    }

    /// <summary>Empty the "since start" list. The file and History keep their lines.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _lines.Clear();
        }

        Changed?.Invoke();
    }

    /// <summary>Empty History and the file too.</summary>
    public void ClearHistory()
    {
        lock (_lock)
        {
            _lines.Clear();
            _history.Clear();
            if (_writer != null && FilePath != null)
            {
                try
                {
                    Rewrite(FilePath);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    _writer = null;
                    FilePath = null;
                }
            }
        }

        Changed?.Invoke();
    }

    /// <summary>"Ripparr.Jobs.JobRunner" → "JobRunner": the class is enough on the page.</summary>
    public static string ShortSource(string category)
    {
        var dot = category.LastIndexOf('.');
        return dot < 0 ? category : category[(dot + 1)..];
    }

    private static void Trim(LinkedList<LogLine> lines, int keep)
    {
        while (lines.Count > keep)
        {
            lines.RemoveFirst();
        }
    }

    /// <summary>Called under the lock. A failed write stops the file, never the app.</summary>
    private void Write(LogLine line)
    {
        try
        {
            _writer!.WriteLine(JsonSerializer.Serialize(new Stored(line.Time, line.Level, line.Source, line.Message, line.Exception), Json));
            if (++_fileLines >= FileLimit && FilePath != null)
            {
                Rewrite(FilePath);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            _writer?.Dispose();
            _writer = null;
            FilePath = null;
        }
    }

    /// <summary>Replace the file with what History holds now, so it never grows past <see cref="FileLimit"/> lines.</summary>
    private void Rewrite(string path)
    {
        _writer?.Dispose();
        var temp = path + ".tmp";
        File.WriteAllLines(temp, _history.Select(l => JsonSerializer.Serialize(new Stored(l.Time, l.Level, l.Source, l.Message, l.Exception), Json)));
        File.Move(temp, path, overwrite: true);
        _fileLines = _history.Count;
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    }

    private static List<LogLine> Read(string path)
    {
        var lines = new List<LogLine>();
        foreach (var text in File.ReadLines(path))
        {
            try
            {
                if (JsonSerializer.Deserialize<Stored>(text, Json) is { } s)
                {
                    lines.Add(new LogLine(0, s.Time, s.Level, s.Source, s.Message, s.Exception));
                }
            }
            catch (JsonException)
            {
                // A line cut short by a crash: skip it.
            }
        }

        return lines;
    }

    private sealed record Stored(DateTime Time, LogLevel Level, string Source, string Message, string? Exception);
}

/// <summary>Feeds <see cref="MemoryLog"/> from the normal logging, with the usual level filters.</summary>
public sealed class MemoryLoggerProvider(MemoryLog log) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new MemoryLogger(log, categoryName);

    public void Dispose()
    {
    }

    private sealed class MemoryLogger(MemoryLog log, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                log.Add(logLevel, category, formatter(state, exception), exception);
            }
        }
    }
}
