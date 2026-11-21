using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

if (args is ["--help"] or ["-h"])
{
    Console.WriteLine("Run API and MCP together: dotnet run --project backend/SmartInventory.AppHost");
    Console.WriteLine("Uses each server's http launch profile. Press Ctrl+C to stop both servers.");
    return 0;
}
if (args.Length != 0)
{
    Console.Error.WriteLine("Unknown arguments. Use --help for usage.");
    return 1;
}

using var shutdown = new CancellationTokenSource();
ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
Console.CancelKeyPress += onCancel;
using var terminate = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(
    PosixSignal.SIGTERM, context => { context.Cancel = true; shutdown.Cancel(); });
var servers = new List<RunningServer>();
try
{
    var root = FindWorkspace();
    var configuration = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";
    var mcp = ReadServer(root, configuration, "SmartInventory.McpServer", "MCP", "/health/live");
    var api = ReadServer(root, configuration, "SmartInventory.Api", "API", "/health/ready");
    api.StartInfo.Environment["Mcp__ServerUrl"] = new Uri(mcp.BaseAddress, "/mcp").AbsoluteUri;

    Console.WriteLine("Starting backend. Press Ctrl+C to stop API and MCP.");
    var mcpProcess = Start(mcp);
    servers.Add(mcpProcess);
    await WaitReady(mcpProcess, shutdown.Token);
    var apiProcess = Start(api);
    servers.Add(apiProcess);
    await WaitReady(apiProcess, shutdown.Token);

    Console.WriteLine($"[READY] API: {api.BaseAddress}");
    Console.WriteLine($"[READY] MCP: {new Uri(mcp.BaseAddress, "/mcp")}");
    var stop = Task.Delay(Timeout.Infinite, shutdown.Token);
    await Task.WhenAny(apiProcess.Process.WaitForExitAsync(), mcpProcess.Process.WaitForExitAsync(), stop);
    if (shutdown.IsCancellationRequested) return 0;
    var exited = servers.First(server => server.Process.HasExited);
    Console.Error.WriteLine($"[{exited.Spec.Label}] exited with code {exited.Process.ExitCode}; stopping backend.");
    return exited.Process.ExitCode == 0 ? 1 : exited.Process.ExitCode;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    return 0;
}
catch (Exception error) when (error is IOException or InvalidOperationException or JsonException or System.ComponentModel.Win32Exception or HttpRequestException)
{
    Console.Error.WriteLine($"[AppHost] {error.Message}");
    return 1;
}
finally
{
    Console.WriteLine("Stopping backend...");
    shutdown.Cancel();
    foreach (var server in servers.AsEnumerable().Reverse())
    {
        try { await Stop(server); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            Console.Error.WriteLine($"[{server.Spec.Label}] shutdown failed: {error.Message}");
        }
    }
    Console.CancelKeyPress -= onCancel;
}

static string FindWorkspace()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "AI-Smart-Inventory.slnx"))) return directory.FullName;
    throw new InvalidOperationException("Run AppHost from the AI-Smart-Inventory checkout.");
}

static ServerSpec ReadServer(string root, string configuration, string project, string label, string healthPath)
{
    var directory = Path.Combine(root, "backend", project);
    var assembly = Path.Combine(directory, "bin", configuration, "net10.0", project + ".dll");
    if (!File.Exists(assembly))
        throw new InvalidOperationException($"Build {project} first, or run AppHost without --no-build.");
    using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "Properties", "launchSettings.json")));
    var profile = settings.RootElement.GetProperty("profiles").GetProperty("http");
    var urls = profile.GetProperty("applicationUrl").GetString()!;
    var address = urls.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(url => new Uri(url))
        .FirstOrDefault(url => url.Scheme == "http" && url.IsLoopback)
        ?? throw new InvalidOperationException($"{project}: http launch profile requires a local HTTP URL.");
    var info = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = directory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    info.ArgumentList.Add(assembly);
    foreach (var variable in profile.GetProperty("environmentVariables").EnumerateObject())
        info.Environment[variable.Name] = variable.Value.GetString();
    info.Environment["ASPNETCORE_URLS"] = urls;
    info.Environment["DOTNET_ENVIRONMENT"] = info.Environment["ASPNETCORE_ENVIRONMENT"];
    return new(label, info, address, healthPath);
}

static RunningServer Start(ServerSpec spec)
{
    var process = Process.Start(spec.StartInfo) ?? throw new InvalidOperationException($"Could not start {spec.Label}.");
    Console.WriteLine($"[{spec.Label}] started (PID {process.Id}).");
    return new(spec, process, Forward(process.StandardOutput, spec.Label), Forward(process.StandardError, spec.Label));
}

static async Task Forward(StreamReader stream, string label)
{
    while (await stream.ReadLineAsync() is { } line) Console.WriteLine($"[{label}] {line}");
}

static async Task WaitReady(RunningServer server, CancellationToken cancellationToken)
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    var watch = Stopwatch.StartNew();
    while (watch.Elapsed < TimeSpan.FromSeconds(30))
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (server.Process.HasExited)
            throw new InvalidOperationException($"{server.Spec.Label} exited with code {server.Process.ExitCode}. See its log above.");
        try
        {
            using var response = await client.GetAsync(new Uri(server.Spec.BaseAddress, server.Spec.HealthPath), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                // Bind failures can happen while another process answers the health URL.
                await Task.Delay(250, cancellationToken);
                if (!server.Process.HasExited) return;
            }
        }
        catch (HttpRequestException) { }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        await Task.Delay(250, cancellationToken);
    }
    throw new InvalidOperationException($"{server.Spec.Label} is not ready after 30 seconds. Check its port, PostgreSQL configuration and migrations.");
}

static async Task Stop(RunningServer server)
{
    try
    {
        if (!server.Process.HasExited)
        {
            if (!OperatingSystem.IsWindows())
            {
                var signal = new ProcessStartInfo("/bin/kill") { UseShellExecute = false };
                signal.ArgumentList.Add("-TERM");
                signal.ArgumentList.Add(server.Process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
                using var sender = Process.Start(signal);
                if (sender is not null) await sender.WaitForExitAsync();
            }
            else server.Process.Kill(entireProcessTree: true);
            try { await server.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { server.Process.Kill(entireProcessTree: true); await server.Process.WaitForExitAsync(); }
        }
        await Task.WhenAll(server.Output, server.Error);
        Console.WriteLine($"[{server.Spec.Label}] stopped.");
    }
    finally { server.Process.Dispose(); }
}

internal sealed record ServerSpec(string Label, ProcessStartInfo StartInfo, Uri BaseAddress, string HealthPath);
internal sealed record RunningServer(ServerSpec Spec, Process Process, Task Output, Task Error);
