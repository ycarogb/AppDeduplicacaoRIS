using System.Text.Json;
using DeduplicacaoCSharp.Models;
using DeduplicacaoCSharp.Services;

var checks = 0;

void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new Exception("Falha: " + description);
    }

    checks++;
    Console.WriteLine("OK: " + description);
}

void MustThrow(Action action, string description)
{
    var threw = false;

    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        threw = true;
    }

    Check(threw, description);
}

string Ris(string title, string doi = "")
{
    return $"TY  - JOUR\nTI  - {title}\nAU  - Silva, Ana\nPY  - 2024\nDO  - {doi}\nER  -\n";
}

ReviewDecision Merge(string keep, string remove)
{
    return new ReviewDecision
    {
        Kind = "merge",
        Keep = keep,
        Remove = remove,
        Reviewer = "Teste",
        Reason = "Metadados conferidos"
    };
}

Check(TextNormalizer.NormalizeDoi("https://doi.org/10.1234/TEST") == "10.1234/test", "DOI normalizado");
Check(TextNormalizer.NormalizeTitle("Água: SEGURANÇA!") == "agua seguranca", "Título normalizado");
Check(SimilarityCalculator.Calculate("ABCD", "ABCE") == 2.0 / 3.0, "Dice com resultado conhecido");

var records = RisParser.Parse(
    Ris("Women and shared bicycle safety", "10.1234/test")
    + Ris("Women and shared bicycle safety", "https://doi.org/10.1234/TEST")
    + Ris("Women and shared bicycle safety", "10.1234/test"),
    "base.ris");

var project = new ReviewProject { Records = records };

Check(records.Count == 3, "Três registros importados");
Check(DuplicateDetector.FindCandidates(records).Count == 3, "Três pares, não três cópias excedentes");

project.Events.Add(Merge("R00001", "R00002"));
var state = new ReviewState(project);
Check(state.PendingPairs(project).Count == 1, "Representantes consolidados preservam candidato restante");
Check(RisParser.Parse(ExportService.Ris(project), "saida").Count == 2, "Exportação após uma remoção");

project.Events.Add(Merge("R00003", "R00001"));
state = new ReviewState(project);
Check(state.Representative("R00002") == "R00003", "Cadeia de remoções");
Check(state.RemovedToKept.Count == 2, "Duas cópias removidas");
Check(RisParser.Parse(ExportService.Ris(project), "saida").Count == 1, "Uma referência final");

project.Events.RemoveAt(project.Events.Count - 1);
Check(new ReviewState(project).PendingPairs(project).Count == 1, "Desfazer reabre candidato");

project.Events.Insert(0, new ReviewDecision
{
    Kind = "distinct",
    A = "R00002",
    B = "R00003"
});

state = new ReviewState(project);
MustThrow(() => state.Apply(Merge("R00001", "R00003")), "Bloqueio de conflito transitivo");

var emptyDois = RisParser.Parse(Ris("First unrelated work") + Ris("Another unrelated topic"), "base");
Check(DuplicateDetector.FindCandidates(emptyDois).Count == 0, "DOIs vazios não correspondem");

var conflicts = RisParser.Parse(Ris("Identical title", "10.1/a") + Ris("Identical title", "10.1/b"), "base");
Check(DuplicateDetector.FindCandidates(conflicts).Single().ConflictingDois, "DOIs diferentes destacados");

MustThrow(() => RisParser.Parse("TY  - JOUR\nTI  - Incompleto", "base"), "Registro sem ER rejeitado");
MustThrow(() => RisParser.Parse("Texto inválido", "base"), "RIS inválido rejeitado");

var multiline = RisParser.Parse("TY  - JOUR\nTI  - Teste\nAB  - Primeira\n segunda\nAU  - A\nAU  - B\nER  -", "base").Single();
Check(multiline.First("AB") == "Primeira segunda", "Resumo multilinha");
Check(multiline.Authors == "A; B", "Autores repetidos preservados");

var backup = JsonSerializer.Serialize(new ReviewProject
{
    Version = "1.0.0",
    Records = records
}, ProjectStore.JsonOptions);

Check(ProjectStore.ReadBackup(backup).Records.Count == 3, "Backup da versão 1 aceito");

var invalidProject = new ReviewProject { Records = RisParser.Parse(Ris("Original"), "base") };
invalidProject.Records[0].Fields["TI"][0] = "Alterado sem modificar o RIS";
MustThrow(() => ProjectStore.Validate(invalidProject), "Divergência entre metadados e RIS rejeitada");

Console.WriteLine($"\n{checks} verificações concluídas.");
