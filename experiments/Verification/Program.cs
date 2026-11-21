using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Catalog;
using SmartInventory.Infrastructure.Data;

var root = FindWorkspace();
if (args is ["--help"] or ["-h"])
{
    Console.WriteLine("Run from the workspace: dotnet run --project experiments/Verification");
    Console.WriteLine("Use --check-paths to verify relocated files without accessing a database or provider.");
    return;
}
if (args is ["--check-paths"])
{
    foreach (var path in new[]
    {
        "AI-Smart-Inventory.slnx", "backend/SmartInventory.Infrastructure/SmartInventory.Infrastructure.csproj",
        "experiments/Verification/browser-qa.mjs", "experiments/MCP_CSharp_Inventory_Experiments.ipynb",
        "experiments/Training/Training.csproj", "experiments/data/mcp_tool_intents.csv",
        "experiments/data/training_configurations.json", "experiments/data/mcp_read_configurations.json",
        "experiments/reports/TRAINING-TEST-REPORT.md", "experiments/quiz/20_CAU_HOI_TRAC_NGHIEM.txt",
        "frontend/scripts/vite.mjs"
    })
    {
        if (!File.Exists(Path.Combine(root, path))) throw new InvalidOperationException("Missing file: " + path);
        Console.WriteLine("PASS " + path);
    }
    return;
}
if (args.Length != 0) throw new ArgumentException("Unknown arguments. Use --help.");
var config = new ConfigurationBuilder().AddUserSecrets("1dcb4022-f6f0-43a4-bacd-034019b89896").AddEnvironmentVariables().Build();
var source = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Configure database privately.");
var connection = new NpgsqlConnectionStringBuilder(source);
var databaseName = "test_inventory_" + Guid.NewGuid().ToString("N");
var adminConnection = new NpgsqlConnectionStringBuilder(source) { Database = "postgres", Pooling = false };
connection.Database = databaseName;
connection.Pooling = false;
var password = "Verify_" + Guid.NewGuid().ToString("N");
var jwtKey = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
var processes = new List<Process>();
var evidence = new List<object>();
var created = false;
var completed = false;
var liveLlm = Environment.GetEnvironmentVariable("INVENTORY_LIVE_LLM") == "1";
var passwordQa = Environment.GetEnvironmentVariable("INVENTORY_PASSWORD_QA") == "1";
var recoveryQa = Environment.GetEnvironmentVariable("INVENTORY_RECOVERY_QA") == "1";
if (recoveryQa && (passwordQa || liveLlm || Environment.GetEnvironmentVariable("INVENTORY_BROWSER_QA") == "1" || Environment.GetEnvironmentVariable("INVENTORY_NOTEBOOK_QA") == "1"))
    throw new InvalidOperationException("Run recovery QA separately to preserve authentication rate limits.");
using var testSmtp = recoveryQa ? new TestSmtpServer() : null;
if (passwordQa && (liveLlm || Environment.GetEnvironmentVariable("INVENTORY_BROWSER_QA") == "1" ||
    Environment.GetEnvironmentVariable("INVENTORY_NOTEBOOK_QA") == "1"))
    throw new InvalidOperationException("Run password QA separately to preserve the authentication rate limit.");
