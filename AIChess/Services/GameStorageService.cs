using System.Text.Json;
using Microsoft.JSInterop;

namespace AIChess.Services;

public class GameStorageService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "aichess.saves";

    public GameStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public class SavedGame
    {
        public string Name { get; set; } = string.Empty;
        public string Fen { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public async Task<List<SavedGame>> GetAllAsync()
    {
        var json = await _js.InvokeAsync<string?>("aiChess.storage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return new List<SavedGame>();
        try
        {
            var list = JsonSerializer.Deserialize<List<SavedGame>>(json) ?? new List<SavedGame>();
            return list.OrderByDescending(g => g.UpdatedAt).ToList();
        }
        catch
        {
            return new List<SavedGame>();
        }
    }

    private async Task SaveAllAsync(List<SavedGame> games)
    {
        var json = JsonSerializer.Serialize(games);
        await _js.InvokeVoidAsync("aiChess.storage.setItem", StorageKey, json);
    }

    public async Task SaveAsync(string name, string fen)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required", nameof(name));
        var games = await GetAllAsync();
        var existing = games.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            games.Add(new SavedGame
            {
                Name = name,
                Fen = fen,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Fen = fen;
            if (existing.CreatedAt == default) existing.CreatedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await SaveAllAsync(games);
    }

    public async Task<string?> LoadFenAsync(string name)
    {
        var games = await GetAllAsync();
        return games.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))?.Fen;
    }

    public async Task<bool> DeleteAsync(string name)
    {
        var games = await GetAllAsync();
        var removed = games.RemoveAll(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        await SaveAllAsync(games);
        return removed > 0;
    }
}
