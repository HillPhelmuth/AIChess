using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using AIChess.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIChess.Core.Services;

public class OpenRouterService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
{
    private const string OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
    private readonly ILogger<OpenRouterService> _logger = loggerFactory.CreateLogger<OpenRouterService>();
    private List<OpenRouterModel>? _modelsWithToolsAndStructuredOutputs;
    public async Task<List<OpenRouterModel>> GetModelsAsync()
    {
        if (_modelsWithToolsAndStructuredOutputs is not null)
        {
            _logger.LogInformation("Returning {count} cached models from OpenRouter", _modelsWithToolsAndStructuredOutputs.Count);
            return _modelsWithToolsAndStructuredOutputs;
        }
        var client = httpClientFactory.CreateClient("openrouter");
        client.DefaultRequestHeaders.Authorization = new("Bearer", configuration["OpenRouter:ApiKey"]);
        var response = await client.GetFromJsonAsync<OpenRouterModels>($"{OpenRouterBaseUrl}/models");
        _logger.LogInformation("Fetched {count} models from OpenRouter", response?.Data.Count ?? 0);
        _modelsWithToolsAndStructuredOutputs ??= response.GetModelsWithToolsAndStructuredOutputs();
        return _modelsWithToolsAndStructuredOutputs;
    }
    public async Task<bool> ModelSupportsImageInputAsync(string modelId)
    {
        var availableModels = await GetModelsAsync();
        var model = availableModels.Find(m => m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        return model?.Architecture.InputModalities.Contains("image") ?? false;
    }
}