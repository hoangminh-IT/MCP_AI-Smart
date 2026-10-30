import { useEffect, useState } from 'react';
import axios from 'axios';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material';
import { errorMessage } from '../api';
import { useAuth } from '../auth';
import { useToast } from '../shared';
import { getPurchaseOrder, receivePurchaseOrder } from '../purchaseOrders';
import type { PurchaseOrder, ReceiptInput } from '../purchaseOrders';

interface ReceiptIntent { signature: string; input: ReceiptInput }

function receiptIntent(input: ReceiptInput): ReceiptIntent {
  return { signature: JSON.stringify({ reason: input.reason, items: input.items }), input };
}
function restoredIntent(key: string): ReceiptIntent | null {
  try {
    const value = JSON.parse(sessionStorage.getItem(key) ?? 'null') as ReceiptInput | null;
    if (!value || typeof value.requestId !== 'string' || !/^[0-9a-f-]{36}$/i.test(value.requestId) ||
        !(value.reason === null || typeof value.reason === 'string' && value.reason.length <= 1000) ||
        !Array.isArray(value.items) || value.items.length < 1 || value.items.length > 100 ||
        value.items.some((item) => !Number.isInteger(item.purchaseOrderItemId) || item.purchaseOrderItemId <= 0 || !Number.isInteger(item.quantity) || item.quantity <= 0)) return null;
    return receiptIntent({ requestId: value.requestId, reason: value.reason, items: value.items.map((item) => ({ purchaseOrderItemId: item.purchaseOrderItemId, quantity: item.quantity })) });
  } catch { return null; }
}

