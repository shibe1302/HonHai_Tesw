using System.Threading.Tasks;
using TestSystem.Core.Models;

namespace TestSystem.Core.Interfaces;

public interface ITestStep
{
    string StepName { get; }

    Task<StepResult> ExecuteAsync(TestContext context);
}