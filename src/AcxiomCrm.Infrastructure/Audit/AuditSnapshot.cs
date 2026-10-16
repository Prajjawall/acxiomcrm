using System.Text.Json;
using System.Text.Json.Serialization;

namespace AcxiomCrm.Infrastructure.Audit;

public static class AuditSnapshot
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    public static string Serialize(IDictionary<string, object?> values)
    {
        return JsonSerializer.Serialize(values, JsonOptions);
    }
}
