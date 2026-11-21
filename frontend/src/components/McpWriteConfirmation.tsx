import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, LinearProgress, Paper, Stack, Typography } from '@mui/material';
import { dateTime, errorMessage } from '../api';
import { ExecutionResult, JsonPanel } from '../mcp';
import { cancelMcpWrite, confirmMcpWrite, getMcpWriteIntent, invalidateMcpWriteData, writeStatusLabels } from '../mcpWrites';
import type { McpWriteIntent, McpWriteStatus } from '../mcpWrites';

function Impact({ intent }: { intent: McpWriteIntent }) {
  const args = intent.arguments;
  const quantity = typeof args.quantity === 'number' ? args.quantity : undefined;
  const product = typeof args.productId === 'number' ? args.productId : undefined;
  const warehouse = typeof args.warehouseId === 'number' ? `Kho #${args.warehouseId}`
    : intent.toolName === 'UpdatePurchaseOrderStatus' ? 'Kho đã lưu trên đơn mua' : 'Kho mặc định MAIN';
  const message = intent.toolName === 'CreateStockReceipt' ? `Nhập thêm ${quantity ?? 'số lượng đã nêu'} đơn vị cho sản phẩm #${product ?? '—'}.`
    : intent.toolName === 'CreateStockIssue' ? `Xuất ${quantity ?? 'số lượng đã nêu'} đơn vị khỏi sản phẩm #${product ?? '—'}.`
      : intent.toolName === 'AdjustInventory' ? `Đặt tồn kho của sản phẩm #${product ?? '—'} về ${quantity ?? 'mức đã nêu'} đơn vị.`
        : intent.toolName === 'CreatePurchaseOrder' ? `Tạo đơn mua nháp cho nhà cung cấp #${args.supplierId ?? '—'}, gồm ${Array.isArray(args.items) ? args.items.length : 'các'} dòng. Tạo đơn chưa làm tăng tồn kho.`
          : `Chuyển đơn mua #${args.id ?? '—'} sang trạng thái ${args.status ?? 'đã nêu'}.`;
  return <Alert severity="warning">{message} Phạm vi: {warehouse}. Máy chủ kiểm tra lại quyền và trạng thái nghiệp vụ khi thực thi. Số dư sau thao tác chỉ được xác nhận từ kết quả thực tế.</Alert>;
}

