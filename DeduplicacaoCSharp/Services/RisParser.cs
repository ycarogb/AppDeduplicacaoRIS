using System.Text.RegularExpressions;
using DeduplicacaoCSharp.Models;

namespace DeduplicacaoCSharp.Services;

public static class RisParser
{
    private static readonly Regex FieldPattern = new(@"^([A-Z0-9]{2})\s{2}-\s?(.*)$");

    public static List<BibliographicRecord> Parse(string text, string source, int start = 0)
    {
        var records = new List<BibliographicRecord>();
        BibliographicRecord? current = null;
        var rawLines = new List<string>();
        string? lastTag = null;

        foreach (var line in text.TrimStart('\uFEFF').Replace("\r\n", "\n").Split('\n'))
        {
            var match = FieldPattern.Match(line);
            var tag = match.Success ? match.Groups[1].Value : "";

            if (tag == "TY")
            {
                if (current is not null)
                {
                    throw new InvalidDataException("Registro sem ER antes do próximo TY.");
                }

                current = new BibliographicRecord
                {
                    Id = $"R{start + records.Count + 1:D5}",
                    Source = source
                };

                rawLines.Clear();
                lastTag = null;
            }

            if (current is null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    throw new InvalidDataException("Texto fora de um registro RIS.");
                }

                continue;
            }

            rawLines.Add(line);

            if (tag == "ER")
            {
                current.Raw = string.Join('\n', rawLines) + "\n";
                records.Add(current);
                current = null;
            }
            else if (match.Success)
            {
                if (!current.Fields.ContainsKey(tag))
                {
                    current.Fields[tag] = new List<string>();
                }

                current.Fields[tag].Add(match.Groups[2].Value);
                lastTag = tag;
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                if (lastTag is null)
                {
                    throw new InvalidDataException("Continuação sem campo RIS.");
                }

                var values = current.Fields[lastTag];
                values[^1] += " " + line.Trim();
            }
        }

        if (current is not null)
        {
            throw new InvalidDataException("Último registro sem terminador ER.");
        }

        if (records.Count == 0)
        {
            throw new InvalidDataException("Nenhum registro RIS encontrado.");
        }

        return records;
    }
}
