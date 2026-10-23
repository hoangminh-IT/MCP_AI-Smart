import { useState } from 'react';
import { Link, Navigate, Outlet, useLocation } from 'react-router-dom';
import { Alert, AppBar, Avatar, Box, Button, Chip, CircularProgress, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText, Stack, Toolbar, Typography } from '@mui/material';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import CategoryOutlined from '@mui/icons-material/CategoryOutlined';
import AssessmentOutlined from '@mui/icons-material/AssessmentOutlined';
import SwapVertOutlined from '@mui/icons-material/SwapVertOutlined';
import PersonOutline from '@mui/icons-material/PersonOutline';
import LogoutOutlined from '@mui/icons-material/LogoutOutlined';
import MenuOutlined from '@mui/icons-material/MenuOutlined';
import TerminalOutlined from '@mui/icons-material/TerminalOutlined';
import PlayCircleOutline from '@mui/icons-material/PlayCircleOutline';
import TimelineOutlined from '@mui/icons-material/TimelineOutlined';
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined';
import { useAuth } from './auth';
import { ConfirmDialog } from './shared';

const navigation = [
  { path: '/inventory', label: 'Tồn kho', group: 'QUẢN LÝ KHO', icon: <AssessmentOutlined /> },
  { path: '/products', label: 'Sản phẩm', group: 'QUẢN LÝ KHO', icon: <Inventory2Outlined /> },
  { path: '/categories', label: 'Danh mục', group: 'QUẢN LÝ KHO', icon: <CategoryOutlined /> },
  { path: '/stock-movements', label: 'Lịch sử biến động', group: 'QUẢN LÝ KHO', icon: <SwapVertOutlined /> },
  { path: '/ai', label: 'AI Assistant', group: 'AI & MCP', icon: <SmartToyOutlined /> },
  { path: '/mcp/lab', label: 'MCP Client Lab', group: 'AI & MCP', icon: <TerminalOutlined /> },
  { path: '/mcp/playground', label: 'Tool Playground', group: 'AI & MCP', icon: <PlayCircleOutline /> },
  { path: '/mcp/trace', label: 'Execution Trace', group: 'AI & MCP', icon: <TimelineOutlined /> },
  { path: '/profile', label: 'Tài khoản của tôi', group: 'TÀI KHOẢN', icon: <PersonOutline /> },
];

