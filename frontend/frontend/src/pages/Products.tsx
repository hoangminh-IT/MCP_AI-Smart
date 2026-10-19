import { useCallback, useEffect, useState } from 'react';
import {
  Alert, Box, Button, Chip, IconButton, InputAdornment, LinearProgress, Paper, Snackbar, Table, TableBody,
  TableCell, TableContainer, TableHead, TablePagination, TableRow, TextField, Tooltip, Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import VisibilityOffIcon from '@mui/icons-material/VisibilityOff';
import SearchIcon from '@mui/icons-material/Search';
import ConfirmDialog from '../components/ConfirmDialog';
import ProductFormDialog from '../components/ProductFormDialog';
import { errorMessage } from '../api/client';
import { listCategories } from '../api/categories';
import { listInventory } from '../api/inventory';
import { hideProduct, listProducts } from '../api/products';
import type { Category, Product } from '../types';

const vnd = (n: number) => n.toLocaleString('vi-VN') + ' ₫';

export default function Products() {
  const [rows, setRows] = useState<Product[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [categories, setCategories] = useState<Category[]>([]);
  const [stock, setStock] = useState<Record<number, number>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [hiding, setHiding] = useState<Product | null>(null);
  const [hideBusy, setHideBusy] = useState(false);
  const [toast, setToast] = useState<{ msg: string; severity: 'success' | 'error' } | null>(null);

  const catName = (id: number) => categories.find((c) => c.id === id)?.name ?? `#${id}`;

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [res, inv] = await Promise.all([listProducts({ search, page, pageSize }), listInventory()]);
      setRows(res.items);
      setTotal(res.total);
      setStock(Object.fromEntries(inv.map((i) => [i.productId, i.quantity])));
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setLoading(false);
    }
  }, [search, page, pageSize]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    listCategories().then(setCategories).catch((e) => setError(errorMessage(e)));
  }, []);

  // Debounce ô tìm kiếm
  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(searchInput);
      setPage(0);
    }, 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const openAdd = () => { setEditing(null); setFormOpen(true); };
  const openEdit = (p: Product) => { setEditing(p); setFormOpen(true); };

  const onSaved = (msg: string) => {
    setFormOpen(false);
    setToast({ msg, severity: 'success' });
    load();
  };

  const confirmHide = async () => {
    if (!hiding) return;
    setHideBusy(true);
    try {
      await hideProduct(hiding.id);
      setToast({ msg: `Đã ẩn "${hiding.name}"`, severity: 'success' });
      setHiding(null);
      // Nếu ẩn dòng cuối của trang cuối thì lùi 1 trang
      if (rows.length === 1 && page > 0) setPage(page - 1);
      else load();
    } catch (e) {
      setToast({ msg: errorMessage(e), severity: 'error' });
      setHiding(null);
    } finally {
      setHideBusy(false);
    }
  };

  return (
    <>
      <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
        <TextField
          size="small"
          placeholder="Tìm theo tên hoặc SKU…"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          sx={{ flexGrow: 1, maxWidth: 420 }}
          InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> }}
        />
        <Box sx={{ flexGrow: 1 }} />
        <Button variant="contained" startIcon={<AddIcon />} onClick={openAdd}>Thêm sản phẩm</Button>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" size="small" onClick={load}>Thử lại</Button>}>
          {error}
        </Alert>
      )}

      <Paper variant="outlined">
        {loading && <LinearProgress />}
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Mã SKU</TableCell>
                <TableCell>Tên sản phẩm</TableCell>
                <TableCell>Danh mục</TableCell>
                <TableCell align="right">Giá</TableCell>
                <TableCell align="right">Tồn</TableCell>
                <TableCell align="right">Thao tác</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((p) => {
                const qty = stock[p.id] ?? 0;
                return (
                  <TableRow key={p.id} hover>
                    <TableCell>{p.sku}</TableCell>
                    <TableCell>{p.name}</TableCell>
                    <TableCell>{catName(p.categoryId)}</TableCell>
                    <TableCell align="right">{vnd(p.price)}</TableCell>
                    <TableCell align="right">
                      {qty < p.minStock ? <Chip size="small" color="warning" label={`${qty} • sắp hết`} /> : qty}
                    </TableCell>
                    <TableCell align="right">
                      <Tooltip title="Sửa"><IconButton size="small" onClick={() => openEdit(p)}><EditIcon fontSize="small" /></IconButton></Tooltip>
                      <Tooltip title="Ẩn"><IconButton size="small" onClick={() => setHiding(p)}><VisibilityOffIcon fontSize="small" /></IconButton></Tooltip>
                    </TableCell>
                  </TableRow>
                );
              })}
              {!loading && rows.length === 0 && !error && (
                <TableRow>
                  <TableCell colSpan={6} align="center" sx={{ py: 5 }}>
                    <Typography color="text.secondary">
                      {search ? `Không có sản phẩm khớp "${search}"` : 'Chưa có sản phẩm nào'}
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
        <TablePagination
          component="div"
          count={total}
          page={page}
          rowsPerPage={pageSize}
          rowsPerPageOptions={[5, 10, 25]}
          labelRowsPerPage="Số dòng"
          onPageChange={(_, p) => setPage(p)}
          onRowsPerPageChange={(e) => { setPageSize(Number(e.target.value)); setPage(0); }}
        />
      </Paper>

      <ProductFormDialog open={formOpen} product={editing} categories={categories} onClose={() => setFormOpen(false)} onSaved={onSaved} />

      <ConfirmDialog
        open={!!hiding}
        title="Ẩn sản phẩm"
        message={`Ẩn "${hiding?.name ?? ''}" khỏi danh sách? Lịch sử kho vẫn được giữ lại.`}
        confirmText="Ẩn"
        busy={hideBusy}
        onConfirm={confirmHide}
        onClose={() => setHiding(null)}
      />

      <Snackbar open={!!toast} autoHideDuration={3500} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}>
        {toast ? <Alert severity={toast.severity} variant="filled" onClose={() => setToast(null)}>{toast.msg}</Alert> : undefined}
      </Snackbar>
    </>
  );
}
