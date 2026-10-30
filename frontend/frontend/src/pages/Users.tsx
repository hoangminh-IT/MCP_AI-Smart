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
import PeopleOutline from '@mui/icons-material/PeopleOutline';
import KeyOutlined from '@mui/icons-material/KeyOutlined';
import { dateTime, errorMessage } from '../api';
import { useAuth } from '../auth';
import { ConfirmDialog, FilterBar, PageHeading, useDebounced, useToast } from '../shared';
import { ServerDataTable } from '../components/ServerDataTable';
import { createUser, getUser, listUsers, resetUserPassword, updateUser, userInput } from '../users';
import type { CreateUserInput, ManagedUser, UserInput, UserQuery, UserRole, UserSort } from '../users';

type StatusFilter = 'all' | 'active' | 'inactive';
const roleLabels: Record<UserRole, string> = { Admin: 'Quản trị viên', Staff: 'Nhân viên', Customer: 'Khách hàng' };
const roleDescriptions: Record<UserRole, string> = {
  Customer: 'Xem, cập nhật hồ sơ và đổi mật khẩu cá nhân.',
  Staff: 'Sử dụng kho, đơn hàng, báo cáo và AI theo quyền được cấp.',
  Admin: 'Quản trị tài khoản, phân quyền và cấu hình.',
};
function UserStatus({ active }: { active: boolean }) {
  return <Chip size="small" label={active ? 'Hoạt động' : 'Ngừng hoạt động'} color={active ? 'success' : 'default'} variant="outlined" />;
}

export default function Users() {
  const { user } = useAuth();
  if (user?.role !== 'Admin') return <>
    <PageHeading eyebrow="HỆ THỐNG" title="Người dùng" description="Quản lý tài khoản và quyền truy cập." />
    <Alert severity="error">Bạn không có quyền quản lý người dùng. Chức năng này chỉ dành cho quản trị viên.</Alert>
  </>;
  return <UserManagement />;
}

