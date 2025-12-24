using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace AIChess.Core.Models;

public class Filters : IAutoFunctionInvocationFilter
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(){WriteIndented = true};
    public event Action<AutoFunctionInvocationContext>? OnFunctionStart;
    public event Action<AutoFunctionInvocationContext>? OnFunctionCompleted;
    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
	{
		Console.WriteLine($"{context.Function.Name} Invoking.\n Args:\n--------\n{JsonSerializer.Serialize(context.Arguments, JsonSerializerOptions)}\n--------------\nSequence: {context.RequestSequenceIndex}, Function Sequence: {context.FunctionSequenceIndex}");
        OnFunctionStart?.Invoke(context);
        if (context.FunctionSequenceIndex > 0)
            context.Terminate = true;
		await next(context);
		Console.WriteLine($"{context.Function.Name} Invoked. Result:\n--------------\n{context.Result}\n--------------\n");
        OnFunctionCompleted?.Invoke(context);
    }

    
}
public class PromptFilter : IPromptRenderFilter
{
    public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
    {
        await next(context);
		Console.WriteLine($"Prompt Rendered:\n {context.RenderedPrompt}");
    }
}