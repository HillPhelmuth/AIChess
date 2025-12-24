using Chess;
using Microsoft.AspNetCore.Components;

namespace AIChess.Components.Chess;
public partial class GameEndPopup
{
    [Parameter]
    public EndgameEventArgs? EndGameEventArgs { get; set; } = null!;

    public void Stay()
    {
        DialogService.Close("Stay");
    }
}
