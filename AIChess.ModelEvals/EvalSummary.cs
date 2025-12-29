namespace AIChess.ModelEvals;

public record EvalSummary(EvalResult Model1Result, EvalResult Model2Result, List<MatchResult> MatchResults);