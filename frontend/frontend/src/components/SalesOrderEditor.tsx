import { useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Divider, IconButton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material';
import AddOutlined from '@mui/icons-material/AddOutlined';
import DeleteOutline from '@mui/icons-material/DeleteOutline';
import { errorMessage } from '../api';
import { useToast } from '../shared';
import { useWarehouseScope, WarehouseSelect } from '../warehouses';
import { RemoteEntitySelect } from './RemoteEntitySelect';
import { salesCustomerOptions, salesProductOptions, saveSalesOrder } from '../salesOrders';
import type { SalesOrder, SalesOrderInput } from '../salesOrders';
import type { Product } from '../types';
import type { Customer } from '../customers';

type DraftLine = { productId: number; sku: string; name: string; quantity: string; price: string };
const maxSafe = BigInt(Number.MAX_SAFE_INTEGER);
function cents(input: string): bigint | null {
  const normalized = input.trim();
  if (!/^(?:\d+|\d*\.\d{1,2})$/.test(normalized)) return null;
  const [whole = '0', fraction = ''] = normalized.split('.');
  return BigInt(whole || '0') * 100n + BigInt(fraction.padEnd(2, '0') || '0');
}
function money(centsValue: bigint): string {
  const fraction = centsValue % 100n;
  return `${new Intl.NumberFormat('vi-VN').format(centsValue / 100n)}${fraction ? `,${String(fraction).padStart(2, '0')}` : ''} ₫`;
}
function parsePrice(value: number): string {
  if (!Number.isFinite(value) || value < 0 || value > Number.MAX_SAFE_INTEGER / 100) return '';
  const fixed = value.toFixed(2);
  return fixed.endsWith('.00') ? fixed.slice(0, -3) : fixed;
}
export function SalesOrderEditor({ order, onClose, onSaved }: {
  order: SalesOrder | null; onClose: () => void; onSaved: (order: SalesOrder) => void;
}) {
  const toast = useToast();
  const scope = useWarehouseScope(); const [warehouseId, setWarehouseId] = useState<number | undefined>(order?.warehouseId ?? scope.warehouseId);
  const [customer, setCustomer] = useState<Customer | null>(order ? { id: order.customerId, code: '', name: order.customerName, email: null, phone: null, address: null, isActive: true, createdAt: '', updatedAt: '' } : null);
  const [product, setProduct] = useState<Product | null>(null);
  const [notes, setNotes] = useState(order?.notes ?? '');
  const [lines, setLines] = useState<DraftLine[]>(order?.items.map((item) => ({ productId: item.productId, sku: item.sku,
    name: item.productName, quantity: String(item.quantity), price: parsePrice(item.unitPrice) })) ?? []);
  const [validation, setValidation] = useState('');
  const save = useMutation({ mutationFn: (input: SalesOrderInput) => saveSalesOrder(input, order?.id), onSuccess: (saved) => {
    toast(order ? 'Đã cập nhật đơn bán hàng.' : 'Đã tạo đơn bán hàng.'); onSaved(saved);
  } });
  const amounts = lines.map((line) => {
    const quantity = Number(line.quantity); const unitPrice = cents(line.price);
    return line.quantity.trim() && Number.isInteger(quantity) && quantity > 0 && quantity <= 2_147_483_647 && unitPrice !== null
      ? unitPrice * BigInt(quantity) : null;
  });
  const total = amounts.every((amount) => amount !== null) ? amounts.reduce<bigint>((sum, amount) => sum + (amount ?? 0n), 0n) : null;
  function addProduct() {
    setValidation(''); if (!product) return;
    if (lines.some((line) => line.productId === product.id)) { setValidation('Sản phẩm đã có trong đơn. Hãy sửa số lượng trên dòng hiện tại.'); return; }
    if (lines.length >= 100) { setValidation('Mỗi đơn có tối đa 100 dòng sản phẩm.'); return; }
    setLines((previous) => [...previous, { productId: product.id, sku: product.sku, name: product.name, quantity: '1', price: parsePrice(product.price) }]);
    setProduct(null);
  }
  function submit(event: FormEvent) {
    event.preventDefault(); setValidation('');
    if (!warehouseId || !customer || !lines.length) { setValidation('Chọn khách hàng và thêm ít nhất một sản phẩm.'); return; }
    const items: SalesOrderInput['items'] = [];
    for (const line of lines) {
      const quantity = Number(line.quantity); const unitPrice = cents(line.price);
      if (!Number.isInteger(quantity) || quantity <= 0 || quantity > 2_147_483_647) { setValidation(`Số lượng ${line.sku} phải là số nguyên dương.`); return; }
      if (unitPrice === null || unitPrice > maxSafe || unitPrice * BigInt(quantity) > maxSafe) { setValidation(`Giá ${line.sku} không hợp lệ hoặc vượt giới hạn tính chính xác.`); return; }
      items.push({ productId: line.productId, quantity, unitPrice: `${unitPrice / 100n}.${String(unitPrice % 100n).padStart(2, '0')}` });
    }
    if (total !== null && total > maxSafe) { setValidation('Tổng đơn vượt giới hạn biểu diễn an toàn của giao diện.'); return; }
    save.mutate({ warehouseId, customerId: customer.id, notes: notes.trim() || null, items });
  }
  return <Dialog open fullWidth maxWidth="lg" onClose={save.isPending ? undefined : onClose}><Box component="form" onSubmit={submit}>
    <DialogTitle>{order ? `Chỉnh sửa đơn ${order.code}` : 'Tạo đơn bán hàng'}<Typography variant="body2" color="text.secondary" sx={{ mt: .5 }}>Lập đơn nháp; xác nhận trước khi giao hàng. Tồn kho chỉ giảm khi xác nhận giao.</Typography></DialogTitle>
    <DialogContent><Stack spacing={3} sx={{ pt: 1 }}>{(validation || save.isError) && <Alert severity="error">{validation || errorMessage(save.error)}</Alert>}
      <WarehouseSelect label="Kho xuất hàng" value={warehouseId} onChange={setWarehouseId} activeOnly disabled={save.isPending} />
      <RemoteEntitySelect<Customer> label="Khách hàng" required value={customer} onChange={setCustomer} optionLabel={(item) => item.code ? `${item.code} · ${item.name}` : item.name} loadPage={salesCustomerOptions} queryKey="sales-active-customers" disabled={save.isPending} />
      <TextField label="Ghi chú đơn bán hàng" value={notes} onChange={(event) => setNotes(event.target.value)} multiline minRows={2} disabled={save.isPending} slotProps={{ htmlInput: { maxLength: 2000 } }} />
      <Divider /><Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ sm: 'flex-start' }}>
        <RemoteEntitySelect<Product> label="Chọn sản phẩm" value={product} onChange={setProduct} optionLabel={(item) => `${item.sku} · ${item.name}`} loadPage={salesProductOptions} queryKey="sales-active-products" disabled={save.isPending || lines.length >= 100} />
        <Button variant="outlined" startIcon={<AddOutlined />} onClick={addProduct} disabled={!product || save.isPending || lines.length >= 100}>Thêm dòng sản phẩm</Button>
      </Stack>
      {lines.length ? <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 2 }}><Table sx={{ minWidth: 710 }} aria-label="Dòng đơn bán hàng">
        <TableHead><TableRow><TableCell>SẢN PHẨM</TableCell><TableCell>SỐ LƯỢNG</TableCell><TableCell>ĐƠN GIÁ BÁN (VND)</TableCell><TableCell align="right">THÀNH TIỀN</TableCell><TableCell /></TableRow></TableHead>
        <TableBody>{lines.map((line, index) => <TableRow key={line.productId}><TableCell><Typography fontSize={13} fontWeight={650}>{line.name}</Typography><Typography variant="caption" color="text.secondary">{line.sku}</Typography></TableCell>
          <TableCell><TextField label={`Số lượng ${line.sku}`} type="number" value={line.quantity} onChange={(event) => setLines((previous) => previous.map((row, i) => i === index ? { ...row, quantity: event.target.value } : row))} required disabled={save.isPending} sx={{ width: 140 }} slotProps={{ htmlInput: { min: 1, max: 2_147_483_647, step: 1 } }} /></TableCell>
          <TableCell><TextField label={`Giá bán ${line.sku}`} type="number" value={line.price} onChange={(event) => setLines((previous) => previous.map((row, i) => i === index ? { ...row, price: event.target.value } : row))} required disabled={save.isPending} sx={{ width: 175 }} slotProps={{ htmlInput: { min: 0, step: '0.01' } }} /></TableCell>
          <TableCell align="right" sx={{ whiteSpace: 'nowrap', fontWeight: 650 }}>{amounts[index] !== null ? money(amounts[index]!) : '—'}</TableCell>
          <TableCell><IconButton aria-label={`Xóa dòng ${line.sku}`} disabled={save.isPending} onClick={() => setLines((previous) => previous.filter((_, i) => i !== index))}><DeleteOutline fontSize="small" /></IconButton></TableCell>
        </TableRow>)}</TableBody></Table></TableContainer> : <Alert severity="info">Chọn sản phẩm để lập đơn bán hàng.</Alert>}
      <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="text.secondary">{lines.length}/100 dòng · Giá bán theo đơn; không thay đổi giá vốn.</Typography><Box sx={{ textAlign: 'right' }}><Typography variant="caption" color="text.secondary">TỔNG ĐƠN</Typography><Typography variant="h6">{total !== null ? money(total) : '—'}</Typography></Box></Stack>
    </Stack></DialogContent>
    <DialogActions sx={{ p: 2.5 }}><Button onClick={onClose} disabled={save.isPending}>Hủy</Button><Button type="submit" variant="contained" disabled={save.isPending || !warehouseId || !customer || !lines.length}>{save.isPending ? 'Đang lưu…' : 'Lưu bản nháp'}</Button></DialogActions>
  </Box></Dialog>;
}
