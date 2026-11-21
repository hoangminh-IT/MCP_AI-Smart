import { usePreferredPageSize } from '../settings';
import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Avatar, Box, Button, Chip, Dialog, DialogActions, DialogContent,
  DialogTitle, Divider, Drawer, FormControlLabel, IconButton, InputAdornment,
  MenuItem, Paper, Skeleton, Stack, Switch, TableCell, TableRow, TableSortLabel,
  TextField, Tooltip, Typography,
} from '@mui/material';
import AddOutlined from '@mui/icons-material/AddOutlined';
import SearchOutlined from '@mui/icons-material/SearchOutlined';
import EditOutlined from '@mui/icons-material/EditOutlined';
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined';
import BlockOutlined from '@mui/icons-material/BlockOutlined';
import CheckCircleOutline from '@mui/icons-material/CheckCircleOutline';
import CloseOutlined from '@mui/icons-material/CloseOutlined';
import LocalShippingOutlined from '@mui/icons-material/LocalShippingOutlined';
import { dateTime, errorMessage } from '../api';
import { useAuth } from '../auth';
import { ConfirmDialog, FilterBar, PageHeading, useDebounced, useToast } from '../shared';
import {
  createSupplier, deactivateSupplier, getSupplier, listSuppliers, supplierInput,
  updateSupplier,
} from '../suppliers';
import type { Supplier, SupplierInput, SupplierQuery, SupplierSort } from '../suppliers';
import { ServerDataTable } from '../components/ServerDataTable';

type StatusFilter = 'active' | 'inactive' | 'all';

function SupplierStatus({ active }: { active: boolean }) {
  return <Chip size="small" label={active ? 'Hoạt động' : 'Ngừng hoạt động'} color={active ? 'success' : 'default'} variant="outlined" sx={{ fontSize: 11 }} />;
}

