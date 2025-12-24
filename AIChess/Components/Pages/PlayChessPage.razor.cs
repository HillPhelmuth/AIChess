using AIChess.Core.Models;
using AIChess.Core;
using Chess;
using Markdig;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using AIChess.Components.Chess;
using AIChess.Components;
using AIChess.Core.Services;
using AIChess.Services;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;

namespace AIChess.Components.Pages;

public partial class PlayChessPage : AppComponentBase, IDisposable
{
    // NavigationManager is already provided by AppComponentBase
    private Popup? _popup;
    private ElementReference? _button;
    private bool _lockBoard;
    private void Refresh()
    {
        AppState.Reset();
        StateHasChanged();
    }
    private string _moveMessage = "";

    private record MoveMessage(string Header)
    {
        public string Message { get; set; } = "";
        public DateTime Time { get; } = DateTime.Now;
        public string Sender { get; set; } = "ai";
    }
    private List<MoveMessage> _moveMessages = [];
    private string _whiteMessage = "";
    private bool _isGenerating;
    private bool _showAvailableMoves;
    private bool _isEnded;
    private EndgameEventArgs? _endGameEventArgs;
    private bool _showCheck;
    private string _checkColor = "";
    protected override Task OnInitializedAsync()
    {
        if (!AppState.GameOptions.IsValid)
            NavigationManager.NavigateTo("/");
        ChessService.UpdateBoardFen += s => _moveMessage += $"\n\n{s}";
        AppState.EndGameOccurred += HandleEndGameOccurred;
        AppState.WhiteKingChecked += HandleWhiteKingChecked;
        AppState.BlackKingChecked += HandleBlackKingChecked;
        return base.OnInitializedAsync();
    }

    

