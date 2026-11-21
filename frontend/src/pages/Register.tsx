import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link as RouterLink, Navigate, useNavigate } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, IconButton, InputAdornment, Link, Stack, TextField, Typography } from '@mui/material';
import PersonAddAltOutlined from '@mui/icons-material/PersonAddAltOutlined';
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined';
import VisibilityOffOutlined from '@mui/icons-material/VisibilityOffOutlined';
import { useAuth } from '../auth';
import { errorMessage } from '../api';
import { AuthLayout } from '../components/AuthLayout';
import { registerAccount } from '../users';
import type { RegisterInput } from '../users';

const initialForm: RegisterInput = { username: '', fullName: '', email: '', password: '', confirmPassword: '' };
export default function Register() {
  const auth = useAuth(); const navigate = useNavigate();
  const [form, setForm] = useState<RegisterInput>(initialForm);
  const [pending, setPending] = useState(false); const [error, setError] = useState('');
  const [showPassword, setShowPassword] = useState(false); const [showConfirmation, setShowConfirmation] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [touched, setTouched] = useState<Partial<Record<keyof RegisterInput, boolean>>>({});
  if (auth.loading) return <Stack alignItems="center" justifyContent="center" spacing={2} sx={{ minHeight: '100dvh' }}><CircularProgress /><Typography color="text.secondary">Đang xác thực phiên…</Typography></Stack>;
  if (auth.authenticated && auth.user) return <Navigate to={auth.user.role === 'Customer' ? '/profile' : '/dashboard'} replace />;
  const validation: Partial<Record<keyof RegisterInput, string>> = {};
  if (!form.fullName.trim()) validation.fullName = 'Nhập họ và tên của bạn.';
  if (!form.email.trim()) validation.email = 'Nhập địa chỉ email của bạn.';
  else if (!/^[^\s@]+@[^\s@]+$/.test(form.email.trim())) validation.email = 'Địa chỉ email không hợp lệ.';
  if (!/^[A-Za-z0-9._-]{1,80}$/.test(form.username.trim())) validation.username = 'Dùng chữ không dấu, số hoặc dấu . _ -; tối đa 80 ký tự.';
  if (!form.password.trim()) validation.password = 'Nhập mật khẩu của bạn.';
  if (!form.confirmPassword.trim()) validation.confirmPassword = 'Nhập lại mật khẩu để xác nhận.';
  else if (form.confirmPassword !== form.password) validation.confirmPassword = 'Mật khẩu xác nhận chưa khớp.';
  function fieldError(name: keyof RegisterInput) { return submitted || touched[name] ? validation[name] : undefined; }
  function touch(name: keyof RegisterInput) { setTouched((previous) => ({ ...previous, [name]: true })); }
  function field(name: keyof RegisterInput, value: string) { setForm((previous) => ({ ...previous, [name]: value })); }
  async function submit(event: FormEvent) {
    event.preventDefault(); if (pending) return; setError('');
    setSubmitted(true);
    if (Object.keys(validation).length) return;
    setPending(true);
    try {
      const account = await registerAccount({ ...form, username: form.username.trim(), email: form.email.trim(), fullName: form.fullName.trim() });
      setForm(initialForm);
      navigate(`/login?registered=1&username=${encodeURIComponent(account.username)}`, { replace: true });
    } catch (value) { setError(errorMessage(value)); }
    finally { setPending(false); }
  }
  function passwordToggle(confirmation = false) {
    const visible = confirmation ? showConfirmation : showPassword;
    return <InputAdornment position="end"><IconButton edge="end" disabled={pending} aria-label={`${visible ? 'Ẩn' : 'Hiện'} ${confirmation ? 'mật khẩu xác nhận' : 'mật khẩu đăng ký'}`} aria-pressed={visible} onClick={() => confirmation ? setShowConfirmation((value) => !value) : setShowPassword((value) => !value)}>{visible ? <VisibilityOffOutlined fontSize="small" /> : <VisibilityOutlined fontSize="small" />}</IconButton></InputAdornment>;
  }
  return <AuthLayout registering><Box component="form" noValidate onSubmit={(event) => void submit(event)}>
    <Typography component="h2" sx={{ fontSize: { xs: 29, sm: 33 }, fontWeight: 850, letterSpacing: -1 }}>Tạo tài khoản</Typography>
    <Typography color="text.secondary" sx={{ mt: 1, mb: 3, fontSize: 14, lineHeight: 1.8 }}>Điền đầy đủ tất cả thông tin bên dưới. Email này được dùng để khôi phục tài khoản khi quên mật khẩu.</Typography>
    <Stack spacing={2.2}>
      {auth.loadError && <Alert severity="warning">{auth.loadError}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      <TextField helperText={fieldError('fullName')} label="Họ và tên" autoComplete="name" autoFocus value={form.fullName} disabled={pending} onChange={(event) => field('fullName', event.target.value)} onBlur={() => touch('fullName')} error={Boolean(fieldError('fullName'))} required fullWidth slotProps={{ htmlInput: { maxLength: 160 } }} />
      <TextField helperText={fieldError('email')} label="Email" type="email" autoComplete="email" value={form.email} disabled={pending} onChange={(event) => field('email', event.target.value)} onBlur={() => touch('email')} error={Boolean(fieldError('email'))} required fullWidth slotProps={{ htmlInput: { maxLength: 254 } }} />
      <TextField label="Tên đăng nhập" autoComplete="username" value={form.username} disabled={pending} onChange={(event) => field('username', event.target.value)} onBlur={() => touch('username')} error={Boolean(fieldError('username'))} required fullWidth helperText={fieldError('username') ?? 'Chữ không dấu, số và các dấu . _ -; phân biệt hoa/thường.'} slotProps={{ htmlInput: { maxLength: 80 } }} />
      <TextField label="Mật khẩu" type={showPassword ? 'text' : 'password'} autoComplete="new-password" value={form.password} disabled={pending} onChange={(event) => field('password', event.target.value)} onBlur={() => touch('password')} error={Boolean(fieldError('password'))} required fullWidth helperText={fieldError('password') ?? 'Không giới hạn độ dài; không được để trống. Khoảng trắng được giữ nguyên.'} slotProps={{ input: { endAdornment: passwordToggle() } }} />
      <TextField helperText={fieldError('confirmPassword')} label="Xác nhận mật khẩu" type={showConfirmation ? 'text' : 'password'} autoComplete="new-password" value={form.confirmPassword} disabled={pending} onChange={(event) => field('confirmPassword', event.target.value)} onBlur={() => touch('confirmPassword')} error={Boolean(fieldError('confirmPassword'))} required fullWidth slotProps={{ input: { endAdornment: passwordToggle(true) } }} />
      <Button type="submit" variant="contained" fullWidth size="large" startIcon={pending ? undefined : <PersonAddAltOutlined />} disabled={pending} sx={{ py: 1.6 }}>{pending ? 'Đang tạo tài khoản…' : 'Tạo tài khoản'}</Button>
    </Stack>
    <Typography sx={{ textAlign: 'center', mt: 3, fontSize: 13, color: 'text.secondary' }}>Đã có tài khoản? <Link component={RouterLink} to="/login" underline="hover" sx={{ fontWeight: 750 }}>Đăng nhập</Link></Typography>
  </Box></AuthLayout>;
}
