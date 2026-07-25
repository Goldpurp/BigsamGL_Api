using System.Text.Json.Serialization;

namespace Api.Controllers;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    [property: JsonPropertyName("hasPreviousPage")] bool HasPrevious,
    [property: JsonPropertyName("hasNextPage")] bool HasNext,
    long? Total,
    int? TotalPages);

public sealed record CursorPagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageSize,
    bool HasMore,
    string? NextCursor);

public readonly record struct CursorPosition(DateTime Timestamp, Guid Id);
