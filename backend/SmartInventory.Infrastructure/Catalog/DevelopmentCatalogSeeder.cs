using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Catalog;

public sealed class DevelopmentCatalogSeeder(AppDbContext database)
{
    public async Task<int> SeedAsync(CancellationToken cancellationToken)
    {
        var samples = new (string Sku, string Name, string Category, decimal Price, decimal Cost)[]
        {
            ("SP001", "Logitech MX Master 3S", "Peripherals", 99.99m, 68m),
            ("SP002", "Logitech MX Keys S", "Peripherals", 109.99m, 76m),
            ("SP003", "Keychron K2 Keyboard", "Peripherals", 89m, 58m),
            ("SP004", "Razer DeathAdder V3", "Peripherals", 69.99m, 44m),
            ("SP005", "Dell P2425H Monitor", "Computers", 229m, 172m),
            ("SP006", "LG 27UP850 Monitor", "Computers", 399m, 305m),
            ("SP007", "ASUS ProArt PA278QV", "Computers", 289m, 210m),
            ("SP008", "Intel NUC Mini PC", "Computers", 549m, 420m),
            ("SP009", "Samsung 990 PRO 1TB SSD", "Storage", 119m, 84m),
            ("SP010", "Samsung T7 Shield 1TB", "Storage", 109m, 78m),
            ("SP011", "WD Blue 2TB Hard Drive", "Storage", 69m, 48m),
            ("SP012", "Seagate IronWolf 4TB", "Storage", 109m, 79m),
            ("SP013", "Kingston DataTraveler 128GB", "Storage", 19.99m, 11m),
            ("SP014", "SanDisk Extreme 256GB SD", "Storage", 39.99m, 26m),
            ("SP015", "TP-Link Archer AX55", "Networking", 109m, 76m),
            ("SP016", "ASUS RT-AX58U Router", "Networking", 159m, 115m),
            ("SP017", "TP-Link TL-SG108 Switch", "Networking", 29.99m, 19m),
            ("SP018", "Ubiquiti UniFi U6 Lite", "Networking", 99m, 72m),
            ("SP019", "Cat6 Ethernet Cable 3m", "Networking", 8.99m, 3.50m),
            ("SP020", "USB-C Ethernet Adapter", "Networking", 24.99m, 13m),
            ("SP021", "Anker USB-C Hub 7-in-1", "Accessories", 49.99m, 31m),
            ("SP022", "UGREEN USB-C Cable 2m", "Accessories", 12.99m, 6m),
            ("SP023", "Anker Nano 65W Charger", "Accessories", 39.99m, 24m),
            ("SP024", "Belkin HDMI 2.1 Cable", "Accessories", 24.99m, 13m),
            ("SP025", "Laptop Stand Aluminum", "Accessories", 29.99m, 15m),
            ("SP026", "Desk Cable Organizer", "Accessories", 9.99m, 4m),
            ("SP027", "Logitech C920 Webcam", "Peripherals", 69.99m, 45m),
            ("SP028", "Jabra Evolve2 40 Headset", "Peripherals", 119m, 82m),
            ("SP029", "HyperX Cloud III Headset", "Peripherals", 99m, 66m),
            ("SP030", "Blue Yeti USB Microphone", "Peripherals", 129m, 87m),
            ("SP031", "Crucial DDR5 32GB Kit", "Computers", 99m, 71m),
            ("SP032", "Corsair Vengeance DDR4 16GB", "Computers", 49.99m, 32m)
        };
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var categories = await database.Categories.OrderBy(category => category.Id).ToListAsync(cancellationToken);
        foreach (var name in samples.Select(sample => sample.Category).Distinct())
        {
            if (categories.Any(category => category.Name == name)) continue;
            var category = new Category { Name = name, Description = $"Development catalog: {name}." };
            database.Categories.Add(category);
            categories.Add(category);
        }
        await database.SaveChangesAsync(cancellationToken);
        var existingSkus = (await database.Products.AsNoTracking().Select(product => product.Sku)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<Product>();
        foreach (var sample in samples)
        {
            if (existingSkus.Contains(sample.Sku)) continue;
            var product = new Product
            {
                Sku = sample.Sku, Name = sample.Name,
                CategoryId = categories.First(category => category.Name == sample.Category).Id,
                Description = "Development sample product. Stock starts at zero; record receipts through inventory operations.",
                Price = sample.Price, CostPrice = sample.Cost, MinStock = 10
            };
            database.Products.Add(product);
            added.Add(product);
        }
        await database.SaveChangesAsync(cancellationToken);
        database.Inventories.AddRange(added.Select(product => new Inventory { ProductId = product.Id, Quantity = 0 }));
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return added.Count;
    }
}
