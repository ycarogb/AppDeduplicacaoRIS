using System.Globalization;
using System.Text.Json;
using DeduplicacaoCSharp.Models;

namespace DeduplicacaoCSharp.Services;

public static class ExportService
{
    public static string Ris(ReviewProject project)
    {
        var state = new ReviewState(project);

        return string.Join("\n", project.Records
            .Where(record => !state.RemovedToKept.ContainsKey(record.Id))
            .Select(record => record.Raw));
    }

    public static string Inventory(ReviewProject project)
    {
        var state = new ReviewState(project);
        var rows = new List<string[]>
        {
            new[] { "id", "titulo", "doi", "fonte_original", "situacao", "id_final", "fontes_conjunto" }
        };

        foreach (var record in project.Records)
        {
            var representative = state.Representative(record.Id);
            var sources = project.Records
                .Where(item => state.Representative(item.Id) == representative)
                .Select(item => item.Source)
                .Distinct();

            rows.Add(new[]
            {
                record.Id,
                record.Title,
                record.Doi,
                record.Source,
                state.RemovedToKept.ContainsKey(record.Id) ? "removido" : "mantido",
                representative,
                string.Join("; ", sources)
            });
        }

        return Csv(rows);
    }

    public static string Decisions(ReviewProject project)
    {
        var rows = new List<string[]>
        {
            new[] { "acao", "mantido", "removido", "a", "b", "criterio", "justificativa", "responsavel", "data" }
        };

        rows.AddRange(project.Audit.Select(decision => new[]
        {
            decision.Kind,
            decision.Keep,
            decision.Remove,
            decision.A,
            decision.B,
            decision.Match,
            decision.Reason,
            decision.Reviewer,
            decision.At.ToString("O", CultureInfo.InvariantCulture)
        }));

        return Csv(rows);
    }

    public static string Report(ReviewProject project)
    {
        var state = new ReviewState(project);
        var pending = state.PendingPairs(project);

        var report = new
        {
            Tool = "Deduplicação local C#",
            Version = "2.0.0",
            Exported = DateTimeOffset.UtcNow,
            Status = pending.Count > 0 ? "PARCIAL: há pares pendentes" : "Candidatos revisados",
            Initial = project.Records.Count,
            Removed = state.RemovedToKept.Count,
            Retained = project.Records.Count - state.RemovedToKept.Count,
            PendingPairs = pending,
            Protocol = new
            {
                Rules = "DOI não vazio normalizado igual; título normalizado igual; ou Dice de bigramas.",
                DuplicateDetector.SimilarityThreshold,
                DuplicateDetector.MinimumTitleLength,
                Normalization = "NFD, remoção U+0300–U+036F, minúsculas, pontuação substituída por espaços.",
                Confirmation = "Todas as remoções dependem de confirmação humana.",
                Limit = "Limiar heurístico. Ausência de candidatos não garante ausência de duplicatas."
            },
            Files = project.Files.Select(file => new
            {
                file.Name, file.Sha256, file.Encoding, file.Count, file.Imported
            }),
            ActiveDecisions = project.Events,
            project.Audit
        };

        return JsonSerializer.Serialize(report, ProjectStore.JsonOptions);
    }

    private static string Csv(IEnumerable<string[]> rows)
    {
        return "\uFEFF" + string.Join("\r\n", rows.Select(row =>
            string.Join(",", row.Select(EscapeCell))));
    }

    private static string EscapeCell(string? value)
    {
        var text = value ?? "";

        if (text.Length > 0 && "=+@-".Contains(text[0]))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
