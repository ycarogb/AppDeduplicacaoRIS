using System.Text.Json.Serialization;

namespace DeduplicacaoCSharp.Models;

public sealed class BibliographicRecord
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "";
    public Dictionary<string, List<string>> Fields { get; set; } = new();
    public string Raw { get; set; } = "";

    [JsonIgnore]
    public string Title => First("TI", "T1", "CT");

    [JsonIgnore]
    public string Doi => First("DO");

    [JsonIgnore]
    public string Year => First("PY", "Y1");

    [JsonIgnore]
    public string Authors
    {
        get
        {
            if (Fields.TryGetValue("AU", out var authors))
            {
                return string.Join("; ", authors);
            }

            return Fields.TryGetValue("A1", out authors)
                ? string.Join("; ", authors)
                : "";
        }
    }

    public string First(params string[] tags)
    {
        foreach (var tag in tags)
        {
            if (Fields.TryGetValue(tag, out var values))
            {
                var value = values.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return "";
    }
}