export function McpWriteConfirmation({ intentId, initialIntent, openReviewInitially = false, onBusyChange }: {
  intentId: string; initialIntent?: McpWriteIntent; openReviewInitially?: boolean; onBusyChange?: (busy: boolean, intentId: string) => void;
}) {
  const cache = useQueryClient(); const [review, setReview] = useState(openReviewInitially); const [uncertain, setUncertain] = useState(false);
  const intentQuery = useQuery({ queryKey: ['mcp-write-intent', intentId], initialData: initialIntent,
    enabled: !initialIntent || review || initialIntent.status === 'Approved',
    queryFn: ({ signal }) => getMcpWriteIntent(intentId, signal), staleTime: 0, refetchOnMount: 'always',
    refetchInterval: (query) => query.state.data?.status === 'Approved' ? 2000 : !initialIntent && query.state.data?.status === 'Pending' ? 10_000 : false,
  });
  useEffect(() => { if (initialIntent) cache.setQueryData<McpWriteIntent>(['mcp-write-intent', intentId], (current) =>
    current && (['Succeeded', 'Cancelled', 'Expired'].includes(current.status) || current.status === 'Approved' && initialIntent.status === 'Pending') ? current : initialIntent); }, [initialIntent, intentId, cache]);
  const intent = intentQuery.data; const previousStatus = useRef<McpWriteStatus | undefined>(initialIntent?.status);
  useEffect(() => {
    if (intent?.status === 'Succeeded' && previousStatus.current && previousStatus.current !== 'Succeeded') { setUncertain(false); setReview(false); void invalidateMcpWriteData(cache); }
    previousStatus.current = intent?.status;
  }, [intent?.status, cache]);
  const confirmation = useMutation({ retry: false, mutationFn: () => confirmMcpWrite(intentId),
    onSuccess: (result) => { cache.setQueryData(['mcp-write-intent', intentId], result.intent); setUncertain(result.intent.status === 'Approved'); if (result.intent.status === 'Succeeded') setReview(false); },
    onError: () => setUncertain(true),
    onSettled: async () => { await Promise.all([intentQuery.refetch(), invalidateMcpWriteData(cache)]); },
  });
  const cancellation = useMutation({ retry: false, mutationFn: () => cancelMcpWrite(intentId),
    onSuccess: (updated) => { cache.setQueryData(['mcp-write-intent', intentId], updated); setUncertain(false); setReview(false); },
    onSettled: async () => { await Promise.all([intentQuery.refetch(), cache.invalidateQueries({ queryKey: ['mcp-write-intents'] })]); },
  });
  const busy = confirmation.isPending || cancellation.isPending;
  useEffect(() => { onBusyChange?.(busy, intentId); return () => onBusyChange?.(false, intentId); }, [busy, onBusyChange, intentId]);
  const canConfirm = intent?.status === 'Pending' || intent?.status === 'Approved';
  const mutationError = confirmation.error ?? cancellation.error;
  function openReview() { confirmation.reset(); cancellation.reset(); setReview(true); void intentQuery.refetch(); }
  const details = intent && <Stack spacing={2}>
    <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap><Typography fontWeight={750} fontFamily="ui-monospace, monospace">{intent.toolName}</Typography><Chip size="small" color={intent.status === 'Succeeded' ? 'success' : intent.status === 'Pending' || intent.status === 'Approved' ? 'warning' : 'default'} label={writeStatusLabels[intent.status]} /></Stack>
    <Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>Intent ID: {intent.id}<br />Tạo lúc {dateTime(intent.createdAt)} · Hạn xác nhận {dateTime(intent.expiresAt)}</Typography>
    <Impact intent={intent} /><JsonPanel title="ARGUMENTS ĐÃ LƯU · JSON" value={intent.arguments} />
    {intent.errorMessage && <Alert severity="error">{intent.errorMessage}</Alert>}
  </Stack>;
  return <>
    <Paper variant="outlined" sx={{ p: 2.5 }}><Stack spacing={2}>
      {intentQuery.isFetching && <LinearProgress />}
      {intentQuery.isError && <Alert severity="error" action={<Button color="inherit" onClick={() => void intentQuery.refetch()}>Kiểm tra trạng thái</Button>}>{errorMessage(intentQuery.error)}</Alert>}
      {details}
      {uncertain && canConfirm && <Alert severity="warning">Chưa xác định thao tác đã hoàn tất. Giữ nguyên đề nghị này; kiểm tra trạng thái hoặc thử lại cùng Intent ID.</Alert>}
      {intent?.status === 'Approved' && <Alert severity="info">Đề nghị đã được cho phép nhưng chưa có kết quả thành công được lưu. Không coi trạng thái này là đã thay đổi tồn kho.</Alert>}
      {mutationError && <Alert severity="error">{errorMessage(mutationError)}</Alert>}
      {confirmation.data?.execution && <><ExecutionResult execution={confirmation.data.execution} /><Button component={Link} to={`/mcp/trace?executionId=${confirmation.data.execution.executionId}`}>Mở execution #{confirmation.data.execution.executionId}</Button></>}
      {intent?.result != null && !confirmation.data?.execution && <JsonPanel title="KẾT QUẢ ĐỀ NGHỊ · JSON" value={intent.result} />}
      {intent?.status === 'Expired' && <Alert severity="info">Đề nghị đã hết hạn. Muốn tiếp tục, chuẩn bị một đề nghị mới và xem lại nội dung trước khi xác nhận.</Alert>}
      <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap><Button disabled={busy || intentQuery.isFetching} onClick={() => void intentQuery.refetch()}>Kiểm tra trạng thái</Button>{canConfirm && <Button variant="contained" disabled={busy || intentQuery.isError || intentQuery.isFetching} onClick={openReview}>{intent?.status === 'Approved' ? 'Thử lại cùng đề nghị' : 'Xem và xác nhận đề nghị'}</Button>}{intent?.status === 'Pending' && <Button color="error" disabled={busy} onClick={() => cancellation.mutate()}>Hủy đề nghị</Button>}</Stack>
    </Stack></Paper>
    <Dialog open={review} fullWidth maxWidth="md" onClose={busy ? undefined : () => setReview(false)}>
      <DialogTitle>Xác nhận thao tác MCP</DialogTitle><DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>
        <Alert severity="info">Nội dung dưới đây được tải từ đề nghị đã lưu trên máy chủ và không thể sửa tại bước xác nhận. LLM chỉ đề xuất; chỉ nút xác nhận của bạn cho phép thực thi.</Alert>
        {intentQuery.isFetching && <LinearProgress />}{intentQuery.isError && <Alert severity="error">{errorMessage(intentQuery.error)}</Alert>}{details}
        {intent && !canConfirm && <Alert severity={intent.status === 'Succeeded' ? 'success' : 'info'}>{writeStatusLabels[intent.status]}.</Alert>}
        {mutationError && <Alert severity="error">{errorMessage(mutationError)} Chỉ kiểm tra hoặc gửi lại cùng Intent ID.</Alert>}
      </Stack></DialogContent><DialogActions sx={{ p: 2.5 }}><Button disabled={busy} onClick={() => setReview(false)}>Đóng xem xét</Button>{canConfirm && <Button variant="contained" color="warning" disabled={busy || intentQuery.isFetching || intentQuery.isError} onClick={() => confirmation.mutate()}>{busy ? 'Đang thực thi…' : intent?.status === 'Approved' ? 'Thử lại cùng đề nghị' : 'Xác nhận và thực thi'}</Button>}</DialogActions>
    </Dialog>
  </>;
}
