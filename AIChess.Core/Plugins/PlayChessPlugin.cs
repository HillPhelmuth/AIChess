using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AIChess.Core.Models;
using Chess;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
#pragma warning disable SKEXP0040

namespace AIChess.Core.Plugins;

public class PlayChessPlugin(AppState state)
{
    [KernelFunction, Description("Move a chess piece on the board")]
    [return: Description("New board state or error message")]
    public string MovePiece([FromKernelServices] AppState appState, [Description("Chess grid starting position (represented by single string rowColumn value. E.g. 'a1")] string from, [Description("Chess grid position after move (represented by single string rowColumn value. E.g. 'a1'")] string to)
    {
        var hasHuman = state.GameOptions.Black == PlayerType.Human || state.GameOptions.White == PlayerType.Human;
        try
        {
            var move = new Move(from, to);
            var currentBoard = state.ChessBoard.ToAscii();
            var isValid = state.Move(move, hasHuman);
            if (!isValid)
            {
                return $"Your move {move} IS INVALID!\n\nReview the boardstate below and try again.\n\n{currentBoard}";
            }
            return state.ChessBoard.ToAscii();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

    }
    private const string StockFishBaseUrl = "https://stockfish.online/api/s/v2.php";

    //[KernelFunction, Description("Get an analysis of the best move from the current board state")]
    [return: Description("string with the suggested next move")]
    public async Task<string> GetBestMove([Description("Fen state of the chess board")] string fenState)
    {
        var httpClient = new HttpClient();
        var response = await httpClient.GetFromJsonAsync<StockfishResponse>($"{StockFishBaseUrl}?fen={fenState}&depth=13");
        var data = response.Bestmove;
        var ponderIndex = data.IndexOf("ponder", StringComparison.OrdinalIgnoreCase);
        if (ponderIndex != -1)
        {
            return data[..ponderIndex].Trim();
        }
        return data;
    }
}
internal class StockfishResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("evaluation")]
    public double? Evaluation { get; set; }

    [JsonPropertyName("mate")]
    public int? Mate { get; set; }

    [JsonPropertyName("bestmove")]
    public string Bestmove { get; set; }

    [JsonPropertyName("continuation")]
    public string Continuation { get; set; }
}