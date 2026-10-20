using DeduplicacaoCSharp.Models;

namespace DeduplicacaoCSharp.Services;

public static class DuplicateDetector
{
    public const double SimilarityThreshold = 0.90;
    public const int MinimumTitleLength = 20;

    public static List<CandidatePair> FindCandidates(
        IReadOnlyList<BibliographicRecord> records)
    {
        var candidates = new List<CandidatePair>();

        for (var firstIndex = 0; firstIndex < records.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < records.Count; secondIndex++)
            {
                var candidate = Compare(records[firstIndex], records[secondIndex]);

                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        return candidates
            .OrderByDescending(pair => pair.Reason == "DOI igual")
            .ThenByDescending(pair => pair.Score)
            .ToList();
    }

    public static CandidatePair? Compare(
        BibliographicRecord first,
        BibliographicRecord second)
    {
        var firstDoi = TextNormalizer.NormalizeDoi(first.Doi);
        var secondDoi = TextNormalizer.NormalizeDoi(second.Doi);
        var firstTitle = TextNormalizer.NormalizeTitle(first.Title);
        var secondTitle = TextNormalizer.NormalizeTitle(second.Title);
        var score = SimilarityCalculator.Calculate(first.Title, second.Title);
        string reason;

        if (firstDoi.Length > 0 && secondDoi.Length > 0 && firstDoi == secondDoi)
        {
            reason = "DOI igual";
        }
        else if (firstTitle.Length > 0 && firstTitle == secondTitle)
        {
            reason = "Título normalizado igual";
        }
        else if (firstTitle.Length >= MinimumTitleLength
            && secondTitle.Length >= MinimumTitleLength
            && score >= SimilarityThreshold)
        {
            reason = $"Título semelhante (Dice ≥ {SimilarityThreshold:0.00})";
        }
        else
        {
            return null;
        }

        var conflictingDois = firstDoi.Length > 0
            && secondDoi.Length > 0
            && firstDoi != secondDoi;

        return new CandidatePair(
            first.Id,
            second.Id,
            reason,
            score,
            conflictingDois,
            first.Id,
            second.Id);
    }
}
