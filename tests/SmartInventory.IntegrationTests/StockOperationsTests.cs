using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SmartInventory.IntegrationTests;

public sealed class StockOperationsTests(InventoryApiFactory app) : IClassFixture<InventoryApiFactory>
{
    private const string ReceiptPath = "/api/inventory/receipts";
    private const string IssuePath = "/api/inventory/issues";
    private const string AdjustmentPath = "/api/inventory/adjustments";

    [Fact]
    public async Task Receipt_commits_inventory_and_its_complete_movement_together()
    {
        var productId = await CreateProductAsync();
        var requestId = Guid.NewGuid();
        using var response = await PostAsync(app.Admin, ReceiptPath, productId, 10, "Initial delivery", requestId);
        var result = await SuccessAsync(response);

        Assert.Equal(productId, result["productId"]!.GetValue<int>());
        Assert.Equal("Receipt", result["type"]!.GetValue<string>());
        Assert.Equal(10, result["quantity"]!.GetValue<int>());
        Assert.Equal(0, result["quantityBefore"]!.GetValue<int>());
        Assert.Equal(10, result["quantityAfter"]!.GetValue<int>());
        Assert.Equal("Initial delivery", result["reason"]!.GetValue<string>());
        Assert.Equal(requestId, result["requestId"]!.GetValue<Guid>());
        Assert.False(result["replayed"]!.GetValue<bool>());
        Assert.Equal(TimeSpan.Zero, result["createdAt"]!.GetValue<DateTimeOffset>().Offset);

        await using var database = app.NewContext();
        var inventory = await database.Inventories.AsNoTracking().SingleAsync(item => item.ProductId == productId);
        var movement = await database.StockMovements.AsNoTracking().SingleAsync(item => item.ProductId == productId);
        var admin = await database.Users.AsNoTracking().SingleAsync(user => user.Username == "integration_admin");
        Assert.Equal(10, inventory.Quantity);
        Assert.Equal(result["movementId"]!.GetValue<int>(), movement.Id);
        Assert.Equal("Receipt", movement.Type);
        Assert.Equal(10, movement.Quantity);
        Assert.Equal(0, movement.QuantityBefore);
        Assert.Equal(10, movement.QuantityAfter);
        Assert.Equal("Initial delivery", movement.Reason);
        Assert.Equal(admin.Id, movement.CreatedBy);
        Assert.Equal(admin.Id, result["createdBy"]!.GetValue<int>());
        Assert.Equal(requestId, movement.RequestId);
        Assert.Equal(DateTimeKind.Utc, movement.CreatedAt.Kind);
    }

    [Fact]
    public async Task Staff_can_receive_and_issue_stock_with_an_attributed_movement()
    {
        var productId = await CreateProductAsync();
        using var receipt = await PostAsync(app.Staff, ReceiptPath, productId, 10, "Staff delivery");
        await SuccessAsync(receipt);
        using var issue = await PostAsync(app.Staff, IssuePath, productId, 4, "Staff dispatch");
        var result = await SuccessAsync(issue);

        Assert.Equal("Issue", result["type"]!.GetValue<string>());
        Assert.Equal(4, result["quantity"]!.GetValue<int>());
        Assert.Equal(10, result["quantityBefore"]!.GetValue<int>());
        Assert.Equal(6, result["quantityAfter"]!.GetValue<int>());
        await using var database = app.NewContext();
        var staff = await database.Users.AsNoTracking().SingleAsync(user => user.Username == "integration_staff");
        Assert.Equal(staff.Id, result["createdBy"]!.GetValue<int>());
        var stored = await database.StockMovements.AsNoTracking().SingleAsync(item => item.Id == result["movementId"]!.GetValue<int>());
        Assert.Equal(staff.Id, stored.CreatedBy);
        Assert.Equal("Staff dispatch", stored.Reason);
        Assert.Equal(6, (await database.Inventories.AsNoTracking().SingleAsync(item => item.ProductId == productId)).Quantity);
        Assert.Equal(2, await database.StockMovements.CountAsync(item => item.ProductId == productId));
    }

