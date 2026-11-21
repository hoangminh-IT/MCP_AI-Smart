internal sealed record LoginResult(string AccessToken, LoginUser? User);
internal sealed record LoginUser(string Role);
internal sealed record SeedProduct(int Id, string Sku, int Quantity);
internal sealed record ProductPage(List<SeedProduct> Items, int TotalCount);
internal sealed record ReceiptResult(int MovementId, int QuantityBefore, int QuantityAfter, bool Replayed);
