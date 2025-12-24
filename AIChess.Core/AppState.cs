using AIChess.Core.Models;
using Chess;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AIChess.Core.Services;

namespace AIChess.Core;

public class AppState : INotifyPropertyChanged
{
    private ChessBoard _chessBoard;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<EndgameEventArgs>? EndGameOccurred;
    public event EventHandler<CheckEventArgs>? WhiteKingChecked;
    public event EventHandler<CheckEventArgs>? BlackKingChecked;
    public bool IsGameEnded { get; set; }
    public AppState()
    {
        _chessBoard = new ChessBoard();
        ChessBoard.OnCaptured += ChessBoard_OnCaptured;
        ChessBoard.OnEndGame += HandleEndGame;
        ChessBoard.OnBlackKingCheckedChanged += HandleBlackKingCheckChanged;
        ChessBoard.OnWhiteKingCheckedChanged += HandleWhiteKingCheckChanged;
        
        SetBoardState();
        Console.WriteLine(ChessBoard.ToFen());
    }

    private void HandleWhiteKingCheckChanged(object sender, CheckEventArgs e)
    {
        WhiteKingChecked?.Invoke(this, e);
    }

    private void HandleBlackKingCheckChanged(object sender, CheckEventArgs e)
    {
        BlackKingChecked?.Invoke(this, e);
    }

    private void HandleEndGame(object? sender, EndgameEventArgs e)
    {
        var winner = e.EndgameInfo.WonSide;
        var winType = e.EndgameInfo.EndgameType.ToString();
        EndGameOccurred?.Invoke(this, e);
        IsGameEnded = true;
        Logs.Insert(0, $"End Game: {winner} won by {winType}");
    }
    private void ChessBoard_OnCaptured(object sender, CaptureEventArgs e)
    {
        Logs.Insert(0, $"{e.CapturedPiece} Captured");
    }

    public List<string> Logs { get; set; } = [];
    public void SetBoardState()
    {
        BoardPieces.Clear();
        // Iterate from rank 8 (Y=7, top visual row) to rank 1 (Y=0, bottom visual row)
        // to display white on bottom in standard chess orientation
        for (short y = 7; y >= 0; y--)
        {
            for (short x = 0; x < 8; x++)
            {
                var piece = ChessBoard[x, y];
                var pos = new Position(x, y);
                BoardPieces.Add(new BoardPiece { Position = pos, Piece = piece });
            }
        }
        OnPropertyChanged(nameof(ChessBoard));
    }

    public List<BoardPiece> BoardPieces { get; private set; } = [];
    public ChessBoard ChessBoard
    {
        get => _chessBoard;
        private set => SetField(ref _chessBoard, value);
    }

    public BoardPiece? SelectedPiece
    {
        get;
        set => SetField(ref field, value);
    }

    public GameOptions GameOptions
    {
        get;
        set => SetField(ref field, value);
    } = new();

    public Move? LastPlayerMove
    {
        get;
        set => SetField(ref field, value);
    }

    public EvalResult? GameStateEval
    {
        get;
        set => SetField(ref field, value);
    }
    public string? LastAiColor
    {
        get;
        set => SetField(ref field, value);
    }

    public bool Move(Position from, Position to, bool isAI = false)
    {
        
        try
        {
            var move = new Move(from, to);
            
            var success = ChessBoard.Move(move);
            if (success)
            {
                Logs.Insert(0, $"Move: {move}");
                BoardPieces.Clear();
                if (!isAI)
                    LastPlayerMove = move;
                SetBoardState();
            }

            SelectedPiece = null;
            return success;

        }
        catch (ChessGameEndedException endedException)
        {
            EndGameOccurred?.Invoke(this, new EndgameEventArgs(ChessBoard, endedException.EndgameInfo));
            Console.WriteLine("Handled Game Ended Exception");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n----------------------------\n{ex.Message}\n----------------------------\n");
            throw;
        }

        //BoardPieces.ForEach(x => x.IsSelected = false);

    }
    public bool Move(Move move, bool isAi = false)
    {
        return Move(move.OriginalPosition, move.NewPosition, isAi);
    }

    public bool Move(string move, bool isAi = false)
    {
        try
        {
            var convertMove = move;
            Console.WriteLine($"Move: {move} converting to {convertMove}");
            var pos1 = convertMove.Split('-')[0].Trim().TrimEnd('x');
            var pos2 = convertMove.Split('-')[1].Trim();
            var success = Move(new Move(new Position(pos1), new Position(pos2)), isAi);
            SetBoardState();
            OnPropertyChanged(nameof(ChessBoard));
            Console.WriteLine($"Move: {move} executed");
            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n----------------------------\n{ex.Message}\n----------------------------\n");
            return false;
        }
    }
    public static string ConvertMove(string move)
    {
        if (move.Length != 4)
            throw new ArgumentException("Invalid move format. Expected format is 'f1b5'.");
        var sanitizedMove = move.Replace("x", "");
        var from = sanitizedMove[..2];
        var to = sanitizedMove.Substring(2, 2);

        return $"{from} - {to}";
    }
    public void CancelLastMove()
    {
        try
        {
            var tryFrom = ChessBoard.ExecutedMoves[^1];
            Logs.Insert(0, $"Cancel: {tryFrom}");
            ChessBoard.Cancel();
            Logs.Insert(0, $"Reset to: {ChessBoard.ExecutedMoves[^1]}");
            SetBoardState();
        }
        catch (Exception ex)
        {
            var format = $"\n----------------------------\n{ex.Message}\n----------------------------\n";
            Logs.Insert(0, format);
            Console.WriteLine(format);
        }
    }

    public void FromFen(string fen)
    {
        if (ChessBoard != null)
        {
            ChessBoard.OnEndGame -= HandleEndGame;
            ChessBoard.OnCaptured -= ChessBoard_OnCaptured;
        }
        ChessBoard = ChessBoard.LoadFromFen(fen, AutoEndgameRules.All);
        ChessBoard.OnEndGame += HandleEndGame;
        ChessBoard.OnCaptured += ChessBoard_OnCaptured;
        IsGameEnded = false;
    }
    public void Reset()
    {
        // Detach event handlers from the old ChessBoard if needed
        ChessBoard.OnEndGame -= HandleEndGame;
        ChessBoard.OnCaptured -= ChessBoard_OnCaptured;
        ChessBoard.OnWhiteKingCheckedChanged -= HandleWhiteKingCheckChanged;
        ChessBoard.OnBlackKingCheckedChanged -= HandleBlackKingCheckChanged;

        // Create new ChessBoard
        ChessBoard = new ChessBoard();

        // Reattach event handlers
        ChessBoard.OnEndGame += HandleEndGame;
        ChessBoard.OnCaptured += ChessBoard_OnCaptured;
        ChessBoard.OnWhiteKingCheckedChanged += HandleWhiteKingCheckChanged;
        ChessBoard.OnBlackKingCheckedChanged += HandleBlackKingCheckChanged;
        Logs.Clear();
        GameStateEval = null;
        IsGameEnded = false;
        SetBoardState();
    }
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }



}