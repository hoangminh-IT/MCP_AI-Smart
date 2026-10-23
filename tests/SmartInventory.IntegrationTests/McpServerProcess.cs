using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace SmartInventory.IntegrationTests;

/// <summary>A real SDK server over loopback HTTP; no in-memory MCP transport or simulated tool execution.</summary>
public sealed class McpServerProcess : IAsyncDisposable
{
    private Process? process;
    public string Url { get; private set; } = "";

    public async Task StartAsync(string connectionString, string signingKey)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AI-Smart-Inventory.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Could not locate the solution root for the real MCP server.");
        var dll = Path.Combine(root.FullName, "backend", "SmartInventory.McpServer", "bin", "Debug", "net10.0", "SmartInventory.McpServer.dll");
        if (!File.Exists(dll)) throw new InvalidOperationException("Build the solution before running MCP integration tests.");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var baseUrl = $"http://127.0.0.1:{port}";
        Url = baseUrl + "/mcp";
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(dll)!
        };
        start.ArgumentList.Add(dll);
        start.Environment["ConnectionStrings__DefaultConnection"] = connectionString;
        start.Environment["Jwt__SigningKey"] = signingKey;
        start.Environment["Jwt__Issuer"] = "inventory-integration-tests";
        start.Environment["Jwt__Audience"] = "inventory-integration-tests";
        start.Environment["ASPNETCORE_URLS"] = baseUrl;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["AllowedHosts"] = "localhost;127.0.0.1";
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        process = Process.Start(start) ?? throw new InvalidOperationException("Could not launch the real C# MCP server.");
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (process.HasExited) throw new InvalidOperationException("The real MCP server exited during startup. Check its configuration.");
            try
            {
                if ((await client.GetAsync(baseUrl + "/health/live")).IsSuccessStatusCode) return;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException) { }
            await Task.Delay(100);
        }
        throw new TimeoutException("The real MCP server did not become ready.");
    }

    public async ValueTask DisposeAsync()
    {
        if (process is null) return;
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        process.Dispose();
    }
}
