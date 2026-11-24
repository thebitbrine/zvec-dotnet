namespace Zvec;

/// <summary>
/// Snapshot of collection statistics from ZvecCollection.GetStats().
/// </summary>
public class CollectionStats
{
    public ulong DocCount { get; }

    public IReadOnlyList<IndexStats> Indexes { get; }

    internal CollectionStats(ulong docCount, IReadOnlyList<IndexStats> indexes)
    {
        DocCount = docCount;
        Indexes = indexes;
    }
}

/// <summary>
/// Build state of one index. Completeness is 0..1; below 1 means part of the
/// data is still served from the unindexed buffer until Optimize() runs.
/// </summary>
public class IndexStats
{
    public string Name { get; }

    public float Completeness { get; }

    internal IndexStats(string name, float completeness)
    {
        Name = name;
        Completeness = completeness;
    }
}
