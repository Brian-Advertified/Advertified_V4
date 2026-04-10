using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

namespace Advertified.App.Controllers;

[ApiController]
[Route("public/suburbs")]
[AllowAnonymous]
[EnableRateLimiting("public_general")]
public sealed class PublicSuburbsController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;

    public PublicSuburbsController(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    [HttpGet]
    public async Task<ActionResult<List<string>>> Get([FromQuery] string city, CancellationToken cancellationToken)
    {
        city = (city ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(city))
        {
            return Ok(new List<string>());
        }

        const string sql = @"
select distinct trim(iif.suburb) as suburb
from inventory_items_final iif
where lower(coalesce(iif.city, '')) = lower(@City)
  and coalesce(trim(iif.suburb), '') <> ''
order by suburb;";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(sql, new { City = city }, cancellationToken: cancellationToken));
        return Ok(rows.ToList());
    }
}
