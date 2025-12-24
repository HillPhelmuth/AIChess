using AIChess.Core.Plugins;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIChess.Core.Services;

public class StockFishService(IHttpClientFactory httpClientFactory)
{
    private const string StockFishBaseUrl = "https://stockfish.online/api/s/v2.php";
    public async Task<string> GetBestMove([Description("Fen state of the chess board")] string fenState)
    {
        var response = await GetStockfishResponse(fenState);
        var data = response?.Bestmove;
        var ponderIndex = data?.IndexOf("ponder", StringComparison.OrdinalIgnoreCase) ?? -1;
        if (string.IsNullOrEmpty(data))
        {
            return "No best move found.";
        }
        if (ponderIndex != -1)
        {
            return data[..ponderIndex].Trim();
        }
        return data;
    }
    public async Task<EvalResult?> GetEvaluation([Description("Fen state of the chess board")] string fenState)
    {
        try
        {
            var response = await GetStockfishResponse(fenState);
            var responseEvaluation = response?.Evaluation ?? 0.0;
            if (responseEvaluation == 0.0)
            {
                return null;
            }

            var favoredColor = responseEvaluation > 0 ? "White" : "Black";
            var evaluation = Math.Abs(responseEvaluation);
            var evalResult = new EvalResult(evaluation, favoredColor);
            Console.WriteLine(evalResult);
            return evalResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred while getting evaluation: {ex.Message}");
            return null;
        }
    }

    private async Task<StockfishResponse?> GetStockfishResponse(string fenState, int depth = 15)
    {
        var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.GetFromJsonAsync<StockfishResponse>($"{StockFishBaseUrl}?fen={fenState}&depth={depth}");
        return response;
    }
}
public record EvalResult(double Evaluation, string FavoredColor);