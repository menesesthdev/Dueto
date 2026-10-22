namespace Dueto.Core.Catalog;

/// <summary>Caso de uso de leitura do catálogo de wallpapers.</summary>
public interface IWallpaperCatalog
{
    /// <summary>Lista o catálogo, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<WallpaperDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Busca um wallpaper pelo slug; nulo se não existir.</summary>
    Task<WallpaperDto?> FindAsync(string id, CancellationToken cancellationToken);
}