    [Fact]
    public async Task Insufficient_stock_rolls_back_without_a_movement_or_inventory_change()
    {
        var productId = await CreateStockedProductAsync();
        using var response = await PostAsync(app.Staff, IssuePath, productId, 11, "Too much stock requested");
        await ProblemAsync(response, HttpStatusCode.Conflict);
        await AssertStockAsync(productId, quantity: 10, movementCount: 1);
    }

    [Fact]
    public async Task Concurrent_issues_of_six_against_ten_allow_one_issue_and_preserve_nonnegative_stock()
    {
        var productId = await CreateStockedProductAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = PostWhenReadyAsync(start.Task, app.Staff, IssuePath, productId, 6, "Concurrent dispatch A", Guid.NewGuid());
        var second = PostWhenReadyAsync(start.Task, app.Staff, IssuePath, productId, 6, "Concurrent dispatch B", Guid.NewGuid());
        start.SetResult();
        var responses = await Task.WhenAll(first, second);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var succeeded = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflicted = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var result = await SuccessAsync(succeeded);
        await ProblemAsync(conflicted, HttpStatusCode.Conflict);
        Assert.Equal(10, result["quantityBefore"]!.GetValue<int>());
        Assert.Equal(4, result["quantityAfter"]!.GetValue<int>());
        await AssertStockAsync(productId, quantity: 4, movementCount: 2);
        await using var database = app.NewContext();
        Assert.Equal(1, await database.StockMovements.CountAsync(item => item.ProductId == productId && item.Type == "Issue"));
    }

    [Fact]
    public async Task Concurrent_identical_request_ids_execute_once_and_return_one_replay()
    {
        var productId = await CreateStockedProductAsync();
        var requestId = Guid.NewGuid();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = PostWhenReadyAsync(start.Task, app.Staff, IssuePath, productId, 6, "Same dispatch", requestId);
        var second = PostWhenReadyAsync(start.Task, app.Staff, IssuePath, productId, 6, "Same dispatch", requestId);
        start.SetResult();
        var responses = await Task.WhenAll(first, second);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        var results = await Task.WhenAll(responses.Select(SuccessAsync));

        Assert.Single(results, result => result["replayed"]!.GetValue<bool>());
        Assert.Single(results, result => !result["replayed"]!.GetValue<bool>());
        Assert.Equal(results[0]["movementId"]!.GetValue<int>(), results[1]["movementId"]!.GetValue<int>());
        Assert.All(results, result =>
        {
            Assert.Equal(requestId, result["requestId"]!.GetValue<Guid>());
            Assert.Equal(10, result["quantityBefore"]!.GetValue<int>());
            Assert.Equal(4, result["quantityAfter"]!.GetValue<int>());
        });
        await AssertStockAsync(productId, quantity: 4, movementCount: 2);
        await using var database = app.NewContext();
        Assert.Equal(1, await database.StockMovements.CountAsync(item => item.RequestId == requestId));
    }