    private async void HandleEndGameOccurred(object? sender, EndgameEventArgs e)
    {
        try
        {
            _isEnded = true;
            _endGameEventArgs = e;
            var ending = await DialogService.OpenAsync<GameEndPopup>("",
                parameters: new Dictionary<string, object>() { [nameof(GameEndPopup.EndGameEventArgs)] = e },
                new DialogOptions()
                { Draggable = true, Resizable = true, CloseDialogOnOverlayClick = true, CloseDialogOnEsc = true });
            if (ending is not null && ending == true)
            {
                AppState.Reset();
                NavigationManager.NavigateTo("/");
                return;
            }
            else if (ending is not null && ending == "Stay")
            {
                _lockBoard = true;
            }

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling end game: {ex.Message}");
            AppState.Logs.Add($"Error handling end game: {ex.Message}");
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Error",
                Detail = $"An error occurred while handling the end game: {ex.Message}"
            });
        }
    }

    protected override async void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.LastPlayerMove))
        {
            var moveColor = AppState.LastPlayerMove.Piece.Color.Name;
            var nextMoveColor = moveColor.Equals("White", StringComparison.InvariantCultureIgnoreCase) ? "Black" : "White";
            Console.WriteLine($"Executing chess move because {e.PropertyName} changed");
            if (AppState.GameOptions.NoChat)
                await ExecuteChessMove(nextMoveColor);
            else
                await ExecuteChessMove(nextMoveColor);
        }

        if (e.PropertyName == nameof(AppState.LastAiColor) && AppState.GameOptions.AiOnly)
        {
            var nextMoveColor = AppState.LastAiColor.Equals("White", StringComparison.InvariantCultureIgnoreCase) ? "Black" : "White";
            await Task.Delay(500);
            Console.WriteLine($"Executing chess move because {e.PropertyName} changed");
            await ExecuteChessMove(nextMoveColor);
        }

        if (e.PropertyName == nameof(AppState.LastAiColor) && !AppState.GameOptions.AiOnly)
        {
            var nextMoveColor = AppState.LastAiColor.Equals("White", StringComparison.InvariantCultureIgnoreCase) ? "Black" : "White";
            await Task.Delay(500);
            if (AppState.GameOptions.White.ToString() != "Human" && nextMoveColor == "White")
            {
                Console.WriteLine($"Executing chess move because {e.PropertyName} changed");
                await ExecuteChessMove(nextMoveColor);
            }
            if (AppState.GameOptions.Black.ToString() != "Human" && nextMoveColor == "Black")
            {
                Console.WriteLine($"Executing chess move because {e.PropertyName} changed");
                await ExecuteChessMove(nextMoveColor);
            }
        }
        base.OnPropertyChanged(sender, e);
    }

    private async Task ExecuteChessMove(string nextMoveColor)
    {
        var boardState = AppState.ChessBoard.ToPgn();
        while (_isGenerating)
        {
            await Task.Delay(500);
            Console.WriteLine("waiting...");
        }
        if (AppState.IsGameEnded) return;
        if (!AppState.GameOptions.NoChat)
        {

            var response = await ChessService.PlayChessChat(nextMoveColor);
            var nextMoveHeader =
                $"<br/><strong>{nextMoveColor} move {(AppState.ChessBoard.ExecutedMoves.Where(x => x.Piece.Color.Name.Equals(nextMoveColor))?.Count() ?? 0) + 1}</strong><br/>";
            _moveMessages.Add(new MoveMessage(nextMoveHeader));
            await ExecuteChatStream(TempAsyncReflect(response));
        }
        else
        {
            var response = await ChessService.PlayChessNoChat(nextMoveColor);
            var nextMoveHeader =
                $"<br/><strong>{nextMoveColor} move {(AppState.ChessBoard.ExecutedMoves.Where(x => x.Piece.Color.Name.Equals(nextMoveColor))?.Count() ?? 0) + 1}</strong><br/>";
            _moveMessages.Add(new MoveMessage(nextMoveHeader));
            _moveMessage += nextMoveHeader;
        }
    }
    private string _chatInput = "";

    private async Task SendChat()
    {
        if (string.IsNullOrWhiteSpace(_chatInput)) return;
        _moveMessages.Add(new MoveMessage("**You**\n") { Message = _chatInput, Sender = "user" });
        var userColor = AppState.GameOptions.White == PlayerType.Human ? "White" : "Black";
        StateHasChanged();
        var chatResponse = await ChessService.SendChat(userColor, _chatInput);
        _moveMessages.Add(new MoveMessage("**AI**\n") { Message = chatResponse });
        _chatInput = "";
        StateHasChanged();

    }

    private async IAsyncEnumerable<string> TempAsyncReflect(string entireResponse)
    {
        await Task.Yield();
        yield return entireResponse;
    }
    private bool _noChatMode;
    private async Task ExecuteNoChatMove(string nextMoveColor)
    {
        await ChessService.PlayChessNoChat(nextMoveColor);
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var hasHuman = AppState.GameOptions.White == PlayerType.Human;
        if (firstRender && !hasHuman && !AppState.GameOptions.AiOnly)
        {
            await ExecuteChessMove("White");
        }
        if (firstRender && AppState.GameOptions.AiOnly)
        {
            await ExecuteNoChatMove("White");
        }
        await base.OnAfterRenderAsync(firstRender);
    }
    private async Task ExecuteChatStream(IAsyncEnumerable<string> stream)
    {
        //_moveMessage = "";
        var moveMessage = _moveMessages.LastOrDefault();
        if (moveMessage is null)
        {
            _moveMessages.Add(new MoveMessage($"<br/><strong>White move 1</strong><br/>"));
            moveMessage = _moveMessages.LastOrDefault()!;
        }
        _isGenerating = true;
        await foreach (var item in stream)
        {
            moveMessage.Message += item;
            StateHasChanged();
        }
        _isGenerating = false;
    }
    private void Cancel()
    {
        AppState.CancelLastMove();
        StateHasChanged();
    }
    private void Save()
    {
        _ = SaveAsAsync();
    }
    private async Task SaveAsAsync()
    {
        var fen = AppState.ChessBoard.ToFen();
        var result = await DialogService.OpenAsync<SaveGameDialog>("Save Game",
            new Dictionary<string, object> { [nameof(SaveGameDialog.Fen)] = fen },
            new DialogOptions { CloseDialogOnOverlayClick = true, CloseDialogOnEsc = true, Resizable = true });
        if (result is true)
        {
            NotificationService.Notify(NotificationSeverity.Success, "Saved", "Game saved to your browser");
        }
    }

    private async Task OpenLoadSaved()
    {
        var picked = await DialogService.OpenAsync<LoadGameDialog>("Load Game",
                null,
                new DialogOptions { CloseDialogOnOverlayClick = true, CloseDialogOnEsc = true, Resizable = true });
        if (picked is GameStorageService.SavedGame g)
        {
            if (!string.IsNullOrWhiteSpace(g.Fen))
            {
                AppState.FromFen(g.Fen);
                AppState.SetBoardState();
                StateHasChanged();
                NotificationService.Notify(NotificationSeverity.Info, "Loaded", $"Loaded '{g.Name}'");
            }
        }
    }

    private async Task Best()
    {
        var bestMove = await ChessService.CheatAsync(AppState.ChessBoard.ToFen());
        if (string.IsNullOrEmpty(bestMove))
            NotificationService.Notify(NotificationSeverity.Warning, "Stockfish Api Request failed.", $"Error on Api Request");
        else
            AppState.Move(bestMove);
        StateHasChanged();
    }
    private string _fen = "";
    private void Load()
    {
        //AppState.ChessBoard = JsonSerializer.Deserialize<ChessBoard>(File.ReadAllText("board.json"))!;
        if (string.IsNullOrWhiteSpace(_fen)) return;
        AppState.FromFen(_fen);
        AppState.SetBoardState();
        StateHasChanged();
    }
    private void ShowAvailable()
    {
        //AppState.ToggleShowAvailable();
        StateHasChanged();
    }
    private string AsHtml(string? text)
    {
        if (text == null) return "";
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var result = Markdown.ToHtml(text, pipeline);
        return result;

    }

    public void Dispose()
    {
        AppState.WhiteKingChecked -= HandleWhiteKingChecked;
        AppState.BlackKingChecked -= HandleBlackKingChecked;
        AppState.EndGameOccurred -= HandleEndGameOccurred;

    }
}