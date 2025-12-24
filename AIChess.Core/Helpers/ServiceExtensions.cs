using AIChess.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIChess.Core.Helpers;

public static class ServiceExtensions
{
	public static IServiceCollection AddChessService(this IServiceCollection services)
	{
	 	return services.AddScoped<AppState>().AddScoped<ChessService>();
	}
}