    [Fact]
    public async Task Identical_replay_returns_original_result_after_other_stock_changes()
    {
        var productId = await CreateStockedProductAsync();
        var requestId = Guid.NewGuid();
        using var originalResponse = await PostAsync(app.Admin, IssuePath, productId, 3, "Original dispatch", requestId);
        var original = await SuccessAsync(originalResponse);
        using var laterReceipt = await PostAsync(app.Admin, ReceiptPath, productId, 5, "Later delivery");
        await SuccessAsync(laterReceipt);
        using var replayResponse = await PostAsync(app.Admin, IssuePath, productId, 3, "Original dispatch", requestId);
        var replay = await SuccessAsync(replayResponse);

        Assert.True(replay["replayed"]!.GetValue<bool>());
        foreach (var name in new[] { "movementId", "productId", "type", "quantity", "quantityBefore", "quantityAfter", "reason", "createdBy", "createdAt", "requestId" })
            Assert.True(JsonNode.DeepEquals(original[name], replay[name]), $"Replayed field '{name}' differs from the original movement.");
        Assert.Equal(10, replay["quantityBefore"]!.GetValue<int>());
        Assert.Equal(7, replay["quantityAfter"]!.GetValue<int>());
        await AssertStockAsync(productId, quantity: 12, movementCount: 3);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("reason")]
    [InlineData("type")]
    [InlineData("product")]
    public async Task Reusing_a_request_id_with_different_payload_is_a_conflict(string changedField)
    {
        var productId = await CreateStockedProductAsync();
        var otherProductId = await CreateStockedProductAsync();
        var requestId = Guid.NewGuid();
        using var originalResponse = await PostAsync(app.Admin, IssuePath, productId, 2, "Original payload", requestId);
        await SuccessAsync(originalResponse);
        using var response = await PostAsync(app.Admin,
            changedField == "type" ? ReceiptPath : IssuePath,
            changedField == "product" ? otherProductId : productId,
            changedField == "quantity" ? 3 : 2,
            changedField == "reason" ? "Different payload" : "Original payload", requestId);

        await ProblemAsync(response, HttpStatusCode.Conflict);
        await AssertStockAsync(productId, quantity: 8, movementCount: 2);
        await AssertStockAsync(otherProductId, quantity: 10, movementCount: 1);
        await using var database = app.NewContext();
        Assert.Equal(1, await database.StockMovements.CountAsync(item => item.RequestId == requestId));
    }

    [Fact]
    public async Task Another_user_cannot_replay_an_existing_request_id()
    {
        var productId = await CreateStockedProductAsync();
        var requestId = Guid.NewGuid();
        using var original = await PostAsync(app.Admin, IssuePath, productId, 2, "Owned dispatch", requestId);
        await SuccessAsync(original);
        using var otherUser = await PostAsync(app.Staff, IssuePath, productId, 2, "Owned dispatch", requestId);

        await ProblemAsync(otherUser, HttpStatusCode.Conflict);
        await AssertStockAsync(productId, quantity: 8, movementCount: 2);
    }

    [Fact]
    public async Task Adjustment_requires_admin_and_sets_the_target_including_zero()
    {
        var productId = await CreateStockedProductAsync();
        using var staff = await PostAsync(app.Staff, AdjustmentPath, productId, 4, "Physical stock count");
        await ProblemAsync(staff, HttpStatusCode.Forbidden);
        await AssertStockAsync(productId, quantity: 10, movementCount: 1);

        using var adjustment = await PostAsync(app.Admin, AdjustmentPath, productId, 4, "Physical stock count");
        var result = await SuccessAsync(adjustment);
        Assert.Equal("Adjustment", result["type"]!.GetValue<string>());
        Assert.Equal(10, result["quantityBefore"]!.GetValue<int>());
        Assert.Equal(4, result["quantityAfter"]!.GetValue<int>());
        await AssertStockAsync(productId, quantity: 4, movementCount: 2);

        using var toZero = await PostAsync(app.Admin, AdjustmentPath, productId, 0, "Count confirmed empty");
        var zero = await SuccessAsync(toZero);
        Assert.Equal(4, zero["quantityBefore"]!.GetValue<int>());
        Assert.Equal(0, zero["quantityAfter"]!.GetValue<int>());
        await AssertStockAsync(productId, quantity: 0, movementCount: 3);
    }

    [Theory]
    [InlineData(ReceiptPath, 0)]
    [InlineData(ReceiptPath, -1)]
    [InlineData(IssuePath, 0)]
    [InlineData(IssuePath, -1)]
    [InlineData(AdjustmentPath, -1)]
    public async Task Invalid_quantity_is_rejected_without_creating_a_movement(string path, int quantity)
    {
        var productId = await CreateProductAsync();
        using var response = await PostAsync(app.Admin, path, productId, quantity, "Invalid quantity");
        await ProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertStockAsync(productId, quantity: 0, movementCount: 0);
    }

    [Fact]
    public async Task Empty_request_id_is_rejected_without_modifying_inventory()
    {
        var productId = await CreateProductAsync();
        using var response = await PostAsync(app.Admin, ReceiptPath, productId, 1, "Missing idempotency key", Guid.Empty);
        await ProblemAsync(response, HttpStatusCode.BadRequest);
        await AssertStockAsync(productId, quantity: 0, movementCount: 0);
    }

