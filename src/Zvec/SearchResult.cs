namespace Zvec;

public class SearchResult
{
    public string Id { get; }

    /// <summary>
    /// Meaning depends on the query; results are always ordered best first.
    /// Vector queries: a distance, lower = more similar (cosine: 0.0 = identical,
    /// 2.0 = opposite; L2: 0.0 = identical).
    /// Full-text and multi-queries: a relevance score, higher = better.
    /// </summary>
    public float Score { get; }

    internal SearchResult(string id, float score)
    {
        Id = id;
        Score = score;
    }
}
