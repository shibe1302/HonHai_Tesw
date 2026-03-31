using System.Threading.Tasks;
using TestSystem.Core.Interfaces;
using TestSystem.Core.Models;
using TestSystem.Infrastructure.Communication;

namespace TestSystem.Steps;

public class PingStep : ITestStep
{
    public string StepName => "Ping DUT";

    private readonly int _timeoutSec;

    public PingStep(int timeoutSec = 60)
    {
        _timeoutSec = timeoutSec;
    }

    public async Task<StepResult> ExecuteAsync(TestContext context)
    {
        if (string.IsNullOrEmpty(context.IpAddress))
            return StepResult.Fail("PING", "Chưa có IP trong context");

        bool ok = await PingHelper.WaitForDutAsync(
            ip: context.IpAddress,
            totalTimeoutSec: _timeoutSec,
            onProgress: msg => context.AddLog(msg)
        );

        if (!ok)
            return StepResult.Fail("PING", $"DUT không phản hồi sau {_timeoutSec}s");

        return StepResult.Ok("DUT đã kết nối");
    }
}