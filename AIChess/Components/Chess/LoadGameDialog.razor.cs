using AIChess.Services;

namespace AIChess.Components.Chess;

public partial class LoadGameDialog
{
    private List<GameStorageService.SavedGame> _games = new();
    private string _filter = string.Empty;

    private IEnumerable<GameStorageService.SavedGame> Filtered => _games
        .Where(g => string.IsNullOrWhiteSpace(_filter) || g.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        await Reload();
    }

    private async Task Reload()
    {
        _games = await Storage.GetAllAsync();
        StateHasChanged();
    }

    private void Load(GameStorageService.SavedGame g)
    {
        DialogService.Close(g);
    }

    private async Task Delete(GameStorageService.SavedGame g)
    {
        var ok = await Storage.DeleteAsync(g.Name);
        if (ok)
        {
            _games.RemoveAll(x => string.Equals(x.Name, g.Name, StringComparison.OrdinalIgnoreCase));
            StateHasChanged();
        }
    }
}