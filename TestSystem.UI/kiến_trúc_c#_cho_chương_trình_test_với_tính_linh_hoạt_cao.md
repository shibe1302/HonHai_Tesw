# Conversation with Claude

## Human (Mar 29, 2026, 11:25 AM):

tôi đang được giao nhiệm vụ chuyển đổi chương trình test từ C++ sang C#. tôi cần bạn tư vấn kiến trúc cho chương trình C#. kiến trúc mà sau có thể áp dụng cho con hàng khác 1 cách nhanh chóng(tính linh hoạt tái sử dụng cao ít hard code). hiện tại chương trình cũ đang sử dụng access làm nơi lưu trữ config local (cần bạn tư vấn nên sử dụng tiếp access hay đổi qua công nghệ khác). tư vấn thêm kiến trúc để sau ví dụ chạy ổn tôi muốn thêm api để hiện thị thêm thông tin chi tiết kiểu monitor qua trình duyệt vì UI của C# rất tù không đẹp và hạn chế nhiều.  nhưng cái quan trọng bạn phải nghiên cứu kỹ ở source code cũ là sfis (shop floor), lưu log và đọc log để quyết định pass fail. với cả bạn hướng dẫn tôi cơ bản từ ping hoặc com trước nhé . vì nếu code một loạt tôi chả hiểu gì bạn hướng dẫn đơn giản để tôi tự code các phần còn lại.

---

## Claude:

Tôi đã đọc kỹ toàn bộ source code. Để hiểu rõ trước khi tư vấn kiến trúc, dưới đây là **phân tích và lộ trình từng bước** cho bạn.

---

## 📋 Phân tích code cũ — những điểm cốt lõi

Từ source code, tôi thấy hệ thống cũ hoạt động theo flow này:

**`Load` → `Trigger`(scan barcode) → `ConnectDUT`(Ping/COM) → các TestStep → `SFISSentResult` → `UnLoad`**

Các phần quan trọng nhất cần chú ý khi chuyển sang C#:

| Phần | C++ cũ | Ghi chú |
|---|---|---|
| **SFIS** | `CSfis::SFISInit`, `SFISSentPreData`, `SFISSentPassResult/Fail` | Giao tiếp shop floor, báo PASS/FAIL |
| **Log** | File text: `PASS_MAC_MODEL_STATION_DATE_.txt` | Tên file chứa kết quả luôn |
| **Pass/Fail** | `getTestStatus()` + `setErrorCode()` + đọc log DUT | Kết quả tổng hợp nhiều nguồn |
| **COM port** | `SerialOpen/Read/Write/Close` | Giao tiếp DUT qua serial |
| **Config** | `getGlobalVeriableString/Int(pContainer, "Section", "Key")` | Đọc từ Access/INI |
| **Ping** | Dùng `PingTest.log` file | DUT ping về PC để xác nhận kết nối |

---

## 🏗️ Kiến trúc C# đề xuấtDưới đây là sơ đồ kiến trúc tổng thể cho hệ thống C# mới:---

## 💬 Tư vấn chi tiết từng vấn đề

### 1. Access → nên đổi sang SQLite

Bỏ Access đi, dùng **SQLite** vì:

- Access cần Office/Jet driver (32/64-bit hay bị xung đột, nightmare khi deploy).
- SQLite là một file `.db` duy nhất, không cần cài, .NET hỗ trợ native qua `Microsoft.Data.Sqlite`.
- Dễ migrate lên SQL Server sau này nếu cần chia sẻ config qua mạng nội bộ.
- Config hiện tại dạng `getGlobalVeriableString(pContainer, "Section", "Key")` → map 1:1 thành bảng `Config(Section, Key, Value)` trong SQLite.

### 2. Kiến trúc mở rộng cho Web Monitor

Chạy WinForms và ASP.NET Minimal API **cùng một process**. Khi test engine cập nhật kết quả, nó push qua **SignalR** → browser nhận real-time. Không cần viết UI phức tạp trong C# nữa, chỉ cần một trang HTML đơn giản là xong.

### 3. SFIS — điểm cốt lõi từ code cũ

Từ `LoadRoutines.cpp`, flow SFIS là:
```
SFISInit() → SFISSentPreData(MAC, Station) → [chạy test] → SFISSentPassResult() hoặc SFISSentFailResult(ErrorCode)
```
Trả về chuỗi `"SFIS_SUCCESS"` mới được tính là OK. Bạn cần wrap cái DLL cũ (`SFISDLL`) hoặc viết lại service gọi API SFIS trực tiếp.

### 4. Pass/Fail logic từ Log

Code cũ có hàm `CheckLog()` — đọc file log của DUT, tìm từ khoá PASS/FAIL bên trong. Đây là điểm **quan trọng nhất**: kết quả không chỉ từ test step trả về mà còn phải parse nội dung log DUT.

---

## 🚀 Hướng dẫn bước 1: Bắt đầu với Ping và COM port

Thay vì code tất cả một lúc, hãy bắt đầu với **project console đơn giản** để hiểu cơ bản.

### Bước 1a — Ping đơn giản trong C#

```csharp
// PingHelper.cs
using System.Net.NetworkInformation;

public class PingHelper
{
    public static async Task<bool> PingAsync(string ipAddress, int timeoutMs = 1000)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(ipAddress, timeoutMs);
        return reply.Status == IPStatus.Success;
    }
}

// Dùng trong test step:
// bool ok = await PingHelper.PingAsync("192.168.1.1");
```

Tương đương với phần code cũ dùng `PingTest.log` — nhưng trong C# bạn dùng `System.Net.NetworkInformation.Ping` trực tiếp, không cần file log trung gian.

### Bước 1b — Mở COM port đọc/ghi

```csharp
// SerialPortHelper.cs
using System.IO.Ports;

public class SerialPortHelper : IDisposable
{
    private SerialPort _port;

    public void Open(string portName, int baudRate = 115200)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
        _port.ReadTimeout  = 3000;
        _port.WriteTimeout = 3000;
        _port.Open();
    }

    public void Write(string data) => _port.Write(data);

    // Tương đương ExpectCom() trong C++ cũ
    public string ReadUntil(string expected, int timeoutMs = 5000)
    {
        var result = new System.Text.StringBuilder();
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < deadline)
        {
            if (_port.BytesToRead > 0)
                result.Append(_port.ReadExisting());
            if (result.ToString().Contains(expected))
                return result.ToString();
            Thread.Sleep(100);
        }
        throw new TimeoutException($"Không tìm thấy '{expected}' trong {timeoutMs}ms");
    }

    public void Dispose() => _port?.Close();
}
```

### Bước 1c — Thử ngay trong console

```csharp
// Program.cs — chạy thử trước khi làm gì phức tạp
var ping = await PingHelper.PingAsync("192.168.1.1");
Console.WriteLine($"Ping: {(ping ? "OK" : "FAIL")}");

using var com = new SerialPortHelper();
com.Open("COM3", 115200);
com.Write("version\r\n");
var response = com.ReadUntil("OK", timeoutMs: 3000);
Console.WriteLine($"DUT response: {response}");
```

---

Sau khi bạn test được Ping và COM port chạy ổn, bước tiếp theo tôi sẽ hướng dẫn cách tạo `ITestStep`, `TestContext` và `TestEngine` để ghép các bước test lại thành một flow hoàn chỉnh. Bạn muốn bắt đầu với **Ping** hay **COM port** trước?Bạn trả lời xong tôi sẽ code hướng dẫn phần tiếp theo cụ thể cho trường hợp của bạn. Tóm tắt những gì cần nhớ:

- **SQLite** thay Access — không cần cài thêm gì, 1 file, dễ backup
- **ITestStep** là interface cốt lõi — mỗi test step là 1 class riêng, dễ thêm cho model mới
- **SFIS flow** giữ nguyên logic cũ: Init → PreData → [test] → SendResult
- **Log** vẫn là file text tên `PASS/FAIL_MAC_...txt` nhưng parse trong C# thay vì đọc file tay
- **Web monitor** thêm sau — không block gì cả ở giai đoạn đầu

---

## Human (Mar 29, 2026, 11:32 AM):

Q: Bạn muốn bắt đầu coding phần nào trước?
A: Ping test + kết nối DUT

