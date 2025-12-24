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
    private const int MaxHalfMoves = 400; // Prevent endless matches when neither model can convert.
    private readonly AppState _appState;
    public EvalEngine(IConfiguration configuration, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, AppState appState)
    {

        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        this._appState = appState;

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

        for (var index = 0; index < _matchCount; index++)
        {
            var pairing = index % 2 == 0
                ? (_aiModel1, _aiModel2)
                : (_aiModel2, _aiModel1);

            var (outcome, finalFen, endgameType) = await RunMatchAsync(pairing.Item1, pairing.Item2);
            ApplyOutcome(outcome, pairing.Item1, pairing.Item2, model1Result, model2Result, finalFen, endgameType);
        }

        return new EvalSummary(model1Result, model2Result);
    }

    private record MatchResult(MatchOutcome Outcome, string FinalFen, EndgameType? EndgameType);
    private async Task<MatchResult> RunMatchAsync(string whiteModel, string blackModel)
    {
        _appState.Reset();
        _appState.GameOptions = new GameOptions
        {
            White = PlayerType.AIModel,
            WhiteModel = whiteModel,
            Black = PlayerType.AIModel,
            BlackModel = blackModel,
            AiOnly = true,
            NoChat = true

        };

        var chessService = new ChessService(_appState, _configuration, _loggerFactory, _httpClientFactory);
        PieceColor? winner = null;
        EndgameType? endgameType = null;
        _appState.EndGameOccurred += (_, args) =>
        {
            winner = args.EndgameInfo.WonSide;
            endgameType = args.EndgameInfo.EndgameType;
        };

        var currentColor = "White";
        for (var halfMove = 0; halfMove < MaxHalfMoves && !_appState.IsGameEnded; halfMove++)
        {
            try
            {
                var response = await chessService.PlayChessChat(currentColor);
                if (response.Contains("No moves available, returning empty result.", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Match failure for {Color}", currentColor);
                return currentColor.Equals("White", StringComparison.OrdinalIgnoreCase)
                    ? new(MatchOutcome.BlackWin, _appState.ChessBoard.ToFen(), endgameType)
                    : new(MatchOutcome.WhiteWin, _appState.ChessBoard.ToFen(), endgameType);
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
                return new(MatchOutcome.WhiteWin, _appState.ChessBoard.ToFen(), endgameType);
            }

            if (winner.Equals(PieceColor.Black))
            {
                return new(MatchOutcome.BlackWin, _appState.ChessBoard.ToFen(), endgameType);
            }
        }

        var eval = _appState.GameStateEval;
        if (eval?.FavoredColor.Equals("White", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new(MatchOutcome.WhiteWinPoints, _appState.ChessBoard.ToFen(), endgameType);
        }
        else if (eval?.FavoredColor.Equals("Black", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new(MatchOutcome.BlackWinPoints, _appState.ChessBoard.ToFen(), endgameType);
        }
        return new(MatchOutcome.Draw, _appState.ChessBoard.ToFen(), endgameType);
    }

    private void ApplyOutcome(MatchOutcome outcome, string whiteModel, string blackModel,
        EvalResult model1Result, EvalResult model2Result, string finalFen, EndgameType? endgameType)
    {
        model1Result.FinalBoardStates.Add(finalFen);
        model2Result.FinalBoardStates.Add(finalFen);
        model1Result.EndgameTypes.Add(endgameType);
        model2Result.EndgameTypes.Add(endgameType);
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

    private enum MatchOutcome
    {
        Draw,
        WhiteWin,
        BlackWin,
        WhiteWinPoints,
        BlackWinPoints
    }

}
public class EvalResult(string aiModel)
{
    public string AIModel { get; } = aiModel;
    public int Wins { get; set; }
    public int WinsOnPoints { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int Failures { get; set; }
    public List<EndgameType?> EndgameTypes { get; set; } = [];
    public List<string> FinalBoardStates { get; set; } = [];

}

public record EvalSummary(EvalResult Model1Result, EvalResult Model2Result);
