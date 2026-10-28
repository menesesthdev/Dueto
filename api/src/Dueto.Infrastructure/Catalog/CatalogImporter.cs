using System.Text.Json;
using System.Text.Json.Serialization;
using Dueto.Core.Catalog;
using Dueto.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dueto.Infrastructure.Catalog;

/// <summary>O catalog.json está ausente, ilegível ou inválido. A mensagem lista todos os problemas.</summary>
public sealed class CatalogImportException(string message) : Exception(message);

/// <summary>
/// Importa o catalog.json para o banco por upsert (insere os novos, atualiza os existentes pelo id).
/// Nunca remove wallpapers: retirar um do catálogo é uma operação manual, porque estados de pares já
/// podem apontar para ele.
/// </summary>
internal sealed class CatalogImporter(
    DuetoDbContext db,
    IOptions<CatalogOptions> options,
    ILogger<CatalogImporter> logger)
{
    private const long MaxFileBytes = 1_048_576;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Um nome de campo errado (ex.: "desktopUrI") vira erro em vez de ser ignorado em silêncio.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ImportFile))
        {
            return;
        }

        var document = await ReadAsync(settings.ImportFile, cancellationToken);
        var result = CatalogValidator.Validate(document, settings.EffectiveAllowedHosts);

        if (!result.IsValid)
        {
            throw new CatalogImportException(
                $"The catalog file is invalid ({result.Errors.Count} problem(s)):{Environment.NewLine}"
                + string.Join(Environment.NewLine, result.Errors.Take(20).Select(e => "  - " + e)));
        }

        var ids = result.Wallpapers.Select(w => w.Id).ToList();
        var existing = await db.Wallpapers
            .Where(w => ids.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        var added = 0;
        var updated = 0;

        foreach (var incoming in result.Wallpapers)
        {
            if (!existing.TryGetValue(incoming.Id, out var current))
            {
                db.Wallpapers.Add(incoming);
                added++;
                continue;
            }

            current.Title = incoming.Title;
            current.Kind = incoming.Kind;
            current.DesktopUrl = incoming.DesktopUrl;
            current.DesktopSha256 = incoming.DesktopSha256;
            current.MobileUrl = incoming.MobileUrl;
            current.MobileSha256 = incoming.MobileSha256;
            current.ThumbnailUrl = incoming.ThumbnailUrl;
            current.Tags = incoming.Tags;
            current.CreatedAt = incoming.CreatedAt;

            if (db.Entry(current).State == EntityState.Modified)
            {
                updated++;
            }
        }

        // Um único SaveChanges é uma única transação: ou o catálogo inteiro entra, ou nada.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Catalog imported: {Added} added, {Updated} updated, {Total} in the file.",
            added,
            updated,
            result.Wallpapers.Count);
    }

    private static async Task<CatalogDocument?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new CatalogImportException($"The catalog file was not found: {file.FullName} (relative paths are resolved from the working directory).");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new CatalogImportException($"The catalog file is larger than {MaxFileBytes / 1024} KiB.");
        }

        try
        {
            await using var stream = file.OpenRead();
            return await JsonSerializer.DeserializeAsync<CatalogDocument>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            // A mensagem do JsonException traz a posição do erro, não o conteúdo do arquivo.
            throw new CatalogImportException($"The catalog file is not valid JSON: {exception.Message}");
        }
    }
}