function UserManagement() {
  const auth = useAuth(); const cache = useQueryClient(); const toast = useToast();
  const [search, setSearch] = useState(''); const debouncedSearch = useDebounced(search.trim());
  const [role, setRole] = useState<UserRole | ''>(''); const [status, setStatus] = useState<StatusFilter>('all');
  const [page, setPage] = useState(0); const [pageSize, setPageSize] = usePreferredPageSize();
  const [sortBy, setSortBy] = useState<UserSort>('username'); const [descending, setDescending] = useState(false);
  const [editor, setEditor] = useState<{ user: ManagedUser | null } | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [changingAccess, setChangingAccess] = useState<ManagedUser | null>(null);
  const [resetting, setResetting] = useState<ManagedUser | null>(null);
  useEffect(() => setPage(0), [debouncedSearch]);
  const query: UserQuery = { search: debouncedSearch || undefined, role: role || undefined, isActive: status === 'all' ? undefined : status === 'active', page: page + 1, pageSize, sortBy, descending };
  const users = useQuery({ queryKey: ['users', query], queryFn: ({ signal }) => listUsers(query, signal), placeholderData: keepPreviousData });
  async function refresh() {
    await Promise.all([cache.invalidateQueries({ queryKey: ['users'] }), cache.invalidateQueries({ queryKey: ['managed-user'] })]);
  }
  async function saved(user: ManagedUser, previous: ManagedUser | null) {
    setEditor(null); setChangingAccess(null); setPage(0);
    if (user.id === auth.user?.id && previous && (previous.role !== user.role || previous.isActive !== user.isActive)) {
      auth.clearSession('Quyền truy cập của bạn đã thay đổi. Vui lòng đăng nhập lại.'); return;
    }
    if (user.id === auth.user?.id) auth.updateUser(user);
    cache.setQueryData(['managed-user', user.id], user); await refresh();
  }
  const toggleAccess = useMutation({ mutationFn: (user: ManagedUser) => updateUser(user.id, { ...userInput(user), isActive: !user.isActive }),
    onSuccess: async (updated, previous) => { toast(updated.isActive ? 'Đã kích hoạt tài khoản.' : 'Đã ngừng hoạt động tài khoản và thu hồi phiên đăng nhập.'); await saved(updated, previous); },
  });
  function changeAccess(user: ManagedUser) { toggleAccess.reset(); setChangingAccess(user); }
  function sort(field: UserSort) { setDescending(sortBy === field ? !descending : field === 'createdAt' || field === 'updatedAt'); setSortBy(field); setPage(0); }
  function sortHeader(field: UserSort, label: string) {
    return <TableCell sortDirection={sortBy === field ? descending ? 'desc' : 'asc' : false}><TableSortLabel active={sortBy === field} direction={descending ? 'desc' : 'asc'} onClick={() => sort(field)}>{label}</TableSortLabel></TableCell>;
  }
  return <>
    <PageHeading eyebrow="HỆ THỐNG" title="Người dùng" description="Quản lý tài khoản, phê duyệt nhân viên và cấp quyền truy cập."
      actions={<Button variant="contained" startIcon={<AddOutlined />} onClick={() => setEditor({ user: null })}>Thêm người dùng</Button>} />
    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 250px), 1fr))', gap: 1.5, mb: 2 }}>
      {(['Customer', 'Staff', 'Admin'] as const).map((item) => <Paper key={item} variant="outlined" sx={{ p: 1.75, borderColor: role === item ? 'primary.main' : 'divider', bgcolor: role === item ? 'action.selected' : 'background.paper' }}>
        <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={1} flexWrap="wrap" useFlexGap><Chip size="small" label={roleLabels[item]} color={item === 'Admin' ? 'primary' : item === 'Staff' ? 'success' : 'default'} variant="outlined" /><Button size="small" onClick={() => { setRole(item); setPage(0); }}>Xem tài khoản</Button></Stack>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1, lineHeight: 1.6, fontSize: 12 }}>{roleDescriptions[item]}</Typography>
      </Paper>)}
    </Box>
    <Typography variant="body2" color="text.secondary" sx={{ mb: 2, fontSize: 12 }}>Tài khoản đăng ký mới là <strong>Khách hàng</strong>. Để cấp quyền làm việc, chọn <strong>Chỉnh sửa</strong> và đổi vai trò thành <strong>Nhân viên</strong>.</Typography>
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <FilterBar>
        <TextField label="Tìm người dùng" placeholder="Tên đăng nhập, họ tên hoặc email…" value={search} onChange={(event) => setSearch(event.target.value)} slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined fontSize="small" /></InputAdornment> } }} />
        <TextField select label="Vai trò người dùng" value={role} onChange={(event) => { setRole(event.target.value as UserRole | ''); setPage(0); }}><MenuItem value="">Tất cả vai trò</MenuItem><MenuItem value="Admin">Quản trị viên</MenuItem><MenuItem value="Staff">Nhân viên</MenuItem><MenuItem value="Customer">Khách hàng</MenuItem></TextField>
        <TextField select label="Trạng thái người dùng" value={status} onChange={(event) => { setStatus(event.target.value as StatusFilter); setPage(0); }}><MenuItem value="all">Tất cả trạng thái</MenuItem><MenuItem value="active">Đang hoạt động</MenuItem><MenuItem value="inactive">Ngừng hoạt động</MenuItem></TextField>
      </FilterBar><Divider />
      <Stack direction="row" justifyContent="space-between" sx={{ px: 2.5, py: 1.5 }}><Typography fontWeight={650} fontSize={13}>Danh sách người dùng</Typography><Typography variant="caption" color="text.secondary">{users.data?.totalCount ?? '—'} kết quả</Typography></Stack>
      <ServerDataTable<ManagedUser> label="Danh sách người dùng" minWidth={1130} rows={users.data?.items ?? []} rowKey={(user) => user.id}
        loading={users.isLoading} fetching={users.isFetching} error={users.isError ? errorMessage(users.error) : undefined} onRetry={() => void users.refetch()}
        totalCount={users.data?.totalCount ?? 0} page={page} pageSize={pageSize} onPageChange={setPage} onPageSizeChange={(value) => { setPageSize(value); setPage(0); }}
        emptyTitle="Chưa có người dùng phù hợp" emptyDescription="Thay đổi từ khóa, vai trò hoặc trạng thái để tìm tài khoản."
        head={<TableRow>{sortHeader('username', 'TÊN ĐĂNG NHẬP')}{sortHeader('fullName', 'HỌ TÊN')}{sortHeader('email', 'EMAIL')}{sortHeader('role', 'VAI TRÒ')}{sortHeader('isActive', 'TRẠNG THÁI')}{sortHeader('createdAt', 'NGÀY TẠO')}{sortHeader('updatedAt', 'CẬP NHẬT')}<TableCell align="right">THAO TÁC</TableCell></TableRow>}
        renderRow={(user) => <TableRow hover>
          <TableCell><Button sx={{ p: 0, minHeight: 'auto', textAlign: 'left', fontFamily: 'ui-monospace, monospace' }} onClick={() => setSelectedId(user.id)}>{user.username}</Button>{user.id === auth.user?.id && <Chip label="Bạn" size="small" sx={{ ml: 1 }} />}</TableCell>
          <TableCell sx={{ fontWeight: 650 }}>{user.fullName}</TableCell><TableCell sx={{ fontSize: 12 }}>{user.email}</TableCell>
          <TableCell><Chip label={roleLabels[user.role]} size="small" color={user.role === 'Admin' ? 'primary' : user.role === 'Staff' ? 'success' : 'default'} variant="outlined" /></TableCell><TableCell><UserStatus active={user.isActive} /></TableCell>
          <TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(user.createdAt)}</TableCell><TableCell sx={{ fontSize: 12, whiteSpace: 'nowrap', color: 'text.secondary' }}>{dateTime(user.updatedAt)}</TableCell>
          <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
            <Tooltip title="Chi tiết"><IconButton size="small" aria-label={`Xem người dùng ${user.username}`} onClick={() => setSelectedId(user.id)}><VisibilityOutlined fontSize="small" /></IconButton></Tooltip>
            <Tooltip title="Chỉnh sửa"><IconButton size="small" aria-label={`Sửa người dùng ${user.username}`} onClick={() => setEditor({ user })}><EditOutlined fontSize="small" /></IconButton></Tooltip>
            <Tooltip title="Đặt lại mật khẩu"><IconButton size="small" aria-label={`Đặt lại mật khẩu người dùng ${user.username}`} onClick={() => setResetting(user)}><KeyOutlined fontSize="small" /></IconButton></Tooltip>
            <Tooltip title={user.isActive ? 'Ngừng hoạt động' : 'Kích hoạt'}><IconButton size="small" color={user.isActive ? 'default' : 'success'} aria-label={`${user.isActive ? 'Ngừng hoạt động' : 'Kích hoạt'} người dùng ${user.username}`} onClick={() => changeAccess(user)}>{user.isActive ? <BlockOutlined fontSize="small" /> : <CheckCircleOutline fontSize="small" />}</IconButton></Tooltip>
          </TableCell>
        </TableRow>} />
    </Paper>
    {editor && <UserEditor user={editor.user} self={editor.user?.id === auth.user?.id} onClose={() => setEditor(null)} onSaved={(user) => void saved(user, editor.user)} />}
    <UserDetail id={selectedId} onClose={() => setSelectedId(null)} onEdit={(user) => setEditor({ user })} onChangeAccess={changeAccess} onResetPassword={setResetting} />
    <ConfirmDialog open={changingAccess !== null} title={changingAccess?.isActive ? 'Ngừng hoạt động người dùng?' : 'Kích hoạt người dùng?'}
      description={`${changingAccess?.username ?? ''} sẽ ${changingAccess?.isActive ? 'ngừng hoạt động và các phiên đăng nhập sẽ bị thu hồi' : 'được phép đăng nhập trở lại'}.${changingAccess?.id === auth.user?.id ? ' Đây là tài khoản của bạn; phiên hiện tại sẽ kết thúc.' : ''} Hệ thống sẽ giữ ít nhất một quản trị viên đang hoạt động.`}
      confirmLabel={changingAccess?.isActive ? 'Ngừng hoạt động' : 'Kích hoạt'} pending={toggleAccess.isPending} error={toggleAccess.isError ? errorMessage(toggleAccess.error) : undefined}
      onClose={() => setChangingAccess(null)} onConfirm={() => changingAccess && toggleAccess.mutate(changingAccess)} />
    {resetting && <ResetPasswordDialog user={resetting} self={resetting.id === auth.user?.id} onClose={() => setResetting(null)} onCompleted={() => {
      setResetting(null); if (resetting.id === auth.user?.id) auth.clearSession('Mật khẩu đã được đặt lại. Vui lòng đăng nhập bằng mật khẩu mới.');
    }} />}
  </>;
}

