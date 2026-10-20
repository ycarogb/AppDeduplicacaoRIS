using DeduplicacaoCSharp.Models;

namespace DeduplicacaoCSharp.Services;

public sealed class ReviewState
{
    public Dictionary<string, string> RemovedToKept { get; } = new();
    private readonly List<(string A, string B)> _distinctPairs = new();
    private readonly HashSet<string> _ids;

    public ReviewState(ReviewProject project)
    {
        _ids = project.Records.Select(record => record.Id).ToHashSet();

        foreach (var decision in project.Events)
        {
            Apply(decision);
        }
    }

    public string Representative(string id)
    {
        while (RemovedToKept.TryGetValue(id, out var kept))
        {
            id = kept;
        }

        return id;
    }

    public bool AreDistinct(string first, string second)
    {
        return _distinctPairs.Any(pair =>
            (Representative(pair.A) == first && Representative(pair.B) == second)
            || (Representative(pair.A) == second && Representative(pair.B) == first));
    }

    public void Apply(ReviewDecision decision)
    {
        if (decision.Kind is not ("merge" or "distinct"))
        {
            throw new InvalidDataException("Tipo de decisão ativa inválido.");
        }

        var first = decision.Kind == "merge" ? decision.Keep : decision.A;
        var second = decision.Kind == "merge" ? decision.Remove : decision.B;

        if (!_ids.Contains(first) || !_ids.Contains(second) || first == second
            || RemovedToKept.ContainsKey(first) || RemovedToKept.ContainsKey(second))
        {
            throw new InvalidDataException("Par inválido ou já alterado. Atualize a página.");
        }

        if (decision.Kind == "distinct")
        {
            _distinctPairs.Add((first, second));
            return;
        }

        if (AreDistinct(first, second))
        {
            throw new InvalidDataException(
                "Há uma decisão de manter publicações distintas nestes conjuntos. "
                + "Desfaça a decisão anterior antes de consolidar.");
        }

        RemovedToKept[second] = first;
    }

    public List<CandidatePair> PendingPairs(ReviewProject project)
    {
        var result = new Dictionary<(string, string), CandidatePair>();

        foreach (var pair in DuplicateDetector.FindCandidates(project.Records))
        {
            var first = Representative(pair.A);
            var second = Representative(pair.B);

            if (first == second || AreDistinct(first, second))
            {
                continue;
            }

            var key = string.CompareOrdinal(first, second) < 0
                ? (first, second)
                : (second, first);

            if (!result.ContainsKey(key))
            {
                result[key] = pair with { A = first, B = second };
            }
        }

        return result.Values.ToList();
    }
}
