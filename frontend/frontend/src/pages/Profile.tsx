import { useState } from 'react';
import type { FormEvent } from 'react';
import { alpha, Alert, Avatar, Box, Button, Chip, Divider, Paper, Stack, TextField, Typography } from '@mui/material';
import ManageAccountsOutlined from '@mui/icons-material/ManageAccountsOutlined';
import LockOutlined from '@mui/icons-material/LockOutlined';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '../auth';
import { api, errorMessage } from '../api';
import { PageHeading, useToast } from '../shared';
import type { User } from '../types';

export default function Profile() {
  const { user, updateUser, clearSession } = useAuth();
  const cache = useQueryClient(); const toast = useToast();
  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [email, setEmail] = useState(user?.email ?? '');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [repeatPassword, setRepeatPassword] = useState('');
  const [profileValidation, setProfileValidation] = useState(''); const [validation, setValidation] = useState('');
  const profile = useMutation({ mutationFn: async () => (await api.put<User>('/auth/profile', { fullName: fullName.trim(), email: email.trim() })).data, onSuccess: (value) => {
    updateUser(value); toast('Đã cập nhật hồ sơ.');
    void cache.invalidateQueries({ queryKey: ['users'] }); void cache.invalidateQueries({ queryKey: ['managed-user', value.id] }); void cache.invalidateQueries({ queryKey: ['audit-logs'] });
  } });
  const password = useMutation({ gcTime: 0, mutationFn: () => api.post('/auth/change-password', { currentPassword, newPassword }), onSuccess: () => {
    setCurrentPassword(''); setNewPassword(''); setRepeatPassword(''); clearSession('Đã đổi mật khẩu. Vui lòng đăng nhập lại.');
  } });
  const busy = profile.isPending || password.isPending;
  const roleLabel = user?.role === 'Admin' ? 'Quản trị viên' : user?.role === 'Staff' ? 'Nhân viên' : 'Khách hàng';
  function saveProfile(event: FormEvent) {
    event.preventDefault(); setProfileValidation(''); if (busy) return;
    if (!fullName.trim() || !email.trim()) { setProfileValidation('Nhập họ tên và email của bạn.'); return; }
    profile.mutate();
  }
  function changePassword(event: FormEvent) {
    event.preventDefault(); setValidation(''); if (busy) return;
    if (!currentPassword || currentPassword.length > 128) { setValidation('Nhập mật khẩu hiện tại, tối đa 128 ký tự.'); return; }
    if (newPassword.length < 12 || newPassword.length > 128) { setValidation('Mật khẩu mới cần từ 12 đến 128 ký tự.'); return; }
    if (newPassword !== repeatPassword) { setValidation('Mật khẩu xác nhận chưa khớp.'); return; }
    password.mutate();
  }
  return <>
    <PageHeading eyebrow="KHÔNG GIAN CÁ NHÂN" title="Hồ sơ của tôi" description="Quản lý thông tin liên hệ và bảo mật tài khoản của bạn." />
    <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, mb: 2.5 }}>
      <Stack direction="row" spacing={1.75} alignItems="center">
        <Avatar variant="rounded" sx={{ width: 52, height: 52, borderRadius: 2.5, bgcolor: (theme) => alpha(theme.palette.primary.main, .09), color: 'primary.main', fontSize: 21, fontWeight: 700 }}>{(user?.fullName || user?.username)?.trim().split(/\s+/).slice(-2).map((part) => part[0]).join('').toUpperCase()}</Avatar>
        <Box sx={{ flex: 1, minWidth: 0 }}><Stack direction="row" spacing={1.25} flexWrap="wrap" useFlexGap alignItems="center"><Typography component="h2" sx={{ fontSize: { xs: 17, sm: 19 }, fontWeight: 700, overflowWrap: 'anywhere' }}>{user?.fullName}</Typography><Chip label={roleLabel} size="small" color={user?.role === 'Admin' ? 'primary' : user?.role === 'Staff' ? 'success' : 'default'} variant="outlined" /></Stack><Typography variant="body2" color="text.secondary" sx={{ mt: .35, overflowWrap: 'anywhere', fontSize: 12 }}>@{user?.username} · {user?.email}</Typography></Box>
      </Stack>
      {user?.role !== 'Admin' && <><Divider sx={{ my: 1.75 }} /><Typography variant="caption" color="text.secondary">{user?.role === 'Customer' ? 'Bạn có thể quản lý hồ sơ cá nhân. Nếu cần sử dụng chức năng kho, liên hệ quản trị viên cấp quyền Nhân viên rồi đăng nhập lại.' : 'Bạn có quyền sử dụng nghiệp vụ kho theo phân quyền. Liên hệ quản trị viên khi cần thay đổi quyền truy cập.'}</Typography></>}
    </Paper>
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' }, gap: 2, alignItems: 'start' }}>
      <Paper variant="outlined" component="form" onSubmit={saveProfile} sx={{ p: { xs: 2, md: 2.5 } }}><Stack spacing={2.25}>
        <Stack direction="row" spacing={1.25} alignItems="center"><Box sx={{ display: 'grid', placeItems: 'center', bgcolor: 'action.hover', color: 'primary.main', width: 34, height: 34, borderRadius: 2 }}><ManageAccountsOutlined sx={{ fontSize: 20 }} /></Box><Box><Typography component="h2" sx={{ fontSize: 14, fontWeight: 700 }}>Thông tin cá nhân</Typography><Typography variant="caption" color="text.secondary">Thông tin liên hệ của bạn.</Typography></Box></Stack>
        {(profileValidation || profile.isError) && <Alert severity="error">{profileValidation || errorMessage(profile.error)}</Alert>}
        <TextField label="Tên đăng nhập" value={user?.username ?? ''} slotProps={{ input: { readOnly: true } }} helperText="Tên đăng nhập được giữ cố định." />
        <TextField label="Họ và tên" value={fullName} disabled={busy} onChange={(event) => setFullName(event.target.value)} required autoComplete="name" slotProps={{ htmlInput: { maxLength: 160 } }} />
        <TextField label="Email" type="email" value={email} disabled={busy} onChange={(event) => setEmail(event.target.value)} required autoComplete="email" slotProps={{ htmlInput: { maxLength: 254 } }} />
        <Box sx={{ pt: .25 }}><Button variant="contained" type="submit" disabled={busy}>{profile.isPending ? 'Đang lưu…' : 'Lưu thay đổi'}</Button></Box>
      </Stack></Paper>
      <Paper variant="outlined" component="form" onSubmit={changePassword} sx={{ p: { xs: 2, md: 2.5 } }}><Stack spacing={2.25}>
        <Stack direction="row" spacing={1.25} alignItems="center"><Box sx={{ display: 'grid', placeItems: 'center', bgcolor: 'action.hover', color: 'primary.main', width: 34, height: 34, borderRadius: 2, flexShrink: 0 }}><LockOutlined sx={{ fontSize: 20 }} /></Box><Box><Typography component="h2" sx={{ fontSize: 14, fontWeight: 700 }}>Đổi mật khẩu</Typography><Typography variant="caption" color="text.secondary">Bạn sẽ cần đăng nhập lại trên các thiết bị sau khi đổi.</Typography></Box></Stack>
        {(validation || password.isError) && <Alert severity="error">{validation || errorMessage(password.error)}</Alert>}
        <TextField label="Mật khẩu hiện tại" type="password" autoComplete="current-password" required value={currentPassword} disabled={busy} onChange={(event) => setCurrentPassword(event.target.value)} slotProps={{ htmlInput: { maxLength: 128 } }} />
        <TextField label="Mật khẩu mới" type="password" autoComplete="new-password" required value={newPassword} disabled={busy} onChange={(event) => setNewPassword(event.target.value)} slotProps={{ htmlInput: { minLength: 12, maxLength: 128 } }} helperText="Từ 12 đến 128 ký tự; khoảng trắng được giữ nguyên." />
        <TextField label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" required value={repeatPassword} disabled={busy} onChange={(event) => setRepeatPassword(event.target.value)} slotProps={{ htmlInput: { minLength: 12, maxLength: 128 } }} />
        <Button variant="outlined" type="submit" disabled={busy} sx={{ alignSelf: 'start' }}>{password.isPending ? 'Đang cập nhật…' : 'Cập nhật mật khẩu'}</Button>
      </Stack></Paper>
    </Box>
  </>;
}
