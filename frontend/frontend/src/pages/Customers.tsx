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
import { ConfirmDialog, FilterBar, PageHeading, useDebounced, useToast } from '../shared';
import { ServerDataTable } from '../components/ServerDataTable';
import { createCustomer, customerInput, deactivateCustomer, getCustomer, listCustomers, updateCustomer } from '../customers';
import type { Customer, CustomerInput, CustomerQuery, CustomerSort } from '../customers';

type StatusFilter = 'active' | 'inactive' | 'all';
function CustomerStatus({ active }: { active: boolean }) {
  return <Chip size="small" label={active ? 'Hoạt động' : 'Ngừng hoạt động'} color={active ? 'success' : 'default'} variant="outlined" sx={{ fontSize: 11 }} />;
}

export default function Customers() {
  const admin = useAuth().user?.role === 'Admin';
  const queryClient = useQueryClient();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounced(search.trim());
  const [status, setStatus] = useState<StatusFilter>('active');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = usePreferredPageSize();
  const [sortBy, setSortBy] = useState<CustomerSort>('name');
  const [descending, setDescending] = useState(false);
  const [editor, setEditor] = useState<{ customer: Customer | null } | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [deactivating, setDeactivating] = useState<Customer | null>(null);
  useEffect(() => setPage(0), [debouncedSearch]);
  const query: CustomerQuery = { search: debouncedSearch || undefined, isActive: status === 'all' ? undefined : status === 'active', page: page + 1, pageSize, sortBy, descending };
  const customers = useQuery({ queryKey: ['customers', query], queryFn: ({ signal }) => listCustomers(query, signal), placeholderData: keepPreviousData });
  async function refresh() {
    await Promise.all([queryClient.invalidateQueries({ queryKey: ['customers'] }), queryClient.invalidateQueries({ queryKey: ['customer'] })]);
  }
  const deactivate = useMutation({ mutationFn: () => deactivateCustomer(deactivating!.id), onSuccess: async () => {
    setDeactivating(null); setPage(0); toast('Đã ngừng hoạt động khách hàng.'); await refresh();
  } });
  const activate = useMutation({ mutationFn: (customer: Customer) => updateCustomer(customer.id, { ...customerInput(customer), isActive: true }), onSuccess: async () => {
    setPage(0); toast('Đã kích hoạt khách hàng.'); await refresh();
  } });
  function requestDeactivation(customer: Customer) { deactivate.reset(); activate.reset(); setDeactivating(customer); }
  function sort(field: CustomerSort) { setDescending(sortBy === field ? !descending : false); setSortBy(field); setPage(0); }
  function sortHeader(field: CustomerSort, label: string) {
    return <TableCell sortDirection={sortBy === field ? descending ? 'desc' : 'asc' : false}><TableSortLabel active={sortBy === field} direction={descending ? 'desc' : 'asc'} onClick={() => sort(field)}>{label}</TableSortLabel></TableCell>;
  }
  return <>
    <PageHeading eyebrow="QUẢN LÝ KHO" title="Khách hàng" description="Thông tin khách hàng và đầu mối liên hệ."
      actions={admin && <Button variant="contained" startIcon={<AddOutlined />} onClick={() => setEditor({ customer: null })}>Thêm khách hàng</Button>} />
    {activate.isError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => activate.reset()}>{errorMessage(activate.error)}</Alert>}
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <FilterBar>
        <TextField label="Tìm khách hàng" placeholder="Tìm mã, tên hoặc liên hệ…" value={search} onChange={(event) => setSearch(event.target.value)}
          slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined fontSize="small" /></InputAdornment> } }} />
        <TextField select label="Trạng thái khách hàng" value={status} onChange={(event) => { setStatus(event.target.value as StatusFilter); setPage(0); }}>
          <MenuItem value="active">Đang hoạt động</MenuItem><MenuItem value="inactive">Ngừng hoạt động</MenuItem><MenuItem value="all">Tất cả trạng thái</MenuItem>
        </TextField>
      </FilterBar><Divider />
      <Stack direction="row" justifyContent="space-between" sx={{ px: 2.5, py: 1.5 }}><Typography fontWeight={650} fontSize={13}>Danh sách khách hàng</Typography><Typography variant="caption" color="text.secondary">{customers.data?.totalCount ?? '—'} kết quả</Typography></Stack>
      <ServerDataTable<Customer> label="Danh sách khách hàng" minWidth={1040} rows={customers.data?.items ?? []} rowKey={(customer) => customer.id}
        loading={customers.isLoading} fetching={customers.isFetching} error={customers.isError ? errorMessage(customers.error) : undefined}
        onRetry={() => void customers.refetch()} totalCount={customers.data?.totalCount ?? 0} page={page} pageSize={pageSize}
        onPageChange={setPage} onPageSizeChange={(value) => { setPageSize(value); setPage(0); }}
        emptyTitle="Chưa có khách hàng phù hợp" emptyDescription={search || status !== 'active' ? 'Thay đổi từ khóa hoặc trạng thái để tìm khách hàng.' : 'Thêm khách hàng đầu tiên để lưu thông tin liên hệ.'}
        emptyAction={admin && !search && status === 'active' && <Button startIcon={<AddOutlined />} onClick={() => setEditor({ customer: null })}>Thêm khách hàng</Button>}
        head={<TableRow>{sortHeader('code', 'MÃ KH')}{sortHeader('name', 'KHÁCH HÀNG')}<TableCell>LIÊN HỆ</TableCell>{sortHeader('isActive', 'TRẠNG THÁI')}{sortHeader('createdAt', 'NGÀY TẠO')}{sortHeader('updatedAt', 'CẬP NHẬT')}<TableCell align="right">THAO TÁC</TableCell></TableRow>}
        renderRow={(customer) => <TableRow hover>
          <TableCell sx={{ fontFamily: 'ui-monospace, monospace', fontSize: 12, color: 'text.secondary' }}>{customer.code}</TableCell>
          <TableCell><Stack direction="row" spacing={1.5} alignItems="center"><Avatar variant="rounded" sx={{ bgcolor: 'action.selected', color: 'primary.main', width: 32, height: 32 }}><PersonOutline sx={{ fontSize: 18 }} /></Avatar><Button onClick={() => { activate.reset(); setSelectedId(customer.id); }} sx={{ p: 0, minHeight: 'auto', color: 'text.primary', textAlign: 'left', justifyContent: 'flex-start' }}>{customer.name}</Button></Stack></TableCell>
          <TableCell><Typography fontSize={12}>{customer.email || '—'}</Typography><Typography variant="caption" color="text.secondary">{customer.phone || '—'}</Typography></TableCell>
          <TableCell><CustomerStatus active={customer.isActive} /></TableCell>
          <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(customer.createdAt)}</TableCell>
          <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(customer.updatedAt)}</TableCell>
          <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
            <Tooltip title="Chi tiết"><IconButton size="small" aria-label={`Xem khách hàng ${customer.name}`} onClick={() => { activate.reset(); setSelectedId(customer.id); }}><VisibilityOutlined fontSize="small" /></IconButton></Tooltip>
            {admin && <><Tooltip title="Chỉnh sửa"><IconButton size="small" aria-label={`Sửa khách hàng ${customer.name}`} onClick={() => setEditor({ customer })}><EditOutlined fontSize="small" /></IconButton></Tooltip>
              {customer.isActive ? <Tooltip title="Ngừng hoạt động"><IconButton size="small" aria-label={`Ngừng hoạt động khách hàng ${customer.name}`} onClick={() => requestDeactivation(customer)}><BlockOutlined fontSize="small" /></IconButton></Tooltip>
                : <Tooltip title="Kích hoạt"><span><IconButton size="small" color="success" disabled={activate.isPending} aria-label={`Kích hoạt khách hàng ${customer.name}`} onClick={() => activate.mutate(customer)}><CheckCircleOutline fontSize="small" /></IconButton></span></Tooltip>}
            </>}
          </TableCell>
        </TableRow>} />
    </Paper>
    {editor && <CustomerEditor customer={editor.customer} onClose={() => setEditor(null)} onSaved={() => { setEditor(null); setPage(0); void refresh(); }} />}
    <CustomerDetail id={selectedId} admin={admin} onClose={() => setSelectedId(null)} onEdit={(customer) => setEditor({ customer })}
      onDeactivate={requestDeactivation} onActivate={(customer) => activate.mutate(customer)} activating={activate.isPending}
      activationError={activate.isError ? errorMessage(activate.error) : undefined} />
    <ConfirmDialog open={deactivating !== null} title="Ngừng hoạt động khách hàng?"
      description={`“${deactivating?.name ?? ''}” sẽ chuyển sang ngừng hoạt động. Thông tin khách hàng vẫn được giữ lại trong hệ thống.`}
      confirmLabel="Ngừng hoạt động" pending={deactivate.isPending} error={deactivate.isError ? errorMessage(deactivate.error) : undefined}
      onClose={() => setDeactivating(null)} onConfirm={() => deactivate.mutate()} />
  </>;
}

