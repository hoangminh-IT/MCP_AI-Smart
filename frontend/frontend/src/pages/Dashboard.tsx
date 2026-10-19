import { useEffect, useState } from 'react';
import { Alert, Box, Button } from '@mui/material';
import CategoryIcon from '@mui/icons-material/Category';
import InventoryIcon from '@mui/icons-material/Inventory';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import PaidIcon from '@mui/icons-material/Paid';
import StatCard from '../components/StatCard';
import { errorMessage } from '../api/client';
import { listInventory } from '../api/inventory';
import { listProducts } from '../api/products';

interface Stats {
  skus: number;
  totalQty: number;
  lowStock: number;
  value: number;
}

const vnd = (n: number) => n.toLocaleString('vi-VN') + ' ₫';

export default function Dashboard() {
  const [stats, setStats] = useState<Stats | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [prod, inv] = await Promise.all([listProducts({ pageSize: 1000 }), listInventory()]);
      const qty = new Map(inv.map((i) => [i.productId, i.quantity]));
      let totalQty = 0, lowStock = 0, value = 0;
      for (const p of prod.items) {
        const q = qty.get(p.id) ?? 0;
        totalQty += q;
        value += q * p.price;
        if (q < p.minStock) lowStock++;
      }
      setStats({ skus: prod.items.length, totalQty, lowStock, value });
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  return (
    <>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={load}>Thử lại</Button>}>
          {error}
        </Alert>
      )}
      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(4, 1fr)' } }}>
        <StatCard label="Tổng SKU" value={String(stats?.skus ?? 0)} icon={<CategoryIcon fontSize="inherit" />} loading={loading} />
        <StatCard label="Tổng tồn kho" value={String(stats?.totalQty ?? 0)} icon={<InventoryIcon fontSize="inherit" />} loading={loading} color="success.main" />
        <StatCard label="Sắp hết hàng" value={String(stats?.lowStock ?? 0)} icon={<WarningAmberIcon fontSize="inherit" />} loading={loading} color="warning.main" />
        <StatCard label="Giá trị tồn kho" value={vnd(stats?.value ?? 0)} icon={<PaidIcon fontSize="inherit" />} loading={loading} color="info.main" />
      </Box>
    </>
  );
}
