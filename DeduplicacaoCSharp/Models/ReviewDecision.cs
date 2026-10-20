namespace DeduplicacaoCSharp.Models;

public sealed class ReviewDecision
{
    // Nomes compatíveis com os backups da primeira aplicação.
    public string Kind { get; set; } = "";
    public string Keep { get; set; } = "";
    public string Remove { get; set; } = "";
    public string A { get; set; } = "";
    public string B { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Reviewer { get; set; } = "";
    public string Match { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public double OriginalScore { get; set; }
}
