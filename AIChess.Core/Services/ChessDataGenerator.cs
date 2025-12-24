using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIChess.Core.Helpers;
using AIChess.Core.Models;
using AIChess.Core.Plugins;
using Chess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Polly;

namespace AIChess.Core.Services;

public class ChessDataGenerator
{
    private readonly IConfiguration _configuration;
    private static ChessBoard _board = new ChessBoard();
    private const string DataPath = @"C:\Users\adamh\source\repos\AIChess\AIChess.Core\new_games_prepared.jsonl";
    private static readonly SemaphoreSlim _httpSemaphore = new(10); // Limit concurrent API calls
    private static readonly SemaphoreSlim _fileLock = new(1); // For thread-safe file access
    private static IHttpClientFactory _httpClientFactory;
    public ChessDataGenerator(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _configuration = config;
        _httpClientFactory = httpClientFactory;

    }
    public event Action<string>? OnDataGenerated;
    public async Task GenerateFineTuneData()
    {
        var readAllLinesAsync = (await File.ReadAllLinesAsync(DataPath));
        var shuffledList = readAllLinesAsync.ShuffledList();
        var trainTranscripts = shuffledList.Take(500).Select(x => JsonSerializer.Deserialize<ChessData>(x)).Select(y => y.Completion);
        var testTranscripts = shuffledList.Take(50).Select(x => JsonSerializer.Deserialize<ChessData>(x)).Select(y => y.Completion);
        var kernel = CreateKernel();
        var fineTuneData = new List<FineTuneDataLine>();
        var testFineTuneData = new List<FineTuneDataLine>();
        await PopulateFineTuneData(trainTranscripts, kernel, fineTuneData);
        await PopulateFineTuneData(testTranscripts, kernel, testFineTuneData, true);
        var fineTuneLineBuilder = new StringBuilder();
        foreach (var line in fineTuneData)
        {
            var value = line.ToString();
            fineTuneLineBuilder.AppendLine(value);
            
            
        }
        await File.WriteAllTextAsync("completed_train_data.jsonl", fineTuneLineBuilder.ToString());
        foreach (var line in testFineTuneData)
        {
            fineTuneLineBuilder.Clear();
            var value = line.ToString();
            fineTuneLineBuilder.AppendLine(value);
            //AppendLineToFile(@"Data\TEMP_test_fine_tune_data.jsonl", value);
        }
        await File.WriteAllTextAsync("completed_validation_data.jsonl", fineTuneLineBuilder.ToString());

    }
    private async Task PopulateFineTuneData(IEnumerable<string> trainTranscripts, Kernel kernel, List<FineTuneDataLine> fineTuneData, bool isTestData = false)
    {
        var concurrentFineTuneData = new ConcurrentBag<FineTuneDataLine>();
        var promptTemplateFactory = new KernelPromptTemplateFactory();
        var promptTemplate = promptTemplateFactory.Create(new PromptTemplateConfig(ChessPromptTrain));
        var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var tempPath = isTestData ? @"Data\validation_data.jsonl" : @"Data\train_data.jsonl";

        // Create directory in advance to avoid repeated checks
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath) ?? string.Empty);

        // System message is constant, so create it once
        var systemMessage = new Message { Role = "system", Content = "You are a chess playing AI. Reply in json format." };

        foreach (var train in trainTranscripts)
        {
            var localBoard = new ChessBoard();
            List<BoardState> boardStates = [];
            try
            {
                boardStates = ConvertToBoardStates(train, localBoard);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                continue;
            }

            var randomSelection = boardStates.SelectRandomUniqueItems(10);
            Console.WriteLine($"Starting transcript {trainTranscripts.ToList().IndexOf(train)}");
            var index = 0;
            Console.WriteLine($"");
            try
            {
                // Process board states in parallel
                await Parallel.ForEachAsync(randomSelection, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                    async (boardState, cancellationToken) =>
                {
                    try
                    {
                        // Create thread-local board instance
                        var threadBoard = ChessBoard.LoadFromFen(boardState.Fen);

                        var color = boardState.Fen.Split(' ')[1];
                        // Skip pieceColor assignment as it's not used

                        // Pre-compute move list
                        var movesArray = threadBoard.Moves().ToArray();
                        var movesList = string.Join("\n", movesArray.Select(x => $"{x.OriginalPosition}{x.NewPosition}"));

                        var trainArgs = new KernelArguments
                        {
                            ["availableMoves"] = movesList,
                            ["boardStateFen"] = boardState.Fen
                        };

                        // Limit concurrent HTTP requests with semaphore
                        await _httpSemaphore.WaitAsync(cancellationToken);

                        string bestMove;
                        string trainPrompt;

                        try
                        {
                            // Render the prompt and get best move in parallel
                            var promptTask = promptTemplate.RenderAsync(kernel, trainArgs);
                            var bestMoveTask = GetBestMove(boardState.Fen);

                            await Task.WhenAll(promptTask, bestMoveTask);

                            trainPrompt = promptTask.Result;
                            bestMove = bestMoveTask.Result;
                        }
                        finally
                        {
                            _httpSemaphore.Release();
                        }

                        // Pre-compute serialized response once
                        var response = JsonSerializer.Serialize(new ChessOutput(bestMove), jsonOptions);

                        var output = $"Line generated with BestMove:{bestMove}, FEN state:{boardState.Fen} moveList:{string.Join(", ", movesArray.Select(x => $"{x.OriginalPosition}{x.NewPosition}"))}";

                        var fineTuneDataLine = new FineTuneDataLine
                        {
                            Messages =
                            [
                                systemMessage,
                                new Message { Role = "user", Content = trainPrompt },
                                new Message { Role = "assistant", Content = response }
                            ]
                        };

                        // Add to thread-safe collection
                        concurrentFineTuneData.Add(fineTuneDataLine);
                        OnDataGenerated?.Invoke($"{Interlocked.Increment(ref index)} " + output);

                        // Thread-safe file writing - append directly to save memory
                        await AppendLineToFileAsync(tempPath, fineTuneDataLine.ToString());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        OnDataGenerated?.Invoke($"ERROR: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        // Add all items to the original list at once
        fineTuneData.AddRange(concurrentFineTuneData);
    }
    
    // Add thread-safe file writing method
    private async Task AppendLineToFileAsync(string filePath, string line)
    {
        try
        {
            await _fileLock.WaitAsync();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                await File.AppendAllTextAsync(filePath, line + Environment.NewLine);
            }
            finally
            {
                _fileLock.Release();
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    
    // Original AppendLineToFile method kept for compatibility
    public void AppendLineToFile(string filePath, string line)
    {
        // Call the async version synchronously
        AppendLineToFileAsync(filePath, line).GetAwaiter().GetResult();
    }

    // Modified to accept a board parameter instead of using the static one
    public static List<BoardState> ConvertToBoardStates(string transcript, ChessBoard? board = null)
    {
        var moves = ConvertToTurnList(transcript);
        var localBoard = board ?? new ChessBoard();
        
        var fenPositions = new List<BoardState>();
        foreach (var move in moves)
        {
            localBoard.Move(move);
            fenPositions.Add(new BoardState(localBoard.ToFen(), localBoard.ToAscii()));
        }

        for (var i = 0; i < fenPositions.Count; i++)
        {
            Console.WriteLine($"Move {i + 1}: {fenPositions[i].Fen}");
        }
        return fenPositions;
    }

    private Kernel CreateKernel(string modelId = "gpt-4.1-mini")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId, _configuration["OpenAI:ApiKey"]!, serviceId: "OpenAI");
        builder.Services.AddLogging(o =>
        {
            o.AddConsole();
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

        return kernel;
    }
    private const string StockFishBaseUrl = "https://stockfish.online/api/s/v2.php";
    
    // Add these fields at the class level
    private static readonly ConcurrentDictionary<string, (string bestMove, DateTimeOffset expiration)> _bestMoveCache = new();
    private static readonly ConcurrentQueue<(string fen, TaskCompletionSource<string> tcs)> _batchQueue = new();
    private static readonly SemaphoreSlim _batchSemaphore = new(1);
    private const int BATCH_SIZE = 10;
    private const int CACHE_MINUTES = 60;
    private static readonly Timer _batchTimer;

    // Add constructor initialization for the timer
    static ChessDataGenerator()
    {
        _batchTimer = new Timer(ProcessBatchQueue, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    // Replace the existing GetBestMove method with this implementation
    public static async Task<string> GetBestMove([Description("Fen state of the chess board")] string fenState)
    {
        // Check cache first
        if (_bestMoveCache.TryGetValue(fenState, out var cached))
        {
            if (cached.expiration > DateTimeOffset.UtcNow)
            {
                return cached.bestMove;
            }
            // Remove expired entry
            _bestMoveCache.TryRemove(fenState, out _);
        }

        // Create completion source for this request
        var tcs = new TaskCompletionSource<string>();
        _batchQueue.Enqueue((fenState, tcs));

        // Wait for result
        return await tcs.Task;
    }

    // Add new private method for batch processing
    private static async void ProcessBatchQueue(object? state)
    {
        if (!_batchQueue.Any() || !_batchSemaphore.Wait(0))
            return;

        try
        {
            var batch = new List<(string fen, TaskCompletionSource<string> tcs)>();
            
            // Collect batch of requests
            while (batch.Count < BATCH_SIZE && _batchQueue.TryDequeue(out var request))
            {
                // Skip if already in cache
                if (_bestMoveCache.TryGetValue(request.fen, out var cached) && 
                    cached.expiration > DateTimeOffset.UtcNow)
                {
                    request.tcs.SetResult(cached.bestMove);
                    continue;
                }
                batch.Add(request);
            }

            if (!batch.Any())
                return;

            // Process batch
            using var httpClient = _httpClientFactory.CreateClient();
            foreach (var request in batch)
            {
                try
                {
                    var response = await httpClient.GetFromJsonAsync<StockfishResponse>($"{StockFishBaseUrl}?fen={request.fen}&depth=15");
                    if (response == null)
                    {
                        request.tcs.SetException(new Exception("Null response from Stockfish API"));
                        continue;
                    }

                    var data = response.Bestmove;
                    var ponderIndex = data.IndexOf("ponder", StringComparison.OrdinalIgnoreCase);
                    var bestMove = ponderIndex != -1 
                        ? data[..ponderIndex].Replace("bestmove", "").Trim()
                        : data.Replace("bestmove ", "").Trim();

                    // Cache the result
                    _bestMoveCache.TryAdd(request.fen, (bestMove, DateTimeOffset.UtcNow.AddMinutes(CACHE_MINUTES)));
                    
                    // Complete the task
                    request.tcs.SetResult(bestMove);
                }
                catch (Exception ex)
                {
                    request.tcs.SetException(ex);
                }
            }
        }
        finally
        {
            _batchSemaphore.Release();
        }
    }

    // Add cleanup method
    public static void Dispose()
    {
        _batchTimer?.Dispose();
        _batchSemaphore?.Dispose();
    }

    public static List<BoardState> ConvertToBoardStates(string transcript)
    {
        var moves = ConvertToTurnList(transcript);

        var fenPositions = new List<BoardState>();
        foreach (var move in moves)
        {
            _board.Move(move);
            fenPositions.Add(new BoardState(_board.ToFen(), _board.ToAscii()));

        }

        for (var i = 0; i < fenPositions.Count; i++)
        {
            Console.WriteLine($"Move {i + 1}: {fenPositions[i].Fen}");
        }
        return fenPositions;
    }

    private static List<string> ConvertToTurnList(string transcript)
    {
        var turns = new List<string>();
        var regex = new Regex(@"\d+\.(\S+)(?:\s+(\S+))?");
        var matches = regex.Matches(transcript);

        foreach (Match match in matches)
        {
            turns.Add(match.Groups[1].Value);
            if (match.Groups[2].Success)
            {
                turns.Add(match.Groups[2].Value);
            }
        }

        return turns;
    }

    public const string ChessPromptTrain =
        """
        Given the available moves based on the current board state in FEN notation, think carefully about which move would be best, then make that move. Be sure to note your color indicated in the FEN notation before deciding.
        ## Ouput Json Example
                {
          "BestMove":"<-best move->",
        }
                
        ## Available Moves
        {{ $availableMoves }}
       
        ## Board State Fen
        {{ $boardStateFen }}
        """;
    private const string ChessPromptExplain =
        """
        You are a chess playing AI. Given the the Board State and the expert's move, explain why that move is clearly the best move. Limit your explaination to 100 words.
       
        ## Board State Ascii Key

        White pieces are UPPERCASE and Black pieces are lowercase. So, white king = 'K' and black king = 'k'. White knight = 'N', black knight = 'n'. White pawn = 'P', black pawn = 'p'. White queen = 'Q', black queen = 'q'. White rook = 'R', black rook = 'r'. White bishop = 'B', black bishop = 'b'.
        Empty squares are represented by a '.'.

        ## Board State Ascii
        {{ $boardState }}

        ## Board State Fen
        {{ $boardStateFen }}
        
        ## Expert's Move
        {{ $expertsMove }}
        """;
}
public record BoardState(string Fen, string Ascii);
public class ChessData
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; }

    [JsonPropertyName("completion")]
    public string Completion { get; set; }

}
public class FineTuneDataLine
{
    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; }
    public override string ToString()
    {
        return JsonSerializer.Serialize(this);

    }
}

public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }
}
public class ChessOutput(string bestMove)
{
    [JsonPropertyName("Explanation")]
    public string? Explanation { get; set; }

    [JsonPropertyName("BestMove")]
    public string BestMove { get; set; } = bestMove;
}