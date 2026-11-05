namespace Arrkit.Tables;

/// <summary>
/// How a list sorts, the same in every app: every column sorts both ways, a
/// column clicked first sorts its natural way (sizes, dates and durations
/// biggest or newest first, the rest A to Z), clicking the sorted column flips
/// it. The column and direction live in the URL (<c>?sort=size&amp;dir=asc</c>).
/// </summary>
/// <param name="defaultColumn">The column the list opens with.</param>
/// <param name="columns">Every column's key, as used in the URL.</param>
/// <param name="descendingFirst">The columns that start biggest or newest first.</param>
public sealed class TableSort(string defaultColumn, IReadOnlyCollection<string> columns, IReadOnlyCollection<string> descendingFirst)
{
    public string Default { get; } = defaultColumn;

    public IReadOnlyCollection<string> Columns { get; } = columns;

    /// <summary>A known column, or the default for anything else (an old or hand-typed link).</summary>
    public string Column(string? sort) =>
        Columns.FirstOrDefault(c => string.Equals(c, sort?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;

    /// <summary>How a column sorts when it is first clicked.</summary>
    public bool StartsDescending(string column) => descendingFirst.Contains(column);

    /// <summary>The direction from the URL ("asc" or "desc"), or the column's own when there is none.</summary>
    public bool Descending(string column, string? dir) => dir?.Trim().ToLowerInvariant() switch
    {
        "asc" => false,
        "desc" => true,
        _ => StartsDescending(column)
    };

    /// <summary>Clicking a header: the sorted column flips, another column starts its own way.</summary>
    public (string Column, bool Descending) Click(string current, bool descending, string clicked) =>
        clicked == current ? (current, !descending) : (clicked, StartsDescending(clicked));

    /// <summary>The URL's <c>dir</c> value.</summary>
    public static string Dir(bool descending) => descending ? "desc" : "asc";

    /// <summary>
    /// Orders by a key that may be missing (a size not known yet, a file not read):
    /// missing values go last both ways.
    /// </summary>
    public static IOrderedEnumerable<T> By<T, TKey>(IEnumerable<T> items, Func<T, TKey?> key, bool descending,
        IComparer<TKey>? comparer = null)
    {
        comparer ??= Comparer<TKey>.Default;
        var missingLast = items.OrderBy(item => key(item) is null);
        return descending
            ? missingLast.ThenByDescending(item => key(item)!, comparer)
            : missingLast.ThenBy(item => key(item)!, comparer);
    }
}
