namespace DeduplicacaoCSharp.Models;

public sealed class ReviewProject
{
    public string Version { get; set; } = "2.0.0";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public long Revision { get; set; }
    public string Reviewer { get; set; } = "";
    public List<BibliographicRecord> Records { get; set; } = new();
    public List<ImportedFile> Files { get; set; } = new();
    public List<ReviewDecision> Events { get; set; } = new();
    public List<ReviewDecision> Audit { get; set; } = new();
}

public sealed class ImportedFile
{
    public string Name { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Encoding { get; set; } = "utf-8";
    public int Count { get; set; }
    public DateTimeOffset? Imported { get; set; }
    public string OriginalText { get; set; } = "";
}
