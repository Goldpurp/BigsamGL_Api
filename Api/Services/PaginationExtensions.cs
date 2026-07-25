using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public static class PaginationExtensions
{
    public const int DefaultPageSize = 25;
    private static readonly int[] AllowedPageSizes = [10, 25, 50, 100, 200];
    public const int MaximumPageSize = 200;

    public static async Task<PagedResponse<T>> ToPagedResponseAsync<T>(this IQueryable<T> query,
        int page, int pageSize, bool includeTotal, CancellationToken cancellationToken)
    {
        page = Math.Clamp(page, 1, 100_000);
        pageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;
        long? total = await query.LongCountAsync(cancellationToken);
        var skip = checked((page - 1) * pageSize);
        var requested = pageSize;
        var rows = await query.Skip(skip).Take(requested).ToListAsync(cancellationToken);
        var hasNext = (long)page * pageSize < total.Value;
        int? totalPages = (int)Math.Ceiling(total.Value / (double)pageSize);
        return new PagedResponse<T>(rows, page, pageSize, page > 1, hasNext, total, totalPages);
    }

    public static string EncodeCursor(DateTime timestamp, Guid id)
    {
        var value = string.Create(CultureInfo.InvariantCulture, $"{timestamp.ToUniversalTime().Ticks}:{id:D}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
    }

    public static CursorPosition DecodeCursor(string cursor)
    {
        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(normalized)).Split(':', 2);
            if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var ticks) || !Guid.TryParse(parts[1], out var id))
                throw new FormatException();
            return new CursorPosition(new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new ArgumentException("The pagination cursor is invalid.");
        }
    }
}