Q: SFIS ở chỗ bạn là DLL cũ hay gọi API web?
A: app sẽ giao tiếp với sfis với 1 cái terminal cái ter này sẽ giao tiếp với sfis. kiến trúc hiện tại là đang pair cặp com ảo. app test send lệnh com còn terminal sẽ giao tiếp với sfis và trả về xem địa chỉ mac hiện tại đang ở trạng thái nào, trạm test nào, bị hold do fail nhiều vân vân. nhưng bạn nên xem xét thiết kế kiến trúc để ví dụ sau này công ty nâng cấp lên api thì app của chúng ta không cần đập đi xây lại phần sfis.

---

## Claude:

Rất tốt! Kiến trúc virtual COM pair rất phổ biến trong môi trường nhà máy. Tôi sẽ thiết kế để sau này đổi sang HTTP API chỉ cần thêm 1 class mới, không đụng gì đến phần còn lại.

---

## Phần 1 — Kiến trúc SFIS linh hoạt (thiết kế trước, code sau)**Chìa khóa:** `ISfisProvider` là interface trung gian. Hiện tại inject `VirtualComSfisProvider`, sau này chỉ cần viết thêm `HttpApiSfisProvider` và swap — không đụng `TestEngine` hay bất kỳ test step nào.

---

## Phần 2 — Code thực tế, bắt đầu từ Ping

Tôi chia làm 3 file nhỏ để bạn hiểu rõ từng bước.

### File 1 — `ISfisProvider.cs` (interface, viết 1 lần dùng mãi)

```csharp
// Models/SfisResult.cs
public record SfisResult(
    bool   IsAllowed,    // MAC có được phép test không
    string Status,       // "OK", "HOLD", "FAIL_LOCK"
    string Station,      // Tên trạm đang được assign
    string Message       // Raw message từ terminal trả về
);

// Interfaces/ISfisProvider.cs
public interface ISfisProvider
{
    /// <summary>
    /// Gửi MAC lên SFIS, lấy trạng thái trước khi test.
    /// Tương đương SFISSentPreData() trong C++ cũ.
    /// </summary>
    Task<SfisResult> QueryMacAsync(string mac, string station);

    /// <summary>
    /// Báo kết quả PASS sau khi test xong.
    /// </summary>
    Task<bool> SendPassAsync(string mac, string station, string bom, string fw);

    /// <summary>
    /// Báo kết quả FAIL sau khi test xong.
    /// </summary>
    Task<bool> SendFailAsync(string mac, string station, string errorCode, string bom, string fw);
}
```

### File 2 — `VirtualComSfisProvider.cs` (implement hiện tại)

```csharp
// Sfis/VirtualComSfisProvider.cs
using System.IO.Ports;

public class VirtualComSfisProvider : ISfisProvider, IDisposable
{
    private readonly SerialPort _port;
    private readonly int _timeoutMs;

    public VirtualComSfisProvider(string comPort, int baudRate = 9600, int timeoutMs = 5000)
    {
        _timeoutMs = timeoutMs;
        _port = new SerialPort(comPort, baudRate)
        {
            ReadTimeout  = timeoutMs,
            WriteTimeout = timeoutMs
        };
        _port.Open();
    }

    public async Task<SfisResult> QueryMacAsync(string mac, string station)
    {
        // Gửi lệnh sang terminal — format tuỳ theo terminal của bạn
        // Ví dụ: "QUERY,AABBCCDDEEFF,L1-PT1-01\r\n"
        var command = $"QUERY,{mac},{station}\r\n";
        _port.Write(command);

        // Đọc response, chờ tối đa _timeoutMs
        var response = await ReadUntilAsync("\r\n");

        // Parse response từ terminal
        // Ví dụ terminal trả về: "OK,PASS,L1-PT1-01" hoặc "HOLD,FAIL_MANY,L1-PT1-01"
        return ParseResponse(response);
    }

    public async Task<bool> SendPassAsync(string mac, string station, string bom, string fw)
    {
        var command = $"PASS,{mac},{station},{bom},{fw}\r\n";
        _port.Write(command);
        var response = await ReadUntilAsync("\r\n");
        return response.Contains("SFIS_SUCCESS");
    }

    public async Task<bool> SendFailAsync(string mac, string station, string errorCode, string bom, string fw)
    {
        var command = $"FAIL,{mac},{station},{errorCode},{bom},{fw}\r\n";
        _port.Write(command);
        var response = await ReadUntilAsync("\r\n");
        return response.Contains("SFIS_SUCCESS");
    }

    // ── Private helpers ───────────────────────────────────────────────

    private SfisResult ParseResponse(string raw)
    {
        // Tuỳ format terminal bạn đang dùng — đây là ví dụ
        // raw = "OK,ALLOWED,L1-PT1-01" hoặc "HOLD,FAIL_LOCK,"
        var parts = raw.Trim().Split(',');
        bool allowed = parts.Length > 0 && parts[0] == "OK";
        string status  = parts.Length > 1 ? parts[1] : "UNKNOWN";
        string station = parts.Length > 2 ? parts[2] : "";
        return new SfisResult(allowed, status, station, raw);
    }

    private async Task<string> ReadUntilAsync(string terminator)
    {
        // Chạy blocking read trên thread pool để không block UI
        return await Task.Run(() =>
        {
            var sb = new System.Text.StringBuilder();
            var deadline = DateTime.Now.AddMilliseconds(_timeoutMs);
            while (DateTime.Now < deadline)
            {
                if (_port.BytesToRead > 0)
                {
                    sb.Append(_port.ReadExisting());
                    if (sb.ToString().Contains(terminator))
                        return sb.ToString();
                }
                Thread.Sleep(50);
            }
            throw new TimeoutException($"SFIS terminal không phản hồi trong {_timeoutMs}ms");
        });
    }

    public void Dispose() => _port?.Close();
}
```

### File 3 — `PingHelper.cs` + test thử ngay

```csharp
// Helpers/PingHelper.cs
using System.Net.NetworkInformation;

public class PingHelper
{
    /// <summary>
    /// Ping một IP, retry nhiều lần giống logic C++ cũ (loop đến timeout).
    /// </summary>
    public static async Task<bool> WaitForDutAsync(
        string ip,
        int totalTimeoutSec,
        int intervalMs    = 1000,
        Action<string>? onProgress = null)
    {
        var deadline = DateTime.Now.AddSeconds(totalTimeoutSec);
        int elapsed  = 0;

        while (DateTime.Now < deadline)
        {
            try
            {
                using var ping  = new Ping();
                var reply = await ping.SendPingAsync(ip, 800);
                if (reply.Status == IPStatus.Success)
                    return true;
            }
            catch { /* bỏ qua lỗi mạng tạm thời */ }

            elapsed += intervalMs;
            onProgress?.Invoke($"Chờ DUT kết nối... {elapsed / 1000}s / {totalTimeoutSec}s");
            await Task.Delay(intervalMs);
        }

        return false; // timeout
    }
}
```

### Test thử ngay — `Program.cs` (console app)

```csharp
// Bước 1: Ping thử DUT
Console.Write("Nhập IP DUT (VD: 192.168.1.1): ");
var ip = Console.ReadLine()!;

Console.WriteLine("Đang chờ DUT boot lên...");
bool connected = await PingHelper.WaitForDutAsync(
    ip,
    totalTimeoutSec: 60,
    onProgress: msg => Console.WriteLine($"  {msg}")
);

if (!connected)
{
    Console.WriteLine("❌ DUT không phản hồi sau 60 giây!");
    return;
}
Console.WriteLine("✅ DUT đã kết nối!");

// Bước 2: Hỏi SFIS (nếu đã có virtual COM)
Console.Write("Nhập MAC DUT (VD: AABBCCDDEEFF): ");
var mac = Console.ReadLine()!;

using var sfis = new VirtualComSfisProvider("COM3"); // đổi lại COM bạn đang dùng
try
{
    var result = await sfis.QueryMacAsync(mac, "L1-PT1-01");
    Console.WriteLine($"SFIS Status : {result.Status}");
    Console.WriteLine($"Station     : {result.Station}");
    Console.WriteLine($"Allowed     : {result.IsAllowed}");

    if (!result.IsAllowed)
    {
        Console.WriteLine($"⛔ MAC bị HOLD: {result.Message}");
        return;
    }
}
catch (TimeoutException ex)
{
    Console.WriteLine($"⚠️ {ex.Message}");
}
```

---

## Điểm quan trọng bạn cần hỏi team để điền vào code

Trước khi code tiếp, bạn cần xác nhận **3 thứ** từ người biết terminal SFIS:

