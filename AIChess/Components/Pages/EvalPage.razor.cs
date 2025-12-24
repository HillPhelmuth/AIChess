using AIChess.Core.Models;
using AIChess.ModelEvals;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace AIChess.Components.Pages;
public partial class EvalPage
{
    private bool _isBusy;
    private string _output = string.Empty;
    private GameOptions _gameOptions = new();
    [Inject]
    private EvalEngine EvalEngine { get; set; } = default!; 
    private async Task EvaluateModels()
    {
        _isBusy = true;
        StateHasChanged();
        await Task.Delay(1);
        var model1 = _gameOptions.WhiteModel;
        var model2 = _gameOptions.BlackModel;
        var results = await EvalEngine.EvaluateAsync(model1, model2, 5);
        _output = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        var modelName1 = model1.Split('/')[1];
        var modelName2 = model2.Split('/')[1];
        await File.WriteAllTextAsync($"evaluation_results_{modelName1}_vs_{modelName2}.json", _output);
        _isBusy = false;
        StateHasChanged();
    }
}
