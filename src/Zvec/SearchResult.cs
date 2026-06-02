namespace Zvec;

public class SearchResult
{
    public string Id { get; }

    /// <summary>
    /// Distance score. Lower = more similar.
    /// For cosine: 0.0 = identical, 2.0 = opposite.
    /// For L2: 0.0 = identical, higher = farther.
    /// </summary>
    public float Score { get; }

    internal SearchResult(string id, float score)
    {
        Id = id;
        Score = score;
    }
}
