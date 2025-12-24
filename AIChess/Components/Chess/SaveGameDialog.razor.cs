using Microsoft.AspNetCore.Components;

namespace AIChess.Components.Chess;

public partial class SaveGameDialog
{
    [Parameter] public string Fen { get; set; } = string.Empty;
    private string _name = string.Empty;
    private string _error = string.Empty;
    private bool _busy;

    protected override void OnInitialized()
    {
        _name = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
    }

    private async Task SaveAsync()
    {
        try
        {
            _error = string.Empty;
            _busy = true;
            await Storage.SaveAsync(_name, Fen);
            DialogService.Close(true);
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _busy = false;
        }
    }
}