var apiPort = FreePort();
var mcpPort = FreePort();
using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{apiPort}"), Timeout = TimeSpan.FromSeconds(150) };
try
{
    await using (var admin = new NpgsqlConnection(adminConnection.ConnectionString))
    {
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin).ExecuteNonQueryAsync();
        created = true;
    }
    await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options))
    {
        await db.Database.MigrateAsync();
        foreach (var role in new[] { "Admin", "Staff", "Customer" })
        {
            var user = new User { Username = "verify_" + role.ToLowerInvariant(), Role = role, Email = role + "@example.test", FullName = "Verify " + role, IsActive = true };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
        await new DevelopmentCatalogSeeder(db).SeedAsync(default);
    }
    processes.Add(Start("SmartInventory.McpServer", mcpPort));
    processes.Add(Start("SmartInventory.Api", apiPort));
    await WaitReady(http);
    using var mcpHealth = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{mcpPort}") };
    await WaitReady(mcpHealth);
    await Call("AUTH-ANON", "GET", "/api/products", null, 401);
    var adminToken = await Login("Admin");
    var staffToken = await Login("Staff");
    var customerToken = await Login("Customer");
    foreach (var role in new[] { (Name: "Customer", Token: customerToken), (Name: "Staff", Token: staffToken), (Name: "Admin", Token: adminToken) })
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", role.Token);
        await Call("ROLE-"+role.Name+"-PRODUCTS", "GET", "/api/products", null, role.Name == "Customer" ? 403 : 200);
        await Call("ROLE-"+role.Name+"-USERS", "GET", "/api/users", null, role.Name == "Admin" ? 200 : 403);
        await Call("ROLE-"+role.Name+"-MCP", "GET", "/api/mcp/tools", null, role.Name == "Customer" ? 403 : 200);
    }
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    var list = await Call("READ-CATALOG", "GET", "/api/products?page=1&pageSize=100", null, 200);
    Check(list!["totalCount"]!.GetValue<int>() == 32, "Expected 32 seeded products.");
    var id = list["items"]![0]!["id"]!.GetValue<int>();
    var sku = list["items"]![0]!["sku"]!.GetValue<string>();
    foreach (var quantity in new[] { 2, 5, 9, 10, 15, 20, 30, 50 })
    {
        var index = Array.IndexOf(new[] { 2, 5, 9, 10, 15, 20, 30, 50 }, quantity);
        var productId = list["items"]![index]!["id"]!.GetValue<int>();
        await Call("SETUP-RECEIPT-"+quantity, "POST", "/api/inventory/receipts", new { productId, quantity, reason = "Verification sample receipt", requestId = Guid.NewGuid() }, 200);
    }
    foreach (var size in new[] { 5, 10, 20, 100 })
    {
        var r = await Tool("PAGE-"+size, "GetAllProducts", new { page = 1, pageSize = size });
        Check(r!["totalCount"]!.GetValue<int>() == 32 && r["items"]!.AsArray().Count == Math.Min(32, size), "Paging mismatch.");
    }
    foreach (var threshold in new[] { 0, 5, 20 })
    {
        var r = await Tool("LOW-"+threshold, "GetLowStockProducts", new { threshold, page = 1, pageSize = 100 });
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
        var expected = await db.Inventories.CountAsync(x => x.Quantity < threshold);
        Check(r!["totalCount"]!.GetValue<int>() == expected, "Low stock SQL mismatch.");
    }
    var outOfStock = await Tool("OUT", "GetOutOfStockProducts", new { page = 1, pageSize = 100 });
    Check(outOfStock!["totalCount"]!.GetValue<int>() == 24, "Out-of-stock mismatch.");
    var byId = await Tool("ID", "GetProductById", new { id });
    var bySku = await Tool("SKU", "GetProductBySku", new { sku });
    Check(byId!["id"]!.GetValue<int>() == id && bySku!["id"]!.GetValue<int>() == id, "Product identity mismatch.");
    var invalid = await Call("VALID-PAGE", "POST", "/api/mcp/tools/GetAllProducts/execute", new { arguments = new { page = 0, pageSize = 10 } }, 200);
    Check(invalid!["isError"]!.GetValue<bool>(), "Invalid paging accepted.");
    var intent = await Call("CONFIRM-PREPARE", "POST", "/api/mcp/write-intents", new { toolName = "CreateStockReceipt", arguments = new { productId = id, quantity = 7, reason = "Verify confirmed receipt", requestId = Guid.NewGuid() } }, 201);
    var intentId = intent!["id"]!.GetValue<string>();
    var before = (await Call("BEFORE-CONFIRM", "GET", $"/api/products/{id}", null, 200))!["quantity"]!.GetValue<int>();
    Check(before == 2, "Prepare changed stock.");
    var confirm = await Call("CONFIRM-EXECUTE", "POST", $"/api/mcp/write-intents/{intentId}/confirm", new { }, 200);
    Check(confirm!["intent"]!["status"]!.GetValue<string>() == "Succeeded", "Confirmed tool failed.");
    await Call("REPLAY", "POST", $"/api/mcp/write-intents/{intentId}/confirm", new { }, 200);
    var after = (await Call("AFTER-REPLAY", "GET", $"/api/products/{id}", null, 200))!["quantity"]!.GetValue<int>();
    Check(after == before + 7, "Replay duplicated mutation.");
    await Call("ISSUE", "POST", "/api/inventory/issues", new { productId = id, quantity = 3, reason = "Verify issue", requestId = Guid.NewGuid() }, 200);
    await Call("OVERDRAW", "POST", "/api/inventory/issues", new { productId = id, quantity = 9999, reason = "Verify rejection", requestId = Guid.NewGuid() }, 409);
    var cancelled = await Call("CANCEL-PREPARE", "POST", "/api/mcp/write-intents", new { toolName = "CreateStockReceipt", arguments = new { productId = id, quantity = 4, reason = "Verify cancel", requestId = Guid.NewGuid() } }, 201);
    await Call("CANCEL", "POST", $"/api/mcp/write-intents/{cancelled!["id"]!.GetValue<string>()}/cancel", new { }, 200);
    var final = (await Call("FINAL", "GET", $"/api/products/{id}", null, 200))!["quantity"]!.GetValue<int>();
    Check(final == 6, "Rejected/cancelled write changed inventory.");
    await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options))
    {
        Check(await db.StockMovements.CountAsync() == 10, "Unexpected movement count (replay/rollback).");
        Check(await db.Inventories.SumAsync(x => x.Quantity) == 145, "SQL total mismatch.");
    }
    if (recoveryQa)
        await PasswordRecoveryVerification.Run(http.BaseAddress!, connection.ConnectionString, customerToken, testSmtp!,
            Path.Combine(root, "experiments/results/password-recovery-results.json"));
    if (passwordQa)
        await PasswordPolicyVerification.RunAsync(http.BaseAddress!, connection.ConnectionString, adminToken,
            Path.Combine(root, "experiments/results/password-policy-results.json"));
    if (Environment.GetEnvironmentVariable("INVENTORY_BROWSER_QA") == "1")
    {
        var webPort = FreePort();
        var vite = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root, "frontend"), RedirectStandardOutput = true, RedirectStandardError = true };
        vite.ArgumentList.Add("scripts/vite.mjs"); vite.ArgumentList.Add("dev"); vite.ArgumentList.Add("--host"); vite.ArgumentList.Add("127.0.0.1"); vite.ArgumentList.Add("--port"); vite.ArgumentList.Add(webPort.ToString());
        vite.Environment["VITE_API_PROXY_TARGET"] = http.BaseAddress!.ToString().TrimEnd('/');
        var web = Process.Start(vite)!; processes.Add(web);
        web.OutputDataReceived += (_, _) => { }; web.ErrorDataReceived += (_, _) => { }; web.BeginOutputReadLine(); web.BeginErrorReadLine();
        using var ready = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{webPort}") }; await WaitReady(ready);
        var browserInfo = new ProcessStartInfo("node") { WorkingDirectory = root, RedirectStandardInput = true };
        browserInfo.ArgumentList.Add("experiments/Verification/browser-qa.mjs");
        using var browser = Process.Start(browserInfo)!;
        await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { baseUrl = ready.BaseAddress!.ToString().TrimEnd('/'), password, outputDirectory = Path.Combine(root, "experiments/results/browser") }));
        browser.StandardInput.Close(); await browser.WaitForExitAsync();
        Check(browser.ExitCode == 0, "Browser QA failed.");
    }
    if (Environment.GetEnvironmentVariable("INVENTORY_NOTEBOOK_QA") == "1")
    {
        var notebookInfo = new ProcessStartInfo("python3") { WorkingDirectory = root, RedirectStandardInput = true };
        notebookInfo.ArgumentList.Add("-c");
        notebookInfo.ArgumentList.Add("""
import sys,json,os,tempfile
from pathlib import Path
import nbformat
from nbclient import NotebookClient
config=json.loads(sys.stdin.read())
root=Path.cwd()
path=root/'experiments/MCP_CSharp_Inventory_Experiments.ipynb'
book=nbformat.read(path,as_version=4)
os.environ['INVENTORY_API_URL']=config.pop('apiUrl')
os.environ['INVENTORY_VERIFY_INPUTS']=json.dumps(config['answers'])
os.environ['MPLBACKEND']='module://matplotlib_inline.backend_inline'
os.environ['IPYTHONDIR']=tempfile.mkdtemp(prefix='inventory-ipython-')
os.environ['JUPYTER_RUNTIME_DIR']=tempfile.mkdtemp(prefix='inventory-jupyter-')
try:
 NotebookClient(book,timeout=180,kernel_name='python3',resources={'metadata':{'path':str(root/'experiments')}}).execute()
finally:
 nbformat.write(book,path)
print('Notebook executed and saved; injected credentials were not stored in cells.')
""");
        using var notebook = Process.Start(notebookInfo)!;
        await notebook.StandardInput.WriteAsync(JsonSerializer.Serialize(new { apiUrl = http.BaseAddress!.ToString().TrimEnd('/'), answers = new[] { connection.Host, connection.Port.ToString(), connection.Database, connection.Username, connection.Password, "verify_admin", password } }));
        notebook.StandardInput.Close(); await notebook.WaitForExitAsync();
        Check(notebook.ExitCode == 0, "Notebook execution failed.");
    }
    if (liveLlm)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var provider = await Call("AI-PROVIDER", "GET", "/api/ai/status", null, 200);
        Check(provider!["configured"]!.GetValue<bool>(), "Live LLM is not configured.");
        var session = await Call("AI-SESSION", "POST", "/api/ai/sessions", new { title = "Inventory experiment verification" }, 201);
        var reply = await Call("AI-LIVE", "POST", $"/api/ai/sessions/{session!["id"]!.GetValue<string>()}/messages", new { message = "Hãy gọi GetInventorySummary để đọc dữ liệu kho hiện tại và cho biết tổng số sản phẩm, tổng tồn và số sản phẩm hết hàng. Không suy đoán dữ liệu." }, 200);
        Check(reply!["toolExecutions"]!.AsArray().Any(x => x!["toolName"]!.GetValue<string>() == "GetInventorySummary" && !x["isError"]!.GetValue<bool>()), "LLM did not execute the expected MCP tool.");
        Check(reply["message"]!["content"]!.GetValue<string>().Contains("145"), "LLM answer does not report expected total quantity 145.");
    }
    completed = true;
    Console.WriteLine("ALL ASSERTIONS PASSED");
}
finally
{
    foreach (var process in processes)
    {
        if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        process.Dispose();
    }
    var outPath = Path.Combine(root, "experiments/results"); Directory.CreateDirectory(outPath);
    // Keep the approved live-LLM acceptance evidence when running a smaller local suite.
    var resultName = liveLlm ? "verification.json" : passwordQa ? "password-policy-suite.json" : recoveryQa ? "password-recovery-suite.json" : "local-verification.json";
    await File.WriteAllTextAsync(Path.Combine(outPath, resultName), JsonSerializer.Serialize(new { observedAt = DateTimeOffset.UtcNow, source = "REAL_CSHARP_MCP_POSTGRESQL", passed = completed, liveLlm, cases = evidence }, new JsonSerializerOptions { WriteIndented = true }));
    if (created)
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection.ConnectionString);
        await admin.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
    }
}

