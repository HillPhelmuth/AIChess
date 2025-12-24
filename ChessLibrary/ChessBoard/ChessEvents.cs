// *****************************************************
// *                                                   *
// * O Lord, Thank you for your goodness in our lives. *
// *     Please bless this code to our compilers.      *
// *                     Amen.                         *
// *                                                   *
// *****************************************************
//                                    Made by Geras1mleo

namespace Chess;

public partial class ChessBoard
{
	/// <summary>
	/// Raises when trying to make or validate move but after the move would have been made, king would have been checked
	/// </summary>
	public event ChessCheckedChangedEventHandler? OnInvalidMoveKingChecked;
	/// <summary>
	/// Raises when white king is (un)checked
	/// </summary>
	public event ChessCheckedChangedEventHandler? OnWhiteKingCheckedChanged;
	/// <summary>
	/// Raises when black king is (un)checked
	/// </summary>
	public event ChessCheckedChangedEventHandler? OnBlackKingCheckedChanged;
	/// <summary>
	/// Raises when user has to choose promotion action
	/// </summary>
	public event ChessPromotionResultEventHandler? OnPromotePawn;
	/// <summary>
	/// Raises when it's end of game
	/// </summary>
	public event ChessEndGameEventHandler? OnEndGame;
	/// <summary>
	/// Raises when any piece has been captured
	/// </summary>
	public event ChessCaptureEventHandler? OnCaptured;
	

	private void OnWhiteKingCheckedChangedEvent(CheckEventArgs e) => OnWhiteKingCheckedChanged?.Invoke(this, e);

    private void OnBlackKingCheckedChangedEvent(CheckEventArgs e) => OnBlackKingCheckedChanged?.Invoke(this, e);

	private void OnInvalidMoveKingCheckedEvent(CheckEventArgs e) => OnInvalidMoveKingChecked?.Invoke(this, e);		
	

	private void OnPromotePawnEvent(PromotionEventArgs e) => OnPromotePawn?.Invoke(this, e);

    private void OnEndGameEvent() => OnEndGame?.Invoke(this, new EndgameEventArgs(this, EndGame!));

    private void OnCapturedEvent(Piece piece) => OnCaptured?.Invoke(this, new CaptureEventArgs(this, piece, CapturedWhite, CapturedBlack));
}
