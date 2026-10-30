import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, Navigate, Outlet, useLocation } from 'react-router-dom';
import { alpha, Alert, AppBar, Avatar, Badge, Box, Button, ButtonBase, Chip, CircularProgress, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText, ListSubheader, Menu, MenuItem, Stack, Toolbar, Tooltip, Typography } from '@mui/material';
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
import LocalShippingOutlined from '@mui/icons-material/LocalShippingOutlined';
import ShoppingCartOutlined from '@mui/icons-material/ShoppingCartOutlined';
import PeopleOutline from '@mui/icons-material/PeopleOutline';
import ManageAccountsOutlined from '@mui/icons-material/ManageAccountsOutlined';
import SettingsOutlined from '@mui/icons-material/SettingsOutlined';
import FactCheckOutlined from '@mui/icons-material/FactCheckOutlined';
import PointOfSaleOutlined from '@mui/icons-material/PointOfSaleOutlined';
import NotificationsNoneOutlined from '@mui/icons-material/NotificationsNoneOutlined';
import KeyboardArrowDownRounded from '@mui/icons-material/KeyboardArrowDownRounded';
import ChevronRightRounded from '@mui/icons-material/ChevronRightRounded';
import DashboardOutlined from '@mui/icons-material/DashboardOutlined';
import { getNotificationSummary } from './notifications';
import { useAuth } from './auth';
import { ConfirmDialog } from './shared';
import { useApplicationSettings } from './settings';
import { AppearanceMenu } from './appearance';

const navigation = [
  { path: '/dashboard', label: 'Dashboard', group: 'TỔNG QUAN', icon: <DashboardOutlined /> },
  { path: '/reports', label: 'Báo cáo', group: 'TỔNG QUAN', icon: <AssessmentOutlined /> },
  { path: '/notifications', label: 'Thông báo', group: 'TỔNG QUAN', icon: <NotificationsNoneOutlined /> },
  { path: '/inventory', label: 'Tồn kho', group: 'QUẢN LÝ KHO', icon: <AssessmentOutlined /> },
  { path: '/warehouses', label: 'Kho hàng', group: 'QUẢN LÝ KHO', icon: <Inventory2Outlined /> },
  { path: '/transfers', label: 'Chuyển kho', group: 'QUẢN LÝ KHO', icon: <SwapVertOutlined /> },
  { path: '/products', label: 'Sản phẩm', group: 'QUẢN LÝ KHO', icon: <Inventory2Outlined /> },
  { path: '/categories', label: 'Danh mục', group: 'QUẢN LÝ KHO', icon: <CategoryOutlined /> },
  { path: '/stock-movements', label: 'Lịch sử biến động', group: 'QUẢN LÝ KHO', icon: <SwapVertOutlined /> },
  { path: '/suppliers', label: 'Nhà cung cấp', group: 'ĐỐI TÁC & ĐƠN HÀNG', icon: <LocalShippingOutlined /> },
  { path: '/customers', label: 'Khách hàng', group: 'ĐỐI TÁC & ĐƠN HÀNG', icon: <PeopleOutline /> },
  { path: '/purchase-orders', label: 'Đơn mua hàng', group: 'ĐỐI TÁC & ĐƠN HÀNG', icon: <ShoppingCartOutlined /> },
  { path: '/sales-orders', label: 'Đơn bán hàng', group: 'ĐỐI TÁC & ĐƠN HÀNG', icon: <PointOfSaleOutlined /> },
  { path: '/ai', label: 'AI Assistant', group: 'AI & MCP', icon: <SmartToyOutlined /> },
  { path: '/mcp/lab', label: 'MCP Client Lab', group: 'AI & MCP', icon: <TerminalOutlined /> },
  { path: '/mcp/playground', label: 'Tool Playground', group: 'AI & MCP', icon: <PlayCircleOutline /> },
  { path: '/mcp/trace', label: 'Execution Trace', group: 'AI & MCP', icon: <TimelineOutlined /> },
  { path: '/profile', label: 'Tài khoản của tôi', group: 'TÀI KHOẢN', icon: <PersonOutline /> },
  { path: '/users', label: 'Người dùng', group: 'TÀI KHOẢN', icon: <ManageAccountsOutlined />, adminOnly: true },
  { path: '/settings', label: 'Cài đặt', group: 'TÀI KHOẢN', icon: <SettingsOutlined /> },
  { path: '/audit', label: 'Nhật ký kiểm toán', group: 'TÀI KHOẢN', icon: <FactCheckOutlined />, adminOnly: true },
];
const navigationGroups = [
  { id: 'TỔNG QUAN', label: 'Tổng quan' },
  { id: 'QUẢN LÝ KHO', label: 'Kho & sản phẩm' },
  { id: 'ĐỐI TÁC & ĐƠN HÀNG', label: 'Đối tác & đơn hàng' },
  { id: 'AI & MCP', label: 'AI & MCP' },
  { id: 'TÀI KHOẢN', label: 'Tài khoản & quản trị' },
];
const sidebarWidth = 244;

