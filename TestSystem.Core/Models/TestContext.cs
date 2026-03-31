using System;
using System.Collections.Generic;

namespace TestSystem.Core.Models;

public class TestContext
{
    public string Mac { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string Station { get; set; } = "";
    public string Model { get; set; } = "";

    public bool IsPassed { get; set; } = true;
    public string ErrorCode { get; set; } = "";
    public string UserMessage { get; set; } = "";

    // Log từng bước để ghi ra file sau này
    public List<string> StepLogs { get; } = new();

    public void AddLog(string message)
    {
        StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}