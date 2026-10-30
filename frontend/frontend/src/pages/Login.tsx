import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link as RouterLink, Navigate, useSearchParams } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, IconButton, InputAdornment, Link, Stack, TextField, Typography } from '@mui/material';
import LockOutlined from '@mui/icons-material/LockOutlined';
import PersonOutline from '@mui/icons-material/PersonOutline';
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined';
import VisibilityOffOutlined from '@mui/icons-material/VisibilityOffOutlined';
import ArrowForwardRounded from '@mui/icons-material/ArrowForwardRounded';
import { useAuth } from '../auth';
import { errorMessage } from '../api';
import { AuthLayout } from '../components/AuthLayout';

export default function Login() {
  const { user, authenticated, loading, loadError, refreshing, retryRefresh, login, notice } = useAuth();
  const [params] = useSearchParams();
  const [username, setUsername] = useState(params.get('username') ?? '');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  if (loading && !pending) return <Stack alignItems="center" justifyContent="center" spacing={2} sx={{ minHeight: '100dvh' }}><CircularProgress /><Typography color="text.secondary">Đang xác thực phiên…</Typography></Stack>;
  if (authenticated && user) return <Navigate to={user.role === 'Customer' ? '/profile' : '/dashboard'} replace />;
  async function submit(event: FormEvent) {
    event.preventDefault(); if (pending) return;
    setPending(true); setError('');
    try { await login(username.trim(), password); setPassword(''); }
    catch (value) { setError(errorMessage(value)); }
    finally { setPending(false); }
  }
  return <AuthLayout><Box component="form" onSubmit={(event) => void submit(event)}>
    <Typography component="h2" sx={{ fontSize: { xs: 29, sm: 33 }, fontWeight: 850, letterSpacing: -1 }}>Đăng nhập</Typography>
    <Typography color="text.secondary" sx={{ mt: 1.2, mb: 4, fontSize: 14, lineHeight: 1.8 }}>Nhập tên đăng nhập và mật khẩu của bạn.</Typography>
    <Stack spacing={2.5}>
      {params.get('registered') === '1' && <Alert severity="success">Đăng ký thành công! Bạn có thể đăng nhập ngay.</Alert>}
      {notice && <Alert severity="info">{notice}</Alert>}
      {loadError && <Alert severity="warning" action={<Button color="inherit" disabled={refreshing} onClick={retryRefresh}>Khôi phục phiên</Button>}>{loadError}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      <TextField label="Tên đăng nhập" autoComplete="username" autoFocus value={username} disabled={pending} onChange={(event) => setUsername(event.target.value)} required fullWidth slotProps={{ htmlInput: { maxLength: 80 }, input: { startAdornment: <InputAdornment position="start"><PersonOutline fontSize="small" /></InputAdornment> } }} />
      <TextField label="Mật khẩu" type={showPassword ? 'text' : 'password'} autoComplete="current-password" value={password} disabled={pending} onChange={(event) => setPassword(event.target.value)} required fullWidth slotProps={{ htmlInput: { maxLength: 128 }, input: { startAdornment: <InputAdornment position="start"><LockOutlined fontSize="small" /></InputAdornment>, endAdornment: <InputAdornment position="end"><IconButton edge="end" aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={showPassword} disabled={pending} onClick={() => setShowPassword((value) => !value)}>{showPassword ? <VisibilityOffOutlined fontSize="small" /> : <VisibilityOutlined fontSize="small" />}</IconButton></InputAdornment> } }} />
      <Button type="submit" variant="contained" fullWidth size="large" endIcon={pending ? undefined : <ArrowForwardRounded />} disabled={pending} sx={{ py: 1.6, mt: .5 }}>{pending ? 'Đang đăng nhập…' : 'Đăng nhập'}</Button>
    </Stack>
    <Typography sx={{ textAlign: 'center', mt: 3.5, fontSize: 13, color: 'text.secondary' }}>Chưa có tài khoản? <Link component={RouterLink} to="/register" underline="hover" sx={{ fontWeight: 750 }}>Đăng ký ngay</Link></Typography>
  </Box></AuthLayout>;
}
