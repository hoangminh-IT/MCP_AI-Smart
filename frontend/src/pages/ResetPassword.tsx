import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Link, Stack, TextField, Typography } from '@mui/material';
import { AuthLayout } from '../components/AuthLayout';
import { errorMessage, resetForgottenPassword } from '../api';
import { useAuth } from '../auth';

export default function ResetPassword() {
  const [token] = useState(() => new URLSearchParams(window.location.hash.slice(1)).get('token') ?? '');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);
  const { clearSession } = useAuth();
  const navigate = useNavigate();
  useEffect(() => { if (window.location.hash) window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search); }, []);
  async function submit(event: FormEvent) {
    event.preventDefault(); if (pending) return;
    setError('');
    if (!password.trim()) { setError('Mật khẩu không được để trống hoặc chỉ có khoảng trắng.'); return; }
    if (password !== confirmation) { setError('Mật khẩu xác nhận chưa khớp.'); return; }
    setPending(true);
    try {
      await resetForgottenPassword(token, password, confirmation);
      setPassword(''); setConfirmation('');
      clearSession('Đã đặt lại mật khẩu. Vui lòng đăng nhập lại.');
      navigate('/login?passwordReset=1', { replace: true });
    } catch (value) { setError(errorMessage(value)); }
    finally { setPending(false); }
  }
  const validLink = /^[0-9A-Fa-f]{64}$/.test(token);
  return <AuthLayout><Box component="form" onSubmit={(event) => void submit(event)}>
    <Typography component="h2" sx={{ fontSize: { xs: 29, sm: 33 }, fontWeight: 850, letterSpacing: -1 }}>Đặt lại mật khẩu</Typography>
    <Typography color="text.secondary" sx={{ mt: 1.2, mb: 3, fontSize: 14, lineHeight: 1.8 }}>Chọn mật khẩu mới cho tài khoản. Các phiên đăng nhập cũ sẽ kết thúc sau khi cập nhật.</Typography>
    <Stack spacing={2.5}>
      {!validLink && <Alert severity="warning">Liên kết không hợp lệ. Hãy yêu cầu một liên kết đặt lại mật khẩu mới.</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      {validLink && <>
        <TextField label="Mật khẩu mới" type="password" autoComplete="new-password" autoFocus required fullWidth value={password} disabled={pending} onChange={(event) => setPassword(event.target.value)} />
        <TextField label="Xác nhận mật khẩu mới" type="password" autoComplete="new-password" required fullWidth value={confirmation} disabled={pending} onChange={(event) => setConfirmation(event.target.value)} />
        <Button type="submit" variant="contained" fullWidth size="large" disabled={pending}>{pending ? 'Đang cập nhật…' : 'Lưu mật khẩu mới'}</Button>
      </>}
      <Link component={RouterLink} to="/forgot-password" underline="hover" textAlign="center">Yêu cầu liên kết mới</Link>
      <Link component={RouterLink} to="/login" underline="hover" textAlign="center">Quay lại đăng nhập</Link>
    </Stack>
  </Box></AuthLayout>;
}
