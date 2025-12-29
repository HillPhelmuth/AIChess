using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using AIChess.Core;
using AIChess.Core.Models;
using AIChess.Core.Services;
using Chess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIChess.ModelEvals;

public class EvalEngine
{
    private string _aiModel1;
    private string _aiModel2;
    private int _matchCount;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private const int MaxHalfMoves = 150; // Prevent endless matches when neither model can convert.
    private readonly AppState _appState;
    private readonly OpenRouterService _openRouterService;
    public event Action<MatchResult>? MatchCompleted;
    public EvalEngine(IConfiguration configuration, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, AppState appState, OpenRouterService openRouterService)
    {

        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _appState = appState;
        _openRouterService = openRouterService;
        _logger = _loggerFactory.CreateLogger<EvalEngine>();

    }
    /// <summary>
    /// Evaluates two AI models by having them play a series of matches against each other.
    /// </summary>
    /// <returns><see cref="EvalSummary"/></returns>

    public async Task<EvalSummary> EvaluateAsync(string aiModel1, string aiModel2, int matchCount)
    {
        if (string.IsNullOrWhiteSpace(aiModel1))
            throw new ArgumentException("Model id is required.", nameof(aiModel1));
        if (string.IsNullOrWhiteSpace(aiModel2))
            throw new ArgumentException("Model id is required.", nameof(aiModel2));
        if (matchCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(matchCount), "Match count must be greater than zero.");

        _aiModel1 = aiModel1;
        _aiModel2 = aiModel2;
        _matchCount = matchCount;
        var model1Result = new EvalResult(_aiModel1);
        var model2Result = new EvalResult(_aiModel2);
        List<MatchResult> matchResults = [];
        for (var index = 0; index < _matchCount; index++)
        {
            var pairing = index % 2 == 0
                ? (_aiModel1, _aiModel2)
                : (_aiModel2, _aiModel1);
           
            var matchResult = await RunMatchAsync(pairing.Item1, pairing.Item2);
            ApplyOutcome(matchResult.Outcome, pairing.Item1, pairing.Item2, model1Result, model2Result);
            matchResults.Add(matchResult);
            MatchCompleted?.Invoke(matchResult);
        }

        return new EvalSummary(model1Result, model2Result, matchResults);
    }

    //private record MatchResult(MatchOutcome Outcome, string FinalFen, EndgameType? EndgameType);
    private async Task<MatchResult> RunMatchAsync(string whiteModel, string blackModel)
    {
        _appState.Reset();
        _appState.GameOptions = new GameOptions
        {
            White = PlayerType.AIModel,
            WhiteModelId = whiteModel,
            Black = PlayerType.AIModel,
            BlackModelId = blackModel,
            AiOnly = true,
            NoChat = true

        };

        var chessService = new ChessService(_appState, _configuration, _loggerFactory, _httpClientFactory, _openRouterService);
        PieceColor? winner = null;
        EndgameType? endgameType = null;
        _appState.EndGameOccurred += (_, args) =>
        {
            winner = args.EndgameInfo.WonSide;
            endgameType = args.EndgameInfo.EndgameType;
        };

        var currentColor = "White";
        var count = 1;
        for (var halfMove = 0; halfMove < MaxHalfMoves && !_appState.IsGameEnded && endgameType is null; halfMove++)
        {
            
            try
            {
                var response = await chessService.PlayChessChat(currentColor);
                if (response.Contains("No moves available, returning empty result.", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Match failure for {Color}", currentColor);
                return currentColor.Equals("White", StringComparison.OrdinalIgnoreCase)
                    ? new(MatchOutcome.BlackWin, blackModel, _appState.ChessBoard.ToFen(), endgameType, halfMove + 1, ex.ToString())
                    : new(MatchOutcome.WhiteWin, whiteModel, _appState.ChessBoard.ToFen(), endgameType, halfMove + 1, ex.ToString());
            }

            if (_appState.IsGameEnded)
            {
                break;
            }

            currentColor = currentColor.Equals("White", StringComparison.OrdinalIgnoreCase) ? "Black" : "White";
        }

        if (winner is not null)
        {
            if (winner.Equals(PieceColor.White))
            {
                return new(MatchOutcome.WhiteWin, whiteModel, _appState.ChessBoard.ToFen(), endgameType, count);
            }

            if (winner.Equals(PieceColor.Black))
            {
                return new(MatchOutcome.BlackWin, blackModel, _appState.ChessBoard.ToFen(), endgameType, count);
            }
        }

        var eval = _appState.GameStateEval;
        if (eval?.FavoredColor.Equals("White", StringComparison.OrdinalIgnoreCase) == true && endgameType is null && count >= 400)
        {
            return new(MatchOutcome.WhiteWinPoints, whiteModel, _appState.ChessBoard.ToFen(), endgameType, count);
        }
        else if (eval?.FavoredColor.Equals("Black", StringComparison.OrdinalIgnoreCase) == true && endgameType is null && count <= 400)
        {
            return new(MatchOutcome.BlackWinPoints, blackModel, _appState.ChessBoard.ToFen(), endgameType, count);
        }
        return new(MatchOutcome.Draw, "", _appState.ChessBoard.ToFen(), endgameType, count);
    }

    private void ApplyOutcome(MatchOutcome outcome, string whiteModel, string blackModel,
        EvalResult model1Result, EvalResult model2Result)
    {
        
        switch (outcome)
        {
            case MatchOutcome.Draw:
                model1Result.Draws++;
                model2Result.Draws++;
                break;
            case MatchOutcome.WhiteWin:
                RegisterWin(whiteModel, blackModel, model1Result, model2Result);
                break;
            case MatchOutcome.BlackWin:
                RegisterWin(blackModel, whiteModel, model1Result, model2Result);
                break;
            case MatchOutcome.WhiteWinPoints:
                RegisterWinOnPoints(whiteModel, blackModel, model1Result, model2Result);
                break;
            case MatchOutcome.BlackWinPoints:
                RegisterWinOnPoints(blackModel, whiteModel, model1Result, model2Result);
                break;
        }
    }

    private void RegisterWin(string winnerModel, string loserModel, EvalResult model1Result, EvalResult model2Result)
    {
        if (IsModel1(winnerModel))
        {
            model1Result.Wins++;
            if (IsModel1(loserModel))
            {
                model1Result.Losses++;
            }
            else
            {
                model2Result.Losses++;
            }
        }
        else
        {
            model2Result.Wins++;
            if (IsModel1(loserModel))
            {
                model1Result.Losses++;
            }
            else
            {
                model2Result.Losses++;
            }
        }
    }
    private void RegisterWinOnPoints(string winnerModel, string loserModel, EvalResult model1Result, EvalResult model2Result)
    {
        if (IsModel1(winnerModel))
        {
            model1Result.WinsOnPoints++;
            if (IsModel1(loserModel))
            {
                model1Result.Losses++;
            }
            else
            {
                model2Result.Losses++;
            }
        }
        else
        {
            model2Result.WinsOnPoints++;
            if (IsModel1(loserModel))
            {
                model1Result.Losses++;
            }
            else
            {
                model2Result.Losses++;
            }
        }
    }

    private bool IsModel1(string modelId) =>
        string.Equals(modelId, _aiModel1, StringComparison.OrdinalIgnoreCase);

    

}