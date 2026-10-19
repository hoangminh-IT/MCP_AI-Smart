import type { Category, InventoryItem, Product } from '../types';

const categories: Category[] = [
  { id: 1, name: 'Chuột & Bàn phím' },
  { id: 2, name: 'Màn hình' },
  { id: 3, name: 'Tai nghe' },
  { id: 4, name: 'Lưu trữ' },
  { id: 5, name: 'Phụ kiện' },
];

const seed: [number, string, string, number, number][] = [
  [1, 'LG-MX3S', 'Logitech MX Master 3S', 2490000, 10],
  [1, 'LG-K380', 'Logitech K380 Bluetooth', 790000, 10],
  [1, 'LG-G102', 'Logitech G102 Lightsync', 399000, 15],
  [1, 'LG-MK270', 'Logitech MK270 Combo', 550000, 10],
  [1, 'RZ-DAV3', 'Razer DeathAdder V3', 1590000, 8],
  [2, 'DL-U2723', 'Dell UltraSharp U2723QE 27"', 13990000, 5],
  [2, 'LG-27UP', 'LG 27UP850 4K 27"', 9990000, 5],
  [2, 'AS-VG249', 'ASUS VG249Q 24" 144Hz', 4290000, 6],
  [2, 'SS-S24', 'Samsung S24R350 24"', 2790000, 8],
  [3, 'SN-WH1000', 'Sony WH-1000XM5', 7990000, 5],
  [3, 'JB-T510', 'JBL Tune 510BT', 990000, 12],
  [3, 'LG-H390', 'Logitech H390 USB', 890000, 10],
  [3, 'AP-APP2', 'Apple AirPods Pro 2', 5990000, 6],
  [4, 'SS-T7-1T', 'Samsung T7 SSD 1TB', 2890000, 8],
  [4, 'WD-BLU-2T', 'WD Blue HDD 2TB', 1390000, 10],
  [4, 'KS-A400', 'Kingston A400 480GB', 890000, 15],
  [4, 'SD-CZ48', 'SanDisk Ultra USB 64GB', 199000, 20],
  [5, 'AK-65W', 'Anker Sạc nhanh 65W', 890000, 10],
  [5, 'UG-HUB6', 'UGREEN Hub USB-C 6in1', 690000, 10],
  [5, 'BL-CB2', 'Belkin Cáp USB-C 2m', 290000, 20],
];

const products: Product[] = seed.map(([categoryId, sku, name, price, minStock], i) => ({
  id: i + 1,
  categoryId,
  sku,
  name,
  price,
  minStock,
}));

// Một vài mặt hàng cố ý dưới ngưỡng để thấy cảnh báo low stock.
const lowStockIds = new Set([2, 5, 10, 16]);
const inventory: InventoryItem[] = products.map((p) => ({
  productId: p.id,
  quantity: lowStockIds.has(p.id) ? 3 : 12 + ((p.id * 7) % 40),
}));

export const mockDb = { categories, products, inventory };

export const delay = (ms = 250) => new Promise((r) => setTimeout(r, ms));
