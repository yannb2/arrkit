using Arrkit.Files;

namespace Arrkit.Tests;

[TestClass]
public sealed class MountsTests
{
    [TestMethod]
    public void ReadsMountPointsAndTheirEscapes()
    {
        var info = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(info,
            [
                "22 1 0:21 / / rw,relatime - overlay overlay rw",
                "40 22 8:17 / /mnt/pc rw,relatime shared:5 - ext4 /dev/sdb1 rw",
                "41 40 8:33 / /mnt/pc/usb01 rw,relatime shared:6 - exfat /dev/sdc1 rw",
                @"42 40 0:50 / /mnt/pc/my\040share rw,relatime - cifs //pc/share rw"
            ]);

            CollectionAssert.AreEqual(new[] { "/", "/mnt/pc", "/mnt/pc/usb01", "/mnt/pc/my share" }, Mounts.Points(info)!.ToArray());
            Assert.AreEqual("/mnt/pc/usb01", Mounts.Of("/mnt/pc/usb01/Film/Film.mkv", info));
            Assert.AreEqual("/mnt/pc", Mounts.Of("/mnt/pc/usb02", info));
            Assert.AreEqual("/mnt/pc", Mounts.Of("/mnt/pc/usb011", info), "usb011 is not inside usb01");
            Assert.IsNull(Mounts.Of("/x", "/no/such/mountinfo"));
        }
        finally
        {
            File.Delete(info);
        }
    }

    [TestMethod]
    public void UnescapesOnlyThreeOctalDigits()
    {
        Assert.AreEqual(@"a b\c", Mounts.Unescape(@"a\040b\134c"));
        Assert.AreEqual(@"keep\09 \x", Mounts.Unescape(@"keep\09\040\x"));
        Assert.AreEqual(@"end\04", Mounts.Unescape(@"end\04"));
    }

    [TestMethod]
    public void AnEmptyFolderOnItsParentsMountHasNoDisk()
    {
        var root = Directory.CreateTempSubdirectory("arrkit-mounts-").FullName;
        try
        {
            var unplugged = Directory.CreateDirectory(Path.Combine(root, "usb02")).FullName;
            var plugged = Directory.CreateDirectory(Path.Combine(root, "usb01")).FullName;
            File.WriteAllText(Path.Combine(plugged, "Film.mkv"), "x");
            var emptyDisk = Directory.CreateDirectory(Path.Combine(root, "usb03")).FullName;
            string[] points = ["/", root, emptyDisk];

            Assert.IsFalse(Mounts.HasDisk(unplugged, points));
            Assert.IsTrue(Mounts.HasDisk(plugged, points));
            Assert.IsTrue(Mounts.HasDisk(emptyDisk, points), "an empty disk is still a mount of its own");
            Assert.IsTrue(Mounts.HasDisk(emptyDisk + "/", points));

            CollectionAssert.AreEqual(new[] { plugged, emptyDisk },
                Mounts.WithDisk(new[] { plugged, unplugged, emptyDisk }, points).ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void WithoutAMountTableEveryFolderIsKept()
    {
        var root = Directory.CreateTempSubdirectory("arrkit-mounts-").FullName;
        try
        {
            var empty = Directory.CreateDirectory(Path.Combine(root, "usb02")).FullName;
            Assert.IsTrue(Mounts.HasDisk(empty, Mounts.Points("/no/such/mountinfo")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
