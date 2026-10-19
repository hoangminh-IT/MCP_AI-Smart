import { useEffect, useState } from 'react';
import {
  Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField,
} from '@mui/material';
import { createProduct, updateProduct } from '../api/products';
import { errorMessage } from '../api/client';
import type { Category, Product } from '../types';

interface Props {
  open: boolean;
  product: Product | null; // null = thêm mới
  categories: Category[];
  onClose: () => void;
  onSaved: (message: string) => void;
}

interface FormState {
  sku: string;
  name: string;
  categoryId: string;
  price: string;
  minStock: string;
}

const empty: FormState = { sku: '', name: '', categoryId: '', price: '', minStock: '10' };

export default function ProductFormDialog({ open, product, categories, onClose, onSaved }: Props) {
  const [form, setForm] = useState<FormState>(empty);
  const [errors, setErrors] = useState<Partial<Record<keyof FormState, string>>>({});
  const [apiError, setApiError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setApiError(null);
    setForm(
      product
        ? {
            sku: product.sku,
            name: product.name,
            categoryId: String(product.categoryId),
            price: String(product.price),
            minStock: String(product.minStock),
          }
        : empty,
    );
  }, [open, product]);

  const set = (k: keyof FormState) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  const validate = () => {
    const e: Partial<Record<keyof FormState, string>> = {};
    if (!form.sku.trim()) e.sku = 'Nhập mã SKU';
    if (!form.name.trim()) e.name = 'Nhập tên sản phẩm';
    if (!form.categoryId) e.categoryId = 'Chọn danh mục';
    const price = Number(form.price);
    if (form.price === '' || Number.isNaN(price) || price <= 0) e.price = 'Giá phải lớn hơn 0';
    const min = Number(form.minStock);
    if (form.minStock === '' || !Number.isInteger(min) || min < 0) e.minStock = 'Ngưỡng tối thiểu phải là số nguyên ≥ 0';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const submit = async () => {
    if (!validate()) return;
    setSaving(true);
    setApiError(null);
    const input = {
      sku: form.sku.trim(),
      name: form.name.trim(),
      categoryId: Number(form.categoryId),
      price: Number(form.price),
      minStock: Number(form.minStock),
    };
    try {
      if (product) {
        await updateProduct(product.id, input);
        onSaved('Đã cập nhật sản phẩm');
      } else {
        await createProduct(input);
        onSaved('Đã thêm sản phẩm');
      }
    } catch (e) {
      setApiError(errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>{product ? 'Sửa sản phẩm' : 'Thêm sản phẩm'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {apiError && <Alert severity="error">{apiError}</Alert>}
          <TextField label="SKU" value={form.sku} onChange={set('sku')} error={!!errors.sku} helperText={errors.sku} required autoFocus />
          <TextField label="Tên sản phẩm" value={form.name} onChange={set('name')} error={!!errors.name} helperText={errors.name} required />
          <TextField select label="Danh mục" value={form.categoryId} onChange={set('categoryId')} error={!!errors.categoryId} helperText={errors.categoryId} required>
            {categories.map((c) => (
              <MenuItem key={c.id} value={String(c.id)}>{c.name}</MenuItem>
            ))}
          </TextField>
          <TextField label="Giá (₫)" type="number" value={form.price} onChange={set('price')} error={!!errors.price} helperText={errors.price} required />
          <TextField label="Tồn tối thiểu (min stock)" type="number" value={form.minStock} onChange={set('minStock')} error={!!errors.minStock} helperText={errors.minStock} required />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Hủy</Button>
        <Button onClick={submit} variant="contained" disabled={saving}>{saving ? 'Đang lưu…' : 'Lưu'}</Button>
      </DialogActions>
    </Dialog>
  );
}