export function Shell() {
  const { user, authenticated, loading, loadError, retry, logout } = useAuth();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [confirmLogout, setConfirmLogout] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);
  if (!authenticated) return <Navigate to="/login" replace />;
  if (loading) return <Stack alignItems="center" justifyContent="center" spacing={2} sx={{ minHeight: '100dvh' }}><CircularProgress /><Typography color="text.secondary">Đang xác thực phiên…</Typography></Stack>;
  if (loadError || !user) return <Stack spacing={2} sx={{ maxWidth: 560, mx: 'auto', p: 4, mt: 10 }}><Alert severity="error">{loadError || 'Không tải được tài khoản.'}</Alert><Button onClick={retry} variant="contained">Thử lại</Button><Button onClick={() => void logout()}>Đăng xuất</Button></Stack>;
  const drawer = <Stack sx={{ height: '100%', bgcolor: '#0A1830', color: 'white', px: 1.5 }}>
    <Stack direction="row" spacing={1.5} alignItems="center" sx={{ height: 97, px: 1 }}><Avatar variant="rounded" sx={{ bgcolor: 'primary.main', width: 40, height: 40 }}><Inventory2Outlined /></Avatar><Box><Typography sx={{ fontSize: 14, fontWeight: 850, letterSpacing: '.2px' }}>SMART INVENTORY</Typography><Typography sx={{ fontSize: 9, color: '#9BB1D0', letterSpacing: 1.2, mt: 0.5 }}>MCP C# SDK PLATFORM</Typography></Box></Stack>
    {['QUẢN LÝ KHO', 'AI & MCP', 'TÀI KHOẢN'].map((group) => <Box key={group}><Typography sx={{ color: '#8197B8', fontSize: 10, fontWeight: 750, letterSpacing: 1.5, px: 1.5, mt: 2, mb: 1.5 }}>{group}</Typography><List disablePadding>{navigation.filter((item) => item.group === group).map((item) => <ListItemButton key={item.path} component={Link} to={item.path} selected={location.pathname === item.path} onClick={() => setMobileOpen(false)} sx={{ minHeight: 46, borderRadius: 2, mb: 0.7, color: '#B7C8E0', '&.Mui-selected': { bgcolor: 'primary.main', color: 'white' }, '&.Mui-selected:hover': { bgcolor: 'primary.dark' } }}><ListItemIcon sx={{ color: 'inherit', minWidth: 36 }}>{item.icon}</ListItemIcon><ListItemText primary={item.label} slotProps={{ primary: { fontSize: 13, fontWeight: location.pathname === item.path ? 750 : 550 } }} /></ListItemButton>)}</List></Box>)}
    <Box sx={{ flex: 1 }} /><Box sx={{ bgcolor: '#14243E', p: 2, mb: 2, borderRadius: 3 }}><Typography sx={{ fontSize: 11, fontWeight: 750, color: '#DCE8FF' }}>KHÔNG GIAN QUẢN LÝ</Typography><Typography sx={{ color: '#98ACCB', fontSize: 11, mt: 0.8 }}>ASP.NET Core · PostgreSQL</Typography></Box>
  </Stack>;
  return <Box sx={{ display: 'flex', height: '100dvh', overflow: 'hidden' }}>
    <Box component="nav" sx={{ width: { md: 250 }, flexShrink: { md: 0 } }}><Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)} ModalProps={{ keepMounted: true }} sx={{ display: { xs: 'block', md: 'none' }, '& .MuiDrawer-paper': { width: 250, border: 0 } }}>{drawer}</Drawer><Drawer variant="permanent" sx={{ display: { xs: 'none', md: 'block' }, '& .MuiDrawer-paper': { width: 250, border: 0 } }} open>{drawer}</Drawer></Box>
    <Box sx={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}><AppBar position="static" color="transparent" elevation={0} sx={{ bgcolor: 'white', borderBottom: '1px solid #E8EDF5' }}><Toolbar sx={{ minHeight: '73px !important', gap: 1.5, px: { xs: 2, md: 4 } }}><IconButton aria-label="Mở menu" onClick={() => setMobileOpen(true)} sx={{ display: { md: 'none' } }}><MenuOutlined /></IconButton><Typography color="text.secondary" sx={{ fontSize: 13, flex: 1 }}>Workspace <Box component="span" sx={{ mx: 1, color: '#C4CCDA' }}>/</Box> <Box component="span" sx={{ color: 'text.primary' }}>{navigation.find((item) => item.path === location.pathname)?.label}</Box></Typography><Chip size="small" label={user.role === 'Admin' ? 'Quản trị viên' : 'Nhân viên · nhập/xuất kho'} sx={{ display: { xs: 'none', sm: 'flex' }, bgcolor: '#EAF0FF', color: 'primary.main' }} /><Avatar sx={{ bgcolor: '#EAF0FF', color: 'primary.main', width: 34, height: 34, fontSize: 14, fontWeight: 800 }}>{(user.fullName || user.username)[0]?.toUpperCase()}</Avatar><Typography sx={{ fontSize: 13, fontWeight: 650, maxWidth: 140, display: { xs: 'none', lg: 'block' } }} noWrap>{user.fullName || user.username}</Typography><IconButton aria-label="Đăng xuất" onClick={() => setConfirmLogout(true)} size="small"><LogoutOutlined fontSize="small" /></IconButton></Toolbar></AppBar><Box component="main" sx={{ flex: 1, overflow: 'auto', p: { xs: 2, md: 4 } }}><Box sx={{ maxWidth: 1550, mx: 'auto' }}><Outlet /></Box></Box></Box>
    <ConfirmDialog open={confirmLogout} title="Đăng xuất khỏi hệ thống?" description="Phiên làm việc trên trình duyệt sẽ kết thúc và bạn sẽ trở về màn hình đăng nhập." confirmLabel="Đăng xuất" pending={loggingOut} onClose={() => setConfirmLogout(false)} onConfirm={() => { setLoggingOut(true); void logout().finally(() => setLoggingOut(false)); }} />
  </Box>;
}
