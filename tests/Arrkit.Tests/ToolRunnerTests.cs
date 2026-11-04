using Arrkit.Processes;

namespace Arrkit.Tests;

[TestClass]
public class ToolRunnerTests
{
    [TestMethod]
    public async Task CollectsOutputAndEnvironment()
    {
        var result = await ToolRunner.RunAsync("sh", ["-c", "echo \"$GREETING\"; echo oops >&2; exit 3"], TimeSpan.FromSeconds(10),
            new Dictionary<string, string> { ["GREETING"] = "hello" });

        Assert.AreEqual(3, result.ExitCode);
        Assert.AreEqual("hello", result.Output.Trim());
        Assert.AreEqual("oops", result.LastLine);
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task AMissingToolIsNotStarted()
    {
        var result = await ToolRunner.RunAsync("arrkit-no-such-tool", [], TimeSpan.FromSeconds(10));
        Assert.IsTrue(result.NotStarted);
    }

    [TestMethod]
    public async Task ASlowToolIsStopped()
    {
        var result = await ToolRunner.RunAsync("sleep", ["30"], TimeSpan.FromMilliseconds(300));
        Assert.IsTrue(result.TimedOut);
    }
}
