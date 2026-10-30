using System.ComponentModel.DataAnnotations;
using SmartInventory.Application.Catalog;

namespace SmartInventory.Application.Suppliers;

public sealed class SupplierQuery
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "name";
    public bool Descending { get; set; }
}

public sealed class SupplierRequest
{
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(160)] public string? ContactPerson { get; set; }
    [StringLength(254)] public string? Email { get; set; }
    [StringLength(40)] public string? Phone { get; set; }
    [StringLength(1000)] public string? Address { get; set; }
    [StringLength(40)] public string? TaxCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record SupplierDto(int Id, string Code, string Name, string? ContactPerson, string? Email,
    string? Phone, string? Address, string? TaxCode, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetSuppliersAsync(SupplierQuery query, CancellationToken cancellationToken);
    Task<SupplierDto> GetSupplierAsync(int id, CancellationToken cancellationToken);
    Task<SupplierDto> CreateSupplierAsync(SupplierRequest request, CancellationToken cancellationToken);
    Task<SupplierDto> UpdateSupplierAsync(int id, SupplierRequest request, CancellationToken cancellationToken);
    Task DeactivateSupplierAsync(int id, CancellationToken cancellationToken);
}
