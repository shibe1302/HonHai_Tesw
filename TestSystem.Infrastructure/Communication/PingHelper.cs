using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace TestSystem.Infrastructure.Communication;

public class PingHelper
{
    /// <summary>
    /// Chờ DUT boot lên và phản hồi ping.
    /// Tương đương vòng loop ping trong ConnectDUT() của C++ cũ.
    /// </summary>
    public static async Task<bool> WaitForDutAsync(
        string ip,
        int totalTimeoutSec,
        int intervalMs = 1000,
        Action<string>? onProgress = null)
    {
        var deadline = DateTime.Now.AddSeconds(totalTimeoutSec);
        int elapsed = 0;

        while (DateTime.Now < deadline)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 800);

                if (reply.Status == IPStatus.Success)
                    return true;
            }
            catch
            {
                // Bỏ qua lỗi mạng tạm thời, thử lại
            }

            elapsed += intervalMs;
            onProgress?.Invoke($"Chờ DUT... {elapsed / 1000}s / {totalTimeoutSec}s");
            await Task.Delay(intervalMs);
        }

        return false;
    }
}