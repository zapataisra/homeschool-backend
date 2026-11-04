using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using HomeschoolAI.Application.Evaluation;
using HomeschoolAI.Application.Progress;
using HomeschoolAI.Application.Validation;
using HomeschoolAI.Domain.Progress;
using HomeschoolAI.Domain.Validation;
using Xunit;

namespace HomeschoolAI.Tests;

public class CriticalEvaluationServiceTests
{
    private static CriticalEvaluationService BuildService(
        FakeAgentCompletionService completion,
        IMathValidator? mathValidator = null,
        IXPCalculator? xpCalculator = null) =>
        new(
            mathValidator ?? new MathValidator(),
            xpCalculator ?? new XPCalculator(),
            completion,
            NullLogger<CriticalEvaluationService>.Instance);

    private static string JsonOf(EvaluationResultDto dto) => JsonSerializer.Serialize(dto);

    [Fact]
    public async Task Correct_math_awards_full_xp_on_first_attempt()
    {
        var completion = new FakeAgentCompletionService
        {
            Reply = JsonOf(new EvaluationResultDto
            {
                Precision = 100,
                Confidence = 0.9,
                ExtractedOperation = new ExtractedOperationDto { Operand1 = 5, Operation = "+", Operand2 = 3, ProposedResult = 8 },
                SourceChunkIds = new List<int> { 1, 2 }
            })
        };
        var service = BuildService(completion);

        var result = await service.EvaluateAsync("5 más 3 es 8", attempt: 1, baseXp: 100);

        Assert.True(result.MathIsCorrect);
        Assert.Equal(100, result.XpAwarded);
        Assert.NotNull(result.Evaluation);
        Assert.Equal(100, result.Evaluation!.Precision);
    }

    [Fact]
    public async Task Correct_math_awards_reduced_xp_on_later_attempts()
    {
        var completion = new FakeAgentCompletionService
        {
            Reply = JsonOf(new EvaluationResultDto
            {
                Precision = 100,
                Confidence = 1.0,
                ExtractedOperation = new ExtractedOperationDto { Operand1 = 10, Operation = "-", Operand2 = 4, ProposedResult = 6 }
            })
        };
        var service = BuildService(completion);

        var second = await service.EvaluateAsync("10 menos 4 es 6", attempt: 2, baseXp: 100);
        var third = await service.EvaluateAsync("10 menos 4 es 6", attempt: 3, baseXp: 100);

        Assert.True(second.MathIsCorrect);
        Assert.Equal(75, second.XpAwarded);
        Assert.Equal(50, third.XpAwarded);
    }

    [Fact]
    public async Task Incorrect_math_awards_no_xp()
    {
        var completion = new FakeAgentCompletionService
        {
            Reply = JsonOf(new EvaluationResultDto
            {
                Precision = 50,
                Confidence = 0.4,
                ExtractedOperation = new ExtractedOperationDto { Operand1 = 5, Operation = "+", Operand2 = 3, ProposedResult = 9 }
            })
        };
        var service = BuildService(completion);

        var result = await service.EvaluateAsync("5 más 3 es 9", attempt: 1, baseXp: 100);

        Assert.False(result.MathIsCorrect);
        Assert.Equal(0, result.XpAwarded);
    }

    [Fact]
    public async Task Missing_extracted_operation_awards_no_xp()
    {
        var completion = new FakeAgentCompletionService
        {
            Reply = JsonOf(new EvaluationResultDto
            {
                Precision = 0,
                Confidence = 0.1,
                ExtractedOperation = null
            })
        };
        var service = BuildService(completion);

        var result = await service.EvaluateAsync("no sé", attempt: 1, baseXp: 100);

        Assert.False(result.MathIsCorrect);
        Assert.Equal(0, result.XpAwarded);
    }

    [Fact]
    public async Task Division_by_zero_in_extracted_operation_is_invalid()
    {
        var completion = new FakeAgentCompletionService
        {
            Reply = JsonOf(new EvaluationResultDto
            {
                Precision = 0,
                Confidence = 0.5,
                ExtractedOperation = new ExtractedOperationDto { Operand1 = 5, Operation = "/", Operand2 = 0, ProposedResult = 0 }
            })
        };
        var service = BuildService(completion);

        var result = await service.EvaluateAsync("5 entre 0", attempt: 1, baseXp: 100);

        Assert.False(result.MathIsCorrect);
        Assert.Equal(0, result.XpAwarded);
    }

    [Fact]
    public async Task Invalid_json_returns_fallback_without_xp()
    {
        var completion = new FakeAgentCompletionService { Reply = "esto no es JSON" };
        var service = BuildService(completion);

        var result = await service.EvaluateAsync("respuesta", attempt: 1, baseXp: 100);

        Assert.Null(result.Evaluation);
        Assert.False(result.MathIsCorrect);
        Assert.Equal(0, result.XpAwarded);
    }

    [Fact]
    public async Task Deserializes_camel_case_json_from_the_llm()
    {
        var completion = new FakeAgentCompletionService
        {
            Reply = "{\"precision\":100,\"confidence\":0.9,\"missingConcepts\":[]," +
                    "\"extractedOperation\":{\"operand1\":5,\"operation\":\"+\",\"operand2\":3,\"proposedResult\":8}," +
                    "\"sourceChunkIds\":[1,2]}"
        };
        var service = BuildService(completion);

        var result = await service.EvaluateAsync("5 más 3", attempt: 1, baseXp: 100);

        Assert.True(result.MathIsCorrect);
        Assert.Equal(100, result.XpAwarded);
        Assert.Equal(100, result.Evaluation!.Precision);
    }

    [Fact]
    public async Task Passes_critical_agent_system_prompt()
    {
        var completion = new FakeAgentCompletionService
        {
            Reply = JsonOf(new EvaluationResultDto
            {
                Precision = 100,
                Confidence = 1.0,
                ExtractedOperation = new ExtractedOperationDto { Operand1 = 1, Operation = "+", Operand2 = 1, ProposedResult = 2 }
            })
        };
        var service = BuildService(completion);

        await service.EvaluateAsync("1 más 1 es 2", attempt: 1, baseXp: 100);

        Assert.NotNull(completion.LastSystemPrompt);
        Assert.Contains("Agente Crítico", completion.LastSystemPrompt);
    }
}
