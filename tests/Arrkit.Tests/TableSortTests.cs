using Arrkit.Tables;

namespace Arrkit.Tests;

[TestClass]
public sealed class TableSortTests
{
    private static readonly TableSort Sort = new("name", ["name", "size", "changed"], ["size", "changed"]);

    [TestMethod]
    public void UnknownColumnsFallBackToTheDefault()
    {
        Assert.AreEqual("size", Sort.Column("SIZE"));
        Assert.AreEqual("name", Sort.Column("bitrate"));
        Assert.AreEqual("name", Sort.Column(null));
    }

    [TestMethod]
    public void DirectionComesFromTheUrlOrTheColumn()
    {
        Assert.IsTrue(Sort.Descending("size", null));
        Assert.IsFalse(Sort.Descending("size", "asc"));
        Assert.IsFalse(Sort.Descending("name", "sideways"));
        Assert.IsTrue(Sort.Descending("name", "desc"));
        Assert.AreEqual("desc", TableSort.Dir(true));
    }

    [TestMethod]
    public void ClickFlipsTheSortedColumnAndStartsOthersTheirOwnWay()
    {
        Assert.AreEqual(("name", true), Sort.Click("name", false, "name"));
        Assert.AreEqual(("size", true), Sort.Click("name", false, "size"));
        Assert.AreEqual(("name", false), Sort.Click("size", true, "name"));
    }

    [TestMethod]
    public void MissingValuesGoLastBothWays()
    {
        long?[] sizes = [5, null, 9, 1];
        CollectionAssert.AreEqual(new long?[] { 9, 5, 1, null }, TableSort.By(sizes, s => s, descending: true).ToArray());
        CollectionAssert.AreEqual(new long?[] { 1, 5, 9, null }, TableSort.By(sizes, s => s, descending: false).ToArray());
    }
}
