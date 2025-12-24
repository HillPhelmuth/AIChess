using Chess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIChess.Core.Models;

public class BoardPiece
{
	public Piece? Piece { get; set; }
	public Position Position { get; init; }
	public bool IsSelected {get;set;}
	public bool IsPossible { get; set; }
}