using System.Text.Json;
using System.Text.RegularExpressions;
using DeduplicacaoCSharp.Models;

namespace DeduplicacaoCSharp.Services;

/// <summary>
/// Um projeto local por processo. Alterações são validadas antes da gravação.
/// Uma cópia temporária é renomeada para evitar arquivos parcialmente escritos.
/// </summary>
public sealed class ProjectStore
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _path;
    private ReviewProject _project;

    public ProjectStore(IWebHostEnvironment environment)
    {
        var directory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "projeto.json");

        _project = File.Exists(_path)
            ? ReadBackup(File.ReadAllText(_path))
            : new ReviewProject();
    }

    public ReviewProject Snapshot()
    {
        lock (_sync)
        {
            return Clone(_project);
        }
    }

    public void Update(long revision, Action<ReviewProject> change)
    {
        lock (_sync)
        {
            if (revision != _project.Revision)
            {
                throw new InvalidOperationException(
                    "O projeto mudou em outra aba. Atualize a página antes de continuar.");
            }

            var next = Clone(_project);
            change(next);
            Validate(next);
            next.Version = "2.0.0";
            next.Revision = _project.Revision + 1;

            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(next, JsonOptions));

            if (File.Exists(_path))
            {
                File.Copy(_path, _path + ".previous", overwrite: true);
            }

            File.Move(temporaryPath, _path, overwrite: true);
            _project = next;
        }
    }

    public static ReviewProject ReadBackup(string json)
    {
        var project = JsonSerializer.Deserialize<ReviewProject>(json, JsonOptions)
            ?? throw new InvalidDataException("Backup vazio.");

        Validate(project);
        return project;
    }

    public static void Validate(ReviewProject project)
    {
        if (project.Version is not ("1.0.0" or "2.0.0")
            || project.Records is null || project.Files is null
            || project.Events is null || project.Audit is null)
        {
            throw new InvalidDataException("Formato ou versão do projeto incompatível.");
        }

        var ids = new HashSet<string>();

        foreach (var record in project.Records)
        {
            if (record is null || record.Id is null || !Regex.IsMatch(record.Id, @"^R\d+$")
                || !ids.Add(record.Id) || record.Raw is null || record.Source is null
                || record.Fields is null
                || record.Fields.Any(field => field.Value is null
                    || field.Value.Any(value => value is null)))
            {
                throw new InvalidDataException("Registro inválido ou ID repetido no backup.");
            }

            // Impede que metadados exibidos divirjam silenciosamente do RIS exportado.
            var parsed = RisParser.Parse(record.Raw, record.Source);

            if (parsed.Count != 1 || parsed[0].Fields.Count != record.Fields.Count
                || parsed[0].Fields.Any(field =>
                    !record.Fields.TryGetValue(field.Key, out var values)
                    || !field.Value.SequenceEqual(values)))
            {
                throw new InvalidDataException($"RIS e metadados divergem em {record.Id}.");
            }
        }

        if (project.Events.Any(item => item is null)
            || project.Audit.Any(item => item is null)
            || project.Files.Any(item => item is null))
        {
            throw new InvalidDataException("Itens nulos no backup.");
        }

        _ = new ReviewState(project);
    }

    private static ReviewProject Clone(ReviewProject project)
    {
        var json = JsonSerializer.Serialize(project, JsonOptions);
        return JsonSerializer.Deserialize<ReviewProject>(json, JsonOptions)!;
    }
}
