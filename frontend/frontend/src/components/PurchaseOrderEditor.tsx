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
import { decimalCents, decimalString, previewMoney, purchaseProductOptions, purchaseSupplierOptions, savePurchaseOrder } from '../purchaseOrders';
import type { PurchaseOrder, PurchaseOrderInput } from '../purchaseOrders';
import type { Product } from '../types';

type SupplierChoice = { id: number; name: string; code?: string };
interface DraftLine { productId: number; sku: string; productName: string; quantity: string; unitCost: string }
const maximumSafeCents = BigInt(Number.MAX_SAFE_INTEGER);

function editableCost(value: number) {
  const cents = decimalCents(String(value));
  return cents !== null && cents <= maximumSafeCents ? decimalString(cents) : '';
}
function lineCents(line: DraftLine): bigint | null {
  const quantity = Number(line.quantity);
  const cost = decimalCents(line.unitCost);
  return line.quantity.trim() && Number.isInteger(quantity) && quantity > 0 && quantity <= 2_147_483_647 && cost !== null
    ? cost * BigInt(quantity) : null;
}

export function PurchaseOrderEditor({ order, onClose, onSaved }: {
  order: PurchaseOrder | null; onClose: () => void; onSaved: (order: PurchaseOrder) => void;
}) {
  const toast = useToast();
  const scope = useWarehouseScope(); const [warehouseId, setWarehouseId] = useState<number | undefined>(order?.warehouseId ?? scope.warehouseId);
  const [supplier, setSupplier] = useState<SupplierChoice | null>(order ? { id: order.supplierId, name: order.supplierName } : null);
  const [product, setProduct] = useState<Product | null>(null);
  const [notes, setNotes] = useState(order?.notes ?? '');
  const [lines, setLines] = useState<DraftLine[]>(order?.items.map((item) => ({
    productId: item.productId, sku: item.sku, productName: item.productName,
    quantity: String(item.quantity), unitCost: editableCost(item.unitCost),
  })) ?? []);
  const [validation, setValidation] = useState('');
  const save = useMutation({
    mutationFn: (input: PurchaseOrderInput) => savePurchaseOrder(input, order?.id),
    onSuccess: (saved) => { toast(order ? 'Đã cập nhật bản nháp.' : 'Đã tạo đơn mua hàng.'); onSaved(saved); },
  });
  const totals = lines.map(lineCents);
  const total = totals.every((amount) => amount !== null) ? totals.reduce<bigint>((sum, amount) => sum + (amount ?? 0n), 0n) : null;
  function addProduct() {
    setValidation('');
    if (!product) return;
    if (lines.some((line) => line.productId === product.id)) { setValidation('Sản phẩm đã có trong đơn. Hãy sửa số lượng trên dòng hiện tại.'); return; }
    if (lines.length >= 100) { setValidation('Mỗi đơn mua hàng có tối đa 100 dòng sản phẩm.'); return; }
    setLines((previous) => [...previous, { productId: product.id, sku: product.sku, productName: product.name, quantity: '1', unitCost: editableCost(product.costPrice) }]);
    setProduct(null);
  }
  function changeLine(index: number, field: 'quantity' | 'unitCost', value: string) {
    setLines((previous) => previous.map((line, current) => current === index ? { ...line, [field]: value } : line));
  }
  function submit(event: FormEvent) {
    event.preventDefault(); setValidation('');
    if (!warehouseId || !supplier || !lines.length) { setValidation('Chọn nhà cung cấp và thêm ít nhất một sản phẩm.'); return; }
    const items: PurchaseOrderInput['items'] = [];
    for (const line of lines) {
      const quantity = Number(line.quantity);
      const cents = decimalCents(line.unitCost);
      if (!line.quantity.trim() || !Number.isInteger(quantity) || quantity <= 0 || quantity > 2_147_483_647) { setValidation(`Số lượng ${line.sku} phải là số nguyên dương hợp lệ.`); return; }
      if (cents === null) { setValidation(`Đơn giá ${line.sku} phải không âm và có tối đa hai chữ số thập phân.`); return; }
      if (cents > maximumSafeCents || cents * BigInt(quantity) > maximumSafeCents) { setValidation(`Giá trị ${line.sku} vượt giới hạn biểu diễn an toàn của giao diện. Không tự làm tròn số tiền.`); return; }
      items.push({ productId: line.productId, quantity, unitCost: decimalString(cents) });
    }
    if (total !== null && total > maximumSafeCents) { setValidation('Tổng đơn vượt giới hạn biểu diễn an toàn của giao diện.'); return; }
    save.mutate({ warehouseId, supplierId: supplier.id, notes: notes.trim() || null, items });
  }
  return <Dialog open fullWidth maxWidth="lg" onClose={save.isPending ? undefined : onClose}>
    <Box component="form" onSubmit={submit}>
      <DialogTitle>{order ? `Chỉnh sửa đơn ${order.code}` : 'Tạo đơn mua hàng'}
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>Lưu bản nháp trước khi gửi duyệt. Tồn kho chỉ thay đổi khi nhận hàng.</Typography>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={3} sx={{ pt: 1 }}>
          {(validation || save.isError) && <Alert severity="error">{validation || errorMessage(save.error)}</Alert>}
          <WarehouseSelect label="Kho nhận hàng" value={warehouseId} onChange={setWarehouseId} activeOnly disabled={save.isPending} />
      <RemoteEntitySelect<SupplierChoice> label="Nhà cung cấp" required value={supplier} onChange={setSupplier}
            optionLabel={(item) => item.code ? `${item.code} · ${item.name}` : item.name}
            loadPage={purchaseSupplierOptions} queryKey="purchase-active-suppliers" disabled={save.isPending} />
          <TextField label="Ghi chú đơn mua hàng" value={notes} onChange={(event) => setNotes(event.target.value)} multiline minRows={2} disabled={save.isPending} slotProps={{ htmlInput: { maxLength: 2000 } }} />
          <Divider />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ sm: 'flex-start' }}>
            <RemoteEntitySelect<Product> label="Chọn sản phẩm" value={product} onChange={setProduct}
              optionLabel={(item) => `${item.sku} · ${item.name}`} loadPage={purchaseProductOptions}
              queryKey="purchase-active-products" disabled={save.isPending || lines.length >= 100} />
            <Button variant="outlined" startIcon={<AddOutlined />} onClick={addProduct}
              disabled={!product || save.isPending || lines.length >= 100} sx={{ whiteSpace: 'nowrap' }}>Thêm dòng sản phẩm</Button>
          </Stack>
          {lines.length ? <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 2 }}>
            <Table sx={{ minWidth: 720 }} aria-label="Dòng đơn mua hàng">
              <TableHead><TableRow><TableCell>SẢN PHẨM</TableCell><TableCell>SỐ LƯỢNG</TableCell><TableCell>ĐƠN GIÁ (VND)</TableCell><TableCell align="right">THÀNH TIỀN DỰ KIẾN</TableCell><TableCell /></TableRow></TableHead>
              <TableBody>{lines.map((line, index) => <TableRow key={line.productId}>
                <TableCell><Typography fontSize={13} fontWeight={650}>{line.productName}</Typography><Typography variant="caption" color="text.secondary">{line.sku}</Typography></TableCell>
                <TableCell><TextField label={`Số lượng ${line.sku}`} type="number" value={line.quantity} onChange={(event) => changeLine(index, 'quantity', event.target.value)} required disabled={save.isPending} sx={{ width: 145 }} slotProps={{ htmlInput: { min: 1, max: 2_147_483_647, step: 1 } }} /></TableCell>
                <TableCell><TextField label={`Đơn giá ${line.sku}`} type="number" value={line.unitCost} onChange={(event) => changeLine(index, 'unitCost', event.target.value)} required disabled={save.isPending} sx={{ width: 180 }} slotProps={{ htmlInput: { min: 0, step: '0.01' } }} /></TableCell>
                <TableCell align="right" sx={{ whiteSpace: 'nowrap', fontWeight: 650 }}>{totals[index] !== null ? previewMoney(totals[index]!) : '—'}</TableCell>
                <TableCell><IconButton aria-label={`Xóa dòng ${line.sku}`} disabled={save.isPending} onClick={() => setLines((previous) => previous.filter((_, current) => current !== index))}><DeleteOutline fontSize="small" /></IconButton></TableCell>
              </TableRow>)}</TableBody>
            </Table>
          </TableContainer> : <Alert severity="info">Chọn sản phẩm và thêm dòng để lập đơn mua hàng.</Alert>}
          <Stack direction="row" justifyContent="space-between" alignItems="center"><Typography variant="caption" color="text.secondary">{lines.length}/100 dòng · Số tiền xem trước từ thông tin bạn nhập</Typography><Box sx={{ textAlign: 'right' }}><Typography variant="caption" color="text.secondary">TỔNG DỰ KIẾN</Typography><Typography variant="h6">{total !== null ? previewMoney(total) : '—'}</Typography></Box></Stack>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ p: 2.5 }}><Button onClick={onClose} disabled={save.isPending}>Hủy</Button><Button type="submit" variant="contained" disabled={save.isPending || !warehouseId || !supplier || !lines.length}>{save.isPending ? 'Đang lưu…' : 'Lưu bản nháp'}</Button></DialogActions>
    </Box>
  </Dialog>;
}
