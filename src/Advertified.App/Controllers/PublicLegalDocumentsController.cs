using System.Text.Json;
using Advertified.App.Contracts.Legal;
using Advertified.App.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("public/legal-documents")]
[AllowAnonymous]
public sealed class PublicLegalDocumentsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public PublicLegalDocumentsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{documentKey}")]
    public async Task<ActionResult<LegalDocumentResponse>> GetByKey(string documentKey, CancellationToken cancellationToken)
    {
        var document = await _db.LegalDocuments
            .AsNoTracking()
            .Where(x => x.IsActive && x.DocumentKey == documentKey)
            .Select(x => new
            {
                x.DocumentKey,
                x.Title,
                x.VersionLabel,
                x.BodyJson,
                x.UpdatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        var sections = JsonSerializer.Deserialize<IReadOnlyList<LegalDocumentSectionResponse>>(document.BodyJson, JsonOptions)
            ?? Array.Empty<LegalDocumentSectionResponse>();

        return Ok(new LegalDocumentResponse
        {
            DocumentKey = document.DocumentKey,
            Title = document.Title,
            VersionLabel = document.VersionLabel,
            Sections = sections,
            UpdatedAtUtc = document.UpdatedAtUtc
        });
    }
}
