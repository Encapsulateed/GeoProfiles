using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeoProfiles.Infrastructure;

public sealed record ErrorResponse(
    string ErrorCode,
    string ErrorMessage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? ErrorDetails = null)
{
    private readonly DateTime _createdAt = DateTime.UtcNow;

    private string? _errorId;

    public string ErrorId =>
        _errorId ??= ComputeHash(
            ErrorCode,
            ErrorMessage,
            JsonSerializer.Serialize(ErrorDetails ?? ""),
            _createdAt.ToString("yyyy-MM-dd HH:mm")
        ).ToString("x8");

    private static ulong ComputeHash(params object[] fields)
    {
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime  = 1099511628211UL;

        ulong hash = fnvOffset;
        foreach (var field in fields)
        {
            unchecked
            {
                hash ^= (ulong)field.GetHashCode();
                hash *= fnvPrime;
            }
        }
        return hash;
    }
}