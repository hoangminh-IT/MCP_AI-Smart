using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartInventory.Application.Entities;
using Xunit;

namespace SmartInventory.IntegrationTests;

public sealed class CatalogAndAuthTests(InventoryApiFactory app) : IClassFixture<InventoryApiFactory>
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private async Task<JsonObject> CategoryAsync()
    {
        var response = await app.Admin.PostAsJsonAsync("/api/categories", new { name = Unique("Category "), description = "Created by an integration test" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    private static object ProductInput(int categoryId, string sku, string name = "Integration Product", decimal price = 25.5m) => new
    {
        categoryId, sku, name, description = "Integration description", price, costPrice = 12m,
        minStock = 5, imageUrl = (string?)null
    };

    private async Task<JsonObject> ProductAsync(int categoryId, string? sku = null)
    {
        var response = await app.Admin.PostAsJsonAsync("/api/products", ProductInput(categoryId, sku ?? Unique("TEST-")));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task Catalog_requires_authentication_and_staff_cannot_mutate()
    {
        using var anonymous = app.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Staff.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Staff.PostAsJsonAsync("/api/categories", new { name = Unique("Denied") })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Staff.PostAsJsonAsync("/api/products", ProductInput(1, Unique("DENIED")))).StatusCode);
    }

    [Fact]
    public async Task Malformed_token_cannot_access_catalog()
    {
        using var anonymous = app.Anonymous();
        anonymous.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.value");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/products")).StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("sortBy=untrusted")]
    public async Task Invalid_catalog_query_returns_validation_error(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Staff.GetAsync("/api/products?" + query)).StatusCode);
    }

    [Fact]
    public async Task Login_rejects_bad_password_and_inactive_user()
    {
        using var anonymous = app.Anonymous();
        var bad = await anonymous.PostAsJsonAsync("/api/auth/login", new { username = "integration_admin", password = "invalid-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        var username = Unique("inactive");
        await app.AddUserAsync(username);
        await using (var database = app.NewContext())
        {
            var user = await database.Users.SingleAsync(u => u.Username == username);
            user.IsActive = false;
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { username, password = InventoryApiFactory.Password })).StatusCode);
    }

    [Fact]
    public async Task Product_creation_persists_data_and_initializes_zero_stock_atomically()
    {
        var category = await CategoryAsync();
        var product = await ProductAsync(category["id"]!.GetValue<int>());
        Assert.Equal(0, product["quantity"]!.GetValue<int>());
        await using var database = app.NewContext();
        var id = product["id"]!.GetValue<int>();
        Assert.Equal(25.5m, (await database.Products.SingleAsync(p => p.Id == id)).Price);
        Assert.Equal(0, (await database.Inventories.SingleAsync(i => i.ProductId == id)).Quantity);
        Assert.Empty(await database.StockMovements.Where(m => m.ProductId == id).ToListAsync());
        var detail = await app.Admin.GetFromJsonAsync<JsonObject>($"/api/products/{id}");
        Assert.Equal(category["name"]!.GetValue<string>(), detail!["categoryName"]!.GetValue<string>());
    }

    [Fact]
    public async Task Duplicate_sku_returns_conflict_and_keeps_one_product()
    {
        var category = await CategoryAsync();
        var sku = Unique("UNIQUE-");
        await ProductAsync(category["id"]!.GetValue<int>(), sku);
        var duplicate = await app.Admin.PostAsJsonAsync("/api/products", ProductInput(category["id"]!.GetValue<int>(), sku));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using var database = app.NewContext();
        Assert.Equal(1, await database.Products.CountAsync(p => p.Sku == sku));
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(10, -1)]
    public async Task Product_rejects_negative_prices_or_minimum_stock(decimal price, int minStock)
    {
        var category = await CategoryAsync();
        var response = await app.Admin.PostAsJsonAsync("/api/products", new
        {
            categoryId = category["id"]!.GetValue<int>(), sku = Unique("BAD-"), name = "Invalid product",
            price, costPrice = 0, minStock
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_filter_sort_and_pagination_use_persisted_data()
    {
        var category = await CategoryAsync();
        var categoryId = category["id"]!.GetValue<int>();
        var prefix = Unique("PAGE-");
        for (var i = 0; i < 3; i++)
        {
            var response = await app.Admin.PostAsJsonAsync("/api/products", ProductInput(categoryId, prefix + i, "Paging " + i, 10 + i));
            response.EnsureSuccessStatusCode();
        }
        var page = await app.Staff.GetFromJsonAsync<JsonObject>($"/api/products?search={prefix}&categoryId={categoryId}&page=2&pageSize=1&sortBy=price&descending=true");
        Assert.Equal(3, page!["totalCount"]!.GetValue<int>());
        Assert.Equal(2, page["page"]!.GetValue<int>());
        var item = Assert.Single(page["items"]!.AsArray());
        Assert.Equal(11m, item!["price"]!.GetValue<decimal>());
        Assert.Equal(categoryId, item["categoryId"]!.GetValue<int>());
    }

    [Fact]
    public async Task Category_delete_is_blocked_until_empty_and_product_count_is_real()
    {
        var category = await CategoryAsync();
        var id = category["id"]!.GetValue<int>();
        await ProductAsync(id);
        Assert.Equal(HttpStatusCode.Conflict, (await app.Admin.DeleteAsync($"/api/categories/{id}")).StatusCode);
        var detail = await app.Admin.GetFromJsonAsync<JsonObject>($"/api/categories/{id}");
        Assert.Equal(1, detail!["productCount"]!.GetValue<int>());
        var empty = await CategoryAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await app.Admin.DeleteAsync($"/api/categories/{empty["id"]!.GetValue<int>()}")).StatusCode);
    }

    [Fact]
    public async Task Product_updates_are_persisted_and_delete_deactivates_without_losing_stock_row()
    {
        var category = await CategoryAsync();
        var product = await ProductAsync(category["id"]!.GetValue<int>());
        var id = product["id"]!.GetValue<int>();
        var update = await app.Admin.PutAsJsonAsync($"/api/products/{id}", ProductInput(category["id"]!.GetValue<int>(), product["sku"]!.GetValue<string>(), "Updated name", 99m));
        update.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await app.Admin.DeleteAsync($"/api/products/{id}")).StatusCode);
        var page = await app.Admin.GetFromJsonAsync<JsonObject>($"/api/products?search={product["sku"]!.GetValue<string>()}");
        Assert.Equal(0, page!["totalCount"]!.GetValue<int>());
        var all = await app.Admin.GetFromJsonAsync<JsonObject>($"/api/products?includeInactive=true&search={product["sku"]!.GetValue<string>()}");
        Assert.Equal(1, all!["totalCount"]!.GetValue<int>());
        await using var database = app.NewContext();
        var stored = await database.Products.SingleAsync(p => p.Id == id);
        Assert.False(stored.IsActive);
        Assert.Equal("Updated name", stored.Name);
        Assert.Equal(99m, stored.Price);
        Assert.True(await database.Inventories.AnyAsync(i => i.ProductId == id));
    }

    [Fact]
    public async Task Database_itself_rejects_negative_stock_and_orphan_inventory()
    {
        var category = await CategoryAsync();
        var product = await ProductAsync(category["id"]!.GetValue<int>());
        await using (var database = app.NewContext())
        {
            var inventory = await database.Inventories.SingleAsync(i => i.ProductId == product["id"]!.GetValue<int>());
            inventory.Quantity = -1;
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }
        await using (var database = app.NewContext())
        {
            database.Inventories.Add(new Inventory { ProductId = int.MaxValue, Quantity = 0 });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        }
    }

    [Fact]
    public async Task Profile_password_change_and_logout_revoke_previous_tokens()
    {
        var username = Unique("profile");
        await app.AddUserAsync(username);
        using var client = await app.LoginAsync(username);
        var profile = await client.PutAsJsonAsync("/api/auth/profile", new { email = "updated@example.test", fullName = "Updated Full Name" });
        profile.EnsureSuccessStatusCode();
        var me = await client.GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.Equal("Updated Full Name", me!["fullName"]!.GetValue<string>());
        var change = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = InventoryApiFactory.Password, newPassword = "Changed-Integration-2026!" });
        change.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        using var fresh = await app.LoginAsync(username, "Changed-Integration-2026!");
        (await fresh.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Active_token_is_rejected_after_database_account_deactivation()
    {
        var username = Unique("deactivate");
        await app.AddUserAsync(username);
        using var client = await app.LoginAsync(username);
        await using (var database = app.NewContext())
        {
            var user = await database.Users.SingleAsync(u => u.Username == username);
            user.IsActive = false;
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Csv_import_is_atomic_and_export_contains_real_values()
    {
        var category = await CategoryAsync();
        var id = category["id"]!.GetValue<int>();
        var sku = Unique("CSV-");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent($"sku,name,categoryId,description,price,costPrice,minStock,imageUrl\n{sku},Imported product,{id},\"Description, with comma\",30.5,15,3,\n", Encoding.UTF8, "text/csv"), "file", "products.csv");
        (await app.Admin.PostAsync("/api/products/import", content)).EnsureSuccessStatusCode();
        await using (var database = app.NewContext())
        {
            var product = await database.Products.SingleAsync(p => p.Sku == sku);
            Assert.Equal("Description, with comma", product.Description);
            Assert.Equal(0, (await database.Inventories.SingleAsync(i => i.ProductId == product.Id)).Quantity);
        }
        var exported = await app.Staff.GetStringAsync("/api/products/export");
        Assert.Contains(sku, exported);
        var invalidSku = Unique("ROLLBACK-");
        using var invalid = new MultipartFormDataContent();
        invalid.Add(new StringContent($"sku,name,categoryId,description,price,costPrice,minStock,imageUrl\n{invalidSku},Should rollback,{id},Valid,10,5,2,\n{invalidSku}-bad,Bad,{id},Invalid,-1,5,2,\n", Encoding.UTF8, "text/csv"), "file", "invalid.csv");
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin.PostAsync("/api/products/import", invalid)).StatusCode);
        await using var check = app.NewContext();
        Assert.False(await check.Products.AnyAsync(p => p.Sku == invalidSku));
    }

    [Fact]
    public async Task Export_import_roundtrip_preserves_formula_like_text_and_existing_inventory()
    {
        var category = await CategoryAsync();
        var categoryId = category["id"]!.GetValue<int>();
        var sku = Unique("ROUNDTRIP-");
        var created = await app.Admin.PostAsJsonAsync("/api/products", ProductInput(categoryId, sku, "=Formula-like name"));
        created.EnsureSuccessStatusCode();
        var csv = await app.Admin.GetByteArrayAsync("/api/products/export?search=" + sku);
        Assert.Contains("'=Formula-like name", Encoding.UTF8.GetString(csv));
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(csv), "file", "roundtrip.csv");
        (await app.Admin.PostAsync("/api/products/import", form)).EnsureSuccessStatusCode();
        await using var database = app.NewContext();
        var stored = await database.Products.SingleAsync(p => p.Sku == sku);
        Assert.Equal("=Formula-like name", stored.Name);
        Assert.Equal(0, (await database.Inventories.SingleAsync(i => i.ProductId == stored.Id)).Quantity);
        Assert.Equal(1, await database.Products.CountAsync(p => p.Sku == sku));
    }

    [Fact]
    public async Task Valid_image_upload_persists_url_and_serves_uploaded_bytes()
    {
        var category = await CategoryAsync();
        var product = await ProductAsync(category["id"]!.GetValue<int>());
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+xmwAAAABJRU5ErkJggg==");
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "../../unsafe-name.png");
        var response = await app.Admin.PostAsync($"/api/products/{product["id"]!.GetValue<int>()}/image", content);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var url = result["imageUrl"]!.GetValue<string>();
        Assert.StartsWith("/uploads/products/", url);
        Assert.DoesNotContain("unsafe-name", url);
        using var anonymous = app.Anonymous();
        Assert.Equal(bytes, await anonymous.GetByteArrayAsync(url));
        await using var database = app.NewContext();
        Assert.Equal(url, (await database.Products.SingleAsync(p => p.Id == product["id"]!.GetValue<int>())).ImageUrl);
    }

    [Fact]
    public async Task Image_upload_rejects_disguised_executable_content()
    {
        var category = await CategoryAsync();
        var product = await ProductAsync(category["id"]!.GetValue<int>());
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("<script>alert('not-an-image')</script>"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "fake.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Admin.PostAsync($"/api/products/{product["id"]!.GetValue<int>()}/image", content)).StatusCode);
    }

    [Fact]
    public async Task Migration_history_matches_model_in_isolated_postgresql_schema()
    {
        await using var database = app.NewContext();
        Assert.Empty(await database.Database.GetPendingMigrationsAsync());
        Assert.False(database.Database.HasPendingModelChanges());
        Assert.True((await database.Database.GetAppliedMigrationsAsync()).Count() >= 3);
    }
}
