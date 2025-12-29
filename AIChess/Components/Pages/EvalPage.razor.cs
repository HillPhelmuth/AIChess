using AIChess.Core.Models;
using AIChess.ModelEvals;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using AIChess.Core.Services;

namespace AIChess.Components.Pages;
public partial class EvalPage
{
    private bool _isBusy;
    private string _output = string.Empty;
    private GameOptions _gameOptions = new()
    {
        White = PlayerType.AIModel,
        Black = PlayerType.AIModel
    };
    [Inject]
    private EvalEngine EvalEngine { get; set; } = default!;
    [Inject]
    private OpenRouterService OpenRouterService { get; set; } = default!;
   

    private List<MatchResult> _matchResults = [];
    private Dictionary<string, bool>? _availableModels;
    private int _numberOfMatches = 5;

    protected override async Task OnInitializedAsync()
    {
        _availableModels ??= (await OpenRouterService.GetModelsAsync()).ToDictionary(x => x.Id, x => x.ModelSupportsImageInput());
        EvalEngine.MatchCompleted += HandleMatchCompleted;
        await base.OnInitializedAsync();
    }

    private async void HandleMatchCompleted(MatchResult matchResult)
    {
        byte[] imageBytes = await ChessService.PngFromFen();
        matchResult.ImgSrc = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        _matchResults.Add(matchResult);
        InvokeAsync(StateHasChanged);
    }

    private async Task EvaluateModels()
    {
        _matchResults.Clear();
        _isBusy = true;
        StateHasChanged();
        await Task.Delay(1);
        var model1 = _gameOptions.WhiteModelId;
        var model2 = _gameOptions.BlackModelId;
        var results = await EvalEngine.EvaluateAsync(model1, model2, _numberOfMatches);
        _output = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        var modelName1 = model1.Split('/')[1];
        var modelName2 = model2.Split('/')[1];
        await File.WriteAllTextAsync($"evaluation_results_{modelName1}_vs_{modelName2}.json", _output);
        _isBusy = false;
        StateHasChanged();
    }
}
