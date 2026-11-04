using Microsoft.Extensions.Logging;
using Arrkit.Logging;

namespace Arrkit.Tests;

[TestClass]
public sealed class MemoryLogTests
{
    [TestMethod]
    public void KeepsTheNewestLinesFirst()
    {
        var log = new MemoryLog();
        for (var i = 0; i < MemoryLog.Capacity + 5; i++)
        {
            log.Add(LogLevel.Information, "Ripparr.Jobs.JobRunner", $"line {i}", null);
        }

        var lines = log.Lines();
        Assert.HasCount(MemoryLog.Capacity, lines);
        Assert.AreEqual($"line {MemoryLog.Capacity + 4}", lines[0].Message);
        Assert.AreEqual("line 5", lines[^1].Message);
    }

    [TestMethod]
    public void ProviderFeedsTheLogWithExceptions()
    {
        var log = new MemoryLog();
        var logger = new MemoryLoggerProvider(log).CreateLogger("Salvarr.Arr.ArrService");
        logger.LogWarning(new InvalidOperationException("boom"), "Could not read {App}", "Radarr");

        var line = log.Lines().Single();
        Assert.AreEqual(LogLevel.Warning, line.Level);
        Assert.AreEqual("Could not read Radarr", line.Message);
        Assert.Contains("boom", line.Exception!);
        Assert.AreEqual("ArrService", MemoryLog.ShortSource(line.Source));

        log.Clear();
        Assert.IsEmpty(log.Lines());
    }

    [TestMethod]
    public void TheFileKeepsEarlierRunsForHistoryOnly()
    {
        var folder = Directory.CreateTempSubdirectory("arrkit-log-").FullName;
        var path = Path.Combine(folder, "logs", "log.jsonl");
        try
        {
            var first = new MemoryLog();
            first.Add(LogLevel.Information, "App", "before the file", null);
            Assert.IsTrue(first.KeepFile(path));
            first.Add(LogLevel.Error, "App", "first run", new InvalidOperationException("boom"));
            File.AppendAllText(path, "{\"cut short\n"); // a crash in the middle of a line

            var second = new MemoryLog();
            Assert.IsTrue(second.KeepFile(path));
            second.Add(LogLevel.Warning, "App", "second run", null);

            Assert.AreEqual("second run", second.Lines().Single().Message);
            var history = second.History();
            CollectionAssert.AreEqual(new[] { "second run", "first run", "before the file" }, history.Select(l => l.Message).ToArray());
            CollectionAssert.AreEqual(new[] { false, true, true }, history.Select(l => l.EarlierRun).ToArray());
            Assert.Contains("boom", history[1].Exception!);
            Assert.AreEqual(3, history.Select(l => l.Id).Distinct().Count());

            second.ClearHistory();
            Assert.IsEmpty(second.History());
            Assert.IsEmpty(new MemoryLog().Also(l => l.KeepFile(path)).History());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void TheFileKeepsTheSetNumberOfLines()
    {
        var folder = Directory.CreateTempSubdirectory("arrkit-log-").FullName;
        var path = Path.Combine(folder, "log.jsonl");
        try
        {
            var log = new MemoryLog();
            Assert.IsTrue(log.KeepFile(path, 500));
            for (var i = 0; i < 1600; i++)
            {
                log.Add(LogLevel.Information, "App", $"line {i}", null);
            }

            Assert.HasCount(500, log.History());
            Assert.IsLessThan(1000, File.ReadLines(path).Count());
            var reread = new MemoryLog();
            Assert.IsTrue(reread.KeepFile(path, 300)); // a smaller setting after a restart
            Assert.HasCount(300, reread.History());
            Assert.AreEqual("line 1599", reread.History()[0].Message);
            Assert.AreEqual(MemoryLog.MinKeepLines, new MemoryLog().Also(l => l.KeepFile(path, 5)).KeepLines);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void NoWritableFolderKeepsMemoryOnly()
    {
        var file = Path.GetTempFileName(); // "{file}/log.jsonl" can't be made
        try
        {
            var log = new MemoryLog();
            Assert.IsFalse(log.KeepFile(Path.Combine(file, "log.jsonl")));
            Assert.IsNull(log.FilePath);
            log.Add(LogLevel.Information, "App", "still logged", null);
            Assert.AreEqual("still logged", log.History().Single().Message);
        }
        finally
        {
            File.Delete(file);
        }
    }
}

internal static class TestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
