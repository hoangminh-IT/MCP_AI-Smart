using SmartInventory.Application.Common;

namespace SmartInventory.Application.Purchasing;

public static class PurchaseRules
{
    public const decimal MaximumAmount = 9999999999999999.99m;

    public static decimal ValidateOrder(PurchaseOrderRequest request)
    {
        request.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (request.SupplierId <= 0 || request.Notes?.Length > 2000 || request.Items is null || request.Items.Count is < 1 or > 100)
            throw new AppValidationException("Select a supplier and provide 1 to 100 product lines; notes may contain at most 2,000 characters.");
        if (request.Items.Any(item => item is null || item.ProductId <= 0 || item.Quantity <= 0 || item.UnitCost < 0 ||
                item.UnitCost > MaximumAmount || decimal.Round(item.UnitCost, 2) != item.UnitCost))
            throw new AppValidationException("Each line requires a product, a positive integer quantity and a nonnegative unit cost with at most two decimal places.");
        if (request.Items.Select(item => item.ProductId).Distinct().Count() != request.Items.Count)
            throw new AppValidationException("A product may appear only once in a purchase order.");
        var total = request.Items.Sum(item => item.UnitCost * item.Quantity);
        if (total > MaximumAmount) throw new AppValidationException("The purchase order total exceeds the numeric(18,2) limit.");
        return total;
    }

    public static void ValidateReceipt(PurchaseReceiptRequest request)
    {
        request.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (request.RequestId == Guid.Empty || request.Reason?.Length > 1000 || request.Items is null || request.Items.Count is < 1 or > 100 ||
            request.Items.Any(item => item is null || item.PurchaseOrderItemId <= 0 || item.Quantity <= 0))
            throw new AppValidationException("A receipt requires a nonempty requestId and 1 to 100 positive line quantities; reason is limited to 1,000 characters.");
        if (request.Items.Select(item => item.PurchaseOrderItemId).Distinct().Count() != request.Items.Count)
            throw new AppValidationException("A purchase order item may appear only once in a receipt.");
    }
}
