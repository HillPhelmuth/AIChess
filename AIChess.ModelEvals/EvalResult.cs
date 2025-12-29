namespace AIChess.ModelEvals;

public class EvalResult(string aiModel)
{
    public string AIModel { get; } = aiModel;
    public int Wins { get; set; }
    public int WinsOnPoints { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int Failures { get; set; }
    

}