import { useState } from 'react';
import type { FormEvent } from 'react';
import { Navigate } from 'react-router-dom';
import { Alert, Avatar, Box, Button, InputAdornment, Paper, Stack, TextField, Typography } from '@mui/material';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import LockOutlined from '@mui/icons-material/LockOutlined';
import PersonOutline from '@mui/icons-material/PersonOutline';
import ShieldOutlined from '@mui/icons-material/ShieldOutlined';
import { useAuth } from '../auth';
import { errorMessage } from '../api';

export default function Login() {
  const { authenticated, login, notice } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  if (authenticated) return <Navigate to="/products" replace />;
  async function submit(event: FormEvent) {
    event.preventDefault(); setPending(true); setError('');
    try { await login(username.trim(), password); setPassword(''); }
    catch (value) { setError(errorMessage(value)); }
    finally { setPending(false); }
  }
  return <Box sx={{ minHeight: '100dvh', display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1.05fr 1fr' } }}><Stack sx={{ bgcolor: '#0A1830', color: 'white', p: { xs: 4, md: 7, lg: 10 }, position: 'relative', overflow: 'hidden', justifyContent: 'space-between', minHeight: { xs: 280, md: '100dvh' }, '&::after': { content: '""', position: 'absolute', width: 420, height: 420, border: '1px solid #25406D', borderRadius: '50%', right: -160, bottom: -160 } }}><Stack direction="row" alignItems="center" spacing={1.5}><Avatar variant="rounded" sx={{ bgcolor: '#3468F6', width: 44, height: 44 }}><Inventory2Outlined /></Avatar><Box><Typography fontWeight={850} letterSpacing={0.5}>SMART INVENTORY</Typography><Typography sx={{ color: '#9BB1D0', fontSize: 10, letterSpacing: 1.4, mt: 0.5 }}>AI POWERED · MCP C# SDK</Typography></Box></Stack><Box sx={{ my: { xs: 4, md: 8 }, zIndex: 1 }}><Typography sx={{ color: '#83A6FF', fontSize: 12, fontWeight: 750, letterSpacing: 2, mb: 2 }}>MỌI SẢN PHẨM. MỘT KHÔNG GIAN.</Typography><Typography sx={{ fontSize: { xs: 34, lg: 48 }, fontWeight: 850, lineHeight: 1.22, letterSpacing: -1.5 }}>Quản lý kho<br />thông minh hơn.</Typography><Typography sx={{ maxWidth: 390, color: '#AABCD7', lineHeight: 1.9, mt: 3 }}>Tổ chức sản phẩm, quản lý danh mục và xây dựng dữ liệu kho nhất quán trong một nền tảng.</Typography><Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 4, color: '#B2C5E4' }}><ShieldOutlined sx={{ fontSize: 18 }} /><Typography variant="body2">Phân quyền Admin & Staff</Typography></Stack></Box><Typography sx={{ color: '#8197B8', fontSize: 11 }}>Inventory Platform · ASP.NET Core · PostgreSQL</Typography></Stack><Stack alignItems="center" justifyContent="center" sx={{ p: { xs: 3, md: 6 }, bgcolor: '#F6F8FD' }}><Paper component="form" onSubmit={(event) => void submit(event)} variant="outlined" sx={{ width: '100%', maxWidth: 440, p: { xs: 3, sm: 4.5 } }}><Typography variant="h5">Chào mừng trở lại</Typography><Typography color="text.secondary" sx={{ mt: 1, mb: 3.5, lineHeight: 1.7 }}>Đăng nhập để tiếp tục quản lý kho hàng của bạn.</Typography><Stack spacing={2.5}>{notice && <Alert severity="info">{notice}</Alert>}{error && <Alert severity="error">{error}</Alert>}<TextField label="Tên đăng nhập" autoComplete="username" autoFocus value={username} onChange={(event) => setUsername(event.target.value)} required fullWidth slotProps={{ input: { startAdornment: <InputAdornment position="start"><PersonOutline fontSize="small" /></InputAdornment> } }} /><TextField label="Mật khẩu" type="password" autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} required fullWidth slotProps={{ input: { startAdornment: <InputAdornment position="start"><LockOutlined fontSize="small" /></InputAdornment> } }} /><Button type="submit" variant="contained" fullWidth size="large" disabled={pending}>{pending ? 'Đang đăng nhập…' : 'Đăng nhập'}</Button><Typography variant="caption" color="text.secondary" sx={{ textAlign: 'center' }}>Sử dụng tài khoản được quản trị viên cấp.</Typography></Stack></Paper></Stack></Box>;
}