```
1. Format lệnh gửi đi là gì?
   VD: "QUERY,{MAC},{STATION}\r\n"
       hay "CHECK {MAC}\n"
       hay "{MAC};{STATION};PRE\r\n"

2. Format response trả về là gì?
   VD: "OK,ALLOWED,L1-PT1-01\r\n"
       hay "SFIS_OK\r\n"
       hay JSON?

3. COM port ảo bên test app là COM mấy?
   (Cái pair với COM mà terminal đang lắng nghe)
```

Khi bạn có 3 thứ đó, chỉ cần sửa hàm `ParseResponse()` và format `command` trong `VirtualComSfisProvider` là xong — toàn bộ phần còn lại giữ nguyên.

Bước tiếp theo bạn muốn tôi hướng dẫn phần nào?Trong lúc chờ bạn chọn — nhớ format lệnh SFIS là thứ quan trọng nhất cần confirm với team sớm, vì nó ảnh hưởng trực tiếp đến `ParseResponse()`. Còn lại Ping và COM port bạn có thể test ngay bây giờ mà không cần chờ ai.

---

## Human (Mar 29, 2026, 11:34 AM):

Q: Bước tiếp theo bạn muốn tìm hiểu phần nào?
A: [No preference]

---

## Claude:

OK, tôi sẽ đi qua cả 3 phần theo thứ tự logic — mỗi phần đủ để bạn tự code tiếp, không dài dòng.

---

## Phần 1 — `TestContext` + `ITestStep`

`TestContext` thay thế `pContainer` trong C++ cũ. Nó là "túi" chứa mọi thứ một lần test cần dùng.

```csharp
// Core/TestContext.cs
public class TestContext
{
    // Thông tin DUT
    public string Mac       { get; set; } = "";
    public string Barcode   { get; set; } = "";
    public string Model     { get; set; } = "";
    public string Station   { get; set; } = "";
    public string IpAddress { get; set; } = "";

    // Kết quả
    public bool   IsPassed    { get; set; } = true;
    public string ErrorCode   { get; set; } = "";
    public string UserMessage { get; set; } = "";

    // Versions (

---

## Human (Mar 29, 2026, 11:35 AM):

bạn còn chưa đưa ra format cấu trúc project file code bạn ơi cần tạo file gì cần cài thư viện gì, add referce gì. tạo project loại gì (class , blank)

---

## Claude:

Đúng rồi, để tôi làm rõ từ đầu luôn — từ lúc mở Visual Studio đến khi có project chạy được.

---

## Bước 0 — Cấu trúc Solution tổng thể---

## Bước 1 — Tạo Solution và các Project

Mở **Visual Studio 2022**, làm theo thứ tự sau:

### 1.1 Tạo Solution trống
```
File → New → Project
→ Chọn: "Blank Solution"
→ Name: TestSystem
→ OK
```

### 1.2 Tạo 3 Class Library (nhấn chuột phải vào Solution)

```
Right-click Solution → Add → New Project
→ Chọn: "Class Library"   ← KHÔNG phải "Class Library (.NET Framework)"
→ Framework: .NET 8.0
```

Tạo lần lượt 3 cái tên:
- `TestSystem.Core`
- `TestSystem.Infrastructure`
- `TestSystem.Steps`

### 1.3 Tạo Windows Forms App

```
Right-click Solution → Add → New Project
→ Chọn: "Windows Forms App"   ← đúng tên này
→ Framework: .NET 8.0
→ Name: TestSystem.UI
```

### 1.4 Xoá file mặc định thừa

Mỗi project khi tạo ra sẽ có file `Class1.cs` — **xoá hết đi**, không dùng.

---

## Bước 2 — Thêm Reference giữa các project

Các project cần "biết nhau" theo sơ đồ này:

```
TestSystem.Core          ← không reference ai (nền tảng)
TestSystem.Infrastructure → reference TestSystem.Core
TestSystem.Steps          → reference TestSystem.Core + TestSystem.Infrastructure
TestSystem.UI             → reference tất cả 3 project trên
```

**Cách thêm** (ví dụ cho Infrastructure):
```
Right-click TestSystem.Infrastructure → Add → Project Reference
→ Tick chọn: TestSystem.Core
→ OK
```

Làm tương tự cho `Steps` và `UI`.

---

## Bước 3 — Cài NuGet packages

### TestSystem.Infrastructure cần:

```
Right-click TestSystem.Infrastructure → Manage NuGet Packages
→ Browse → tìm và Install:
```

| Package | Dùng cho |
|---|---|
| `Microsoft.Data.Sqlite` | SQLite — thay Access |
| `SSH.NET` | SSH vào DUT nếu cần |

### TestSystem.UI cần:

Không cần thêm gì — `System.IO.Ports` (COM port) và `System.Net.NetworkInformation` (Ping) đã có sẵn trong .NET 8.

---

## Bước 4 — Cấu hình `.csproj` (copy paste, không cần gõ tay)

### `TestSystem.Core.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

### `TestSystem.Infrastructure.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TestSystem.Core\TestSystem.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
    <PackageReference Include="SSH.NET" Version="2024.2.0" />
  </ItemGroup>
</Project>
```

### `TestSystem.Steps.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TestSystem.Core\TestSystem.Core.csproj" />
    <ProjectReference Include="..\TestSystem.Infrastructure\TestSystem.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

### `TestSystem.UI.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <OutputType>WinExe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TestSystem.Core\TestSystem.Core.csproj" />
    <ProjectReference Include="..\TestSystem.Infrastructure\TestSystem.Infrastructure.csproj" />
    <ProjectReference Include="..\TestSystem.Steps\TestSystem.Steps.csproj" />
  </ItemGroup>
</Project>
```

---

## Bước 5 — Tạo file đầu tiên để test build

Tạo file này trong `TestSystem.Core/Models/` để kiểm tra solution build OK:

```csharp
// TestSystem.Core/Models/StepResult.cs
namespace TestSystem.Core.Models;

public class StepResult
{
    public bool   Passed    { get; init; }
    public string ErrorCode { get; init; } = "";
    public string Message   { get; init; } = "";

    public static StepResult Ok(string message = "")
        => new() { Passed = true, Message = message };

    public static StepResult Fail(string errorCode, string message = "")
        => new() { Passed = false, ErrorCode = errorCode, Message = message };
}
```

Sau đó nhấn **Build → Build Solution** (`Ctrl+Shift+B`). Nếu ra `0 errors` là chuẩn, bắt đầu code tiếp được.

---

## Tóm tắt thứ tự làm

```
1. Tạo Solution trống
2. Tạo 4 project (3 Class Library + 1 WinForms)
3. Add Project Reference đúng chiều
4. Cài NuGet: Microsoft.Data.Sqlite, SSH.NET (chỉ Infrastructure)
5. Copy 4 file .csproj vào thay thế
6. Tạo StepResult.cs → Build thử → 0 errors ✅
7. Bắt đầu code từng file theo thứ tự: Core → Infrastructure → Steps → UI
```

Bạn làm đến bước nào thì báo tôi, tôi sẽ đưa code cho bước tiếp theo. Build xong `0 errors` rồi mình tiếp tục với `TestContext` và `ITestStep` nhé.

---

## Human (Mar 29, 2026, 4:29 PM):

với kiến trúc project như của  bạn nêu thì luồng làm việc như nào đấy tận 4 project nên tôi chưa biết luồn hoạt động như nào. chạy từ đâu sang đâu.

---

## Claude:

Câu hỏi hay — 4 project trông phức tạp nhưng thực ra chỉ có **1 project chạy** (TestSystem.UI), 3 cái còn lại là thư viện nó gọi vào. Để hiểu rõ hơn, tôi làm stepper click-through cho bạn xem từng bước luồng hoạt động thực tế.Click qua từng bước để xem luồng chi tiết. Điểm mấu chốt cần nhớ:

**Chỉ có `TestSystem.UI` chạy thật** — 3 project kia build ra file `.dll` nằm cùng thư mục với `.exe`. Khi bạn nhấn F5 trong Visual Studio, nó tự build tất cả rồi chỉ khởi động UI lên.

Quy tắc phụ thuộc theo một chiều duy nhất:

```
UI  →  Steps  →  Infra  →  (không gọi ai)
 \       \          ↑
  \       → Core ───┘
   → Core
