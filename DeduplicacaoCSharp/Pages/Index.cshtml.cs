using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeduplicacaoCSharp.Models;
using DeduplicacaoCSharp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeduplicacaoCSharp.Pages;

public sealed class IndexModel : PageModel
{
    private readonly ProjectStore _store;

    public IndexModel(ProjectStore store)
    {
        _store = store;
    }

    public ReviewProject Project { get; private set; } = new();
    public ReviewState State { get; private set; } = null!;
    public List<CandidatePair> Pending { get; private set; } = new();
    public List<BibliographicRecord> FilteredRecords { get; private set; } = new();
    public CandidatePair? SelectedPair { get; private set; }
    public BibliographicRecord? Detail { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "pairs";

    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string Status { get; set; } = "all";

    [TempData]
    public string? Message { get; set; }

    public void OnGet(string? a, string? b, string? id)
    {
        Project = _store.Snapshot();
        State = new ReviewState(Project);
        Pending = State.PendingPairs(Project);
        SelectedPair = Pending.FirstOrDefault(pair => pair.A == a && pair.B == b)
            ?? Pending.FirstOrDefault();
        Detail = Project.Records.FirstOrDefault(record => record.Id == id);

        var query = TextNormalizer.NormalizeTitle(Query);

        FilteredRecords = Project.Records.Where(record =>
        {
            var removed = State.RemovedToKept.ContainsKey(record.Id);
            var matchesStatus = Status == "all"
                || (Status == "removed" && removed)
                || (Status == "kept" && !removed);
            var searchable = TextNormalizer.NormalizeTitle(
                $"{record.Id} {record.Title} {record.Authors} {record.Doi}");

            return matchesStatus && searchable.Contains(query);
        }).ToList();
    }

    public BibliographicRecord Record(string id)
    {
        return Project.Records.Single(record => record.Id == id);
    }

    public string Sources(string id)
    {
        var representative = State.Representative(id);

        return string.Join("; ", Project.Records
            .Where(record => State.Representative(record.Id) == representative)
            .Select(record => record.Source)
            .Distinct());
    }

    public async Task<IActionResult> OnPostImportAsync(
        List<IFormFile> risFiles,
        string encodingName,
        long revision)
    {
        try
        {
            if (risFiles.Count == 0)
            {
                throw new InvalidDataException("Selecione ao menos um arquivo RIS.");
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var encoding = encodingName == "windows-1252"
                ? Encoding.GetEncoding(1252)
                : new UTF8Encoding(false, true);
            var inputs = new List<ImportedFile>();

            foreach (var upload in risFiles)
            {
                using var stream = new MemoryStream();
                await upload.CopyToAsync(stream);
                var bytes = stream.ToArray();

                inputs.Add(new ImportedFile
                {
                    Name = Path.GetFileName(upload.FileName),
                    Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                    Encoding = encodingName,
                    Imported = DateTimeOffset.UtcNow,
                    OriginalText = encoding.GetString(bytes)
                });
            }

            _store.Update(revision, project =>
            {
                foreach (var input in inputs)
                {
                    if (project.Files.Any(file => file.Sha256 == input.Sha256))
                    {
                        throw new InvalidDataException($"Arquivo idêntico já importado: {input.Name}.");
                    }

                    var lastNumber = project.Records
                        .Select(record => int.Parse(record.Id[1..]))
                        .DefaultIfEmpty(0)
                        .Max();
                    var records = RisParser.Parse(input.OriginalText, input.Name, lastNumber);
                    input.Count = records.Count;
                    project.Records.AddRange(records);
                    project.Files.Add(input);
                }
            });

            Message = "Importação concluída e salva. Confira o total com suas exportações originais.";
        }
        catch (Exception exception)
        {
            Message = "Importação cancelada: " + exception.Message;
        }

        return RedirectToPage(new { tab = "records" });
    }

    public IActionResult OnPostDecide(
        long revision,
        string a,
        string b,
        string choice,
        string reviewer,
        string reason)
    {
        return Change(revision, project =>
        {
            if (string.IsNullOrWhiteSpace(reviewer) || string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidDataException("Preencha o responsável e a justificativa.");
            }

            var state = new ReviewState(project);
            var pair = state.PendingPairs(project).FirstOrDefault(item => item.A == a && item.B == b)
                ?? throw new InvalidDataException("Este par não está mais pendente.");

            if (choice != "distinct" && choice != a && choice != b)
            {
                throw new InvalidDataException("Escolha inválida.");
            }

            var decision = new ReviewDecision
            {
                Kind = choice == "distinct" ? "distinct" : "merge",
                A = a,
                B = b,
                Keep = choice == "distinct" ? "" : choice,
                Remove = choice == "distinct" ? "" : choice == a ? b : a,
                Reviewer = reviewer.Trim(),
                Reason = reason.Trim(),
                Match = $"{pair.Reason}; evidência original: {pair.EvidenceA} × {pair.EvidenceB}",
                OriginalScore = pair.Score
            };

            state.Apply(decision);
            project.Events.Add(decision);
            project.Audit.Add(decision);
            project.Reviewer = reviewer.Trim();
        }, "Decisão registrada e salva.");
    }

    public IActionResult OnPostUndo(long revision)
    {
        return Change(revision, project =>
        {
            if (project.Events.Count == 0)
            {
                throw new InvalidOperationException("Não há decisão para desfazer.");
            }

            var decision = project.Events[^1];
            project.Events.RemoveAt(project.Events.Count - 1);
            project.Audit.Add(new ReviewDecision
            {
                Kind = "undo",
                Reviewer = project.Reviewer,
                Reason = "Desfeita decisão: " + JsonSerializer.Serialize(decision, ProjectStore.JsonOptions)
            });
        }, "Última decisão desfeita. A reversão permanece no histórico.");
    }

    public async Task<IActionResult> OnPostRestoreAsync(
        IFormFile? backup,
        long revision,
        bool confirmReplace)
    {
        try
        {
            if (backup is null || !confirmReplace)
            {
                throw new InvalidDataException("Selecione o backup e confirme a substituição.");
            }

            using var reader = new StreamReader(backup.OpenReadStream());
            var restored = ProjectStore.ReadBackup(await reader.ReadToEndAsync());

            _store.Update(revision, project => CopyProject(restored, project));
            Message = "Projeto restaurado, incluindo registros e decisões.";
        }
        catch (Exception exception)
        {
            Message = "Restauração cancelada: " + exception.Message;
        }

        return RedirectToPage();
    }

    public IActionResult OnPostReset(long revision, bool confirmReplace)
    {
        return Change(revision, project =>
        {
            if (!confirmReplace)
            {
                throw new InvalidOperationException("Confirme a criação de um projeto vazio.");
            }

            CopyProject(new ReviewProject(), project);
        }, "Novo projeto criado.");
    }

    public IActionResult OnGetDownload(string kind, bool partial = false)
    {
        var project = _store.Snapshot();
        var hasPending = new ReviewState(project).PendingPairs(project).Count > 0;

        if (kind == "ris" && hasPending && !partial)
        {
            Message = "Há pares pendentes. Use a opção de exportação parcial para mantê-los no RIS.";
            return RedirectToPage();
        }

        var export = kind switch
        {
            "project" => (JsonSerializer.Serialize(project, ProjectStore.JsonOptions), "projeto.json", "application/json"),
            "ris" => (ExportService.Ris(project), hasPending ? "referencias_parciais.ris" : "referencias_mantidas.ris", "application/x-research-info-systems"),
            "inventory" => (ExportService.Inventory(project), "inventario.csv", "text/csv"),
            "decisions" => (ExportService.Decisions(project), "decisoes.csv", "text/csv"),
            "report" => (ExportService.Report(project), "relatorio.json", "application/json"),
            _ => throw new BadHttpRequestException("Exportação desconhecida.")
        };

        return File(Encoding.UTF8.GetBytes(export.Item1), export.Item3, export.Item2);
    }

    private IActionResult Change(long revision, Action<ReviewProject> change, string success)
    {
        try
        {
            _store.Update(revision, change);
            Message = success;
        }
        catch (Exception exception)
        {
            Message = "Alteração cancelada: " + exception.Message;
        }

        return RedirectToPage();
    }

    private static void CopyProject(ReviewProject source, ReviewProject destination)
    {
        destination.Created = source.Created;
        destination.Reviewer = source.Reviewer;
        destination.Records = source.Records;
        destination.Files = source.Files;
        destination.Events = source.Events;
        destination.Audit = source.Audit;
    }
}
