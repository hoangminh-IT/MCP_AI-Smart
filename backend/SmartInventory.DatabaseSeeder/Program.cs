using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var endpoint = Environment.GetEnvironmentVariable("INVENTORY_API_URL") ?? "http://localhost:5062";
if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var address) || !address.IsLoopback ||
    address.Scheme is not ("http" or "https"))
    throw new InvalidOperationException("Use a local HTTP(S) API endpoint.");
using var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(30) };
try
{
    using var ready = await client.GetAsync("/health/ready");
    ready.EnsureSuccessStatusCode();
    if (Console.IsInputRedirected)
        throw new InvalidOperationException("Run this tool in an interactive terminal to enter credentials securely.");
    Console.Write("Application Admin/Staff username: ");
    var username = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(username))
        throw new InvalidOperationException("Application username is required.");
    Console.Write("Application password (hidden): ");
    var password = ConsolePassword.Read();
    using var loginRequest = JsonContent.Create(new { username, password });
    password = string.Empty;
    using var loginResponse = await client.PostAsync("/api/auth/login", loginRequest);
    if (!loginResponse.IsSuccessStatusCode)
        throw new InvalidOperationException($"Login failed: HTTP {(int)loginResponse.StatusCode}. No receipts created.");
    var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>()
        ?? throw new InvalidOperationException("Empty login response.");
    if (string.IsNullOrWhiteSpace(login.AccessToken)) throw new InvalidOperationException("Missing access token.");
    if (login.User?.Role is not ("Admin" or "Staff"))
        throw new InvalidOperationException("This account cannot seed inventory; Admin or Staff is required.");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    var targets = new Dictionary<string, int>
    {
        ["SP001"] = 2, ["SP002"] = 5, ["SP003"] = 9, ["SP004"] = 10,
        ["SP005"] = 15, ["SP006"] = 20, ["SP007"] = 30, ["SP008"] = 50
    };
    var products = new List<SeedProduct>();
    for (var page = 1; ; page++)
    {
        using var response = await client.GetAsync($"/api/products?page={page}&pageSize=100");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProductPage>()
            ?? throw new InvalidOperationException("Empty product response.");
        products.AddRange(result.Items);
        if ((long)page * 100 >= result.TotalCount) break;
        if (result.Items.Count == 0) throw new InvalidOperationException("Unexpected empty page.");
    }
    if (products.Select(product => product.Id).Distinct().Count() != products.Count)
        throw new InvalidOperationException("Product pages contain duplicate IDs. Stop concurrent catalog changes and retry.");
    var missing = targets.Keys.Except(products.Select(product => product.Sku)).ToArray();
    if (missing.Length > 0)
        throw new InvalidOperationException($"Missing active sample SKUs: {string.Join(", ", missing)}. Run the API catalog --seed command first. No receipts created.");
    var count = 0;
    foreach (var product in products)
    {
        var sku = product.Sku;
        if (!targets.TryGetValue(sku, out var quantity)) continue;
        if (product.Quantity != 0)
        {
            Console.WriteLine($"Skipped {sku}: already has stock.");
            continue;
        }
        var productId = product.Id;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"inventory-csharp-sample-v1/{productId}/{sku}"));
        var requestId = new Guid(hash.AsSpan(0, 16));
        using var receipt = await client.PostAsJsonAsync("/api/inventory/receipts", new
        {
            productId, warehouseId = 1, quantity,
            reason = "C# notebook experiment sample v1: opening receipt", requestId
        });
        if (!receipt.IsSuccessStatusCode)
            throw new InvalidOperationException($"Receipt {sku}: HTTP {(int)receipt.StatusCode}. Earlier successful receipts remain committed.");
        var result = await receipt.Content.ReadFromJsonAsync<ReceiptResult>()
            ?? throw new InvalidOperationException("Empty receipt response; check movement history before retrying.");
        Console.WriteLine($"{sku}: movement {result.MovementId}, stock {result.QuantityBefore} -> {result.QuantityAfter}, replayed={result.Replayed}");
        count++;
    }
    Console.WriteLine($"Finished: {count} receipt responses; {products.Count} products inspected.");
}
catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
{
    Console.Error.WriteLine(error is HttpRequestException or TaskCanceledException
        ? "API unavailable or request failed. Check API/PostgreSQL. Earlier successful receipts may remain committed."
        : error.Message);
    Environment.ExitCode = 1;
}

