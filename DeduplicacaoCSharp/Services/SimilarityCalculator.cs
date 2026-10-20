namespace DeduplicacaoCSharp.Services;

/// <summary>
/// Coeficiente Dice de bigramas, preservando a multiplicidade dos pares.
/// O resultado é semelhança textual, não probabilidade de duplicação.
/// </summary>
public static class SimilarityCalculator
{
    public static double Calculate(string firstTitle, string secondTitle)
    {
        var first = TextNormalizer.NormalizeTitle(firstTitle);
        var second = TextNormalizer.NormalizeTitle(secondTitle);

        if (first.Length == 0 || second.Length == 0)
        {
            return 0;
        }

        if (first == second)
        {
            return 1;
        }

        var firstBigrams = CountBigrams(first);
        var secondBigrams = CountBigrams(second);
        var sharedCount = 0;

        foreach (var (bigram, count) in firstBigrams)
        {
            if (secondBigrams.TryGetValue(bigram, out var otherCount))
            {
                sharedCount += Math.Min(count, otherCount);
            }
        }

        var totalCount = first.Length + second.Length - 2;

        return 2.0 * sharedCount / Math.Max(1, totalCount);
    }

    private static Dictionary<string, int> CountBigrams(string text)
    {
        var counts = new Dictionary<string, int>();

        for (var index = 0; index < text.Length - 1; index++)
        {
            var bigram = text.Substring(index, 2);
            counts[bigram] = counts.GetValueOrDefault(bigram) + 1;
        }

        return counts;
    }
}
