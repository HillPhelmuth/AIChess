using System.ComponentModel;
using AIChess.Core;
using Microsoft.AspNetCore.Components;
using System.Text.Json;
using AIChess.Core.Models;
using AIChess.Core.Services;
using AIChess.ModelEvals;

namespace AIChess.Components.Pages;

public partial class Home
{
	private GameOptions _gameOptions = new();
	[Inject]
	private NavigationManager NavigationManager { get; set; } = default!;
	[Inject]
	private IConfiguration Configuration { get; set; } = default!;
	[Inject]
	private IHttpClientFactory HttpClientFactory { get; set; } = default!;
	[Inject]
	private EvalEngine EvalEngine { get; set; } = default!;
	[Inject]
	private OpenRouterService OpenRouterService { get; set; } = default!;
    private string _input;
	private string _output;
    

    private Dictionary<string, bool>? _availableModels;
    protected override async Task OnInitializedAsync()
    {
        _availableModels ??= (await OpenRouterService.GetModelsAsync()).ToDictionary(x => x.Id, x => x.ModelSupportsImageInput());
        await base.OnInitializedAsync();
    }

    private class DataGenInput
    {
		public string? Transcript { get; set; }
    }

    private async Task EvaluateModels()
    {
		_isBusy = true;
		StateHasChanged();
		await Task.Delay(1);
        var model1 = _gameOptions.WhiteModelId;
		var model2 = _gameOptions.BlackModelId;
		var results = await EvalEngine.EvaluateAsync(model1, model2, 5);
		_output = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        var modelName1 = model1.Split('/')[1];
        var modelName2 = model2.Split('/')[1];
		await File.WriteAllTextAsync($"evaluation_results_{modelName1}_vs_{modelName2}.json", _output);
		_isBusy = false;
		StateHasChanged();
    }
	private DataGenInput _dataGenInput = new();
    private bool _isBusy;

    private bool _isDebug;
    protected override Task OnAfterRenderAsync(bool firstRender)
	{
		if (firstRender)
		{
#if DEBUG
            _isDebug = true;
			StateHasChanged();
#endif
            //var board = AppState.ChessBoard;
            //var piece = board["c2"];
            //Console.WriteLine($"Piece c2: {piece.Color} - {piece.Type}");
        }
		return base.OnAfterRenderAsync(firstRender);
	}
	private void SetGameOptions(GameOptions options)
	{
		AppState.Reset();
        
		AppState.GameOptions = options;
		NavigationManager.NavigateTo("/play");
	}
}