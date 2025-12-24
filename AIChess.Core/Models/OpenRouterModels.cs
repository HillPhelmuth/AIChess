using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using AIChess.Core.Helpers;

namespace AIChess.Core.Models;

public class OpenRouterModels
{
    [JsonPropertyName("data")]
    public List<OpenRouterModel> Data { get; set; } = [];

    public static List<OpenRouterModel> GetModelsWithToolsAndStructuredOutputs()
    {
        var allModelData = FileHelper.ExtractFromAssembly<OpenRouterModels>("OpenRouterModels.json");
        return allModelData.Data
            .Where(model => model.SupportedParameters.Contains("tools") && model.SupportedParameters.Contains("structured_outputs") ).OrderBy(x => x.Id)
            .ToList();
    }

    public static bool ModelSupportsImageInput(string modelId)
    {
        var availableModels = GetModelsWithToolsAndStructuredOutputs();
        var model = availableModels.FirstOrDefault(m => m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        return model?.Architecture.InputModalities.Contains("image") ?? false;
    }
}

public class OpenRouterModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("canonical_slug")]
    public string CanonicalSlug { get; set; }

    [JsonPropertyName("hugging_face_id")]
    public string HuggingFaceId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("created")]
    public int Created { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("context_length")]
    public int ContextLength { get; set; }

    [JsonPropertyName("architecture")]
    public Architecture Architecture { get; set; }

    [JsonPropertyName("pricing")]
    public Pricing Pricing { get; set; }

    [JsonPropertyName("top_provider")]
    public TopProvider TopProvider { get; set; }

    [JsonPropertyName("per_request_limits")]
    public object PerRequestLimits { get; set; }

    [JsonPropertyName("supported_parameters")]
    public List<string> SupportedParameters { get; set; }

    [JsonPropertyName("default_parameters")]
    public DefaultParameters DefaultParameters { get; set; }
}

public class Architecture
{
    [JsonPropertyName("modality")]
    public string Modality { get; set; }

    [JsonPropertyName("input_modalities")]
    public List<string> InputModalities { get; set; }

    [JsonPropertyName("output_modalities")]
    public List<string> OutputModalities { get; set; }

    [JsonPropertyName("tokenizer")]
    public string Tokenizer { get; set; }

    [JsonPropertyName("instruct_type")]
    public string InstructType { get; set; }
}

public class DefaultParameters
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public object FrequencyPenalty { get; set; }
}

public class Pricing
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("completion")]
    public string Completion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("request")]
    public string Request { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("web_search")]
    public string WebSearch { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("internal_reasoning")]
    public string InternalReasoning { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("input_cache_read")]
    public string InputCacheRead { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("input_cache_write")]
    public string InputCacheWrite { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("audio")]
    public string Audio { get; set; }
}

public class TopProvider
{
    [JsonPropertyName("context_length")]
    public int? ContextLength { get; set; }

    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }

    [JsonPropertyName("is_moderated")]
    public bool IsModerated { get; set; }
}