function UserEditor({ user, self, onClose, onSaved }: { user: ManagedUser | null; self: boolean; onClose: () => void; onSaved: (user: ManagedUser) => void }) {
  const toast = useToast();
  const [form, setForm] = useState<CreateUserInput>(user ? { ...userInput(user), username: user.username, password: '' } : { username: '', email: '', fullName: '', role: 'Staff', isActive: true, password: '' });
  const [confirmation, setConfirmation] = useState(''); const [validation, setValidation] = useState(''); const [pendingInput, setPendingInput] = useState<UserInput | null>(null);
  const save = useMutation({ mutationFn: (input: UserInput | CreateUserInput) => user ? updateUser(user.id, input) : createUser(input as CreateUserInput),
    onSuccess: (saved) => { setForm((previous) => ({ ...previous, password: '' })); setConfirmation(''); toast(user ? 'Đã cập nhật người dùng.' : 'Đã tạo tài khoản người dùng.'); onSaved(saved); },
  });
  function field<K extends keyof CreateUserInput>(name: K, value: CreateUserInput[K]) { setForm((previous) => ({ ...previous, [name]: value })); }
  function submit(event: FormEvent) {
    event.preventDefault(); setValidation(''); save.reset();
    if (!form.email.trim() || !form.fullName.trim()) { setValidation('Nhập email và họ tên người dùng.'); return; }
    const input: UserInput = { email: form.email.trim(), fullName: form.fullName.trim(), role: form.role, isActive: form.isActive };
    if (!user) {
      if (!/^[A-Za-z0-9._-]{1,80}$/.test(form.username.trim())) { setValidation('Tên đăng nhập chỉ gồm chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang; tối đa 80 ký tự.'); return; }
      if (form.password.length < 12 || form.password.length > 128) { setValidation('Mật khẩu cần từ 12 đến 128 ký tự.'); return; }
      if (form.password !== confirmation) { setValidation('Hai mật khẩu không khớp.'); return; }
      save.mutate({ ...input, username: form.username.trim(), password: form.password });
    } else if (user.role !== input.role || user.isActive !== input.isActive) setPendingInput(input);
    else save.mutate(input);
  }
  return <>
    <Dialog open fullWidth maxWidth="md" onClose={save.isPending ? undefined : onClose}><Box component="form" onSubmit={submit}>
      <DialogTitle>{user ? 'Chỉnh sửa người dùng' : 'Thêm người dùng mới'}<Typography variant="body2" color="text.secondary" sx={{ mt: .5 }}>{user ? `Tài khoản ${user.username}; tên đăng nhập được giữ cố định.` : 'Cấp tài khoản và quyền truy cập ban đầu.'}</Typography></DialogTitle>
      <DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>
        {(validation || save.isError) && <Alert severity="error">{validation || errorMessage(save.error)}</Alert>}
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2.5 }}>
          <TextField label="Tên đăng nhập" value={form.username} required autoFocus={!user} disabled={save.isPending} onChange={(event) => field('username', event.target.value)} autoComplete="off" slotProps={{ input: { readOnly: Boolean(user) }, htmlInput: { maxLength: 80 } }} helperText={user ? 'Không thể thay đổi tên đăng nhập.' : 'Phân biệt chữ hoa/thường; chữ, số, dấu . _ -'} />
          <TextField label="Họ tên người dùng" value={form.fullName} required autoFocus={Boolean(user)} disabled={save.isPending} onChange={(event) => field('fullName', event.target.value)} slotProps={{ htmlInput: { maxLength: 160 } }} />
          <TextField label="Email người dùng" type="email" value={form.email} required disabled={save.isPending} onChange={(event) => field('email', event.target.value)} slotProps={{ htmlInput: { maxLength: 254 } }} />
          <TextField select label="Vai trò" value={form.role} disabled={save.isPending} onChange={(event) => field('role', event.target.value as UserRole)}><MenuItem value="Customer">Khách hàng</MenuItem><MenuItem value="Staff">Nhân viên</MenuItem><MenuItem value="Admin">Quản trị viên</MenuItem></TextField>
          {!user && <><TextField label="Mật khẩu tạm thời" type="password" value={form.password} required disabled={save.isPending} onChange={(event) => field('password', event.target.value)} autoComplete="new-password" helperText="12–128 ký tự; không tự thay đổi khoảng trắng." slotProps={{ htmlInput: { minLength: 12, maxLength: 128 } }} /><TextField label="Nhập lại mật khẩu tạm thời" type="password" value={confirmation} required disabled={save.isPending} onChange={(event) => setConfirmation(event.target.value)} autoComplete="new-password" slotProps={{ htmlInput: { minLength: 12, maxLength: 128 } }} /></>}
        </Box>
        <Alert severity={form.role === 'Admin' ? 'warning' : 'info'}><strong>{roleLabels[form.role]}:</strong> {roleDescriptions[form.role]}</Alert>
        <FormControlLabel control={<Switch checked={form.isActive} disabled={save.isPending} onChange={(_, checked) => field('isActive', checked)} />} label="Người dùng đang hoạt động" />
        {user && <Alert severity="info">Thay đổi vai trò hoặc trạng thái sẽ thu hồi các phiên hiện có.{self ? ' Bạn đang chỉnh sửa tài khoản của chính mình.' : ''} Quản trị viên đang hoạt động cuối cùng được máy chủ bảo vệ.</Alert>}
      </Stack></DialogContent><DialogActions sx={{ p: 2.5 }}><Button onClick={onClose} disabled={save.isPending}>Hủy</Button><Button type="submit" variant="contained" disabled={save.isPending}>{save.isPending ? 'Đang lưu…' : 'Lưu người dùng'}</Button></DialogActions>
    </Box></Dialog>
    <ConfirmDialog open={pendingInput !== null} title="Thay đổi quyền truy cập?" description={`${user?.username ?? ''}: ${user ? roleLabels[user.role] : ''} → ${pendingInput ? roleLabels[pendingInput.role] : ''}. ${pendingInput ? roleDescriptions[pendingInput.role] : ''} Các phiên hiện có sẽ bị thu hồi và người dùng cần đăng nhập lại.${self ? ' Phiên của bạn sẽ kết thúc sau khi lưu thành công.' : ''}`}
      confirmLabel="Lưu thay đổi quyền" pending={save.isPending} error={save.isError ? errorMessage(save.error) : undefined} onClose={() => setPendingInput(null)} onConfirm={() => pendingInput && save.mutate(pendingInput)} />
  </>;
}