export default function Suppliers() {
  const admin = useAuth().user?.role === 'Admin';
  const queryClient = useQueryClient();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounced(search.trim());
  const [status, setStatus] = useState<StatusFilter>('active');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = usePreferredPageSize();
  const [sortBy, setSortBy] = useState<SupplierSort>('name');
  const [descending, setDescending] = useState(false);
  const [editor, setEditor] = useState<{ supplier: Supplier | null } | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [deactivating, setDeactivating] = useState<Supplier | null>(null);

  useEffect(() => setPage(0), [debouncedSearch]);
  const query: SupplierQuery = {
    search: debouncedSearch || undefined,
    isActive: status === 'all' ? undefined : status === 'active',
    page: page + 1, pageSize, sortBy, descending,
  };
  const suppliers = useQuery({
    queryKey: ['suppliers', query],
    queryFn: ({ signal }) => listSuppliers(query, signal),
    placeholderData: keepPreviousData,
  });

  async function refresh() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['suppliers'] }),
      queryClient.invalidateQueries({ queryKey: ['supplier'] }),
    ]);
  }
  const deactivate = useMutation({
    mutationFn: () => deactivateSupplier(deactivating!.id),
    onSuccess: async () => {
      setDeactivating(null); setPage(0);
      toast('Đã ngừng hoạt động nhà cung cấp.');
      await refresh();
    },
  });
  const activate = useMutation({
    mutationFn: (supplier: Supplier) => updateSupplier(supplier.id, { ...supplierInput(supplier), isActive: true }),
    onSuccess: async () => {
      setPage(0); toast('Đã kích hoạt nhà cung cấp.');
      await refresh();
    },
  });
  function requestDeactivation(supplier: Supplier) {
    deactivate.reset(); activate.reset(); setDeactivating(supplier);
  }
  function sort(field: SupplierSort) {
    setDescending(sortBy === field ? !descending : false); setSortBy(field); setPage(0);
  }
  function sortHeader(field: SupplierSort, label: string) {
    return <TableCell sortDirection={sortBy === field ? descending ? 'desc' : 'asc' : false}>
      <TableSortLabel active={sortBy === field} direction={descending ? 'desc' : 'asc'} onClick={() => sort(field)}>
        {label}
      </TableSortLabel>
    </TableCell>;
  }

  return <>
    <PageHeading eyebrow="QUẢN LÝ KHO" title="Nhà cung cấp"
      description="Quản lý đối tác cung ứng và thông tin liên hệ."
      actions={admin && <Button variant="contained" startIcon={<AddOutlined />} onClick={() => setEditor({ supplier: null })}>Thêm nhà cung cấp</Button>} />
    {activate.isError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => activate.reset()}>{errorMessage(activate.error)}</Alert>}
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <FilterBar>
        <TextField label="Tìm nhà cung cấp" placeholder="Tìm mã, tên hoặc liên hệ…" value={search}
          onChange={(event) => setSearch(event.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined fontSize="small" /></InputAdornment> } }} />
        <TextField select label="Trạng thái nhà cung cấp" value={status}
          onChange={(event) => { setStatus(event.target.value as StatusFilter); setPage(0); }}>
          <MenuItem value="active">Đang hoạt động</MenuItem>
          <MenuItem value="inactive">Ngừng hoạt động</MenuItem>
          <MenuItem value="all">Tất cả trạng thái</MenuItem>
        </TextField>
      </FilterBar>
      <Divider />
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 2.5, py: 1.5 }}>
        <Typography fontSize={13} fontWeight={650}>Danh sách nhà cung cấp</Typography>
        <Typography variant="caption" color="text.secondary">{suppliers.data?.totalCount ?? '—'} kết quả</Typography>
      </Stack>
      <ServerDataTable<Supplier> label="Danh sách nhà cung cấp" minWidth={1060}
        rows={suppliers.data?.items ?? []} rowKey={(supplier) => supplier.id}
        loading={suppliers.isLoading} fetching={suppliers.isFetching}
        error={suppliers.isError ? errorMessage(suppliers.error) : undefined} onRetry={() => void suppliers.refetch()}
        totalCount={suppliers.data?.totalCount ?? 0} page={page} pageSize={pageSize} onPageChange={setPage}
        onPageSizeChange={(value) => { setPageSize(value); setPage(0); }}
        emptyTitle="Chưa có nhà cung cấp phù hợp"
        emptyDescription={search || status !== 'active' ? 'Thay đổi từ khóa hoặc trạng thái để tìm đối tác.' : 'Thêm nhà cung cấp đầu tiên để lưu thông tin đối tác cung ứng.'}
        emptyAction={admin && !search && status === 'active' && <Button startIcon={<AddOutlined />} onClick={() => setEditor({ supplier: null })}>Thêm nhà cung cấp</Button>}
        head={<TableRow>
              {sortHeader('code', 'MÃ NCC')}
              {sortHeader('name', 'NHÀ CUNG CẤP')}
              <TableCell>LIÊN HỆ</TableCell>
              <TableCell>TRẠNG THÁI</TableCell>
              {sortHeader('createdAt', 'NGÀY TẠO')}
              {sortHeader('updatedAt', 'CẬP NHẬT')}
              <TableCell align="right">THAO TÁC</TableCell>
            </TableRow>}
        renderRow={(supplier) => <TableRow key={supplier.id} hover>
              <TableCell sx={{ fontFamily: 'ui-monospace, monospace', fontSize: 12, color: 'text.secondary' }}>{supplier.code}</TableCell>
              <TableCell>
                <Stack direction="row" spacing={1.5} alignItems="center">
                  <Avatar variant="rounded" sx={{ bgcolor: 'action.selected', color: 'primary.main', width: 32, height: 32 }}><LocalShippingOutlined sx={{ fontSize: 18 }} /></Avatar>
                  <Box>
                    <Button onClick={() => { activate.reset(); setSelectedId(supplier.id); }} sx={{ p: 0, minHeight: 'auto', color: 'text.primary', textAlign: 'left', justifyContent: 'flex-start' }}>{supplier.name}</Button>
                    {supplier.contactPerson && <Typography variant="caption" color="text.secondary" display="block">{supplier.contactPerson}</Typography>}
                  </Box>
                </Stack>
              </TableCell>
              <TableCell sx={{ fontSize: 12, color: 'text.secondary' }}>
                <Typography fontSize={12}>{supplier.email || '—'}</Typography>
                <Typography variant="caption" color="text.secondary">{supplier.phone || '—'}</Typography>
              </TableCell>
              <TableCell><SupplierStatus active={supplier.isActive} /></TableCell>
              <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(supplier.createdAt)}</TableCell>
              <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(supplier.updatedAt)}</TableCell>
              <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                <Tooltip title="Chi tiết"><IconButton size="small" aria-label={`Xem nhà cung cấp ${supplier.name}`} onClick={() => { activate.reset(); setSelectedId(supplier.id); }}><VisibilityOutlined fontSize="small" /></IconButton></Tooltip>
                {admin && <>
                  <Tooltip title="Chỉnh sửa"><IconButton size="small" aria-label={`Sửa nhà cung cấp ${supplier.name}`} onClick={() => setEditor({ supplier })}><EditOutlined fontSize="small" /></IconButton></Tooltip>
                  {supplier.isActive
                    ? <Tooltip title="Ngừng hoạt động"><IconButton size="small" aria-label={`Ngừng hoạt động nhà cung cấp ${supplier.name}`} onClick={() => requestDeactivation(supplier)}><BlockOutlined fontSize="small" /></IconButton></Tooltip>
                    : <Tooltip title="Kích hoạt"><span><IconButton size="small" color="success" disabled={activate.isPending} aria-label={`Kích hoạt nhà cung cấp ${supplier.name}`} onClick={() => activate.mutate(supplier)}><CheckCircleOutline fontSize="small" /></IconButton></span></Tooltip>}
                </>}
              </TableCell>
            </TableRow>}
      />
    </Paper>

    {editor && <SupplierEditor supplier={editor.supplier} onClose={() => setEditor(null)} onSaved={() => {
      setEditor(null); setPage(0); void refresh();
    }} />}
    <SupplierDetail id={selectedId} admin={admin} onClose={() => setSelectedId(null)} onEdit={(supplier) => setEditor({ supplier })}
      onDeactivate={requestDeactivation} onActivate={(supplier) => activate.mutate(supplier)}
      activating={activate.isPending} activationError={activate.isError ? errorMessage(activate.error) : undefined} />
    <ConfirmDialog open={Boolean(deactivating)} title="Ngừng hoạt động nhà cung cấp?"
      description={`“${deactivating?.name ?? ''}” sẽ chuyển sang ngừng hoạt động. Thông tin nhà cung cấp vẫn được giữ lại trong hệ thống.`}
      confirmLabel="Ngừng hoạt động" pending={deactivate.isPending}
      error={deactivate.isError ? errorMessage(deactivate.error) : undefined}
      onClose={() => setDeactivating(null)} onConfirm={() => deactivate.mutate()} />
  </>;
}

