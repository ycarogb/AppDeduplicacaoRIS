namespace DeduplicacaoCSharp.Models;

public sealed record CandidatePair(
    string A,
    string B,
    string Reason,
    double Score,
    bool ConflictingDois,
    string EvidenceA,
    string EvidenceB);
