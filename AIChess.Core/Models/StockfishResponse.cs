using System.Text.Json.Serialization;

namespace AIChess.Core.Models;

public class StockfishResponse2
{
    [JsonPropertyName("bestmove")]
    public string Bestmove { get; set; } = string.Empty;
    
    [JsonPropertyName("depth")]
    public int Depth { get; set; }
    
    [JsonPropertyName("seldepth")]
    public int SelDepth { get; set; }
    
    [JsonPropertyName("score")]
    public Score? Score { get; set; }
}

public class Score
{
    [JsonPropertyName("value")]
    public int Value { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
