using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AIChess.Core.Models.HttpDelegates;

public sealed class LoggingHandler(HttpMessageHandler innerHandler, ILoggerFactory loggerFactory) : DelegatingHandler(innerHandler)
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true };

    //private readonly TextWriter _output = output;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {

        Console.WriteLine(request.RequestUri?.ToString());
        var isStream = false;
        if (request.Content is not null)
        {
            var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine("=== REQUEST ===");
            try
            {
                var root = JsonNode.Parse(requestBody);
                if (root != null)
                {
                    isStream = root["stream"]?.GetValue<bool>() ?? false;
                }
                string formattedContent = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(requestBody), s_jsonSerializerOptions);
                Console.WriteLine(formattedContent);
            }
            catch (JsonException)
            {
                Console.WriteLine(requestBody);
            }
            Console.WriteLine(string.Empty);
        }

        // Call the next handler in the pipeline
        var responseMessage = await base.SendAsync(request, cancellationToken);

        if (isStream) return responseMessage;
        var responseBody = await responseMessage.Content.ReadAsStringAsync(cancellationToken);
        Console.WriteLine("=== RESPONSE ===");
        Console.WriteLine(responseBody);
        Console.WriteLine(string.Empty);
        return responseMessage;


    }
}