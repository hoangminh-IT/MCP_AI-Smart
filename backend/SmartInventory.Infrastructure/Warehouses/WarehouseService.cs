using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartInventory.Application.Authentication;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Warehouses;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Warehouses;

public sealed class WarehouseService(AppDbContext database) : IWarehouseService
{
    public async Task<PagedResult<WarehouseDto>> GetWarehousesAsync(WarehouseQuery query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue || query.Search?.Length > 200)
            throw new AppValidationException("Use positive pagination, pageSize up to 100 and search up to 200 characters.");
        var rows = database.Warehouses.AsNoTracking().AsQueryable();
        if (query.IsActive.HasValue) rows = rows.Where(item => item.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = "%" + query.Search.Trim() + "%";
            rows = rows.Where(item => EF.Functions.ILike(item.Code, pattern) || EF.Functions.ILike(item.Name, pattern) || item.Address != null && EF.Functions.ILike(item.Address, pattern));
        }
        var sorted = (query.SortBy?.Trim().ToLowerInvariant() ?? "name") switch
        {
            "code" => query.Descending ? rows.OrderByDescending(item => item.Code) : rows.OrderBy(item => item.Code),
            "name" => query.Descending ? rows.OrderByDescending(item => item.Name) : rows.OrderBy(item => item.Name),
            "createdat" => query.Descending ? rows.OrderByDescending(item => item.CreatedAt) : rows.OrderBy(item => item.CreatedAt),
            "updatedat" => query.Descending ? rows.OrderByDescending(item => item.UpdatedAt) : rows.OrderBy(item => item.UpdatedAt),
            _ => throw new AppValidationException("sortBy must be code, name, createdAt or updatedAt.")
        };
        var count = await rows.CountAsync(cancellationToken);
        var items = await sorted.ThenBy(item => item.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new WarehouseDto(item.Id, item.Code, item.Name, item.Address, item.IsActive, item.IsDefault, item.CreatedAt, item.UpdatedAt))
            .ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<WarehouseDto> GetWarehouseAsync(int id, CancellationToken cancellationToken) =>
        ToDto((await WarehouseAccess.ReadAsync(database, id, cancellationToken))!);

    public async Task<WarehouseDto> CreateAsync(int actorId, WarehouseRequest request, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(actorId, cancellationToken);
        Normalize(request);
        var warehouse = new Warehouse { Code = request.Code, Name = request.Name, Address = request.Address, IsActive = request.IsActive };
        database.Warehouses.Add(warehouse);
        await SaveAsync(cancellationToken);
        return ToDto(warehouse);
    }

    public async Task<WarehouseDto> UpdateAsync(int actorId, int id, WarehouseRequest request, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(actorId, cancellationToken);
        Normalize(request);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var warehouse = await LockAsync(id, cancellationToken);
        if (warehouse.IsDefault && (request.Code != warehouse.Code || !request.IsActive))
            throw new AppConflictException("The MAIN default warehouse must keep its code and remain active.");
        if (!request.IsActive) await CheckDeactivationAsync(warehouse, cancellationToken);
        warehouse.Code = request.Code;
        warehouse.Name = request.Name;
        warehouse.Address = request.Address;
        warehouse.IsActive = request.IsActive;
        warehouse.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(warehouse);
    }

    public async Task DeactivateAsync(int actorId, int id, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(actorId, cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var warehouse = await LockAsync(id, cancellationToken);
        await CheckDeactivationAsync(warehouse, cancellationToken);
        if (warehouse.IsActive)
        {
            warehouse.IsActive = false;
            warehouse.UpdatedAt = DateTime.UtcNow;
            await SaveAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CheckDeactivationAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        if (warehouse.IsDefault) throw new AppConflictException("The MAIN default warehouse cannot be deactivated.");
        if (await database.WarehouseInventories.AnyAsync(item => item.WarehouseId == warehouse.Id && item.Quantity != 0, cancellationToken) ||
            await database.PurchaseOrders.AnyAsync(item => item.WarehouseId == warehouse.Id && item.Status != "Received" && item.Status != "Cancelled", cancellationToken) ||
            await database.SalesOrders.AnyAsync(item => item.WarehouseId == warehouse.Id && item.Status != "Fulfilled" && item.Status != "Cancelled", cancellationToken))
            throw new AppConflictException("A warehouse with stock or open orders cannot be deactivated. Move stock and complete or cancel its orders first.");
    }

    private async Task<Warehouse> LockAsync(int id, CancellationToken cancellationToken)
    {
        var row = await database.Warehouses.FromSqlInterpolated($"SELECT * FROM warehouses WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new AppNotFoundException("Warehouse was not found.");
        await database.Entry(row).ReloadAsync(cancellationToken);
        return row;
    }
    private async Task RequireAdminAsync(int actorId, CancellationToken cancellationToken)
    {
        if (!await database.Users.AsNoTracking().AnyAsync(user => user.Id == actorId && user.IsActive && user.Role == UserRoles.Admin, cancellationToken))
            throw new AppForbiddenException("An active administrator account is required.");
    }
    private static void Normalize(WarehouseRequest request)
    {
        request.Code = request.Code?.Trim().ToUpperInvariant() ?? "";
        request.Name = request.Name?.Trim() ?? "";
        request.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        if (request.Code.Length is < 1 or > 40 || request.Name.Length is < 1 or > 200 || request.Address?.Length > 1000)
            throw new AppValidationException("Use code 1..40 characters, name 1..200 characters and address up to 1,000 characters.");
    }
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new AppConflictException("A warehouse with this code already exists."); }
    }
    private static WarehouseDto ToDto(Warehouse row) => new(row.Id, row.Code, row.Name, row.Address, row.IsActive, row.IsDefault, row.CreatedAt, row.UpdatedAt);
}
