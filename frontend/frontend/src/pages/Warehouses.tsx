import { usePreferredPageSize } from '../settings';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Avatar, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Divider, Drawer, FormControlLabel, IconButton, InputAdornment, MenuItem, Paper, Skeleton, Stack, Switch, TableCell, TableRow, TableSortLabel, TextField, Tooltip, Typography } from '@mui/material';
import AddOutlined from '@mui/icons-material/AddOutlined';
import SearchOutlined from '@mui/icons-material/SearchOutlined';
import EditOutlined from '@mui/icons-material/EditOutlined';
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined';
import BlockOutlined from '@mui/icons-material/BlockOutlined';
import CheckCircleOutline from '@mui/icons-material/CheckCircleOutline';
import CloseOutlined from '@mui/icons-material/CloseOutlined';
import PersonOutline from '@mui/icons-material/PersonOutline';
import { dateTime, errorMessage } from '../api';
import { useAuth } from '../auth';
import { FilterBar, ConfirmDialog, PageHeading, useDebounced, useToast } from '../shared';
import { ServerDataTable } from '../components/ServerDataTable';
import { createWarehouse, warehouseInput, deactivateWarehouse, getWarehouse, listWarehouses, updateWarehouse } from '../warehouses';
import type { Warehouse, WarehouseInput, WarehouseQuery, WarehouseSort } from '../warehouses';

type StatusFilter = 'active' | 'inactive' | 'all';
function WarehouseStatus({ active }: { active: boolean }) {
  return <Chip size="small" label={active ? 'Hoạt động' : 'Ngừng hoạt động'} color={active ? 'success' : 'default'} variant="outlined" sx={{ fontSize: 11 }} />;
}