function ResetPasswordDialog({ user, self, onClose, onCompleted }: { user: ManagedUser; self: boolean; onClose: () => void; onCompleted: () => void }) {
  const toast = useToast(); const [password, setPassword] = useState(''); const [confirmation, setConfirmation] = useState(''); const [validation, setValidation] = useState(''); const [review, setReview] = useState(false);
  const reset = useMutation({ mutationFn: () => resetUserPassword(user.id, password), onSuccess: () => { setPassword(''); setConfirmation(''); toast('Đã đặt lại mật khẩu và thu hồi các phiên đăng nhập của tài khoản.'); onCompleted(); } });
  function submit(event: FormEvent) {
    event.preventDefault(); setValidation(''); reset.reset();
    if (password.length < 12 || password.length > 128) { setValidation('Mật khẩu cần từ 12 đến 128 ký tự.'); return; }
    if (password !== confirmation) { setValidation('Hai mật khẩu không khớp.'); return; }
    setReview(true);
  }
  return <>
    <Dialog open fullWidth maxWidth="sm" onClose={reset.isPending ? undefined : onClose}><Box component="form" onSubmit={submit}>
      <DialogTitle>Đặt lại mật khẩu · {user.username}</DialogTitle><DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>
        <Alert severity="warning">Các phiên hiện có của tài khoản sẽ bị thu hồi.{self ? ' Đây là tài khoản của bạn; bạn cần đăng nhập lại sau khi đặt mật khẩu.' : ''}</Alert>
        {(validation || reset.isError) && <Alert severity="error">{validation || errorMessage(reset.error)}</Alert>}
        <TextField label="Mật khẩu mới cho người dùng" autoFocus type="password" required value={password} disabled={reset.isPending} onChange={(event) => setPassword(event.target.value)} autoComplete="new-password" helperText="Từ 12 đến 128 ký tự." slotProps={{ htmlInput: { minLength: 12, maxLength: 128 } }} />
        <TextField label="Nhập lại mật khẩu mới cho người dùng" type="password" required value={confirmation} disabled={reset.isPending} onChange={(event) => setConfirmation(event.target.value)} autoComplete="new-password" slotProps={{ htmlInput: { minLength: 12, maxLength: 128 } }} />
      </Stack></DialogContent><DialogActions sx={{ p: 2.5 }}><Button disabled={reset.isPending} onClick={onClose}>Hủy</Button><Button type="submit" variant="contained" disabled={reset.isPending}>Đặt lại mật khẩu</Button></DialogActions>
    </Box></Dialog>
    <ConfirmDialog open={review} title="Xác nhận đặt lại mật khẩu?" description={`Mật khẩu của ${user.username} sẽ được thay thế và các phiên hiện có sẽ bị thu hồi.${self ? ' Phiên hiện tại của bạn sẽ kết thúc.' : ''}`}
      confirmLabel="Xác nhận đặt lại mật khẩu" pending={reset.isPending} error={reset.isError ? errorMessage(reset.error) : undefined} onClose={() => setReview(false)} onConfirm={() => reset.mutate()} />
  </>;
}