Process Start(string project, int port)
{
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "backend", project), RedirectStandardOutput = true, RedirectStandardError = true };
    info.ArgumentList.Add(Path.Combine(info.WorkingDirectory, "bin/Debug/net10.0/", project+".dll"));
    foreach (var entry in config.AsEnumerable().Where(x => x.Value != null)) info.Environment[entry.Key.Replace(":", "__")] = entry.Value;
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    info.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    info.Environment["ConnectionStrings__DefaultConnection"] = connection.ConnectionString;
    info.Environment["Jwt__SigningKey"] = jwtKey;
    info.Environment["Jwt__Issuer"] = "http://localhost";
    info.Environment["Jwt__Audience"] = "inventory-first-party";
    info.Environment["Mcp__ServerUrl"] = $"http://127.0.0.1:{mcpPort}/mcp";
    info.Environment["Mcp__UseSharedFirstPartyAudience"] = "true";
    // Rapid browser navigation shares the same test user; do not alter production defaults.
    info.Environment["RateLimiting__PermitLimit"] = "10000";
    info.Environment["Mcp__RateLimitPermitLimit"] = "10000";
    if (testSmtp is not null)
    {
        info.Environment["PasswordRecovery__Host"] = "127.0.0.1";
        info.Environment["PasswordRecovery__Port"] = testSmtp.Port.ToString();
        info.Environment["PasswordRecovery__EnableSsl"] = "false";
        info.Environment["PasswordRecovery__Username"] = "";
        info.Environment["PasswordRecovery__Password"] = "";
        info.Environment["PasswordRecovery__FromEmail"] = "sender@example.test";
        info.Environment["PasswordRecovery__PublicBaseUrl"] = "http://127.0.0.1:5173";
    }
    if (!liveLlm) info.Environment["Llm__ApiKey"] = "";
    var p = Process.Start(info)!;
    p.OutputDataReceived += (_, _) => { }; p.ErrorDataReceived += (_, _) => { };
    p.BeginOutputReadLine(); p.BeginErrorReadLine(); return p;
}
async Task WaitReady(HttpClient client)
{
    for (var i=0; i<80; i++)
    {
        try { using var response = await client.GetAsync("/health/live"); if (response.IsSuccessStatusCode) return; } catch (HttpRequestException) { }
        await Task.Delay(250);
    }
    throw new InvalidOperationException("Host readiness timed out.");
}
async Task<JsonNode?> Call(string id, string method, string path, object? body, int expected)
{
    using var request = new HttpRequestMessage(new HttpMethod(method), path);
    if (body != null) request.Content = JsonContent.Create(body);
    var watch = Stopwatch.StartNew(); using var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    var node = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    evidence.Add(new { id, method, path, input = body, expectedHttp = expected, actualHttp = (int)response.StatusCode, durationMs = watch.ElapsedMilliseconds, output = node?.DeepClone() });
    Check((int)response.StatusCode == expected, $"{id}: HTTP {(int)response.StatusCode}, expected {expected}");
    Console.WriteLine("PASS "+id); return node;
}
async Task<string> Login(string role)
{
    using var response = await http.PostAsJsonAsync("/api/auth/login", new { username = "verify_"+role.ToLowerInvariant(), password });
    response.EnsureSuccessStatusCode(); var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    return node["accessToken"]!.GetValue<string>();
}
async Task<JsonNode?> Tool(string id, string name, object arguments)
{
    var result = await Call(id, "POST", $"/api/mcp/tools/{name}/execute", new { arguments }, 200);
    Check(result!["status"]!.GetValue<string>() == "Succeeded" && !result["isError"]!.GetValue<bool>(), id+": tool error");
    var output = result["output"]!;
    return output["structuredContent"] ?? JsonNode.Parse(output["content"]![0]!["text"]!.GetValue<string>());
}
static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static int FreePort() { var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); l.Start(); var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return port; }

static string FindWorkspace()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "AI-Smart-Inventory.slnx"))) return directory.FullName;
    throw new InvalidOperationException("Cannot locate AI-Smart-Inventory.slnx above the verification output directory.");
}
