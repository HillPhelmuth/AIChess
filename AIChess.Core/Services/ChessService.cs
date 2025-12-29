using AIChess.Core.Helpers;
using AIChess.Core.Models;
using AIChess.Core.Plugins;
using Azure.AI.OpenAI;
using Chess;
using Google.Api.Gax;
using Google.Type;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Logging;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Polly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AIChess.Core.Models.HttpDelegates;

#pragma warning disable SKEXP0010

namespace AIChess.Core.Services;

public class ChessService
{
    private readonly AppState _appState;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StockFishService _stockFishService;
    private readonly OpenRouterService _openRouterService;
    private string _color = "black";
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };
    public event Action<string>? UpdateBoardFen;
    private bool _aiMoved;

    private const string ChessPromptV2 =
        """
		You are a chess-playing AI competing as {{ $color }}. Begin with a concise conceptual checklist (3-7 bullets) outlining your approach before selecting a move. Analyze the provided **Board State** and **Available Moves** to select your optimal next move. Prioritize strong tactical or strategic options and maintain a friendly conversation with your opponent.
		
		# Instructions
		1. Carefully evaluate the **Board State** and choose the best option from **Available Moves**.
		2. Before making your selection, briefly state the purpose of your move and the chosen move input.
		3. Execute your selected move. **Do not send a response until the move is confirmed as successful.**
		4. After the move is executed, validate the action in 1-2 lines: confirm the move succeeded or provide a concise error message. Only proceed if validation is positive.
		5. Upon successful execution, reply using the specified JSON format.
		
		# Board Representation
		- White pieces: UPPERCASE (e.g., 'K' for king, 'Q' for queen, 'N' for knight).
		- Black pieces: lowercase (e.g., 'k', 'q', 'n').
		- Empty squares: '.'
		
		## Output Structure
		If successful:
		```json
		{
		"ReasonForMove": "<Explanation of why you made that particular move in 200 words or less. Include other moves you considered and the benefits of your choice.>",
		"From": "e2", // The starting position of the move
		"To": "e4", // The ending position of the move
		"Message": "<Friendly chat message>" // Converse with your opponent
		}
		```
		- `ReasonForMove`: Explanation of why you made that particular move in 200 words or less. Include other moves you considered and the benefits of your choice.
		- `From`: The source square for the move, e.g., 'e2'.
		- `To`: The target square for the move, e.g., 'e4'.
		- `Message`: Friendly, conversational message.
		
		## Board State and Moves
		
		**Available Moves:** 
		{{ $availableMoves }}
		
		- **Board State (Ascii):**
		 {{ $boardState }}
		
		""";

    private const string ChessPromptV3 =
        """
        Act as an online chess player agent who specializes in trash-talk and insult comedy. For each move you make, select an optimal legal move based on the current board state and communicate it to your opponent. Your response must be a single JSON object with the following structure:
        
        - "ReasonForMove": A concise explanation (200 words or less) of why you made that particular move. Include other moves you considered and the benefits of your choice.
        - "From": The starting square of your chosen move, using standard chess notation (e.g., "e2").
        - "To": The ending square of your chosen move, using standard chess notation (e.g., "e4").
        - "Message": A creative, trash-talking or insult-comedy message directed at your opponent, tailored to the move and game state. Be witty, cheeky, and playfully provocative without using profane, discriminatory, or truly offensive language. All insults should remain strictly within the spirit of competitive banter.
        
        Before outputting the JSON, always internally reason step-by-step to:
        1. Analyze the current board state, determine all legal moves, and evaluate options for the best choice.
        2. Consider the game phase, board dynamics, and potential opponent replies.
        3. Devise a context-aware insult or trash-talk message that matches the move or situation—taunt them on clever moves, rub in your opponent’s mistakes, or deliver witty self-insults when behind. Vary your tone and reference recent play where relevant. Never resort to generic friendliness or compliments.
        4. After fully completing these reasoning steps, only then produce your JSON output.
        
        Do not include any reasoning in your output. Do not start with a conclusion—the JSON answer must result from prior, invisible reasoning. Only output the JSON for each move, and persist in this pattern until the game is finished.
        
        **Board Representation**
        - White pieces: UPPERCASE (e.g., 'K' for king, 'Q' for queen, 'N' for knight).
        - Black pieces: lowercase (e.g., 'k', 'q', 'n').
        - Empty squares: '.'
        
        Edge Cases/Contextual Reminders:
        - If delivering check, checkmate, or stalemate, ensure the message maximally rubs it in for your opponent (feel free to use vulgarity or cruelty).
        - If in a losing/bad position, playfully shift blame.
        - Never output genuine friendliness, bland banter, or neutral commentary—the "Message" must always express trash-talk or insult humor (while remaining playful and non-malicious).
        - All moves must be legal and board-state-appropriate.
        
        # Output Format
        
        - Only output a single JSON object—no explanation, no introductory or trailing text.
        - Use algebraic notation ('a1' or 'h8') for "From" and "To" fields.
        - "Message" should be a short, witty insult, taunt, or creative banter closely tied to the move and game state.
        
        # Notes
        
        - Maintain creativity and humor, and engage in personal, gratuitous, vulgar, or discriminatory attacks.
        - Never slip into real compliments or neutral phrases—even losing positions must be met with playful self-insult or backhanded jabs.
        - Always keep the responses quick, relevant, and on-topic for the game context.
        
        Reminders:
        - Always perform your internal move analysis and message justification prior to output.
        - Only provide the required JSON—no extra text or comments.
        - Maintain this output protocol uninterrupted for every move until the end of the game.
        - **Always** ensure your move is legal and valid for the current board state and selects one of the available moves.
        
        # Game State Information
        **Your Color:** {{ $color }}
        
        **Available Moves:** 
        {{ $availableMoves }}
        
        - **Board State (Ascii):**
         {{ $boardState }}
        
        """;
    private const string ChessPrompt =
        """
		You are a chess playing AI. You are playing as Black. Given the available moves, think carefully about which move would be best, then invoke 'GetBestMove' then execute that move by invoking 'MovePiece'. After moving, explain why that move is best in 100 words or less.
		
		## Board State Ascii Key
		
		White pieces are UPPERCASE and Black pieces are lowercase. So, white king = 'K' and black king = 'k'. White knight = 'N', black knight = 'n'. White pawn = 'P', black pawn = 'p'. White queen = 'Q', black queen = 'q'. White rook = 'R', black rook = 'r'. White bishop = 'B', black bishop = 'b'.
		Empty squares are represented by a '.'.

		## Available Moves
		{{ $availableMoves }}
		
		## Board State Ascii
		{{ $boardState }}

		## Board State Fen
		{{ $boardStateFen }}
		""";

    private const string ChatPrompt =
        """
        You are a friendely chess playing AI. Respond to the user in a friendly way, as if you're playing a casual game of chess and just chatting with your opponent. You are playing as {{ $color }}. 

        **Game History**
        {{ $moveHistory }}
        
        **Game State**
        {{ $gameState }}
        """;
    public ChessService(AppState appState, IConfiguration configuration, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, OpenRouterService openRouterService)
    {
        _appState = appState;
        _configuration = configuration;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _openRouterService = openRouterService;
        _stockFishService = new StockFishService(httpClientFactory);
    }
    private Kernel CreateKernel(string model = "gpt-4o")
    {
        var builder = Kernel.CreateBuilder();
        var loggingHandler = new LoggingHandler(new HttpClientHandler(), _loggerFactory);
        var client = new HttpClient(loggingHandler);
        var endpoint = new Uri("https://openrouter.ai/api/v1");
        var addOpenRouterClient =
            DelegateHandlerFactory.GetHttpClientWithHandler<OpenRouterReasoningHandler>(_loggerFactory,
                TimeSpan.FromMinutes(5));
        builder.AddOpenAIChatCompletion(modelId: model, apiKey: _configuration["OpenRouter:ApiKey"],
            endpoint: endpoint, httpClient: addOpenRouterClient, serviceId: "OpenRouter");

        builder.Services.AddSingleton(_appState);
        builder.Services.AddLogging(o =>
        {
            o.AddConsole();
            o.Services.AddSingleton(_loggerFactory);
        });
        builder.Services.ConfigureHttpClientDefaults(c =>
        {
            c.AddStandardResilienceHandler().Configure(o =>
            {
                o.Retry.ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests);
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.AttemptTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(150) };
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(600);
                o.TotalRequestTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromMinutes(15) };
            });
        });
        var kernel = builder.Build();
        //kernel.ImportPluginFromType<PlayChessPlugin>();
        var filter = new Filters();
        filter.OnFunctionStart += HandleFunctionStart;
        filter.OnFunctionCompleted += HandleFunctionCompleted;
        var promptFilter = new PromptFilter();
        kernel.AutoFunctionInvocationFilters.Add(filter);
        kernel.PromptRenderFilters.Add(promptFilter);
        Console.WriteLine($"Original: AutoFilterCount: {kernel.AutoFunctionInvocationFilters.Count}\n\nPromptFilters: {kernel.PromptRenderFilters.Count}");

        return kernel;
    }

    private void HandleFunctionCompleted(AutoFunctionInvocationContext obj)
    {
        if (obj.Function.Name == "MovePiece")
        {
            _aiMoved = true;
            _appState.SetBoardState();
            UpdateBoardFen?.Invoke(_appState.ChessBoard.ToFen());
            Console.WriteLine($"AI Moved: {obj.Result}");
        }
        else if (obj.Function.Name == "GetBestMove")
        {
            _aiMoved = false;
        }
        else
        {
            _aiMoved = false;
        }
    }

    private void HandleFunctionStart(AutoFunctionInvocationContext obj)
    {
        Console.WriteLine($"Function Start: {obj.Function.Name} - Args:\n {JsonSerializer.Serialize(obj.Arguments)}");
    }

    public ChatHistory ChatHistory { get; set; } = [];

    public async Task<string> SendChat(string color, string userMessage, CancellationToken cancellationToken = default)
    {
        //var model = GetModel("gpt-4.1-mini");
        var kernel = CreateKernel("openai/gpt-5-mini");
        if (ChatHistory.All(x => x.Role != AuthorRole.System))
        {
            var gameState = await _stockFishService.GetEvaluation(_appState.ChessBoard.ToFen());
            _appState.GameStateEval = gameState;
            var args = new KernelArguments { ["moveHistory"] = string.Join("\n", _appState.Logs), ["color"] = color, ["gameState"] = gameState is not null ? JsonSerializer.Serialize(gameState) : "No Evaluation available. Mate is likely imminent" };
            var promptTemplateFactory = new KernelPromptTemplateFactory();
            var promptTemplate = promptTemplateFactory.Create(new PromptTemplateConfig(ChatPrompt));
            var renderedPrompt = await promptTemplate.RenderAsync(kernel, args, cancellationToken);
            ChatHistory.AddSystemMessage(renderedPrompt);
        }
        ChatHistory.AddUserMessage(userMessage);
        var chatService = kernel.Services.GetRequiredKeyedService<IChatCompletionService>("OpenRouter");
        var response = await chatService.GetChatMessageContentAsync(ChatHistory, cancellationToken: cancellationToken);
        ChatHistory.AddAssistantMessage(response.Content!);
        return response.Content!;
    }
    public async Task<string> PlayChessChat(string color = "black",
         CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"PlayChessChat called with color: {color}");
        var model = GetModel(color);
        
        var kernel = CreateKernel(model);
        var pieceColor = color.Equals("Black", StringComparison.InvariantCultureIgnoreCase) ? PieceColor.Black : PieceColor.White;


        var moves = _appState.ChessBoard.Moves(pieceColor).Select(x => JsonSerializer.Serialize(new { From = x.OriginalPosition.ToString(), To = x.NewPosition.ToString() }));
        if (!moves.Any())
        {
            Console.WriteLine("No moves available, returning empty result.");
            return "No moves available, returning empty result.";

        }
        var moveList = string.Join("\n", moves);
        var args = GetKernelArgs(moveList, color);
        var promptTemplateFactory = new KernelPromptTemplateFactory();
        var promptTemplate = promptTemplateFactory.Create(new PromptTemplateConfig(ChessPromptV3));
        var renderedPrompt = await promptTemplate.RenderAsync(kernel, args, cancellationToken);
        //Console.WriteLine($"Rendered Prompt:\n----------------------------------------\n {renderedPrompt}\n----------------------------------------\n");
        var chatService = kernel.Services.GetRequiredKeyedService<IChatCompletionService>("OpenRouter");
        var settings = GetExecutionSettings(model);
        var history = new ChatHistory("You are a chess playing AI");
        
