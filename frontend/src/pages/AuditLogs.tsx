import { useEffect, useMemo, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Accordion, AccordionDetails, AccordionSummary, Alert, Button, Chip, Divider, MenuItem, Paper, Stack, TableCell, TableRow, TextField, Typography } from '@mui/material';
import ExpandMoreOutlined from '@mui/icons-material/ExpandMoreOutlined';
import RefreshOutlined from '@mui/icons-material/RefreshOutlined';
import { dateTime, errorMessage } from '../api';
import { useAuth } from '../auth';
import { FilterBar, PageHeading, useDebounced } from '../shared';
import { ServerDataTable } from '../components/ServerDataTable';
import { listAuditEntries, usePreferredPageSize } from '../settings';
import type { AuditEntry, AuditQuery, AuditSource } from '../settings';

const sourceLabels: Record<string, string> = { audit: 'Nghiệp vụ', mcp: 'MCP execution', ai: 'AI message' };
function utc(value: string): string | undefined { const date = new Date(value); return value && Number.isFinite(date.getTime()) ? date.toISOString() : undefined; }
export default function AuditLogs() {
  const admin = useAuth().user?.role === 'Admin';
  if (!admin) return <><PageHeading eyebrow="HỆ THỐNG" title="Nhật ký kiểm toán" description="Theo dõi tác nhân và kết quả thao tác." /><Alert severity="error">Bạn không có quyền xem nhật ký kiểm toán. Chức năng này chỉ dành cho quản trị viên.</Alert></>;
  return <AuditList />;
}
function AuditList() {
  const [actor, setActor] = useState(''); const [action, setAction] = useState(''); const [entity, setEntity] = useState(''); const [status, setStatus] = useState('');
  const [source, setSource] = useState<AuditSource | ''>(''); const [from, setFrom] = useState(''); const [to, setTo] = useState('');
  const [page, setPage] = useState(0); const [pageSize, setPageSize] = usePreferredPageSize();
  const filters = useDebounced(useMemo(() => ({ actor, action, entity, status }), [actor, action, entity, status]));
  const fromUtc = utc(from); const toUtc = utc(to);
  const validation = filters.actor && (!Number.isSafeInteger(Number(filters.actor)) || Number(filters.actor) < 1) ? 'Mã tác nhân phải là số nguyên dương.' : from && !fromUtc || to && !toUtc ? 'Chọn thời điểm hợp lệ.' : fromUtc && toUtc && fromUtc > toUtc ? 'Thời điểm bắt đầu phải trước hoặc bằng thời điểm kết thúc.' : '';
  useEffect(() => setPage(0), [filters.actor, filters.action, filters.entity, filters.status]);
  const query: AuditQuery = { actorId: filters.actor ? Number(filters.actor) : undefined, action: filters.action.trim() || undefined, entityName: filters.entity.trim() || undefined, status: filters.status.trim() || undefined, source: source || undefined, from: fromUtc, to: toUtc, page: page + 1, pageSize };
  const entries = useQuery({ queryKey: ['audit-logs', query], queryFn: ({ signal }) => listAuditEntries(query, signal), enabled: !validation, staleTime: 0, refetchOnMount: 'always', placeholderData: keepPreviousData });
  return <>
    <PageHeading eyebrow="HỆ THỐNG" title="Nhật ký kiểm toán" description="Theo dõi người thao tác, thay đổi dữ liệu và kết quả xử lý." actions={<Button variant="outlined" startIcon={<RefreshOutlined />} disabled={entries.isFetching || Boolean(validation)} onClick={() => void entries.refetch()}>Làm mới nhật ký</Button>} />
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <FilterBar><TextField label="Nguồn nhật ký" select value={source} onChange={(event) => { setSource(event.target.value as AuditSource | ''); setPage(0); }}><MenuItem value="">Tất cả nguồn</MenuItem><MenuItem value="audit">Nghiệp vụ</MenuItem><MenuItem value="mcp">MCP execution</MenuItem><MenuItem value="ai">AI message</MenuItem></TextField>
<TextField label="Hành động" placeholder="Ví dụ: Updated" value={action} onChange={(event) => setAction(event.target.value)} slotProps={{ htmlInput: { maxLength: 80 } }} />
<TextField label="Thực thể" placeholder="Ví dụ: Product" value={entity} onChange={(event) => setEntity(event.target.value)} slotProps={{ htmlInput: { maxLength: 80 } }} /></FilterBar>
      <Accordion disableGutters sx={{ borderTop: 1, borderColor: 'divider', m: 0, '&.Mui-expanded': { m: 0 } }}>
        <AccordionSummary expandIcon={<ExpandMoreOutlined />} sx={{ minHeight: 44, px: 2.5 }}><Typography fontSize={12} fontWeight={650}>Bộ lọc chi tiết</Typography></AccordionSummary>
        <AccordionDetails sx={{ p: 0 }}><FilterBar sx={{ alignItems: 'flex-start', pt: .5 }}><TextField label="Mã tác nhân" type="number" value={actor} onChange={(event) => setActor(event.target.value)} slotProps={{ htmlInput: { min: 1, step: 1 } }} />
<TextField label="Kết quả" value={status} onChange={(event) => setStatus(event.target.value)} helperText="Ví dụ: Succeeded, Failed" slotProps={{ htmlInput: { maxLength: 32 } }} />
<TextField label="Từ thời điểm (địa phương)" type="datetime-local" value={from} onChange={(event) => { setFrom(event.target.value); setPage(0); }} slotProps={{ inputLabel: { shrink: true } }} />
<TextField label="Đến thời điểm (địa phương)" type="datetime-local" value={to} onChange={(event) => { setTo(event.target.value); setPage(0); }} slotProps={{ inputLabel: { shrink: true } }} /></FilterBar></AccordionDetails>
      </Accordion><Divider />
      <Stack direction="row" justifyContent="space-between" sx={{ px: 2.5, py: 1.8 }}><Typography fontWeight={750} fontSize={14}>Lịch sử thao tác</Typography><Typography variant="caption" color="text.secondary">{entries.data?.totalCount ?? '—'} kết quả</Typography></Stack>
      {validation ? <Alert severity="error" sx={{ m: 2.5 }}>{validation}</Alert> : <ServerDataTable<AuditEntry> label="Nhật ký kiểm toán" rows={entries.data?.items ?? []} rowKey={(entry) => entry.id} minWidth={1100}
        loading={entries.isLoading} fetching={entries.isFetching} error={entries.isError ? errorMessage(entries.error) : undefined} onRetry={() => void entries.refetch()}
        totalCount={entries.data?.totalCount ?? 0} page={page} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(value) => { setPageSize(value); setPage(0); }} emptyTitle="Chưa có nhật ký phù hợp" emptyDescription="Thay đổi bộ lọc hoặc thực hiện nghiệp vụ để tạo nhật ký thực tế."
        head={<TableRow><TableCell>THỜI GIAN ĐỊA PHƯƠNG</TableCell><TableCell>TÁC NHÂN</TableCell><TableCell>NGUỒN</TableCell><TableCell>HÀNH ĐỘNG</TableCell><TableCell>THỰC THỂ</TableCell><TableCell>ĐÍCH</TableCell><TableCell>KẾT QUẢ</TableCell></TableRow>}
        renderRow={(entry) => <TableRow hover><TableCell sx={{ whiteSpace: 'nowrap', fontSize: 12 }}>{dateTime(entry.createdAt)}</TableCell><TableCell><Typography fontSize={13} fontWeight={650}>{entry.actorUsername || (entry.actorId ? `User #${entry.actorId}` : 'Hệ thống')}</Typography>{entry.actorId && <Typography variant="caption" color="text.secondary">#{entry.actorId}</Typography>}</TableCell><TableCell><Chip size="small" variant="outlined" label={sourceLabels[entry.source.toLowerCase()] ?? entry.source} /></TableCell><TableCell sx={{ fontFamily: 'ui-monospace, monospace', fontSize: 12 }}>{entry.action}</TableCell><TableCell>{entry.entityName}</TableCell><TableCell sx={{ fontFamily: 'ui-monospace, monospace', fontSize: 12, overflowWrap: 'anywhere' }}>{entry.targetId || '—'}</TableCell><TableCell><Chip size="small" variant="outlined" color={/^(Succeeded|Success)$/i.test(entry.status) ? 'success' : /failed|error|cancel/i.test(entry.status) ? 'error' : 'default'} label={entry.status} /></TableCell></TableRow>} />}
    </Paper>
    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 2 }}>Nhật ký mới nhất hiển thị trước. Các mốc thời gian dùng múi giờ trên thiết bị.</Typography>
  </>;
}
