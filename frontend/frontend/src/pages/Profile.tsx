import { useState } from 'react';
import type { FormEvent } from 'react';
import { Alert, Avatar, Box, Button, Chip, Paper, Stack, TextField, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { useAuth } from '../auth';
import { api, errorMessage } from '../api';
import { PageHeading, useToast } from '../shared';
import type { User } from '../types';

export default function Profile() {
  const { user, updateUser, clearSession } = useAuth();
  const toast = useToast();
  const [fullName, setFullName] = useState(user?.fullName ?? '');
  const [email, setEmail] = useState(user?.email ?? '');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [repeatPassword, setRepeatPassword] = useState('');
  const [validation, setValidation] = useState('');
  const profile = useMutation({ mutationFn: async () => (await api.put<User>('/auth/profile', { fullName: fullName.trim(), email: email.trim() })).data, onSuccess: (value) => { updateUser(value); toast('Đã cập nhật hồ sơ.'); } });
  const password = useMutation({ mutationFn: () => api.post('/auth/change-password', { currentPassword, newPassword }), onSuccess: () => { setCurrentPassword(''); setNewPassword(''); setRepeatPassword(''); clearSession('Đã đổi mật khẩu. Vui lòng đăng nhập lại.'); } });
  function changePassword(event: FormEvent) { event.preventDefault(); setValidation(''); if (newPassword !== repeatPassword) { setValidation('Mật khẩu xác nhận chưa khớp.'); return; } password.mutate(); }
  return <><PageHeading eyebrow="TÀI KHOẢN" title="Hồ sơ của tôi" description="Cập nhật thông tin cá nhân và bảo vệ tài khoản của bạn." /><Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' }, gap: 3, alignItems: 'start' }}><Paper variant="outlined" component="form" onSubmit={(event) => { event.preventDefault(); profile.mutate(); }} sx={{ p: 3.5 }}><Stack spacing={3}><Stack direction="row" spacing={2} alignItems="center"><Avatar sx={{ bgcolor: '#EAF0FF', color: 'primary.main', width: 56, height: 56, fontWeight: 800 }}>{(user?.fullName || user?.username)?.[0]?.toUpperCase()}</Avatar><Box><Typography variant="h6">Thông tin cá nhân</Typography><Chip label={user?.role} size="small" sx={{ mt: 0.5 }} /></Box></Stack>{profile.isError && <Alert severity="error">{errorMessage(profile.error)}</Alert>}<TextField label="Tên đăng nhập" value={user?.username ?? ''} disabled helperText="Tên đăng nhập do quản trị viên quản lý." /><TextField label="Họ và tên" value={fullName} onChange={(event) => setFullName(event.target.value)} required slotProps={{ htmlInput: { maxLength: 160 } }} /><TextField label="Email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required slotProps={{ htmlInput: { maxLength: 254 } }} /><Button variant="contained" type="submit" disabled={profile.isPending} sx={{ alignSelf: 'start' }}>{profile.isPending ? 'Đang lưu…' : 'Lưu thay đổi'}</Button></Stack></Paper><Paper variant="outlined" component="form" onSubmit={changePassword} sx={{ p: 3.5 }}><Stack spacing={3}><Box><Typography variant="h6">Đổi mật khẩu</Typography><Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Sau khi đổi mật khẩu, bạn cần đăng nhập lại.</Typography></Box>{(validation || password.isError) && <Alert severity="error">{validation || errorMessage(password.error)}</Alert>}<TextField label="Mật khẩu hiện tại" type="password" autoComplete="current-password" required value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} /><TextField label="Mật khẩu mới" type="password" autoComplete="new-password" required value={newPassword} onChange={(event) => setNewPassword(event.target.value)} slotProps={{ htmlInput: { minLength: 12, maxLength: 128 } }} helperText="Từ 12 đến 128 ký tự." /><TextField label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" required value={repeatPassword} onChange={(event) => setRepeatPassword(event.target.value)} /><Button variant="outlined" type="submit" disabled={password.isPending} sx={{ alignSelf: 'start' }}>{password.isPending ? 'Đang cập nhật…' : 'Cập nhật mật khẩu'}</Button></Stack></Paper></Box></>;
}