function SupplierEditor({ supplier, onClose, onSaved }: { supplier: Supplier | null; onClose: () => void; onSaved: () => void }) {
  const toast = useToast();
  const [form, setForm] = useState<SupplierInput>(supplier ? supplierInput(supplier) : {
    code: '', name: '', contactPerson: null, email: null, phone: null, address: null, taxCode: null, isActive: true,
  });
  const [validation, setValidation] = useState('');
  const [pendingDeactivation, setPendingDeactivation] = useState<SupplierInput | null>(null);
  const save = useMutation({
    mutationFn: (input: SupplierInput) => supplier ? updateSupplier(supplier.id, input) : createSupplier(input),
    onSuccess: () => { toast(supplier ? 'Đã cập nhật nhà cung cấp.' : 'Đã thêm nhà cung cấp.'); onSaved(); },
  });
  function field<K extends keyof SupplierInput>(name: K, value: SupplierInput[K]) {
    setForm((previous) => ({ ...previous, [name]: value }));
  }
  function submit(event: FormEvent) {
    event.preventDefault(); setValidation('');
    if (!form.code.trim() || !form.name.trim()) { setValidation('Nhập mã và tên nhà cung cấp.'); return; }
    const optional = (value: string | null) => value?.trim() || null;
    const input: SupplierInput = {
      ...form, code: form.code.trim(), name: form.name.trim(),
      contactPerson: optional(form.contactPerson), email: optional(form.email), phone: optional(form.phone),
      address: optional(form.address), taxCode: optional(form.taxCode),
    };
    if (supplier?.isActive && !input.isActive) setPendingDeactivation(input);
    else save.mutate(input);
  }
  return <><Dialog open fullWidth maxWidth="md" onClose={save.isPending ? undefined : onClose}>
    <Box component="form" onSubmit={submit}>
      <DialogTitle>{supplier ? 'Chỉnh sửa nhà cung cấp' : 'Thêm nhà cung cấp mới'}
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>Thông tin đối tác và đầu mối liên hệ</Typography>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{ pt: 1 }}>
          {(validation || save.isError) && <Alert severity="error">{validation || errorMessage(save.error)}</Alert>}
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2.5 }}>
            <TextField autoFocus required label="Mã nhà cung cấp" value={form.code} onChange={(event) => field('code', event.target.value)}
              helperText="Mã duy nhất; hệ thống chuẩn hóa chữ hoa." slotProps={{ htmlInput: { maxLength: 40 } }} />
            <TextField required label="Tên nhà cung cấp" value={form.name} onChange={(event) => field('name', event.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
            <TextField label="Người liên hệ" value={form.contactPerson ?? ''} onChange={(event) => field('contactPerson', event.target.value)} slotProps={{ htmlInput: { maxLength: 160 } }} />
            <TextField label="Email nhà cung cấp" type="email" value={form.email ?? ''} onChange={(event) => field('email', event.target.value)} slotProps={{ htmlInput: { maxLength: 254 } }} />
            <TextField label="Số điện thoại" type="tel" value={form.phone ?? ''} onChange={(event) => field('phone', event.target.value)} slotProps={{ htmlInput: { maxLength: 40 } }} />
            <TextField label="Mã số thuế" value={form.taxCode ?? ''} onChange={(event) => field('taxCode', event.target.value)} slotProps={{ htmlInput: { maxLength: 40 } }} />
          </Box>
          <TextField label="Địa chỉ" value={form.address ?? ''} onChange={(event) => field('address', event.target.value)} multiline minRows={3} slotProps={{ htmlInput: { maxLength: 1000 } }} />
          <FormControlLabel control={<Switch checked={form.isActive} onChange={(_, checked) => field('isActive', checked)} />} label="Nhà cung cấp đang hoạt động" />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ p: 2.5 }}>
        <Button onClick={onClose} disabled={save.isPending}>Hủy</Button>
        <Button type="submit" variant="contained" disabled={save.isPending || !form.code.trim() || !form.name.trim()}>{save.isPending ? 'Đang lưu…' : 'Lưu nhà cung cấp'}</Button>
      </DialogActions>
    </Box>
  </Dialog>
    <ConfirmDialog open={pendingDeactivation !== null} title="Ngừng hoạt động nhà cung cấp?"
      description="Các thay đổi sẽ được lưu và nhà cung cấp chuyển sang ngừng hoạt động. Thông tin vẫn được giữ lại trong hệ thống."
      confirmLabel="Lưu và ngừng hoạt động" pending={save.isPending}
      error={save.isError ? errorMessage(save.error) : undefined}
      onClose={() => setPendingDeactivation(null)}
      onConfirm={() => { if (pendingDeactivation) save.mutate(pendingDeactivation); }} />
  </>;
}