export default function Warehouses() {
  const admin = useAuth().user?.role === 'Admin';
  const queryClient = useQueryClient();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounced(search.trim());
  const [status, setStatus] = useState<StatusFilter>('active');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = usePreferredPageSize();
  const [sortBy, setSortBy] = useState<WarehouseSort>('name');
  const [descending, setDescending] = useState(false);
  const [editor, setEditor] = useState<{ warehouse: Warehouse | null } | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [deactivating, setDeactivating] = useState<Warehouse | null>(null);
  useEffect(() => setPage(0), [debouncedSearch]);
  const query: WarehouseQuery = { search: debouncedSearch || undefined, isActive: status === 'all' ? undefined : status === 'active', page: page + 1, pageSize, sortBy, descending };
  const warehouses = useQuery({ queryKey: ['warehouses', query], queryFn: ({ signal }) => listWarehouses(query, signal), placeholderData: keepPreviousData });
  async function refresh() {
    await Promise.all([queryClient.invalidateQueries({ queryKey: ['warehouses'] }), queryClient.invalidateQueries({ queryKey: ['warehouse'] }), queryClient.invalidateQueries({ queryKey: ['warehouse-options'] })]);
  }
  const deactivate = useMutation({ mutationFn: () => deactivateWarehouse(deactivating!.id), onSuccess: async () => {
    setDeactivating(null); setPage(0); toast('Đã ngừng hoạt động kho hàng.'); await refresh();
  } });
  const activate = useMutation({ mutationFn: (warehouse: Warehouse) => updateWarehouse(warehouse.id, { ...warehouseInput(warehouse), isActive: true }), onSuccess: async () => {
    setPage(0); toast('Đã kích hoạt kho hàng.'); await refresh();
  } });
  function requestDeactivation(warehouse: Warehouse) { deactivate.reset(); activate.reset(); setDeactivating(warehouse); }
  function sort(field: WarehouseSort) { setDescending(sortBy === field ? !descending : false); setSortBy(field); setPage(0); }
  function sortHeader(field: WarehouseSort, label: string) {
    return <TableCell sortDirection={sortBy === field ? descending ? 'desc' : 'asc' : false}><TableSortLabel active={sortBy === field} direction={descending ? 'desc' : 'asc'} onClick={() => sort(field)}>{label}</TableSortLabel></TableCell>;
  }
  return <>
    <PageHeading eyebrow="QUẢN LÝ KHO" title="Kho hàng" description="Quản lý kho nhận, xuất và chuyển hàng. Kho MAIN giữ dữ liệu lịch sử ban đầu."
      actions={admin && <Button variant="contained" startIcon={<AddOutlined />} onClick={() => setEditor({ warehouse: null })}>Thêm kho hàng</Button>} />
    {activate.isError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => activate.reset()}>{errorMessage(activate.error)}</Alert>}
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <FilterBar>
        <TextField label="Tìm kho hàng" placeholder="Tìm mã, tên hoặc địa chỉ…" value={search} fullWidth onChange={(event) => setSearch(event.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined fontSize="small" /></InputAdornment> } }} />
        <TextField select label="Trạng thái kho hàng" value={status} sx={{ minWidth: 230 }} onChange={(event) => { setStatus(event.target.value as StatusFilter); setPage(0); }}>
          <MenuItem value="active">Đang hoạt động</MenuItem><MenuItem value="inactive">Ngừng hoạt động</MenuItem><MenuItem value="all">Tất cả trạng thái</MenuItem>
        </TextField>
      </FilterBar><Divider />
      <Stack direction="row" justifyContent="space-between" sx={{ px: 2.5, py: 1.8 }}><Typography fontWeight={750} fontSize={14}>Danh sách kho hàng</Typography><Typography variant="caption" color="text.secondary">{warehouses.data?.totalCount ?? '—'} kết quả theo bộ lọc</Typography></Stack>
      <ServerDataTable<Warehouse> label="Danh sách kho hàng" minWidth={1040} rows={warehouses.data?.items ?? []} rowKey={(warehouse) => warehouse.id}
        loading={warehouses.isLoading} fetching={warehouses.isFetching} error={warehouses.isError ? errorMessage(warehouses.error) : undefined}
        onRetry={() => void warehouses.refetch()} totalCount={warehouses.data?.totalCount ?? 0} page={page} pageSize={pageSize}
        onPageChange={setPage} onPageSizeChange={(value) => { setPageSize(value); setPage(0); }}
        emptyTitle="Chưa có kho hàng phù hợp" emptyDescription={search || status !== 'active' ? 'Thay đổi từ khóa hoặc trạng thái để tìm kho hàng.' : 'Thêm kho hàng đầu tiên để lưu thông tin liên hệ.'}
        emptyAction={admin && !search && status === 'active' && <Button startIcon={<AddOutlined />} onClick={() => setEditor({ warehouse: null })}>Thêm kho hàng</Button>}
        head={<TableRow>{sortHeader('code', 'MÃ KHO')}{sortHeader('name', 'KHO HÀNG')}<TableCell>ĐỊA CHỈ</TableCell><TableCell>TRẠNG THÁI</TableCell>{sortHeader('createdAt', 'NGÀY TẠO')}{sortHeader('updatedAt', 'CẬP NHẬT')}<TableCell align="right">THAO TÁC</TableCell></TableRow>}
        renderRow={(warehouse) => <TableRow hover>
          <TableCell sx={{ fontFamily: 'ui-monospace, monospace', fontSize: 12, color: 'text.secondary' }}>{warehouse.code}</TableCell>
          <TableCell><Stack direction="row" spacing={1.5} alignItems="center"><Avatar sx={{ bgcolor: 'action.selected', color: 'primary.main', width: 38, height: 38 }}><PersonOutline fontSize="small" /></Avatar><Button onClick={() => { activate.reset(); setSelectedId(warehouse.id); }} sx={{ p: 0, minHeight: 'auto', color: 'text.primary', textAlign: 'left', justifyContent: 'flex-start' }}>{warehouse.name}</Button></Stack></TableCell>
          <TableCell sx={{ maxWidth: 260, overflowWrap: 'anywhere' }}>{warehouse.address || '—'}</TableCell>
          <TableCell><WarehouseStatus active={warehouse.isActive} />{warehouse.isDefault && <Chip label="Kho mặc định" size="small" variant="outlined" sx={{ ml: 1 }} />}</TableCell>
          <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(warehouse.createdAt)}</TableCell>
          <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(warehouse.updatedAt)}</TableCell>
          <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
            <Tooltip title="Chi tiết"><IconButton size="small" aria-label={`Xem kho hàng ${warehouse.name}`} onClick={() => { activate.reset(); setSelectedId(warehouse.id); }}><VisibilityOutlined fontSize="small" /></IconButton></Tooltip>
            {admin && <><Tooltip title="Chỉnh sửa"><IconButton size="small" aria-label={`Sửa kho hàng ${warehouse.name}`} onClick={() => setEditor({ warehouse })}><EditOutlined fontSize="small" /></IconButton></Tooltip>
              {warehouse.isActive && !warehouse.isDefault ? <Tooltip title="Ngừng hoạt động"><IconButton size="small" aria-label={`Ngừng hoạt động kho hàng ${warehouse.name}`} onClick={() => requestDeactivation(warehouse)}><BlockOutlined fontSize="small" /></IconButton></Tooltip>
                : !warehouse.isActive && <Tooltip title="Kích hoạt"><span><IconButton size="small" color="success" disabled={activate.isPending} aria-label={`Kích hoạt kho hàng ${warehouse.name}`} onClick={() => activate.mutate(warehouse)}><CheckCircleOutline fontSize="small" /></IconButton></span></Tooltip>}
            </>}
          </TableCell>
        </TableRow>} />
    </Paper>
    {editor && <WarehouseEditor warehouse={editor.warehouse} onClose={() => setEditor(null)} onSaved={() => { setEditor(null); setPage(0); void refresh(); }} />}
    <WarehouseDetail id={selectedId} admin={admin} onClose={() => setSelectedId(null)} onEdit={(warehouse) => setEditor({ warehouse })}
      onDeactivate={requestDeactivation} onActivate={(warehouse) => activate.mutate(warehouse)} activating={activate.isPending}
      activationError={activate.isError ? errorMessage(activate.error) : undefined} />
    <ConfirmDialog open={deactivating !== null} title="Ngừng hoạt động kho hàng?"
      description={`“${deactivating?.name ?? ''}” sẽ chuyển sang ngừng hoạt động. Lịch sử vẫn được giữ. Máy chủ ngăn ngừng hoạt động kho còn tồn hoặc đơn hàng chưa xử lý.`}
      confirmLabel="Ngừng hoạt động" pending={deactivate.isPending} error={deactivate.isError ? errorMessage(deactivate.error) : undefined}
      onClose={() => setDeactivating(null)} onConfirm={() => deactivate.mutate()} />
  </>;
}