```

`Core` là project duy nhất không phụ thuộc ai — nó là ngôn ngữ chung mà tất cả đều hiểu. Khi bạn thêm model mới, chỉ cần thêm file vào `Steps` là xong, không đụng UI hay Infra.

Bạn muốn bắt đầu code file đầu tiên nào — `TestContext.cs` trong Core hay `MainForm` trong UI?

---

## Human (Mar 29, 2026, 4:40 PM):

thế giờ tôi lên công ty có bản mạch thì tôi viết code thứ tự file như nào từ file nào đến file nào ví dụ chức năng gửi sfis đi thì viết thứ tự như nào là hoàn chỉnh sau đó chạy và test được và đc coi là xong để tôi code chức năng tiếp

---

## Claude:

Câu hỏi rất thực tế. Tôi sẽ đưa ra quy trình chuẩn cho **mỗi tính năng** — theo đúng thứ tự từ nền lên UI.Click vào từng tab để xem thứ tự code cho từng tính năng.

---

Quy trình chuẩn luôn là **5 bước theo chiều dọc**:

```
1. Core     → viết interface + model (không có logic)
2. Infra    → viết code thật (COM, Ping, file, DB)
3. Steps    → dùng Infra, trả về StepResult
4. UI       → ghép Step vào Engine, build chạy
5. Test thật → 2 trường hợp: happy path và fail path
```

Một tính năng **coi là xong** khi cả 2 trường hợp test đều đúng hành vi — không chỉ khi build không lỗi.

**Thứ tự làm tính năng** khi lên công ty, tôi khuyên làm theo thứ tự này:

```
1. Config (SQLite)    ← vì mọi thứ khác cần đọc config
2. Ping               ← đơn giản nhất, test được ngay
3. COM port           ← cần bản mạch thật
4. Log PASS/FAIL      ← cần sau khi có kết quả test
5. SFIS               ← cần confirm format lệnh với team trước
```

---

## Human (Mar 30, 2026, 9:51 PM):

tôi đã tạo 4 project như bạn bảo . đã copy testresult  của bạn đã build 0 lỗi giờ làm gì nữa bạn làm mẫu 1 cái từ a-z đi sau tôi tự làm cái khác. nhớ hướng dẫn chi tiết nhé. cái dễ nhất.

---

## Claude:

Làm **Ping** từ A-Z — vì không cần bản mạch, test được ngay với bất kỳ IP nào. Làm xong cái này bạn hiểu luồng, các tính năng sau tự làm được.

---

## Bước 1 — `TestContext.cs` (Core/Models/)

Chuột phải vào folder `Models` trong project `TestSystem.Core` → Add → New Item → Class → đặt tên `TestContext.cs`

```csharp
namespace TestSystem.Core.Models;

public class TestContext
{
    public string Mac         { get; set; } = "";
    public string IpAddress   { get; set; } = "";
    public string Station     { get; set; } = "";
    public string Model       { get; set; } = "";

    public bool   IsPassed    { get; set; } = true;
    public string ErrorCode   { get; set; } = "";
    public string UserMessage { get; set; } = "";

    // Log từng bước để ghi ra file sau này
    public List<string> StepLogs { get; } = new();

