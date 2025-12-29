using Chess;

namespace AIChess.ModelEvals;

public record MatchResult(
    MatchOutcome Outcome,
    string WinningModel,
    string FinalFen,
    EndgameType? EndgameType,
    int NumberOfMoves, string? Error = null)
{
    public string EndgameReason => EndgameType?.ToString() ?? "Unknown";
    public string? ImgSrc { get; set; }
}