function WarehouseEditor({ warehouse, onClose, onSaved }: { warehouse: Warehouse | null; onClose: () => void; onSaved: () => void }) {
  const toast = useToast();
  const [form, setForm] = useState<WarehouseInput>(warehouse ? warehouseInput(warehouse) : { code: '', name: '', address: null, isActive: true });
  const [validation, setValidation] = useState('');
  const [pendingDeactivation, setPendingDeactivation] = useState<WarehouseInput | null>(null);
  const save = useMutation({ mutationFn: (input: WarehouseInput) => warehouse ? updateWarehouse(warehouse.id, input) : createWarehouse(input), onSuccess: () => {
    toast(warehouse ? 'Đã cập nhật kho hàng.' : 'Đã thêm kho hàng.'); onSaved();
  } });
  function field<K extends keyof WarehouseInput>(name: K, value: WarehouseInput[K]) { setForm((previous) => ({ ...previous, [name]: value })); }
  function submit(event: FormEvent) {
    event.preventDefault(); setValidation('');
    if (!form.code.trim() || !form.name.trim()) { setValidation('Nhập mã và tên kho hàng.'); return; }
    const input = { ...form, code: form.code.trim(), name: form.name.trim(), address: form.address?.trim() || null };
    if (warehouse?.isActive && !input.isActive) setPendingDeactivation(input); else save.mutate(input);
  }
  return <>
    <Dialog open fullWidth maxWidth="md" onClose={save.isPending ? undefined : onClose}><Box component="form" onSubmit={submit}>
      <DialogTitle>{warehouse ? 'Chỉnh sửa kho hàng' : 'Thêm kho hàng mới'}<Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>Thông tin kho và địa điểm lưu trữ</Typography></DialogTitle>
      <DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>
        {(validation || save.isError) && <Alert severity="error">{validation || errorMessage(save.error)}</Alert>}
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2.5 }}>
          <TextField autoFocus required label="Mã kho hàng" value={form.code} disabled={warehouse?.isDefault} onChange={(event) => field('code', event.target.value)} helperText="Mã duy nhất; hệ thống chuẩn hóa chữ hoa." slotProps={{ htmlInput: { maxLength: 40 } }} />
          <TextField required label="Tên kho hàng" value={form.name} onChange={(event) => field('name', event.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
        </Box>
        <TextField label="Địa chỉ kho hàng" value={form.address ?? ''} onChange={(event) => field('address', event.target.value)} multiline minRows={3} slotProps={{ htmlInput: { maxLength: 1000 } }} />
        <FormControlLabel control={<Switch disabled={warehouse?.isDefault} checked={form.isActive} onChange={(_, checked) => field('isActive', checked)} />} label="Kho hàng đang hoạt động" />
      </Stack></DialogContent>
      <DialogActions sx={{ p: 2.5 }}><Button onClick={onClose} disabled={save.isPending}>Hủy</Button><Button type="submit" variant="contained" disabled={save.isPending || !form.code.trim() || !form.name.trim()}>{save.isPending ? 'Đang lưu…' : 'Lưu kho hàng'}</Button></DialogActions>
    </Box></Dialog>
    <ConfirmDialog open={pendingDeactivation !== null} title="Ngừng hoạt động kho hàng?"
      description="Các thay đổi sẽ được lưu và kho hàng chuyển sang ngừng hoạt động. Thông tin vẫn được giữ lại trong hệ thống."
      confirmLabel="Lưu và ngừng hoạt động" pending={save.isPending} error={save.isError ? errorMessage(save.error) : undefined}
      onClose={() => setPendingDeactivation(null)} onConfirm={() => { if (pendingDeactivation) save.mutate(pendingDeactivation); }} />
  </>;
}

function WarehouseDetail({ id, admin, onClose, onEdit, onDeactivate, onActivate, activating, activationError }: {
  id: number | null; admin: boolean; onClose: () => void; onEdit: (warehouse: Warehouse) => void;
  onDeactivate: (warehouse: Warehouse) => void; onActivate: (warehouse: Warehouse) => void; activating: boolean; activationError?: string;
}) {
  const warehouse = useQuery({ queryKey: ['warehouse', id], enabled: id !== null, queryFn: ({ signal }) => getWarehouse(id!, signal) });
  const item = warehouse.data;
  return <Drawer anchor="right" open={id !== null} onClose={activating ? undefined : onClose} sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', sm: 490 } } }}>
    <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ p: 3 }}><Typography variant="h6">Chi tiết kho hàng</Typography><IconButton aria-label="Đóng chi tiết kho hàng" onClick={onClose} disabled={activating}><CloseOutlined /></IconButton></Stack><Divider />
    {warehouse.isLoading ? <Stack spacing={2} sx={{ p: 3 }}><Skeleton height={80} /><Skeleton /><Skeleton /><Skeleton /></Stack>
      : warehouse.isError ? <Alert severity="error" sx={{ m: 3 }} action={<Button color="inherit" onClick={() => void warehouse.refetch()}>Thử lại</Button>}>{errorMessage(warehouse.error)}</Alert>
      : item && <Stack spacing={3} sx={{ p: 3 }}>
        <Stack direction="row" spacing={2} alignItems="center"><Avatar sx={{ width: 56, height: 56, bgcolor: 'action.selected', color: 'primary.main' }}><PersonOutline /></Avatar><Box><Typography variant="overline" color="text.secondary">{item.code}</Typography><Typography variant="h5" sx={{ overflowWrap: 'anywhere' }}>{item.name}</Typography></Box></Stack>
        <Box><WarehouseStatus active={item.isActive} /></Box>{activationError && <Alert severity="error">{activationError}</Alert>}<Divider />
        {[['Địa chỉ', item.address]].map(([label, value]) => <Box key={label}><Typography variant="caption" color="text.secondary">{label}</Typography><Typography sx={{ mt: 0.5, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{value || 'Chưa cập nhật'}</Typography></Box>)}
        <Divider /><Typography variant="caption" color="text.secondary">Ngày tạo: {dateTime(item.createdAt)}<br />Cập nhật: {dateTime(item.updatedAt)}</Typography>
        {admin && <Stack spacing={1.5}><Button variant="contained" startIcon={<EditOutlined />} disabled={activating} onClick={() => onEdit(item)}>Chỉnh sửa kho hàng</Button>
          {item.isActive && !item.isDefault ? <Button variant="outlined" color="error" startIcon={<BlockOutlined />} disabled={activating} onClick={() => onDeactivate(item)}>Ngừng hoạt động</Button>
            : !item.isActive && <Button variant="outlined" color="success" startIcon={<CheckCircleOutline />} disabled={activating} onClick={() => onActivate(item)}>{activating ? 'Đang kích hoạt…' : 'Kích hoạt kho hàng'}</Button>}
        </Stack>}
      </Stack>}
  </Drawer>;
}