export function Shell() {
  const { user, authenticated, loading, loadError, refreshError, refreshing, retry, retryRefresh, logout } = useAuth();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [profileAnchor, setProfileAnchor] = useState<HTMLElement | null>(null);
  const [confirmLogout, setConfirmLogout] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);
  const isCustomer = user?.role === 'Customer';
  const workspaceEnabled = authenticated && !loading && !loadError && Boolean(user) && !isCustomer;
  const notifications = useQuery({ queryKey: ['notification-summary'], queryFn: ({ signal }) => getNotificationSummary(signal), refetchInterval: 30_000, enabled: workspaceEnabled });
  const settings = useApplicationSettings(workspaceEnabled);
  useEffect(() => { setMobileOpen(false); setProfileAnchor(null); }, [location.pathname]);
  if (loading) return <Stack alignItems="center" justifyContent="center" spacing={2} sx={{ minHeight: '100dvh' }}><CircularProgress size={32} /><Typography color="text.secondary">Đang xác thực phiên…</Typography></Stack>;
  if (!authenticated) return <Navigate to="/login" replace />;
  if (loadError || !user) return <Stack spacing={2} sx={{ maxWidth: 560, mx: 'auto', p: 4, mt: 10 }}><Alert severity="error">{loadError || 'Không tải được tài khoản.'}</Alert><Button onClick={retry} variant="contained">Thử lại</Button><Button onClick={() => void logout()}>Đăng xuất</Button></Stack>;
  if (isCustomer && location.pathname !== '/profile') return <Navigate to="/profile" replace />;
  const profileOpen = Boolean(profileAnchor);
  const currentPage = navigation.find((item) => item.path === location.pathname);
  const pageLabel = currentPage?.label ?? 'Không gian làm việc';
  const sectionLabel = isCustomer ? 'Tài khoản' : navigationGroups.find(group => group.id === currentPage?.group)?.label ?? 'Không gian làm việc';
  const roleLabel = user.role === 'Admin' ? 'Quản trị viên' : isCustomer ? 'Khách hàng' : 'Nhân viên';
  const displayName = user.fullName || user.username;
  const initials = displayName.trim().split(/\s+/).slice(-2).map(part => part[0]).join('').toUpperCase();
  const unreadCount = notifications.isError ? undefined : notifications.data?.unreadCount;
  const notificationLabel = typeof unreadCount === 'number' ? `Thông báo, ${unreadCount} chưa đọc` : notifications.isError ? 'Thông báo, chưa tải được số chưa đọc' : 'Thông báo, đang tải số chưa đọc';
  const visibleNavigation = navigation.filter(item => (!isCustomer || item.path === '/profile') && (!item.adminOnly || user.role === 'Admin'));
  function requestLogout() { setProfileAnchor(null); setMobileOpen(false); setConfirmLogout(true); }
  const drawer = <Stack component="nav" aria-label="Điều hướng chính" sx={{ height: '100%', bgcolor: '#111E31', color: 'white', overflow: 'hidden' }}>
    <Stack direction="row" spacing={1.2} alignItems="center" sx={{ minHeight: 76, px: 2.5, flexShrink: 0, borderBottom: '1px solid rgba(255,255,255,.07)' }}>
      <Avatar variant="rounded" sx={{ bgcolor: '#426EF0', color: 'white', width: 36, height: 36, borderRadius: 1.5 }}><Inventory2Outlined sx={{ fontSize: 21 }} /></Avatar>
      <Box sx={{ minWidth: 0 }}><Typography noWrap title={settings.data?.companyName} sx={{ fontSize: 13, fontWeight: 750, letterSpacing: '-.2px', maxWidth: 152 }}>{isCustomer ? 'Smart Inventory' : settings.data?.companyName ?? 'Smart Inventory'}</Typography><Typography sx={{ fontSize: 10.5, color: '#8EA4BE', mt: .35 }}>{isCustomer ? 'Tài khoản cá nhân' : 'Quản lý kho hàng'}</Typography></Box>
    </Stack>
    <Box className="sidebar-navigation" sx={{ flex: 1, minHeight: 0, overflowY: 'auto', overflowX: 'hidden', px: 1.5, pt: .25, pb: 2, scrollbarWidth: 'thin', scrollbarColor: '#35485F transparent' }}>
      {navigationGroups.map((group) => {
        const items = visibleNavigation.filter(item => item.group === group.id);
        if (!items.length) return null;
        return <Box key={group.id}>
          <Typography sx={{ color: '#7F95AF', fontSize: 10, fontWeight: 650, letterSpacing: '.35px', px: 1.25, mt: 2.25, mb: .75 }}>{group.label}</Typography>
          <List disablePadding>{items.map((item) => {
            const selected = location.pathname === item.path;
            return <ListItemButton key={item.path} component={Link} to={item.path} selected={selected} aria-current={selected ? 'page' : undefined} onClick={() => setMobileOpen(false)} sx={{
              minHeight: 36, borderRadius: 1, mb: .25, px: 1.25, py: .6, color: '#B9C8DC', position: 'relative', transition: 'background-color 150ms ease, color 150ms ease',
              '&:hover': { bgcolor: 'rgba(255,255,255,.05)', color: 'white' },
              '&.Mui-focusVisible': { outline: '2px solid #A2C1FF', outlineOffset: -2 },
              '&.Mui-selected, &.Mui-selected.Mui-focusVisible': { bgcolor: '#426EF024', color: '#F1F5FF', '&:before': { content: '""', position: 'absolute', left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, bgcolor: '#6A93FF' } },
              '&.Mui-selected:hover': { bgcolor: '#426EF034' },
            }}><ListItemIcon sx={{ color: selected ? '#83A6FF' : '#93A9C3', minWidth: 31, '& .MuiSvgIcon-root': { fontSize: 18 } }}>{item.icon}</ListItemIcon><ListItemText primary={item.label} sx={{ m: 0 }} slotProps={{ primary: { fontSize: 12.25, fontWeight: selected ? 650 : 450 } }} /></ListItemButton>;
          })}</List>
        </Box>;
      })}
    </Box>
    <Box sx={{ borderTop: '1px solid rgba(255,255,255,.08)', px: 2, py: 1.5, flexShrink: 0 }}><Stack direction="row" alignItems="center" spacing={1.1}><Avatar sx={{ width: 32, height: 32, fontSize: 11, fontWeight: 750, bgcolor: '#243C55', color: '#A6E5DC' }}>{initials}</Avatar><Box sx={{ minWidth: 0, flex: 1 }}><Typography noWrap sx={{ color: '#E6EEF9', fontSize: 11.5, fontWeight: 650 }}>{displayName}</Typography><Typography sx={{ fontSize: 10.5, color: '#91A7C3', mt: .2 }}>{roleLabel}</Typography></Box><Tooltip title="Hồ sơ của tôi"><IconButton component={Link} to="/profile" aria-label="Hồ sơ của tôi" size="small" sx={{ color: '#91A7C3', '&:hover': { color: 'white', bgcolor: '#FFFFFF0A' } }}><SettingsOutlined sx={{ fontSize: 17 }} /></IconButton></Tooltip></Stack></Box>
  </Stack>;
  return <Box className="app-workspace" sx={{ display: 'flex', height: '100dvh', overflow: 'hidden' }}>
    <Box component="a" href="#main-content" className="skip-link">Bỏ qua menu, đến nội dung</Box>
    <Box sx={{ width: { md: sidebarWidth }, flexShrink: { md: 0 } }}>
      <Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)} ModalProps={{ keepMounted: true }} sx={{ display: { xs: 'block', md: 'none' }, '& .MuiDrawer-paper': { width: sidebarWidth, border: 0, overflow: 'hidden' } }}>{drawer}</Drawer>
      <Drawer variant="permanent" sx={{ display: { xs: 'none', md: 'block' }, '& .MuiDrawer-paper': { width: sidebarWidth, border: 0, overflow: 'hidden' } }} open>{drawer}</Drawer>
    </Box>
    <Box sx={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
      <AppBar position="static" color="transparent" elevation={0} sx={{ bgcolor: (theme) => alpha(theme.palette.background.paper, .96), borderBottom: 1, borderColor: 'divider' }}>
        <Toolbar sx={{ minHeight: '62px !important', gap: { xs: .5, sm: 1 }, px: { xs: 1.5, sm: 2.5, md: 3, xl: 4 } }}>
          <IconButton aria-label="Mở menu" onClick={() => setMobileOpen(true)} sx={{ display: { md: 'none' }, mr: .25 }}><MenuOutlined sx={{ fontSize: 22 }} /></IconButton>
          <Stack direction="row" spacing={1} alignItems="center" sx={{ flex: 1, minWidth: 0 }}>
            <Typography noWrap sx={{ fontSize: 12, color: 'text.secondary', display: { xs: 'none', sm: 'block' }, flexShrink: 0 }}>{sectionLabel}</Typography>
            <ChevronRightRounded sx={{ fontSize: 16, color: 'text.disabled', display: { xs: 'none', sm: 'block' }, flexShrink: 0 }} />
            <Typography noWrap title={pageLabel} sx={{ fontSize: 12.5, fontWeight: 650 }}>{pageLabel}</Typography>
          </Stack>
          <AppearanceMenu />
          {!isCustomer && <IconButton component={Link} to="/notifications" aria-label={notificationLabel}><Badge badgeContent={unreadCount} invisible={typeof unreadCount !== 'number'} color="error" max={99}><NotificationsNoneOutlined sx={{ fontSize: 21 }} /></Badge></IconButton>}
          <Divider orientation="vertical" flexItem sx={{ my: 2, mx: .5, display: { xs: 'none', sm: 'block' } }} />
          <ButtonBase id="account-menu-button" aria-label="Mở menu tài khoản" aria-haspopup="menu" aria-controls={profileOpen ? 'account-menu' : undefined} aria-expanded={profileOpen} onClick={(event) => setProfileAnchor(event.currentTarget)} onKeyDown={(event) => { if (event.key === 'ArrowDown') { event.preventDefault(); setProfileAnchor(event.currentTarget); } }} sx={{ borderRadius: 1.5, p: .5, gap: .9, flexShrink: 0, '&:hover': { bgcolor: 'action.hover' }, '&.Mui-focusVisible': { outline: '2px solid', outlineColor: 'primary.main', outlineOffset: 2 } }}>
            <Avatar sx={{ bgcolor: (theme) => alpha(theme.palette.secondary.main, .1), color: 'secondary.main', width: 32, height: 32, fontSize: 11, fontWeight: 750 }}>{initials}</Avatar><Typography sx={{ fontSize: 11.5, fontWeight: 600, maxWidth: 140, display: { xs: 'none', lg: 'block' } }} noWrap>{displayName}</Typography><KeyboardArrowDownRounded sx={{ color: 'text.secondary', fontSize: 16, display: { xs: 'none', sm: 'block' } }} />
          </ButtonBase>
          <Tooltip title="Đăng xuất"><IconButton aria-label="Đăng xuất" onClick={requestLogout} size="small" sx={{ ml: .25 }}><LogoutOutlined sx={{ fontSize: 18 }} /></IconButton></Tooltip>
        </Toolbar>
      </AppBar>
      <Box component="main" className="workspace-content" id="main-content" tabIndex={-1} sx={{ flex: 1, minHeight: 0, overflow: 'auto', p: { xs: 2, sm: 2.5, md: 3, xl: 4 }, outline: 0 }}><Box sx={{ maxWidth: 1520, mx: 'auto' }}>{refreshError && <Alert severity="warning" sx={{ mb: 2.5 }} action={<Button color="inherit" disabled={refreshing} onClick={retryRefresh}>{refreshing ? 'Đang gia hạn…' : 'Gia hạn phiên'}</Button>}>{refreshError} Phiên truy cập hiện tại được giữ đến khi hết hạn.</Alert>}<Outlet /></Box></Box>
    </Box>
    <Menu id="account-menu" anchorEl={profileAnchor} open={profileOpen} onClose={() => setProfileAnchor(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }} slotProps={{ list: { 'aria-labelledby': 'account-menu-button' }, paper: { sx: { minWidth: 250, maxWidth: 320, mt: 1 } } }}>
      <ListSubheader disableSticky sx={{ px: 2, py: 1.5, lineHeight: 1.5, bgcolor: 'background.paper' }}><Typography fontWeight={750} color="text.primary" noWrap>{displayName}</Typography><Typography variant="body2" color="text.secondary" noWrap>{user.email}</Typography><Chip size="small" variant="outlined" label={roleLabel} sx={{ mt: 1 }} /></ListSubheader><Divider />
      <MenuItem component={Link} to="/profile" onClick={() => setProfileAnchor(null)}><ListItemIcon><PersonOutline fontSize="small" /></ListItemIcon>Hồ sơ của tôi</MenuItem>
      {!isCustomer && <MenuItem component={Link} to="/settings" onClick={() => setProfileAnchor(null)}><ListItemIcon><SettingsOutlined fontSize="small" /></ListItemIcon>Cài đặt</MenuItem>}<Divider />
      <MenuItem onClick={requestLogout}><ListItemIcon><LogoutOutlined fontSize="small" /></ListItemIcon>Đăng xuất</MenuItem>
    </Menu>
    <ConfirmDialog open={confirmLogout} title="Đăng xuất khỏi hệ thống?" description="Phiên làm việc trên trình duyệt sẽ kết thúc và bạn sẽ trở về màn hình đăng nhập." confirmLabel="Đăng xuất" pending={loggingOut} onClose={() => setConfirmLogout(false)} onConfirm={() => { setLoggingOut(true); void logout().finally(() => setLoggingOut(false)); }} />
  </Box>;
}
