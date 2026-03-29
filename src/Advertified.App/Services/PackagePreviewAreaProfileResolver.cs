using Dapper;
using Npgsql;
using System.Text.Json;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class PackagePreviewAreaProfileResolver : IPackagePreviewAreaProfileResolver
{
    public async Task<PackagePreviewAreaProfile> ResolveAsync(NpgsqlConnection connection, string? selectedArea, CancellationToken cancellationToken)
    {
        var profiles = await GetAreaProfilesAsync(connection, cancellationToken);
        return ResolveSelectedAreaProfile(selectedArea, profiles);
    }

    private static async Task<Dictionary<string, PackagePreviewAreaProfile>> GetAreaProfilesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = (await connection.QueryAsync<PackagePreviewAreaProfileRow>(
            new CommandDefinition(
                @"
                select
                    pap.cluster_code as Code,
                    pap.display_name as Name,
                    pap.description as Description,
                    pap.fallback_locations_json as FallbackLocationsJson,
                    rcm.province as Province,
                    rcm.city as City,
                    rcm.station_or_channel_name as StationOrChannelName
                from package_area_profiles pap
                left join region_clusters rc on rc.code = pap.cluster_code
                left join region_cluster_mappings rcm on rcm.cluster_id = rc.id
                where pap.is_active = true
                order by pap.sort_order, pap.display_name;
                ",
                cancellationToken: cancellationToken)))
            .ToList();

        var result = new Dictionary<string, PackagePreviewAreaProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!result.TryGetValue(row.Code, out var profile))
            {
                profile = new PackagePreviewAreaProfile
                {
                    Code = row.Code,
                    Name = row.Name,
                    Description = row.Description,
                    FallbackExampleLocations = DeserializeList(row.FallbackLocationsJson)
                };
                result[row.Code] = profile;
            }

            AddDistinct(profile.ProvinceTerms, row.Province);
            AddDistinct(profile.CityTerms, row.City);
            AddDistinct(profile.StationTerms, row.StationOrChannelName);
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("Package area profiles have not been configured.");
        }

        return result;
    }

    private static PackagePreviewAreaProfile ResolveSelectedAreaProfile(
        string? selectedArea,
        IReadOnlyDictionary<string, PackagePreviewAreaProfile> areaProfiles)
    {
        if (!string.IsNullOrWhiteSpace(selectedArea)
            && areaProfiles.TryGetValue(selectedArea.Trim(), out var directMatch))
        {
            return directMatch;
        }

        if (!string.IsNullOrWhiteSpace(selectedArea))
        {
            var normalized = selectedArea.Trim().ToLowerInvariant();
            var resolved = areaProfiles.Values.FirstOrDefault(profile =>
                profile.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || profile.Name.Equals(selectedArea.Trim(), StringComparison.OrdinalIgnoreCase)
                || profile.ProvinceTerms.Any(term => term.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                || profile.CityTerms.Any(term => term.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return areaProfiles.Values.First();
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static void AddDistinct(List<string> values, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        var normalized = rawValue.Trim().ToLowerInvariant();
        if (values.Any(existing => existing.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        values.Add(normalized);
    }
}
