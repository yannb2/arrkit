using Arrkit.Library;

namespace Arrkit.Tests;

[TestClass]
public sealed class ExtrasTests
{
    private string _root = "";
    private List<LibraryRoot> _roots = [];

    [TestInitialize]
    public void Setup()
    {
        _root = Directory.CreateTempSubdirectory("arrkit-extras-").FullName;
        _roots = [new(Path.Combine(_root, "movies"), false), new(Path.Combine(_root, "tv"), true)];
        Directory.CreateDirectory(Path.Combine(_root, "movies", "live-action", "Heat (1995)"));
        Directory.CreateDirectory(Path.Combine(_root, "movies", "anime", "Akira (1988)"));
        Directory.CreateDirectory(Path.Combine(_root, "movies", "live-action", ".hidden (2000)"));
        Directory.CreateDirectory(Path.Combine(_root, "tv", "live-action", "Doctor Who (2005)", "Season 01"));
        Directory.CreateDirectory(Path.Combine(_root, "downloads"));
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    private string Source(string name, int bytes = 1000)
    {
        var path = Path.Combine(_root, "downloads", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Enumerable.Range(0, bytes).Select(i => (byte)(i * 7)).ToArray());
        return path;
    }

    private string Heat => Path.Combine(_root, "movies", "live-action", "Heat (1995)");

    [TestMethod]
    [DataRow("Theatrical Trailer.mkv", "trailers")]
    [DataRow("Heat.Teaser.1080p.mkv", "trailers")]
    [DataRow("Deleted Scenes.mkv", "deleted scenes")]
    [DataRow("Alternate Ending.mkv", "deleted scenes")]
    [DataRow("The.Making.Of.Heat.mkv", "behind the scenes")]
    [DataRow("behind-the-scenes.mkv", "behind the scenes")]
    [DataRow("Interview with Michael Mann.mkv", "interviews")]
    [DataRow("Q&A.mkv", "interviews")]
    [DataRow("Gag Reel.mkv", "featurettes")]
    [DataRow("Opening Scene.mkv", "scenes")]
    [DataRow("Featurettes/Anything.mkv", "featurettes")]
    [DataRow("Disc 2/Title 03.mkv", null)]
    [DataRow("Trailerpark.mkv", null)]
    public void GuessesTheTypeFromTheName(string name, string? expected) => Assert.AreEqual(expected, ExtraTypes.Guess(name));

    [TestMethod]
    public void ExtraNamesAreReadable()
    {
        Assert.AreEqual("The Making Of Heat", FileNames.ExtraName("The.Making.Of.Heat.mkv"));
        Assert.AreEqual("Making Of - Part 1", FileNames.ExtraName("Making Of: Part 1.mkv"));
        Assert.AreEqual("Specials", FileNames.SeasonFolder(0));
        Assert.AreEqual("Season 03", FileNames.SeasonFolder(3));
    }

    [TestMethod]
    public void ListsTitleFoldersUnderTheirCategory()
    {
        var folders = LibraryFolders.List(_roots);
        CollectionAssert.AreEqual(new[] { "Akira (1988)", "Doctor Who (2005)", "Heat (1995)" }, folders.Select(f => f.Name).ToArray());
        Assert.AreEqual("anime", folders[0].Category);
        Assert.IsTrue(folders[1].Tv);
        Assert.AreEqual(1995, folders[2].Year);

        Assert.AreEqual(Heat, LibraryFolders.Find(folders, "heat", 1995, tv: false)?.Path);
        Assert.IsNull(LibraryFolders.Find(folders, "Heat", 1995, tv: true));
        Assert.IsNull(LibraryFolders.Find(folders, "Heat", 2005, tv: false));
        Assert.AreEqual("Doctor Who (2005)", LibraryFolders.Find(folders, "Doctor Who!", null, tv: true)?.Name);
    }

    [TestMethod]
    public void OnlyTitleFoldersTakeExtras()
    {
        Assert.IsTrue(LibraryFolders.IsTitleFolder(_roots, Heat));
        Assert.IsFalse(LibraryFolders.IsTitleFolder(_roots, Path.Combine(_root, "movies", "live-action")));
        Assert.IsFalse(LibraryFolders.IsTitleFolder(_roots, Path.Combine(_root, "movies")));
        Assert.IsFalse(LibraryFolders.IsTitleFolder(_roots, Path.Combine(Heat, "featurettes")));
        Assert.IsFalse(LibraryFolders.IsTitleFolder(_roots, Path.Combine(_root, "downloads", "x", "Heat (1995)")));
        Assert.IsFalse(LibraryFolders.IsTitleFolder(_roots, Path.Combine(_root, "movies", "live-action", "..", "..", "downloads")));

        var steps = ExtrasPlan.Make(_roots, Path.Combine(_root, "downloads"), [new ExtraFile(Source("a.mkv"), "featurettes", "A")]);
        Assert.AreEqual(ExtraStepState.Problem, steps[0].State);
    }

    [TestMethod]
    public void PlansTargetsWithoutOverwriting()
    {
        Directory.CreateDirectory(Path.Combine(Heat, "featurettes"));
        File.WriteAllBytes(Path.Combine(Heat, "featurettes", "Taken.mkv"), new byte[5]);
        File.WriteAllBytes(Path.Combine(Heat, "featurettes", "Same.mkv"), new byte[1000]);

        var steps = ExtrasPlan.Make(_roots, Heat,
        [
            new ExtraFile(Source("one.MKV"), "featurettes", "Making Of: Part 1"),
            new ExtraFile(Source("two.mkv"), "featurettes", "Taken"),
            new ExtraFile(Source("three.mkv"), "featurettes", "Same"),
            new ExtraFile(Source("four.mkv"), "trailers", "Trailer"),
            new ExtraFile(Source("five.mkv"), "trailers", "Trailer"),
            new ExtraFile(Source("six.mkv"), "clips", "Clip"),
            new ExtraFile(Path.Combine(_root, "downloads", "gone.mkv"), "trailers", "Gone")
        ]);

        Assert.AreEqual(ExtraStepState.Copy, steps[0].State);
        Assert.AreEqual(Path.Combine("featurettes", "Making Of - Part 1.mkv"), steps[0].Relative);
        Assert.AreEqual(ExtraStepState.Renamed, steps[1].State);
        Assert.AreEqual("Taken (2).mkv", Path.GetFileName(steps[1].Target));
        Assert.AreEqual(ExtraStepState.AlreadyThere, steps[2].State);
        Assert.AreEqual(ExtraStepState.Copy, steps[3].State);
        Assert.AreEqual(ExtraStepState.Renamed, steps[4].State);
        Assert.AreEqual("Trailer (2).mkv", Path.GetFileName(steps[4].Target));
        Assert.AreEqual(ExtraStepState.Problem, steps[5].State);
        Assert.AreEqual(ExtraStepState.Problem, steps[6].State);
    }

    [TestMethod]
    public void SeasonExtrasGoInTheSeasonFolder()
    {
        var who = Path.Combine(_root, "tv", "live-action", "Doctor Who (2005)");
        var steps = ExtrasPlan.Make(_roots, who,
        [
            new ExtraFile(Source("conf.mkv"), "behind the scenes", "Confidential 1", Season: 1),
            new ExtraFile(Source("xmas.mkv"), "extras", "Christmas", Season: 0),
            new ExtraFile(Source("doc.mkv"), "featurettes", "The Story")
        ]);

        Assert.AreEqual(Path.Combine("Season 01", "behind the scenes", "Confidential 1.mkv"), steps[0].Relative);
        Assert.AreEqual(Path.Combine("Specials", "extras", "Christmas.mkv"), steps[1].Relative);
        Assert.AreEqual(Path.Combine("featurettes", "The Story.mkv"), steps[2].Relative);
    }

    [TestMethod]
    public async Task CopiesVerifiesAndCarriesOn()
    {
        var files = new List<ExtraFile>
        {
            new(Source("a.mkv", 5_000_000), "featurettes", "A"),
            new(Source("b.mkv", 3000), "trailers", "B")
        };
        var steps = ExtrasPlan.Make(_roots, Heat, files);
        long last = 0, total = 0;
        var result = await ExtrasCopier.CopyAsync(steps, verify: true, (d, t) => (last, total) = (d, t), null, CancellationToken.None);

        Assert.IsTrue(result.Ok);
        Assert.AreEqual(2, result.Copied);
        Assert.AreEqual(2 * 5_003_000L, total);
        Assert.AreEqual(total, last);
        CollectionAssert.AreEqual(File.ReadAllBytes(files[0].Source), File.ReadAllBytes(Path.Combine(Heat, "featurettes", "A.mkv")));
        Assert.IsEmpty(Directory.GetFiles(Heat, ".*", SearchOption.AllDirectories));
        Assert.AreEqual(File.GetLastWriteTimeUtc(files[1].Source), File.GetLastWriteTimeUtc(Path.Combine(Heat, "trailers", "B.mkv")));
        Assert.IsTrue(File.Exists(files[0].Source), "the source stays");

        // Again: everything is there already, nothing is copied twice.
        var again = await ExtrasCopier.CopyAsync(ExtrasPlan.Make(_roots, Heat, files), verify: false, null, null, CancellationToken.None);
        Assert.AreEqual(0, again.Copied);
        Assert.AreEqual(2, again.AlreadyThere);
        Assert.AreEqual("Copied 0 extras, 2 already there.", again.Summary);
        Assert.HasCount(2, again.Placed);
        Assert.AreEqual(files[0].Source, again.Placed[0].Step.File.Source);
    }

    [TestMethod]
    public async Task ACancelledCopyLeavesNothingBehind()
    {
        var steps = ExtrasPlan.Make(_roots, Heat, [new ExtraFile(Source("big.mkv", 20_000_000), "featurettes", "Big")]);
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ExtrasCopier.CopyAsync(steps, verify: false, (d, _) => { if (d > 0) cancel.Cancel(); }, null, cancel.Token));
        Assert.IsEmpty(Directory.GetFiles(Heat, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task ProblemsAreReportedNotCopied()
    {
        var steps = ExtrasPlan.Make(_roots, Heat, [new ExtraFile(Source("a.mkv"), "nope", "A")]);
        var result = await ExtrasCopier.CopyAsync(steps, verify: false, null, null, CancellationToken.None);
        Assert.IsFalse(result.Ok);
        StringAssert.Contains(result.Summary, "not an extras folder");
    }
}
