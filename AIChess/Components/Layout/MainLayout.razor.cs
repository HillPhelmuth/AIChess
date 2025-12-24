using AIChess.Core;
using Microsoft.AspNetCore.Components;

namespace AIChess.Components.Layout;

public partial class MainLayout
{
	private bool _sidebarExpanded;
	[Inject]
	private AppState AppState { get; set; } = default!;
	private void CloseSidebars()
	{
		_sidebarExpanded = false;
	}
}