function CustomerEditor({ customer, onClose, onSaved }: { customer: Customer | null; onClose: () => void; onSaved: () => void }) {
  const toast = useToast();
  const [form, setForm] = useState<CustomerInput>(customer ? customerInput(customer) : { code: '', name: '', email: null, phone: null, address: null, isActive: true });
  const [validation, setValidation] = useState('');
  const [pendingDeactivation, setPendingDeactivation] = useState<CustomerInput | null>(null);
  const save = useMutation({ mutationFn: (input: CustomerInput) => customer ? updateCustomer(customer.id, input) : createCustomer(input), onSuccess: () => {
    toast(customer ? 'Đã cập nhật khách hàng.' : 'Đã thêm khách hàng.'); onSaved();
  } });
  function field<K extends keyof CustomerInput>(name: K, value: CustomerInput[K]) { setForm((previous) => ({ ...previous, [name]: value })); }
  function submit(event: FormEvent) {
    event.preventDefault(); setValidation('');
    if (!form.code.trim() || !form.name.trim()) { setValidation('Nhập mã và tên khách hàng.'); return; }
    const input = { ...form, code: form.code.trim(), name: form.name.trim(), email: form.email?.trim() || null, phone: form.phone?.trim() || null, address: form.address?.trim() || null };
    if (customer?.isActive && !input.isActive) setPendingDeactivation(input); else save.mutate(input);
  }
  return <>
    <Dialog open fullWidth maxWidth="md" onClose={save.isPending ? undefined : onClose}><Box component="form" onSubmit={submit}>
      <DialogTitle>{customer ? 'Chỉnh sửa khách hàng' : 'Thêm khách hàng mới'}<Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>Thông tin cơ bản và địa chỉ liên hệ</Typography></DialogTitle>
      <DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>
        {(validation || save.isError) && <Alert severity="error">{validation || errorMessage(save.error)}</Alert>}
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2.5 }}>
          <TextField autoFocus required label="Mã khách hàng" value={form.code} onChange={(event) => field('code', event.target.value)} helperText="Mã duy nhất; hệ thống chuẩn hóa chữ hoa." slotProps={{ htmlInput: { maxLength: 40 } }} />
          <TextField required label="Tên khách hàng" value={form.name} onChange={(event) => field('name', event.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
          <TextField label="Email khách hàng" type="email" value={form.email ?? ''} onChange={(event) => field('email', event.target.value)} slotProps={{ htmlInput: { maxLength: 254 } }} />
          <TextField label="Số điện thoại khách hàng" type="tel" value={form.phone ?? ''} onChange={(event) => field('phone', event.target.value)} slotProps={{ htmlInput: { maxLength: 40 } }} />
        </Box>
        <TextField label="Địa chỉ khách hàng" value={form.address ?? ''} onChange={(event) => field('address', event.target.value)} multiline minRows={3} slotProps={{ htmlInput: { maxLength: 1000 } }} />
        <FormControlLabel control={<Switch checked={form.isActive} onChange={(_, checked) => field('isActive', checked)} />} label="Khách hàng đang hoạt động" />
      </Stack></DialogContent>
      <DialogActions sx={{ p: 2.5 }}><Button onClick={onClose} disabled={save.isPending}>Hủy</Button><Button type="submit" variant="contained" disabled={save.isPending || !form.code.trim() || !form.name.trim()}>{save.isPending ? 'Đang lưu…' : 'Lưu khách hàng'}</Button></DialogActions>
    </Box></Dialog>
    <ConfirmDialog open={pendingDeactivation !== null} title="Ngừng hoạt động khách hàng?"
      description="Các thay đổi sẽ được lưu và khách hàng chuyển sang ngừng hoạt động. Thông tin vẫn được giữ lại trong hệ thống."
      confirmLabel="Lưu và ngừng hoạt động" pending={save.isPending} error={save.isError ? errorMessage(save.error) : undefined}
      onClose={() => setPendingDeactivation(null)} onConfirm={() => { if (pendingDeactivation) save.mutate(pendingDeactivation); }} />
  </>;
}

function CustomerDetail({ id, admin, onClose, onEdit, onDeactivate, onActivate, activating, activationError }: {
  id: number | null; admin: boolean; onClose: () => void; onEdit: (customer: Customer) => void;
  onDeactivate: (customer: Customer) => void; onActivate: (customer: Customer) => void; activating: boolean; activationError?: string;
}) {
  const customer = useQuery({ queryKey: ['customer', id], enabled: id !== null, queryFn: ({ signal }) => getCustomer(id!, signal) });
  const item = customer.data;
  return <Drawer anchor="right" open={id !== null} onClose={activating ? undefined : onClose} sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', sm: 490 } } }}>
    <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ p: 3 }}><Typography variant="h6">Chi tiết khách hàng</Typography><IconButton aria-label="Đóng chi tiết khách hàng" onClick={onClose} disabled={activating}><CloseOutlined /></IconButton></Stack><Divider />
    {customer.isLoading ? <Stack spacing={2} sx={{ p: 3 }}><Skeleton height={80} /><Skeleton /><Skeleton /><Skeleton /></Stack>
      : customer.isError ? <Alert severity="error" sx={{ m: 3 }} action={<Button color="inherit" onClick={() => void customer.refetch()}>Thử lại</Button>}>{errorMessage(customer.error)}</Alert>
      : item && <Stack spacing={3} sx={{ p: 3 }}>
        <Stack direction="row" spacing={2} alignItems="center"><Avatar sx={{ width: 56, height: 56, bgcolor: 'action.selected', color: 'primary.main' }}><PersonOutline /></Avatar><Box><Typography variant="overline" color="text.secondary">{item.code}</Typography><Typography variant="h5" sx={{ overflowWrap: 'anywhere' }}>{item.name}</Typography></Box></Stack>
        <Box><CustomerStatus active={item.isActive} /></Box>{activationError && <Alert severity="error">{activationError}</Alert>}<Divider />
        {[['Email', item.email], ['Số điện thoại', item.phone], ['Địa chỉ', item.address]].map(([label, value]) => <Box key={label}><Typography variant="caption" color="text.secondary">{label}</Typography><Typography sx={{ mt: 0.5, whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{value || 'Chưa cập nhật'}</Typography></Box>)}
        <Divider /><Typography variant="caption" color="text.secondary">Ngày tạo: {dateTime(item.createdAt)}<br />Cập nhật: {dateTime(item.updatedAt)}</Typography>
        {admin && <Stack spacing={1.5}><Button variant="contained" startIcon={<EditOutlined />} disabled={activating} onClick={() => onEdit(item)}>Chỉnh sửa khách hàng</Button>
          {item.isActive ? <Button variant="outlined" color="error" startIcon={<BlockOutlined />} disabled={activating} onClick={() => onDeactivate(item)}>Ngừng hoạt động</Button>
            : <Button variant="outlined" color="success" startIcon={<CheckCircleOutline />} disabled={activating} onClick={() => onActivate(item)}>{activating ? 'Đang kích hoạt…' : 'Kích hoạt khách hàng'}</Button>}
        </Stack>}
      </Stack>}
  </Drawer>;
}