    [Theory]
    [InlineData(ReceiptPath)]
    [InlineData(IssuePath)]
    [InlineData(AdjustmentPath)]
    public async Task Missing_product_returns_not_found(string path)
    {
        using var response = await PostAsync(app.Admin, path, int.MaxValue, 1, "Missing product");
        await ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(ReceiptPath)]
    [InlineData(IssuePath)]
    [InlineData(AdjustmentPath)]
    public async Task Inactive_product_rejects_stock_mutation_without_losing_existing_stock(string path)
    {
        var productId = await CreateStockedProductAsync();
        using var deactivate = await app.Admin.DeleteAsync($"/api/products/{productId}");
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        using var response = await PostAsync(app.Admin, path, productId, 1, "Inactive product operation");
        await ProblemAsync(response, HttpStatusCode.Conflict);
        await AssertStockAsync(productId, quantity: 10, movementCount: 1);
    }

    [Theory]
    [InlineData(ReceiptPath)]
    [InlineData(IssuePath)]
    [InlineData(AdjustmentPath)]
    public async Task Stock_mutations_require_authentication(string path)
    {
        using var anonymous = app.Anonymous();
        using var response = await PostAsync(anonymous, path, 1, 1, "Anonymous operation");
        await ProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Catalog_csv_roundtrip_preserves_nonzero_stock_and_its_movement_history()
    {
        var productId = await CreateStockedProductAsync();
        await using var database = app.NewContext();
        var product = await database.Products.AsNoTracking().SingleAsync(item => item.Id == productId);
        var csv = await app.Admin.GetByteArrayAsync("/api/products/export?search=" + Uri.EscapeDataString(product.Sku));
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(csv), "file", "stock-roundtrip.csv");
        using var imported = await app.Admin.PostAsync("/api/products/import", form);
        imported.EnsureSuccessStatusCode();
        await AssertStockAsync(productId, quantity: 10, movementCount: 1);
    }

    private async Task<int> CreateProductAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var categoryResponse = await app.Admin.PostAsJsonAsync("/api/categories", new
        {
            name = "Stock category " + suffix, description = "Isolated stock operation test"
        });
        categoryResponse.EnsureSuccessStatusCode();
        var category = (await categoryResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        using var productResponse = await app.Admin.PostAsJsonAsync("/api/products", new
        {
            categoryId = category["id"]!.GetValue<int>(), sku = "STOCK-" + suffix.ToUpperInvariant(),
            name = "Stock test product", description = "Isolated stock operation test", price = 20m,
            costPrice = 10m, minStock = 5, imageUrl = (string?)null
        });
        productResponse.EnsureSuccessStatusCode();
        return (await productResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
    }

    private async Task<int> CreateStockedProductAsync()
    {
        var productId = await CreateProductAsync();
        using var receipt = await PostAsync(app.Admin, ReceiptPath, productId, 10, "Test opening delivery");
        await SuccessAsync(receipt);
        return productId;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, int productId,
        int quantity, string reason, Guid? requestId = null) =>
        client.PostAsJsonAsync(path, new { productId, quantity, reason, requestId = requestId ?? Guid.NewGuid() });

    private static async Task<HttpResponseMessage> PostWhenReadyAsync(Task ready, HttpClient client,
        string path, int productId, int quantity, string reason, Guid requestId)
    {
        await ready;
        return await PostAsync(client, path, productId, quantity, reason, requestId);
    }

    private static async Task<JsonObject> SuccessAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal((int)expected, problem["status"]!.GetValue<int>());
        Assert.False(string.IsNullOrWhiteSpace(problem["title"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(problem["detail"]!.GetValue<string>()));
    }

    private async Task AssertStockAsync(int productId, int quantity, int movementCount)
    {
        await using var database = app.NewContext();
        Assert.Equal(quantity, (await database.Inventories.AsNoTracking().SingleAsync(item => item.ProductId == productId)).Quantity);
        Assert.Equal(movementCount, await database.StockMovements.CountAsync(item => item.ProductId == productId));
    }
}
