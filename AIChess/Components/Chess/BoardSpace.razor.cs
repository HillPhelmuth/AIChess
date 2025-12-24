using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using Chess;
using System.ComponentModel;
using AIChess.Core.Models;

namespace AIChess.Components.Chess;

public partial class BoardSpace : AppComponentBase
{
	//[Parameter]
	//public Piece? Piece { get; set; }
	//[Parameter]
	//public Position Position { get; set; }
	[Parameter]
	public BoardPiece BoardPiece { get; set; } = default!;
	private bool _isSelected { get; set; }
	private string SelectedCss => BoardPiece.IsSelected ? "selected" : "";
	private string PossibleCss => BoardPiece.IsPossible ? "possible" : "";
	private void HandleClick()
	{
		Console.WriteLine($"Clicked on {BoardPiece.Position}");
		if (BoardPiece.Piece != null && AppState.SelectedPiece == null)
		{
			AppState.SelectedPiece = BoardPiece;
			//AppState.SelectedPosition = BoardPiece.;
			BoardPiece.IsSelected = true;
		}
		else if (AppState.SelectedPiece == BoardPiece)
		{
			AppState.SelectedPiece = null;
			BoardPiece.IsSelected = false;
		}
		else if (AppState.SelectedPiece is not null)
		{
			BoardPiece.Piece = null;
			Console.WriteLine($"Try Move from {AppState.SelectedPiece.Position} on {BoardPiece.Position}");
			AppState.Move(AppState.SelectedPiece.Position, BoardPiece.Position);
		}
	}
	protected override void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppState.ChessBoard))
			BoardPiece.IsSelected = false;
		base.OnPropertyChanged(sender, e);
	}
}