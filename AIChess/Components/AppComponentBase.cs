using AIChess.Core.Services;
using AIChess.Core;
using Microsoft.AspNetCore.Components;
using System.ComponentModel;
using Radzen;

namespace AIChess.Components;

public class AppComponentBase : ComponentBase
{
	[Inject]
	protected ChessService ChessService { get; set; } = default!;
	[Inject]
	protected AppState AppState { get; set; } = default!;
	[Inject]
	protected DialogService DialogService { get; set; } = default!;
	[Inject]
	protected NotificationService NotificationService { get; set; } = default!;
	[Inject]
	protected NavigationManager NavigationManager { get; set; } = default!;
    protected override Task OnInitializedAsync()
	{
		AppState.PropertyChanged += OnPropertyChanged;
		return base.OnInitializedAsync();
	}
	protected virtual void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		InvokeAsync(StateHasChanged);
	}
}