#if DEBUG
        //File.WriteAllBytes("image.png", pngData);
#endif
        if (await _openRouterService.ModelSupportsImageInputAsync(model))
        {
            var pngData = await PngFromFen(cancellationToken);
            var image = new ImageContent(new ReadOnlyMemory<byte>(pngData), "image/png");
            Console.WriteLine($"Image data: {pngData.Length} bytes");
            var text = new TextContent(renderedPrompt);
            history.AddUserMessage([image, text]);
        }
        else
        {
            history.AddUserMessage(renderedPrompt);
        }
        var singleResult = await chatService.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);

        ChessMove resultMove;
        string moveResponse = "";
        try
        {
            resultMove = JsonSerializer.Deserialize<ChessMove>(singleResult.ToString().SanitizeOutput())!;
            moveResponse = singleResult.ToString().SanitizeOutput();
        }
        catch (Exception ex)
        {
            resultMove = new ChessMove()
            {
                ReasonForMove = "",
                From = "",
                To = "",
                FriendlyChatMessage = "Oh shit. I just tried to cheat by making multiple moves! I Suck!",
            };
        }
        var moveSuccess = _appState.Move(resultMove!.Move!, true);
        var tries = 0;
        var errorMessage = "";
        while (!moveSuccess && tries < 3)
        {
            try
            {
                history.AddAssistantMessage(moveResponse);
                history.AddUserMessage($"An error occurred. Try again.\n\n**Error Message:** {errorMessage}");
                singleResult = await chatService.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);
                moveResponse = singleResult.ToString().SanitizeOutput();
                resultMove = JsonSerializer.Deserialize<ChessMove>(moveResponse);
                Console.WriteLine($"Move Selected: {resultMove.Move}");
                moveSuccess = _appState.Move(resultMove!.Move!, true);
            }
            catch (Exception ex)
            {
                var error = $"Exception during move retry: {ex.Message}";
                errorMessage = error;
                Console.WriteLine(error);
                await Task.Delay(tries * 1000, cancellationToken);
            }
            finally
            {
                tries++;
            }
        }
        if (moveSuccess)
        {
            _appState.LastAiColor = pieceColor.Name;
            var evalResult = await _stockFishService.GetEvaluation(_appState.ChessBoard.ToFen());
            if (evalResult is not null)
                _appState.GameStateEval = evalResult;
            UpdateBoardFen?.Invoke(_appState.ChessBoard.ToFen());
        }
       

        _aiMoved = false;
        var friendlyChatMessage = resultMove.FriendlyChatMessage;

        var finalChatResponse = $"Model: {model}\n\n{friendlyChatMessage}";
        ChatHistory.AddAssistantMessage(finalChatResponse);
        return finalChatResponse;
        
    }

    public async Task<byte[]> PngFromFen(CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetAsync($"{FenToImageBaseUrl}/{_appState.ChessBoard.ToFen()}", cancellationToken);
        // Extract the PNG image data from the response
        var pngData = await result.Content.ReadAsByteArrayAsync(cancellationToken);
        return pngData;
    }

    private static HttpClient _httpClient = new();
    private const string FenToImageBaseUrl = "https://fen2image.chessvision.ai";
    public async Task<string> PlayChessNoChat(string color = "black", CancellationToken cancellationToken = default)
    {
        return await PlayChessChat(color, cancellationToken);

    }
    public class ChessMove
    {
        [JsonPropertyName("Move")]
        [JsonIgnore]
        [Description(
            "Move you made as starting grid square and ending grid square seperated by a space and hyphen (e.g. 'e2 - e4'")]
        public string Move => $"{From}-{To}";
        [JsonPropertyName("ReasonForMove")]
        [Description("Explanation of why you made that particular move in 200 words or less.")]
        public required string ReasonForMove { get; set; }
        [JsonPropertyName("From")]
        [Description("The starting square of your move (if move is e2 to e4, `From` = 'e2')")]
        public required string From { get; set; }
        [JsonPropertyName("To")]
        [Description("The ending square of your move (if move is e2 to e4, `To` = 'e4')")]
        public required string To { get; set; }
        [Description("Friendly message as if you're playing a casual game of chess and just chatting with your opponent.")]

        [JsonPropertyName("Message")]
        public required string FriendlyChatMessage { get; set; }
        //public required string ReasoningForMoveChoice { get; set; }
    }
    private const string StockFishBaseUrl = "https://stockfish.online/api/s/v2.php";
    public async Task<string?> CheatAsync(string fen)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response =
                await httpClient.GetFromJsonAsync<StockfishResponse>($"{StockFishBaseUrl}?fen={fen}&depth=15");
            if (response == null)
            {
                //request.tcs.SetException(new Exception("Null response from Stockfish API"));
                throw new Exception($"Stockfish Api Request failed");
            }

            var data = response.Bestmove;
            var ponderIndex = data.IndexOf("ponder", StringComparison.OrdinalIgnoreCase);
            var bestMove = ponderIndex != -1
                ? data[..ponderIndex].Replace("bestmove", "").Trim()
                : data.Replace("bestmove ", "").Trim();
            Console.WriteLine($"Best move cheat: {bestMove}");
            // Add a hyphen between the two positions if not already present
            bestMove = $"{bestMove[..2]}-{bestMove[2..]}";
            return bestMove;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CheatAsync: {ex.Message}");
            return null;
        }
    }
    private KernelArguments GetKernelArgs(string moveList, string color)
    {
        return new KernelArguments()
        {
            ["availableMoves"] = moveList,
            ["color"] = color,
            ["boardState"] = _appState.ChessBoard.ToAscii(),
            ["boardStateFen"] = _appState.ChessBoard.ToFen()
        };

    }

    private PromptExecutionSettings GetExecutionSettings(string model)
    {
        return new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(ChessMove)
        };
    }

    private string? GetModel(string nextMoveColor)
    {
        return nextMoveColor.Equals("black", StringComparison.InvariantCultureIgnoreCase) ?
            _appState.GameOptions.BlackModelId :
            _appState.GameOptions.WhiteModelId;
    }
}