export function PurchaseReceiptDialog({ order, onClose, onCompleted }: {
  order: PurchaseOrder; onClose: () => void; onCompleted: (order: PurchaseOrder) => void;
}) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const actorId = useAuth().user?.id;
  const storageKey = `inventory:pending-purchase-receipt:${actorId}:${order.id}`;
  const [restored] = useState(() => restoredIntent(storageKey));
  const [currentOrder, setCurrentOrder] = useState(order);
  const [quantities, setQuantities] = useState<Record<number, string>>(() => Object.fromEntries(restored?.input.items.map((item) => [item.purchaseOrderItemId, String(item.quantity)]) ?? []));
  const [reason, setReason] = useState(restored?.input.reason ?? '');
  const [validation, setValidation] = useState('');
  const [intent, setIntent] = useState<ReceiptIntent | null>(restored);
  const [reviewOpen, setReviewOpen] = useState(Boolean(restored));
  const [uncertain, setUncertain] = useState(Boolean(restored));
  const [storageUnavailable, setStorageUnavailable] = useState(false);
  const [checking, setChecking] = useState(false);
  const [verificationFailed, setVerificationFailed] = useState(false);
  const [verifiedWithoutReceipt, setVerifiedWithoutReceipt] = useState(false);

  function clearPending() {
    setUncertain(false);
    try { sessionStorage.removeItem(storageKey); } catch { /* The in-memory state is still resolved. */ }
  }
  function preservePending(input: ReceiptInput) {
    setUncertain(true); setIntent(receiptIntent(input));
    // Store only the exact operation needed for safe retry, scoped to user and
    // order. No JWT, supplier contacts, order details or other metadata is copied.
    try { sessionStorage.setItem(storageKey, JSON.stringify(input)); }
    catch { setStorageUnavailable(true); }
  }
  async function verifyOutcome(requestId: string, authoritativeFailure = false) {
    setChecking(true); setVerificationFailed(false); setVerifiedWithoutReceipt(false);
    try {
      const fresh = await getPurchaseOrder(order.id);
      setCurrentOrder(fresh); queryClient.setQueryData(['purchase-order', fresh.id], fresh);
      if (fresh.receipts.some((receipt) => receipt.requestId === requestId)) {
        clearPending();
        toast('Phiếu nhận đã được ghi nhận trên máy chủ. Không tạo lại giao dịch.');
        onCompleted(fresh);
      } else {
        // Absence from one GET does not prove an in-flight POST rolled back.
        if (authoritativeFailure) clearPending();
        setVerifiedWithoutReceipt(true);
      }
    } catch {
      setVerificationFailed(true);
    } finally { setChecking(false); }
  }
  const receive = useMutation({
    // Persist before sending: a reload can interrupt the browser before any
    // response/error callback, while the server may already have committed.
    onMutate: (input: ReceiptInput) => { preservePending(input); },
    mutationFn: (input: ReceiptInput) => receivePurchaseOrder(order.id, input),
    onSuccess: (result) => {
      clearPending();
      toast(result.replayed ? 'Phiếu nhận đã được xác nhận trước đó; tồn kho không cộng lại.' : 'Đã nhận hàng và ghi nhận biến động tồn kho.');
      onCompleted(result.order);
    },
    // A timeout or lost response may occur after commit. Read the authoritative
    // receipt history before offering retry, and keep the same request ID.
    onError: async (error, input) => {
      const status = axios.isAxiosError(error) ? error.response?.status : undefined;
      const authoritativeFailure = status !== undefined && status >= 400 && status < 500;
      if (!authoritativeFailure) preservePending(input);
      await verifyOutcome(input.requestId, authoritativeFailure);
    },
  });
  useEffect(() => { if (restored) void verifyOutcome(restored.input.requestId); }, []);
  const busy = receive.isPending || checking;
  const eligible = currentOrder.status === 'Approved' || currentOrder.status === 'PartiallyReceived';
  const remaining = currentOrder.items.filter((item) => item.receivedQuantity < item.quantity);
  const intentWithinRemaining = intent?.input.items.every((line) => {
    const item = currentOrder.items.find((candidate) => candidate.id === line.purchaseOrderItemId);
    return item && line.quantity > 0 && line.quantity <= item.quantity - item.receivedQuantity;
  }) ?? false;
  function review() {
    setValidation('');
    if (uncertain) { if (intent) setReviewOpen(true); return; }
    if (!eligible) { setValidation('Đơn không còn ở trạng thái cho phép nhận hàng. Kiểm tra chi tiết mới nhất.'); return; }
    const items: ReceiptInput['items'] = [];
    for (const item of remaining) {
      const raw = quantities[item.id]?.trim() ?? '';
      if (!raw) continue;
      const quantity = Number(raw);
      if (!Number.isInteger(quantity) || quantity < 0 || quantity > item.quantity - item.receivedQuantity) {
        setValidation(`Số lượng nhận ${item.sku} phải là số nguyên từ 0 đến ${item.quantity - item.receivedQuantity}.`); return;
      }
      if (quantity > 0) items.push({ purchaseOrderItemId: item.id, quantity });
    }
    if (!items.length) { setValidation('Nhập số lượng lớn hơn 0 cho ít nhất một sản phẩm.'); return; }
    items.sort((first, second) => first.purchaseOrderItemId - second.purchaseOrderItemId);
    const payload = { reason: reason.trim() || null, items };
    const signature = JSON.stringify(payload);
    const next = intent?.signature === signature ? intent : { signature, input: { ...payload, requestId: crypto.randomUUID() } };
    if (intent?.signature !== signature) receive.reset();
    setIntent(next); setReviewOpen(true); setVerifiedWithoutReceipt(false);
  }

  return <>
    <Dialog open fullWidth maxWidth="md" onClose={busy ? undefined : onClose}>
      <DialogTitle>Nhận hàng · {currentOrder.code}
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>Nhập số lượng nhận lần này. Để trống hoặc nhập 0 cho các dòng chưa nhận.</Typography>
      </DialogTitle>
      <DialogContent><Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Kho nhận: {order.warehouseCode || `#${order.warehouseId}`} · {order.warehouseName}. Kho đã cố định theo đơn.</Alert>
        <Stack spacing={2.5} sx={{ pt: 1 }}>
          {validation && <Alert severity="warning">{validation}</Alert>}
          {!eligible && <Alert severity="warning">Đơn đã thay đổi trạng thái. Lịch sử nhận hàng và số lượng được cập nhật từ máy chủ.</Alert>}
          {receive.isError && <Alert severity="error">{errorMessage(receive.error)}</Alert>}
          {checking && <Alert severity="info">Đang kiểm tra kết quả thực tế trên máy chủ trước khi cho phép thử lại…</Alert>}
          {verificationFailed && <Alert severity="warning" action={<Button color="inherit" disabled={checking} onClick={() => { if (intent) void verifyOutcome(intent.input.requestId); }}>Kiểm tra kết quả</Button>}>
            Chưa xác minh được kết quả nhận hàng. Không tạo phiếu mới; hãy kiểm tra lịch sử trước khi thử lại. Request ID vẫn được giữ nguyên.
          </Alert>}
          {verifiedWithoutReceipt && <Alert severity="info">Dữ liệu mới chưa có phiếu mang Request ID này. Nếu thử lại cùng nội dung, hệ thống tiếp tục dùng cùng Request ID để ngăn cộng tồn hai lần.</Alert>}
          {uncertain && <Alert severity="warning">Kết quả phiếu gốc chưa được xác nhận. Một lần kiểm tra chưa thấy phiếu không chứng minh giao dịch đã hủy. Nội dung đang khóa; chỉ được thử lại cùng Request ID và số lượng ban đầu.</Alert>}
          {storageUnavailable && <Alert severity="warning">Trình duyệt không lưu được thông tin thử lại. Giữ màn hình này mở đến khi xác minh xong kết quả.</Alert>}
          <TableContainer sx={{ border: 1, borderColor: 'divider', borderRadius: 2 }}>
            <Table sx={{ minWidth: 620 }} aria-label="Số lượng nhận hàng">
              <TableHead><TableRow><TableCell>SẢN PHẨM</TableCell><TableCell align="right">ĐẶT / ĐÃ NHẬN</TableCell><TableCell align="right">CÒN LẠI</TableCell><TableCell>NHẬN LẦN NÀY</TableCell></TableRow></TableHead>
              <TableBody>{remaining.map((item) => <TableRow key={item.id}>
                <TableCell><Typography fontWeight={650} fontSize={13}>{item.productName}</Typography><Typography variant="caption" color="text.secondary">{item.sku}</Typography></TableCell>
                <TableCell align="right">{item.quantity} / {item.receivedQuantity}</TableCell>
                <TableCell align="right" sx={{ fontWeight: 750 }}>{item.quantity - item.receivedQuantity}</TableCell>
                <TableCell><TextField label={`Số lượng nhận ${item.sku}`} type="number" value={quantities[item.id] ?? ''}
                  onChange={(event) => setQuantities((previous) => ({ ...previous, [item.id]: event.target.value }))}
                  disabled={busy || verificationFailed || uncertain || !eligible} sx={{ width: 180 }}
                  slotProps={{ htmlInput: { min: 0, max: item.quantity - item.receivedQuantity, step: 1 } }} /></TableCell>
              </TableRow>)}</TableBody>
            </Table>
          </TableContainer>
          <TextField label="Lý do nhận hàng" value={reason} onChange={(event) => setReason(event.target.value)} multiline minRows={2}
            helperText="Có thể để trống để dùng lý do theo mã đơn mua hàng." disabled={busy || verificationFailed || uncertain}
            slotProps={{ htmlInput: { maxLength: 1000 } }} />
          {intent && <Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>Request ID: {intent.input.requestId}</Typography>}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ p: 2.5 }}><Button disabled={busy} onClick={onClose}>Đóng</Button><Button variant="contained" disabled={busy || verificationFailed || (uncertain ? !intent : !eligible)} onClick={review}>{uncertain ? 'Mở lại phiếu gốc' : 'Kiểm tra phiếu nhận'}</Button></DialogActions>
    </Dialog>

    <Dialog open={reviewOpen} onClose={busy ? undefined : () => setReviewOpen(false)} fullWidth maxWidth="sm">
      <DialogTitle>Xác nhận phiếu nhận · {currentOrder.code}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Alert severity="info">Các số lượng dưới đây sẽ được cộng vào tồn kho và ghi lịch sử nhập hàng trong cùng giao dịch.</Alert>
          {receive.isError && <Alert severity="error">{errorMessage(receive.error)}</Alert>}
          {checking && <Alert severity="info">Đang kiểm tra lịch sử nhận hàng…</Alert>}
          {verificationFailed && <Alert severity="warning" action={<Button color="inherit" onClick={() => { if (intent) void verifyOutcome(intent.input.requestId); }}>Kiểm tra kết quả</Button>}>Chưa xác minh được kết quả. Việc thử lại đang tạm khóa.</Alert>}
          {verifiedWithoutReceipt && <Alert severity="info">Chưa thấy phiếu trong dữ liệu mới. Thử lại sẽ dùng cùng Request ID, không tạo ý định nhận hàng mới.</Alert>}
          {uncertain && <Alert severity="warning">Kết quả chưa xác nhận. Nội dung phiếu gốc và Request ID được giữ nguyên; không được đổi số lượng hoặc tạo ý định nhận hàng mới.</Alert>}
          {intent && !intentWithinRemaining && <Alert severity="warning">{uncertain ? 'Dữ liệu đơn đã thay đổi. Chỉ thử lại phiếu gốc để xác minh kết quả với cùng Request ID.' : 'Số lượng còn lại đã thay đổi. Quay lại và cập nhật số lượng trước khi xác nhận phiếu mới.'}</Alert>}
          {intent?.input.items.map((line) => {
            const item = currentOrder.items.find((candidate) => candidate.id === line.purchaseOrderItemId);
            return <Stack key={line.purchaseOrderItemId} direction="row" spacing={2} justifyContent="space-between"><Box><Typography fontWeight={650} fontSize={13}>{item?.productName}</Typography><Typography variant="caption" color="text.secondary">{item?.sku}</Typography></Box><Typography fontWeight={800}>+{line.quantity}</Typography></Stack>;
          })}
          <Typography variant="body2" color="text.secondary">Lý do: {intent?.input.reason || 'Theo mã đơn mua hàng'}</Typography>
          <Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>Request ID: {intent?.input.requestId}</Typography>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ p: 2.5 }}>
        <Button disabled={busy} onClick={() => setReviewOpen(false)}>Quay lại</Button>
        <Button variant="contained" disabled={busy || verificationFailed || !intent || (!uncertain && (!eligible || !intentWithinRemaining))} onClick={() => { if (intent) receive.mutate(intent.input); }}>{busy ? 'Đang ghi nhận…' : uncertain || receive.isError ? 'Thử lại cùng Request ID' : 'Xác nhận nhận hàng'}</Button>
      </DialogActions>
    </Dialog>
  </>;
}
