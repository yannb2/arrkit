using Arrkit.Links;

namespace Arrkit.Tests;

[TestClass]
public sealed class SpecarrLinkTests
{
    [TestMethod]
    public void LinksToSpecarrsOpenPage()
    {
        Assert.AreEqual("http://10.1.72.11:8185/open?path=%2Fdownloads%2Ftorrents%2FFilm%20%282019%29%2FFilm%20%23.mkv",
            SpecarrLink.For(" http://10.1.72.11:8185/ ", "/downloads/torrents/Film (2019)/Film #.mkv"));
    }

    [TestMethod]
    public void NoAddressNoLink()
    {
        Assert.IsNull(SpecarrLink.For("", "/downloads/a.mkv"));
        Assert.IsNull(SpecarrLink.For(null, "/downloads/a.mkv"));
        Assert.IsNull(SpecarrLink.For("http://specarr:8185", ""));
    }

    [TestMethod]
    public void VideoAndAudioHaveDetails()
    {
        Assert.IsTrue(SpecarrLink.HasDetails("Film.mkv"));
        Assert.IsTrue(SpecarrLink.HasDetails("01 - Track.FLAC"));
        Assert.IsFalse(SpecarrLink.HasDetails("Film.iso"));
        Assert.IsFalse(SpecarrLink.HasDetails("release.nfo"));
    }
}
