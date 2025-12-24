using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIChess.Core.Models;
using Chess;

namespace AIChess.Core.Helpers;

public static class ChessExtensions
{
    public static string ToEndgameColor(this EndgameType endgameType)
    {
        // Convert endgame type to color string for display purposes
        return endgameType switch
        {
            EndgameType.Checkmate or EndgameType.Resigned or EndgameType.Timeout => "red",
            EndgameType.Stalemate or EndgameType.DrawDeclared or EndgameType.InsufficientMaterial
                or EndgameType.FiftyMoveRule or EndgameType.Repetition => "gray",
            _ => "green"
        };
    }

    public static string ToEndgameColor(this EndGameInfo endgameInfo, AppState appState)
    {
        var playerColor = appState.GameOptions.White == PlayerType.Human ? "white" : "black";
        var winnerColor = endgameInfo.WonSide?.ToString();
        if (winnerColor == null) return "gray"; // No winner, default to gray
        return winnerColor.Equals(playerColor, StringComparison.OrdinalIgnoreCase) ? "green" : // Use the endgame type color
            "red";
    }
    public static string ToEndgameText(this EndgameType endgameType)
    {
        // Convert endgame type to text for display purposes
        return endgameType switch
        {
            EndgameType.Checkmate => "Checkmate",
            EndgameType.Resigned => "Resigned",
            EndgameType.Timeout => "Timeout",
            EndgameType.Stalemate => "Stalemate",
            EndgameType.DrawDeclared => "Draw Declared",
            EndgameType.InsufficientMaterial => "Insufficient Material",
            EndgameType.FiftyMoveRule => "Fifty Move Rule",
            EndgameType.Repetition => "Repetition",
            _ => "Unknown"
        };
    }

    public static string SanitizeOutput(this string output)
    {
        // remove json code blocks
        if (string.IsNullOrWhiteSpace(output)) return output;
        var sanitized = output.Replace("```json", "").Replace("```", "").Trim();
        return sanitized;
    }
}