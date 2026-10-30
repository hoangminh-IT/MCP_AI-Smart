using System.ComponentModel.DataAnnotations;
using SmartInventory.Application.Catalog;

namespace SmartInventory.Application.Warehouses;

public sealed class WarehouseQuery
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "name";
    public bool Descending { get; set; }
}
public sealed class WarehouseRequest
{
    [Required, StringLength(40)] public string Code { get; set; } = "";
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(1000)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed record WarehouseDto(int Id, string Code, string Name, string? Address, bool IsActive,
    bool IsDefault, DateTime CreatedAt, DateTime UpdatedAt);
public interface IWarehouseService
{
    Task<PagedResult<WarehouseDto>> GetWarehousesAsync(WarehouseQuery query, CancellationToken cancellationToken);
    Task<WarehouseDto> GetWarehouseAsync(int id, CancellationToken cancellationToken);
    Task<WarehouseDto> CreateAsync(int actorId, WarehouseRequest request, CancellationToken cancellationToken);
    Task<WarehouseDto> UpdateAsync(int actorId, int id, WarehouseRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(int actorId, int id, CancellationToken cancellationToken);
}