function UserDetail({ id, onClose, onEdit, onChangeAccess, onResetPassword }: { id: number | null; onClose: () => void; onEdit: (user: ManagedUser) => void; onChangeAccess: (user: ManagedUser) => void; onResetPassword: (user: ManagedUser) => void }) {
  const user = useQuery({ queryKey: ['managed-user', id], enabled: id !== null, queryFn: ({ signal }) => getUser(id!, signal) }); const item = user.data;
  return <Drawer anchor="right" open={id !== null} onClose={onClose} sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', sm: 510 } } }}>
    <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ p: 3 }}><Typography variant="h6">Chi tiết người dùng</Typography><IconButton aria-label="Đóng chi tiết người dùng" onClick={onClose}><CloseOutlined /></IconButton></Stack><Divider />
    {user.isLoading ? <Stack spacing={2} sx={{ p: 3 }}><Skeleton height={80} /><Skeleton /><Skeleton /><Skeleton /></Stack>
      : user.isError ? <Alert severity="error" sx={{ m: 3 }} action={<Button color="inherit" onClick={() => void user.refetch()}>Thử lại</Button>}>{errorMessage(user.error)}</Alert>
        : item && <Stack spacing={3} sx={{ p: 3 }}>
          <Stack direction="row" spacing={2} alignItems="center"><Avatar sx={{ width: 56, height: 56, bgcolor: 'action.selected', color: 'primary.main' }}><PeopleOutline /></Avatar><Box><Typography variant="overline" color="text.secondary">{item.username}</Typography><Typography variant="h5" sx={{ overflowWrap: 'anywhere' }}>{item.fullName}</Typography></Box></Stack>
          <Stack direction="row" spacing={1}><UserStatus active={item.isActive} /><Chip label={roleLabels[item.role]} color={item.role === 'Admin' ? 'primary' : item.role === 'Staff' ? 'success' : 'default'} variant="outlined" /></Stack><Divider />
          <Alert severity="info">{roleDescriptions[item.role]}</Alert>
          <Box><Typography variant="caption" color="text.secondary">Email</Typography><Typography sx={{ mt: .5, overflowWrap: 'anywhere' }}>{item.email}</Typography></Box>
          <Typography variant="caption" color="text.secondary">Ngày tạo: {dateTime(item.createdAt)}<br />Cập nhật: {dateTime(item.updatedAt)}</Typography>
          <Stack spacing={1.5}><Button variant="contained" startIcon={<EditOutlined />} onClick={() => onEdit(item)}>Chỉnh sửa người dùng</Button><Button variant="outlined" startIcon={<KeyOutlined />} onClick={() => onResetPassword(item)}>Đặt lại mật khẩu</Button><Button variant="outlined" color={item.isActive ? 'error' : 'success'} startIcon={item.isActive ? <BlockOutlined /> : <CheckCircleOutline />} onClick={() => onChangeAccess(item)}>{item.isActive ? 'Ngừng hoạt động' : 'Kích hoạt người dùng'}</Button></Stack>
          <Alert severity="info" variant="outlined">Thay đổi quyền, trạng thái hoặc mật khẩu được kiểm tra tại máy chủ. Không thể vô hiệu hóa quản trị viên đang hoạt động cuối cùng.</Alert>
        </Stack>}
  </Drawer>;
}
