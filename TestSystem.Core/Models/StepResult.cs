using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestSystem.Core.Models
{
    public class StepResult
    {
        public bool Passed { get; init; }
        public string ErrorCode { get; init; } = "";
        public string Message { get; init; } = "";

        public static StepResult Ok(string message = "")
    => new() { Passed = true, Message = message };

        public static StepResult Fail(string errorCode, string message = "")
            => new() { Passed = false, ErrorCode = errorCode, Message = message };
    }
}