    public void AddLog(string message)
    {
        StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
```

**Build** (`Ctrl+Shift+B`) → phải ra **0 errors** mới đi tiếp.

---

## Bước 2 — `ITestStep.cs` (Core/Interfaces/)

Chuột phải folder `Interfaces` → Add → New Item → Interface → `ITestStep.cs`

```csharp
using TestSystem.Core.Models;

namespace TestSystem.Core.Interfaces;

public interface ITestStep
{
    string StepName { get; }

    Task<StepResult> ExecuteAsync(TestContext context);
}
```

**Build** → 0 errors.

---

## Bước 3 — `TestEngine.cs` (Core/Engine/)

Chuột phải folder `Engine` → Add → New Item → Class → `TestEngine.cs`

```csharp
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
        context.IsPassed  = true;
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
                context.IsPassed  = false;
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
```

**Build** → 0 errors.

---

## Bước 4 — `PingHelper.cs` (Infrastructure/Communication/)

Chuột phải folder `Communication` trong `TestSystem.Infrastructure` → Add → Class → `PingHelper.cs`

```csharp
using System.Net.NetworkInformation;

namespace TestSystem.Infrastructure.Communication;

public class PingHelper
{
    /// <summary>
    /// Chờ DUT boot lên và phản hồi ping.
    /// Tương đương vòng loop ping trong ConnectDUT() của C++ cũ.
    /// </summary>
    public static async Task<bool> WaitForDutAsync(
        string ip,
        int    totalTimeoutSec,
        int    intervalMs          = 1000,
        Action<string>? onProgress = null)
    {
        var deadline = DateTime.Now.AddSeconds(totalTimeoutSec);
        int elapsed  = 0;

        while (DateTime.Now < deadline)
        {
            try
            {
                using var ping  = new Ping();
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
```

**Build** → 0 errors.

---

## Bước 5 — `PingStep.cs` (Steps/)

Chuột phải project `TestSystem.Steps` → Add → Class → `PingStep.cs`

```csharp
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
            ip:             context.IpAddress,
            totalTimeoutSec: _timeoutSec,
            onProgress:     msg => context.AddLog(msg)
        );

        if (!ok)
            return StepResult.Fail("PING", $"DUT không phản hồi sau {_timeoutSec}s");

        return StepResult.Ok("DUT đã kết nối");
    }
}
```

**Build** → 0 errors.

---

## Bước 6 — Sửa `MainForm.cs` (UI/)

Mở `MainForm.cs` → click vào form → nhìn sang **Toolbox** bên trái, kéo vào:

- 1 cái **TextBox** → đặt tên `txtIpAddress` (Properties → Name)
- 1 cái **Button** → đặt tên `btnStart`, Text = "Start Test"
- 1 cái **Label** → đặt tên `lblStatus`, Text = "Sẵn sàng"
- 1 cái **ListBox** → đặt tên `lstLog`

Sau đó double-click vào nút `btnStart` để tạo event, rồi thay toàn bộ file `MainForm.cs` bằng code này:

```csharp
using TestSystem.Core.Engine;
using TestSystem.Core.Models;
using TestSystem.Steps;

namespace TestSystem.UI;

public partial class MainForm : Form
{
    private TestEngine _engine;

    public MainForm()
    {
        InitializeComponent();
        SetupEngine();
    }

    private void SetupEngine()
    {
        _engine = new TestEngine(new List<Core.Interfaces.ITestStep>
        {
            new PingStep(timeoutSec: 30)
            // Sau này thêm step khác vào đây:
            // new ConnectDutStep(),
            // new SfisPreDataStep(),
            // new SfisSendResultStep(),
        });

        // Engine báo status → hiển thị lên UI
        _engine.OnStatusChanged += msg =>
        {
            // Phải chạy trên UI thread
            if (InvokeRequired)
                Invoke(() => UpdateStatus(msg));
            else
                UpdateStatus(msg);
        };
    }

    private void UpdateStatus(string msg)
    {
        lblStatus.Text = msg;
        lstLog.Items.Add(msg);
        lstLog.TopIndex = lstLog.Items.Count - 1; // auto scroll
    }

    private async void btnStart_Click(object sender, EventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(txtIpAddress.Text))
        {
            MessageBox.Show("Nhập IP DUT trước!", "Thiếu thông tin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Chuẩn bị
        btnStart.Enabled = false;
        lstLog.Items.Clear();
        lblStatus.Text = "Đang chạy...";
        lblStatus.ForeColor = Color.Blue;

        // Tạo context cho lần test này
        var context = new TestContext
        {
            IpAddress = txtIpAddress.Text.Trim(),
            Station   = "TEST-PC-01",
            Model     = "TEST-MODEL",
            Mac       = "AABBCCDDEEFF"   // sau này lấy từ barcode scanner
        };

        // Chạy engine
        var result = await _engine.RunAsync(context);

        // Hiển thị kết quả
        if (result.IsPassed)
        {
            lblStatus.Text      = "PASS";
            lblStatus.ForeColor = Color.Green;
        }
        else
        {
            lblStatus.Text      = $"FAIL [{result.ErrorCode}]";
            lblStatus.ForeColor = Color.Red;
        }

        // Đổ log ra listbox
        foreach (var log in result.StepLogs)
            lstLog.Items.Add(log);

        btnStart.Enabled = true;
    }
}
```

**Build** → 0 errors.

---

## Bước 7 — Chạy và test

Nhấn **F5** (hoặc nút Run màu xanh).

**Test case 1 — IP có thật (ping được):**
```
Nhập: 8.8.8.8   ← Google DNS, luôn ping được
Bấm: Start Test
Kỳ vọng: lblStatus hiển thị "PASS" màu xanh
```

**Test case 2 — IP không tồn tại (ping fail):**
```
Nhập: 192.168.99.99   ← IP không ai dùng
Bấm: Start Test
Kỳ vọng: chờ ~30s rồi hiển thị "FAIL [PING]" màu đỏ
```

---

## Khi cả 2 test case đúng → tính năng Ping XONG ✅

Lúc đó bạn sẽ thấy ListBox hiện log như này:

```
[09:15:01] --- Ping DUT bắt đầu ---
[09:15:01] Chờ DUT... 1s / 30s
[09:15:02] Chờ DUT... 2s / 30s
...
[09:15:03] --- Ping DUT: PASS DUT đã kết nối ---
```

---

Tính năng tiếp theo bạn muốn làm là gì — **Config SQLite** hay **COM port**?

---

## Human (Mar 31, 2026, 8:35 PM):

oke làm chi tiết từng bước sqlite đi.

---

## Claude:

## SQLite Config từ A-Z

Tính năng này làm **trước khi làm COM/SFIS** vì các step sau đều cần đọc config (COM port nào, IP mặc định, timeout bao lâu...).

---

## Bước 1 — Cài NuGet package

Chuột phải project `TestSystem.Infrastructure` → **Manage NuGet Packages**

→ Tab **Browse** → tìm `Microsoft.Data.Sqlite` → chọn cái đầu tiên → **Install**

Chờ install xong, nhấn OK nếu có hỏi.

**Build** (`Ctrl+Shift+B`) → 0 errors.

---

## Bước 2 — `IConfigService.cs` (Core/Interfaces/)

Chuột phải folder `Interfaces` trong `TestSystem.Core` → Add → New Item → **Interface** → đặt tên `IConfigService.cs`

```csharp
namespace TestSystem.Core.Interfaces;

public interface IConfigService
{
    // Đọc chuỗi — tương đương getGlobalVeriableString() trong C++ cũ
    string GetString(string section, string key, string defaultValue = "");

    // Đọc số nguyên — tương đương getGlobalVeriableInt() trong C++ cũ
    int GetInt(string section, string key, int defaultValue = 0);

    // Ghi giá trị (dùng khi save config từ UI)
    void SetValue(string section, string key, string value);
}
```

**Build** → 0 errors.

---

## Bước 3 — `SqliteConfigService.cs` (Infrastructure/Config/)

Chuột phải folder `Config` trong `TestSystem.Infrastructure` → Add → Class → `SqliteConfigService.cs`

```csharp
using Microsoft.Data.Sqlite;
using TestSystem.Core.Interfaces;

namespace TestSystem.Infrastructure.Config;

public class SqliteConfigService : IConfigService
{
    private readonly string _dbPath;

    public SqliteConfigService(string dbPath)
    {
        _dbPath = dbPath;

        // Tự tạo file .db và bảng nếu chưa có
        InitDatabase();
    }

    // ── Đọc chuỗi ────────────────────────────────────────────────────
    public string GetString(string section, string key, string defaultValue = "")
    {
        using var conn = OpenConnection();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Value FROM Config
            WHERE Section = $section AND Key = $key
            LIMIT 1";
        cmd.Parameters.AddWithValue("$section", section);
        cmd.Parameters.AddWithValue("$key", key);

        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    // ── Đọc số nguyên ────────────────────────────────────────────────
    public int GetInt(string section, string key, int defaultValue = 0)
    {
        var raw = GetString(section, key);
        return int.TryParse(raw, out int val) ? val : defaultValue;
    }

    // ── Ghi giá trị ──────────────────────────────────────────────────
    public void SetValue(string section, string key, string value)
    {
        using var conn = OpenConnection();

        var cmd = conn.CreateCommand();

        // INSERT nếu chưa có, UPDATE nếu đã có
        cmd.CommandText = @"
            INSERT INTO Config (Section, Key, Value)
            VALUES ($section, $key, $value)
            ON CONFLICT(Section, Key)
            DO UPDATE SET Value = $value";
        cmd.Parameters.AddWithValue("$section", section);
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);

        cmd.ExecuteNonQuery();
    }

    // ── Private helpers ───────────────────────────────────────────────
    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private void InitDatabase()
    {
        using var conn = OpenConnection();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Config (
                Section TEXT NOT NULL,
                Key     TEXT NOT NULL,
                Value   TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (Section, Key)
            )";
        cmd.ExecuteNonQuery();
    }
}
```

**Build** → 0 errors.

---

## Bước 4 — Thêm data mẫu vào DB lúc khởi động

Mở `Program.cs` trong `TestSystem.UI`, thay toàn bộ bằng:

```csharp
using TestSystem.Infrastructure.Config;

namespace TestSystem.UI;

internal static class Program
{
    // ConfigService dùng chung toàn app
    public static SqliteConfigService Config { get; private set; } = null!;

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // File config.db nằm cùng thư mục với file .exe
        string dbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.db"
        );

        Config = new SqliteConfigService(dbPath);

        // Seed data mặc định — chỉ insert nếu chưa có
        SeedDefaultConfig(Config);

        Application.Run(new MainForm());
    }

    private static void SeedDefaultConfig(SqliteConfigService cfg)
    {
        // Chỉ seed nếu key chưa tồn tại
        void SeedIfEmpty(string section, string key, string value)
        {
            if (string.IsNullOrEmpty(cfg.GetString(section, key)))
                cfg.SetValue(section, key, value);
        }

        // GlobalVariable — giống section trong Access cũ
        SeedIfEmpty("GlobalVariable", "DUT_IP",           "192.168.1.1");
        SeedIfEmpty("GlobalVariable", "DUT_POWERON_TIME",  "60");
        SeedIfEmpty("GlobalVariable", "Station",           "L1-PT1-01");
        SeedIfEmpty("GlobalVariable", "Model",             "TEST-MODEL");
        SeedIfEmpty("GlobalVariable", "LogFolder",         @"D:\TestLogs");

        // COM port config
        SeedIfEmpty("COM", "DUT_COM",   "COM3");
        SeedIfEmpty("COM", "SFIS_COM",  "COM4");
        SeedIfEmpty("COM", "BaudRate",  "115200");

        // SFIS config
        SeedIfEmpty("SFIS", "Enabled",  "1");
        SeedIfEmpty("SFIS", "Station",  "L1-PT1-01");
    }
}
```

**Build** → 0 errors.

---

## Bước 5 — Dùng Config trong `MainForm.cs`

Mở `MainForm.cs`, sửa hàm `SetupEngine` và constructor để đọc IP từ config thay vì gõ tay:

```csharp
using TestSystem.Core.Engine;
using TestSystem.Core.Models;
using TestSystem.Steps;

namespace TestSystem.UI;

public partial class MainForm : Form
{
    private TestEngine _engine = null!;

    public MainForm()
    {
        InitializeComponent();

        // Đọc giá trị mặc định từ config.db vào ô IP
        txtIpAddress.Text = Program.Config.GetString("GlobalVariable", "DUT_IP");

        SetupEngine();
    }

    private void SetupEngine()
    {
        // Đọc timeout từ config thay vì hardcode
        int pingTimeout = Program.Config.GetInt("GlobalVariable", "DUT_POWERON_TIME", 60);

        _engine = new TestEngine(new List<Core.Interfaces.ITestStep>
        {
            new PingStep(timeoutSec: pingTimeout)
        });

        _engine.OnStatusChanged += msg =>
        {
            if (InvokeRequired)
                Invoke(() => UpdateStatus(msg));
            else
                UpdateStatus(msg);
        };
    }

    private void UpdateStatus(string msg)
    {
        lblStatus.Text = msg;
        lstLog.Items.Add(msg);
        lstLog.TopIndex = lstLog.Items.Count - 1;
    }

    private async void btnStart_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtIpAddress.Text))
        {
            MessageBox.Show("Nhập IP DUT trước!", "Thiếu thông tin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnStart.Enabled    = false;
        lstLog.Items.Clear();
        lblStatus.Text      = "Đang chạy...";
        lblStatus.ForeColor = Color.Blue;

        var context = new TestContext
        {
            IpAddress = txtIpAddress.Text.Trim(),
            Station   = Program.Config.GetString("GlobalVariable", "Station"),
            Model     = Program.Config.GetString("GlobalVariable", "Model"),
            Mac       = "AABBCCDDEEFF"
        };

        var result = await _engine.RunAsync(context);

        if (result.IsPassed)
        {
            lblStatus.Text      = "PASS";
            lblStatus.ForeColor = Color.Green;
        }
        else
        {
            lblStatus.Text      = $"FAIL [{result.ErrorCode}]";
            lblStatus.ForeColor = Color.Red;
        }

        foreach (var log in result.StepLogs)
            lstLog.Items.Add(log);

        btnStart.Enabled = true;
    }
}
```

**Build** → 0 errors.

---

## Bước 6 — Chạy và kiểm tra

Nhấn **F5**.

**Test case 1 — Đọc config tự động:**
```
Kỳ vọng: ô txtIpAddress tự điền "192.168.1.1" khi form mở lên
→ Nếu đúng: config đọc được từ DB ✅
```

**Test case 2 — Xem file DB được tạo:**
```
Mở Windows Explorer, vào:
TestSystem\TestSystem.UI\bin\Debug\net8.0-windows\

Phải thấy file: config.db
→ Nếu có file đó: DB được tạo thành công ✅
```

**Test case 3 — Sửa config không cần build lại:**

Tải **DB Browser for SQLite** (free, tải tại sqlitebrowser.org) → mở file `config.db` → tab **Browse Data** → chọn table **Config** → sửa giá trị `DUT_IP` thành `8.8.8.8` → nhấn **Write Changes**.

```
Đóng app → mở lại (F5)
Kỳ vọng: ô IP tự điền "8.8.8.8"
→ Nếu đúng: config thay được không cần build lại ✅
```

---

## Khi cả 3 test case đúng → SQLite Config XONG ✅

Cấu trúc bảng trong DB trông như này:

| Section | Key | Value |
|---|---|---|
| GlobalVariable | DUT_IP | 192.168.1.1 |
| GlobalVariable | DUT_POWERON_TIME | 60 |
| COM | DUT_COM | COM3 |
| SFIS | Enabled | 1 |

Về sau mỗi khi cần thêm config mới, chỉ cần thêm 1 dòng `SeedIfEmpty(...)` trong `Program.cs` là xong — không cần đụng DB tay.

---

Tiếp theo làm **COM port** hay **ghi Log** bạn?

---

## Human (Apr 3, 2026, 1:53 AM):

thiết kế chức năng send lệnh và phân tích kết quả trả về qua ssh net. toàn bộ command và output phải đc ghi ra 1 file log.  còn phải viết funtion để tái sử dụng để bắt keyword trong đoạn dữ liệu trả về sau mỗi lệnh hơn nữa phải có expect key để biết lệnh đó đã end hay là chưa. trong đoạn log ví dụ . 
USW-Pro-24-PoE-US.6.4.19# lcm-ctrl -t dump
{
    "adopted": true,
    "backlight": true,
    "brightness": 80,
    "burnin_touch_count": 0,
    "color_borders": "0x0139FF",
    "dfu": 10473,
    "fw_log_level": 0,
    "hash": "ccddf8a18daebccfe452c089e6a5f6ed",
    "hostname": "USW-Pro-24-PoE",
    "id": 301,
    "idle": 199,
    "inform_url": "http://unifi:8080/inform",
    "ip": "192.168.1.20",
    "last_control_event": "",
    "main_dev": "eth0",
    "night_mode": false,
    "orientation": 0,
    "orientation_saved": 0,
    "poe_plus": "1-16",
    "poe_plus_plus": "17-24",
    "poe_power_max": 400000,
    "poe_power_used": 19200,
    "rc": "ok",
    "screen": "menu.main",
    "screen_changed_times": 1,
    "screen_sync": true,
    "sfp_idx": "25-26",
    "state": "ready",
    "test_touch_count": 0,
    "upgrade_fail": 0,
    "upgrade_good": 0,
    "uplink_port": 0,
    "uplink_rx_bytes": 0,
    "uplink_rx_r_bytes": 0,
    "uplink_tx_bytes": 0,
    "uplink_tx_r_bytes": 0,
    "uptime": 247,
    "version": "v5.1.0-0-g68e60e5"
}
USW-Pro-24-PoE-US.6.4.19# telnet localhost
Entering character mode
Escape character is '^]'.
Warning!
The changes may break controller settings and only be effective until reboot.
(UBNT) >en
(UBNT) #show version
Switch: 1
System Description............................. USW-Pro-24-PoE, 6.4.19.14340, Linux 3.6.5
Machine Type................................... USW-Pro-24-PoE
Machine Model.................................. UBNT-US24PRO-POE
Serial Number.................................. 58d61f96c650
Burned In MAC Address.......................... 58:D6:1F:96:C6:50
Software Version............................... 6.4.19.14340
(UBNT) #Log End
thì sau khi nhập lcm-ctrl -t dump thì sẽ hiện USW-Pro-24-PoE-US.6.4.19# chứng tỏ đã end lệnh (ko bị treo) hoặc lệnh telnet localhost 
thì end là (UBNT) . phải tạo ra log như thể là đang tự thao tác như trên terminal thật mọi thứ đều phải ghi ra log. và hơn nữa nếu kết quả trả về fail phải dừng toàn bộ quá trình test và in ra fail trên giao diện sau đó send kết quả fail qua sfis (qua com pair com ảo)

---

## Claude:

## Thiết kế SSH Command + Log — A-Z

Tôi chia làm 3 phần rõ ràng trước khi code.---

## Bước 1 — Cài SSH.NET

Chuột phải `TestSystem.Infrastructure` → Manage NuGet Packages → Browse → tìm `SSH.NET` → Install.

---

## Bước 2 — `CommandResult.cs` (Core/Models/)

```csharp
namespace TestSystem.Core.Models;

public class CommandResult
{
    // Toàn bộ output thô từ DUT
    public string RawOutput     { get; init; } = "";

