using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestSystem.Core.Interfaces;
using TestSystem.Core.Models;

namespace TestSystem.Core.Engine;

public class TestEngine
{
    private readonly List<ITestStep> _steps;

    // Dùng để báo lên UI đang chạy step nào
    public event Action<string>? OnStatusChanged;

    public TestEngine(List<ITestStep> steps)
    {
        _steps = steps;
    }

    public async Task<TestContext> RunAsync(TestContext context)
    {
        context.IsPassed = true;
        context.ErrorCode = "";

        foreach (var step in _steps)
        {
            // Báo UI step đang chạy
            OnStatusChanged?.Invoke($"Đang chạy: {step.StepName}...");
            context.AddLog($"--- {step.StepName} bắt đầu ---");

            StepResult result = await step.ExecuteAsync(context);

            context.AddLog($"--- {step.StepName}: {(result.Passed ? "PASS" : "FAIL")} {result.Message}");

            if (!result.Passed)
            {
                context.IsPassed = false;
                context.ErrorCode = result.ErrorCode;

                // Dừng luôn khi có step fail
                OnStatusChanged?.Invoke($"FAIL tại: {step.StepName} [{result.ErrorCode}]");
                break;
            }
        }

        if (context.IsPassed)
            OnStatusChanged?.Invoke("PASS - Hoàn thành!");

        return context;
    }
}