function SupplierDetail({ id, admin, onClose, onEdit, onDeactivate, onActivate, activating, activationError }: {
  id: number | null; admin: boolean; onClose: () => void; onEdit: (supplier: Supplier) => void;
  onDeactivate: (supplier: Supplier) => void; onActivate: (supplier: Supplier) => void;
  activating: boolean; activationError?: string;
}) {
  const supplier = useQuery({
    queryKey: ['supplier', id], enabled: id !== null,
    queryFn: ({ signal }) => getSupplier(id!, signal),
  });
  const item = supplier.data;
  return <Drawer anchor="right" open={id !== null} onClose={activating ? undefined : onClose}
    sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', sm: 490 } } }}>
    <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ p: 3 }}>
      <Typography variant="h6">Chi tiết nhà cung cấp</Typography>
      <IconButton aria-label="Đóng chi tiết nhà cung cấp" onClick={onClose} disabled={activating}><CloseOutlined /></IconButton>
    </Stack>
    <Divider />
    {supplier.isLoading ? <Stack spacing={2} sx={{ p: 3 }}><Skeleton height={80} /><Skeleton /><Skeleton /><Skeleton /></Stack>
      : supplier.isError ? <Alert severity="error" sx={{ m: 3 }} action={<Button color="inherit" onClick={() => void supplier.refetch()}>Thử lại</Button>}>{errorMessage(supplier.error)}</Alert>
      : item && <Stack spacing={3} sx={{ p: 3 }}>
        <Stack direction="row" spacing={2} alignItems="center">
          <Avatar variant="rounded" sx={{ width: 56, height: 56, bgcolor: 'action.selected', color: 'primary.main' }}><LocalShippingOutlined /></Avatar>
          <Box><Typography variant="overline" color="text.secondary">{item.code}</Typography><Typography variant="h5" sx={{ overflowWrap: 'anywhere' }}>{item.name}</Typography></Box>
        </Stack>
        <Box><SupplierStatus active={item.isActive} /></Box>
        {activationError && <Alert severity="error">{activationError}</Alert>}
        <Divider />
        {[
          ['Người liên hệ', item.contactPerson], ['Email', item.email], ['Số điện thoại', item.phone],
          ['Mã số thuế', item.taxCode], ['Địa chỉ', item.address],
        ].map(([label, value]) => <Box key={label}><Typography variant="caption" color="text.secondary">{label}</Typography><Typography sx={{ mt: 0.5, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{value || 'Chưa cập nhật'}</Typography></Box>)}
        <Divider />
        <Typography variant="caption" color="text.secondary">Ngày tạo: {dateTime(item.createdAt)}<br />Cập nhật: {dateTime(item.updatedAt)}</Typography>
        {admin && <Stack spacing={1.5}>
          <Button variant="contained" startIcon={<EditOutlined />} disabled={activating} onClick={() => onEdit(item)}>Chỉnh sửa nhà cung cấp</Button>
          {item.isActive
            ? <Button variant="outlined" color="error" startIcon={<BlockOutlined />} disabled={activating} onClick={() => onDeactivate(item)}>Ngừng hoạt động</Button>
            : <Button variant="outlined" color="success" startIcon={<CheckCircleOutline />} disabled={activating} onClick={() => onActivate(item)}>{activating ? 'Đang kích hoạt…' : 'Kích hoạt nhà cung cấp'}</Button>}
        </Stack>}
      </Stack>}
  </Drawer>;
}