    // Step tự đánh giá pass/fail sau khi parse
    public bool   Passed        { get; init; } = true;

    // Giá trị extract được (VD: "ready", "6.4.19")
    public string MatchedValue  { get; init; } = "";

    // Lý do fail nếu có
    public string ErrorReason   { get; init; } = "";

    public static CommandResult Ok(string raw, string matched = "")
        => new() { RawOutput = raw, Passed = true, MatchedValue = matched };

    public static CommandResult Fail(string raw, string reason)
        => new() { RawOutput = raw, Passed = false, ErrorReason = reason };
}
```

---

## Bước 3 — `SshLogger.cs` (Infrastructure/Logging/)

File này lo việc ghi log — mọi command gửi đi và mọi output nhận về đều đi qua đây.

```csharp
namespace TestSystem.Infrastructure.Logging;

public class SshLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly string       _filePath;

    public string FilePath => _filePath;

    public SshLogger(string logFolder, string mac, string model, string station)
    {
        Directory.CreateDirectory(logFolder);

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string fileName  = $"{model}_{mac}_{station}_{timestamp}.log";
        _filePath        = Path.Combine(logFolder, fileName);

        _writer = new StreamWriter(_filePath, append: false, System.Text.Encoding.UTF8)
        {
            AutoFlush = true   // ghi ngay, không buffer
        };

        WriteRaw($"=== LOG START | {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        WriteRaw($"=== Model: {model} | MAC: {mac} | Station: {station} ===");
        WriteRaw("");
    }

    // Ghi lệnh gửi đi — hiện dấu >>
    public void LogCommand(string command)
        => WriteRaw($"[{Now()}] >> {command}");

    // Ghi từng dòng output nhận về
    public void LogOutput(string line)
        => WriteRaw($"[{Now()}]    {line}");

    // Ghi expect key tìm thấy
    public void LogExpectFound(string expectKey)
        => WriteRaw($"[{Now()}]    ← expect '{expectKey}' found — command end");

    // Ghi kết quả bước test
    public void LogStepResult(string stepName, bool passed, string detail = "")
    {
        WriteRaw("");
        WriteRaw($"[{Now()}] {'='*40}");
        WriteRaw($"[{Now()}] STEP: {stepName} → {(passed ? "PASS" : "FAIL")} {detail}");
        WriteRaw($"[{Now()}] {'='*40}");
        WriteRaw("");
    }

    // Ghi lỗi
    public void LogError(string message)
        => WriteRaw($"[{Now()}] [ERROR] {message}");

    // Ghi kết quả cuối
    public void LogFinal(bool passed, string errorCode = "")
    {
        WriteRaw("");
        WriteRaw($"=== TEST RESULT: {(passed ? "PASS" : $"FAIL [{errorCode}]")} ===");
        WriteRaw($"=== LOG END | {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    // ── private ───────────────────────────────────────────────────────
    private void WriteRaw(string line) => _writer.WriteLine(line);
    private string Now() => DateTime.Now.ToString("HH:mm:ss.fff");

    public void Dispose()
    {
        _writer.Flush();
        _writer.Dispose();
    }
}
```

---

## Bước 4 — `OutputParser.cs` (Infrastructure/Logging/)

Hàm tái sử dụng để bắt keyword — dùng cho mọi lệnh SSH.

```csharp
using System.Text.RegularExpressions;

namespace TestSystem.Infrastructure.Logging;

public static class OutputParser
{
    // Kiểm tra output có chứa keyword không (case-insensitive)
    public static bool ContainsKeyword(string output, string keyword)
        => output.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    // Lấy giá trị của 1 key trong JSON-like output
    // VD: ExtractJsonValue(output, "state") → "ready"
    public static string ExtractJsonValue(string output, string key)
    {
        // Match: "key": "value" hoặc "key": 123
        var pattern = $@"""{Regex.Escape(key)}""\s*:\s*""?([^"",\r\n}}]+)""?";
        var match   = Regex.Match(output, pattern);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    // Lấy giá trị từ output dạng "Label........ Value"
    // VD: ExtractLabelValue(output, "Software Version") → "6.4.19.14340"
    public static string ExtractLabelValue(string output, string label)
    {
        var pattern = $@"{Regex.Escape(label)}[.\s]+(.+)";
        var match   = Regex.Match(output, pattern);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    // Kiểm tra output có chứa BẤT KỲ keyword nào trong list
    public static bool ContainsAny(string output, params string[] keywords)
        => keywords.Any(k => ContainsKeyword(output, k));

    // Kiểm tra output có chứa TẤT CẢ keyword trong list
    public static bool ContainsAll(string output, params string[] keywords)
        => keywords.All(k => ContainsKeyword(output, k));
}
```

---

## Bước 5 — `SshSession.cs` (Infrastructure/Communication/)

Đây là class quan trọng nhất — xử lý toàn bộ việc gửi lệnh và đọc output đến khi gặp expect key.

```csharp
using Renci.SshNet;
using TestSystem.Core.Models;
using TestSystem.Infrastructure.Logging;

namespace TestSystem.Infrastructure.Communication;

public class SshSession : IDisposable
{
    private SshClient?        _client;
    private ShellStream?      _shell;
    private readonly SshLogger _logger;

    // Timeout mặc định chờ expect key
    private readonly int _defaultTimeoutSec;

    public SshSession(SshLogger logger, int defaultTimeoutSec = 30)
    {
        _logger            = logger;
        _defaultTimeoutSec = defaultTimeoutSec;
    }

    // ── Kết nối SSH ───────────────────────────────────────────────────
    public void Connect(string host, string username, string password, int port = 22)
    {
        _logger.LogCommand($"[SSH CONNECT] {username}@{host}:{port}");

        _client = new SshClient(host, port, username, password);
        _client.Connect();

        // Mở shell stream — giống terminal thật
        _shell = _client.CreateShellStream(
            terminalName:   "xterm",
            columns:        200,
            rows:           50,
            width:          800,
            height:         600,
            bufferSize:     65536
        );

        _logger.LogOutput("[SSH CONNECTED]");
    }

    // ── Gửi lệnh và đọc đến khi gặp expect key ───────────────────────
    /// <param name="command">Lệnh gửi đi, VD: "lcm-ctrl -t dump"</param>
    /// <param name="expectKey">Chuỗi báo hiệu lệnh đã kết thúc, VD: "# " hoặc "(UBNT) #"</param>
    /// <param name="timeoutSec">Timeout, mặc định dùng giá trị của session</param>
    public async Task<CommandResult> SendCommandAsync(
        string command,
        string expectKey,
        int    timeoutSec = 0)
    {
        if (_shell == null)
            throw new InvalidOperationException("Chưa Connect SSH");

        if (timeoutSec <= 0) timeoutSec = _defaultTimeoutSec;

        // Ghi lệnh ra log
        _logger.LogCommand(command);

        // Gửi lệnh
        _shell.WriteLine(command);

        // Đọc output đến khi gặp expectKey hoặc timeout
        var output   = new System.Text.StringBuilder();
        var deadline = DateTime.Now.AddSeconds(timeoutSec);

        while (DateTime.Now < deadline)
        {
            await Task.Delay(100);

            if (_shell.DataAvailable)
            {
                string chunk = _shell.Read();

                foreach (var line in chunk.Split('\n'))
                {
                    var trimmed = line.TrimEnd('\r');
                    if (!string.IsNullOrEmpty(trimmed))
                        _logger.LogOutput(trimmed);
                }

                output.Append(chunk);

                // Kiểm tra expect key
                if (output.ToString().Contains(expectKey))
                {
                    _logger.LogExpectFound(expectKey);
                    return CommandResult.Ok(output.ToString());
                }
            }
        }

        // Timeout — lệnh bị treo
        string timeoutMsg = $"Timeout {timeoutSec}s chờ expect '{expectKey}'";
        _logger.LogError(timeoutMsg);
        return CommandResult.Fail(output.ToString(), timeoutMsg);
    }

    // ── Gửi lệnh không cần đọc output (VD: lệnh enter, Ctrl+]) ───────
    public void SendRaw(string raw)
    {
        _shell?.Write(raw);
        _logger.LogCommand($"[RAW] {raw.Replace("\n","\\n").Replace("\r","\\r")}");
    }

    // ── Chờ expect key không gửi lệnh (sau khi connect, chờ prompt) ──
    public async Task<string> WaitForPromptAsync(string expectKey, int timeoutSec = 0)
    {
        if (timeoutSec <= 0) timeoutSec = _defaultTimeoutSec;

        var output   = new System.Text.StringBuilder();
        var deadline = DateTime.Now.AddSeconds(timeoutSec);

        _logger.LogOutput($"[WAITING FOR PROMPT '{expectKey}']");

        while (DateTime.Now < deadline)
        {
            await Task.Delay(100);

            if (_shell?.DataAvailable == true)
            {
                string chunk = _shell.Read();
                output.Append(chunk);

                foreach (var line in chunk.Split('\n'))
                {
                    var trimmed = line.TrimEnd('\r');
                    if (!string.IsNullOrEmpty(trimmed))
                        _logger.LogOutput(trimmed);
                }

                if (output.ToString().Contains(expectKey))
                {
                    _logger.LogExpectFound(expectKey);
                    return output.ToString();
                }
            }
        }

        throw new TimeoutException($"Không thấy prompt '{expectKey}' sau {timeoutSec}s");
    }

    public void Dispose()
    {
        _shell?.Dispose();
        _client?.Disconnect();
        _client?.Dispose();
    }
}
```

---

## Bước 6 — `SshCommandStep.cs` (Steps/)

Đây là ví dụ thực tế với đúng log bạn mô tả — chạy `lcm-ctrl -t dump` rồi vào `telnet localhost`.

```csharp
using TestSystem.Core.Interfaces;
using TestSystem.Core.Models;
using TestSystem.Infrastructure.Communication;
using TestSystem.Infrastructure.Logging;

namespace TestSystem.Steps;

public class SshCommandStep : ITestStep
{
    public string StepName => "SSH Command Test";

    // Prompt chính của DUT — expect key sau mỗi lệnh thường
    private const string MAIN_PROMPT   = "# ";

    // Prompt sau khi vào telnet
    private const string TELNET_PROMPT = "(UBNT) #";

    public async Task<StepResult> ExecuteAsync(TestContext context)
    {
        // Lấy config từ context (đã được inject từ Program.cs)
        string logFolder = context.GetConfig("GlobalVariable", "LogFolder");
        string sshUser   = context.GetConfig("COM",            "SSH_User");
        string sshPass   = context.GetConfig("COM",            "SSH_Pass");

        using var logger = new SshLogger(
            logFolder: logFolder,
            mac:       context.Mac,
            model:     context.Model,
            station:   context.Station
        );

        // Lưu đường dẫn file log vào context để ghi vào SFIS sau
        context.LogFilePath = logger.FilePath;

        using var ssh = new SshSession(logger, defaultTimeoutSec: 30);

        try
        {
            // ── 1. Kết nối SSH ────────────────────────────────────────
            ssh.Connect(context.IpAddress, sshUser, sshPass);

            // Chờ prompt đầu tiên
            await ssh.WaitForPromptAsync(MAIN_PROMPT, timeoutSec: 15);

            // ── 2. Chạy lcm-ctrl -t dump ─────────────────────────────
            var lcmResult = await ssh.SendCommandAsync(
                command:   "lcm-ctrl -t dump",
                expectKey: MAIN_PROMPT
            );

            if (!lcmResult.Passed)
            {
                logger.LogStepResult("lcm-ctrl dump", false, lcmResult.ErrorReason);
                return StepResult.Fail("LCMD", lcmResult.ErrorReason);
            }

            // Kiểm tra state phải là "ready"
            string state = OutputParser.ExtractJsonValue(lcmResult.RawOutput, "state");
            if (state != "ready")
            {
                string reason = $"state = '{state}', expected 'ready'";
                logger.LogStepResult("lcm-ctrl dump", false, reason);
                return StepResult.Fail("LCMS", reason);
            }

            logger.LogStepResult("lcm-ctrl dump", true, $"state={state}");

            // ── 3. Vào telnet localhost ───────────────────────────────
            var telnetResult = await ssh.SendCommandAsync(
                command:   "telnet localhost",
                expectKey: "(UBNT) >"    // prompt sau khi vào telnet
            );

            if (!telnetResult.Passed)
            {
                logger.LogStepResult("telnet connect", false, telnetResult.ErrorReason);
                return StepResult.Fail("TELN", telnetResult.ErrorReason);
            }

            // ── 4. Enable mode ────────────────────────────────────────
            await ssh.SendCommandAsync("en", expectKey: TELNET_PROMPT);

            // ── 5. Show version ───────────────────────────────────────
            var versionResult = await ssh.SendCommandAsync(
                command:   "show version",
                expectKey: TELNET_PROMPT
            );

            if (!versionResult.Passed)
            {
                logger.LogStepResult("show version", false, versionResult.ErrorReason);
                return StepResult.Fail("SVER", versionResult.ErrorReason);
            }

            // Bắt MAC từ output
            string burnedMac = OutputParser.ExtractLabelValue(
                versionResult.RawOutput,
                "Burned In MAC Address"
            );

            // So sánh MAC với MAC đã scan
            string expectMac = context.Mac.Replace(":", "").ToUpper();
            string actualMac = burnedMac.Replace(":", "").ToUpper();

            if (expectMac != actualMac)
            {
                string reason = $"MAC mismatch: expect {expectMac}, got {actualMac}";
                logger.LogStepResult("show version", false, reason);
                return StepResult.Fail("CMAC", reason);
            }

            logger.LogStepResult("show version", true, $"MAC={burnedMac}");

            // ── 6. Thoát telnet ───────────────────────────────────────
            ssh.SendRaw("\x1D"); // Ctrl+]
            await Task.Delay(500);
            await ssh.WaitForPromptAsync(MAIN_PROMPT, timeoutSec: 5);

            // ── Kết quả cuối ──────────────────────────────────────────
            logger.LogFinal(true);
            return StepResult.Ok("SSH commands completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            logger.LogFinal(false, "SSHE");
            return StepResult.Fail("SSHE", ex.Message);
        }
    }
}
```

---

## Bước 7 — Cập nhật `TestContext.cs`

Thêm 2 property mới vào `TestContext.cs` (Core/Models/):

```csharp
namespace TestSystem.Core.Models;

public class TestContext
{
    public string Mac         { get; set; } = "";
    public string IpAddress   { get; set; } = "";
    public string Station     { get; set; } = "";
    public string Model       { get; set; } = "";

    public bool   IsPassed    { get; set; } = true;
    public string ErrorCode   { get; set; } = "";
    public string UserMessage { get; set; } = "";

    // Đường dẫn file log — dùng để SFIS biết cần upload file nào
    public string LogFilePath { get; set; } = "";

    public List<string> StepLogs { get; } = new();
    public void AddLog(string message)
        => StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

    // Truy cập config không cần inject service vào từng step
    private readonly Dictionary<string, string> _configCache = new();
    public void SetConfig(string section, string key, string value)
        => _configCache[$"{section}:{key}"] = value;
    public string GetConfig(string section, string key, string def = "")
        => _configCache.TryGetValue($"{section}:{key}", out var v) ? v : def;
}
```

---

## Bước 8 — Cập nhật `Program.cs` (UI)

Thêm config SSH và inject vào context trước khi chạy Engine:

```csharp
// Trong SeedDefaultConfig() — thêm vào
SeedIfEmpty("COM", "SSH_User",   "ubnt");
SeedIfEmpty("COM", "SSH_Pass",   "ubnt");
SeedIfEmpty("GlobalVariable", "LogFolder", @"D:\TestLogs");
```

Trong `MainForm.cs`, sửa chỗ tạo context để load config vào:

```csharp
var context = new TestContext
{
    IpAddress = txtIpAddress.Text.Trim(),
    Station   = Program.Config.GetString("GlobalVariable", "Station"),
    Model     = Program.Config.GetString("GlobalVariable", "Model"),
    Mac       = "58D61F96C650"   // sau này từ barcode
};

// Load toàn bộ config cần dùng vào context
// Step sẽ gọi context.GetConfig() thay vì gọi DB trực tiếp
context.SetConfig("GlobalVariable", "LogFolder",
    Program.Config.GetString("GlobalVariable", "LogFolder"));
context.SetConfig("COM", "SSH_User",
    Program.Config.GetString("COM", "SSH_User"));
context.SetConfig("COM", "SSH_Pass",
    Program.Config.GetString("COM", "SSH_Pass"));
```

---

## Bước 9 — Thêm SshCommandStep vào Engine

Trong `MainForm.cs`, hàm `SetupEngine()`:

```csharp
_engine = new TestEngine(new List<Core.Interfaces.ITestStep>
{
    new PingStep(timeoutSec: 30),
    new SshCommandStep(),        // ← thêm vào đây
    // new SfisPreDataStep(),
    // new SfisSendResultStep(),
});
```

**Build** → 0 errors.

---

## File log tạo ra trông như này

```
=== LOG START | 2024-01-15 09:01:00 ===
=== Model: USW-Pro-24 | MAC: 58D61F96C650 | Station: L1-PT1-01 ===

[09:01:00] >> [SSH CONNECT] ubnt@192.168.1.20:22
[09:01:01]    [SSH CONNECTED]
[09:01:01]    [WAITING FOR PROMPT '# ']
[09:01:02]    USW-Pro-24-PoE-US.6.4.19#
[09:01:02]    ← expect '# ' found — command end

[09:01:02] >> lcm-ctrl -t dump
[09:01:02]    {
[09:01:02]        "adopted": true,
[09:01:02]        "state": "ready",
[09:01:02]        "version": "v5.1.0-0-g68e60e5"
[09:01:03]    }
[09:01:03]    USW-Pro-24-PoE-US.6.4.19#
[09:01:03]    ← expect '# ' found — command end

[09:01:03] ========================================
[09:01:03] STEP: lcm-ctrl dump → PASS state=ready
[09:01:03] ========================================

[09:01:03] >> telnet localhost
[09:01:04]    (UBNT) >
[09:01:04]    ← expect '(UBNT) >' found — command end
[09:01:04] >> en
[09:01:04]    (UBNT) #
[09:01:04] >> show version
[09:01:05]    Burned In MAC Address......... 58:D6:1F:96:C6:50
[09:01:05]    Software Version.............. 6.4.19.14340
[09:01:05]    (UBNT) #
[09:01:05]    ← expect '(UBNT) #' found — command end

[09:01:05] ========================================
[09:01:05] STEP: show version → PASS MAC=58:D6:1F:96:C6:50
[09:01:05] ========================================

=== TEST RESULT: PASS ===
=== LOG END | 2024-01-15 09:01:05 ===
```

---

## Khi FAIL — Engine tự dừng và SFIS nhận fail

Luồng fail hoạt động tự động vì `TestEngine.cs` đã xử lý sẵn:

```
SshCommandStep trả về StepResult.Fail("CMAC", "MAC mismatch...")
→ Engine ghi ErrorCode = "CMAC" vào context, break vòng loop
→ UI hiển thị "FAIL [CMAC]" màu đỏ
→ SfisSendResultStep đọc context.IsPassed = false → gọi SendFailAsync("CMAC")
```

Bạn không cần làm gì thêm — Engine đã lo phần dừng và SFIS step đọc kết quả cuối từ context.

---

**Build → F5 → test với IP DUT thật.** Nếu bạn chưa có bản mạch, test với IP bất kỳ có SSH — thậm chí SSH vào máy Linux/Raspberry Pi cũng được để verify luồng log hoạt động đúng.

---

