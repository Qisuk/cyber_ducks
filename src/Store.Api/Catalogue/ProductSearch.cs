using CyberDucks.Shared.Contracts;
using Npgsql;

namespace Store.Api.Catalogue;

/// <summary>Product search behind VULN-SQLI-01.</summary>
public interface IProductSearch
{
    Task<IReadOnlyList<ProductSummary>> SearchAsync(string query, CancellationToken ct);
}

/// <summary>
/// VULNERABLE (VULN-SQLI-01): the search text is concatenated straight into the SQL. A crafted
/// query changes the statement (classic SQL injection) and can read other tables via UNION
/// (threat TH26, assets A1/A2). Verbose errors (VULN-INFO-01) make it easy to develop the payload.
/// </summary>
public sealed class VulnerableProductSearch(NpgsqlDataSource db) : IProductSearch
{
    public async Task<IReadOnlyList<ProductSummary>> SearchAsync(string query, CancellationToken ct)
    {
        var sql = "SELECT id, sku, name, price, stock, image_url FROM products " +
                  $"WHERE name ILIKE '%{query}%' OR description ILIKE '%{query}%' ORDER BY id";
        await using var cmd = db.CreateCommand(sql);
        return await ReadAsync(cmd, ct);
    }

    internal static async Task<IReadOnlyList<ProductSummary>> ReadAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var results = new List<ProductSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new ProductSummary(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                AverageRating: null,
                ReviewCount: 0));
        }
        return results;
    }
}

/// <summary>SECURE (VULN-SQLI-01): the search text is a bound parameter; it can never change the SQL.</summary>
public sealed class SecureProductSearch(NpgsqlDataSource db) : IProductSearch
{
    public async Task<IReadOnlyList<ProductSummary>> SearchAsync(string query, CancellationToken ct)
    {
        const string sql = "SELECT id, sku, name, price, stock, image_url FROM products " +
                           "WHERE name ILIKE '%' || @q || '%' OR description ILIKE '%' || @q || '%' ORDER BY id";
        await using var cmd = db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("q", query);
        return await VulnerableProductSearch.ReadAsync(cmd, ct);
    }
}
