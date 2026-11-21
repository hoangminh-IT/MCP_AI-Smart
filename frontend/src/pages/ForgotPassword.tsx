import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { Alert, Box, Button, Link, Stack, TextField, Typography } from '@mui/material';
import { AuthLayout } from '../components/AuthLayout';
import { errorMessage, requestPasswordRecovery } from '../api';

export default function ForgotPassword() {
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  async function submit(event: FormEvent) {
    event.preventDefault(); if (pending) return;
    setError(''); setMessage('');
    if (!username.trim() || !email.trim()) { setError('Nhập tên đăng nhập và email đã đăng ký.'); return; }
    setPending(true);
    try { setMessage((await requestPasswordRecovery(username.trim(), email.trim())).message); }
    catch (value) { setError(errorMessage(value)); }
    finally { setPending(false); }
  }
  return <AuthLayout><Box component="form" onSubmit={(event) => void submit(event)}>
    <Typography component="h2" sx={{ fontSize: { xs: 29, sm: 33 }, fontWeight: 850, letterSpacing: -1 }}>Quên mật khẩu?</Typography>
    <Typography color="text.secondary" sx={{ mt: 1.2, mb: 3, fontSize: 14, lineHeight: 1.8 }}>Nhập thông tin tài khoản để nhận liên kết đặt lại mật khẩu qua email. Liên kết có hiệu lực trong 15 phút.</Typography>
    <Stack spacing={2.5}>
      {error && <Alert severity="error">{error}</Alert>}
      {message && <Alert severity="info">{message}</Alert>}
      <TextField label="Tên đăng nhập" autoComplete="username" autoFocus value={username} required fullWidth disabled={pending} onChange={(event) => setUsername(event.target.value)} slotProps={{ htmlInput: { maxLength: 80 } }} />
      <TextField label="Email đã đăng ký" type="email" autoComplete="email" value={email} required fullWidth disabled={pending} onChange={(event) => setEmail(event.target.value)} slotProps={{ htmlInput: { maxLength: 254 } }} />
      <Button type="submit" variant="contained" fullWidth size="large" disabled={pending}>{pending ? 'Đang xử lý…' : 'Gửi liên kết đặt lại mật khẩu'}</Button>
    </Stack>
    <Typography sx={{ mt: 3, textAlign: 'center', fontSize: 14 }}><Link component={RouterLink} to="/login" underline="hover">Quay lại đăng nhập</Link></Typography>
  </Box></AuthLayout>;
}
