import { useState } from 'react';
import { Link as RouterLink, Outlet, useLocation } from 'react-router-dom';
import {
  AppBar, Box, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText, Toolbar, Typography,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import DashboardIcon from '@mui/icons-material/Dashboard';
import Inventory2Icon from '@mui/icons-material/Inventory2';
import WarehouseIcon from '@mui/icons-material/Warehouse';
import SmartToyIcon from '@mui/icons-material/SmartToy';

const DRAWER = 240;

const items = [
  { to: '/', label: 'Tổng quan', icon: <DashboardIcon /> },
  { to: '/products', label: 'Sản phẩm', icon: <Inventory2Icon /> },
  { to: '/inventory', label: 'Tồn kho', icon: <WarehouseIcon /> },
  { to: '/chat', label: 'Trợ lý AI', icon: <SmartToyIcon /> },
];

const isActive = (to: string, pathname: string) => (to === '/' ? pathname === '/' : pathname.startsWith(to));

export default function Layout() {
  const [open, setOpen] = useState(false);
  const { pathname } = useLocation();
  const title = items.find((i) => isActive(i.to, pathname))?.label ?? 'Smart Inventory';

  const nav = (
    <Box>
      <Toolbar>
        <Typography variant="h6" noWrap>Smart Inventory</Typography>
      </Toolbar>
      <List>
        {items.map((i) => (
          <ListItemButton
            key={i.to}
            component={RouterLink}
            to={i.to}
            selected={isActive(i.to, pathname)}
            onClick={() => setOpen(false)}
          >
            <ListItemIcon>{i.icon}</ListItemIcon>
            <ListItemText primary={i.label} />
          </ListItemButton>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex' }}>
      <AppBar position="fixed" sx={{ width: { md: `calc(100% - ${DRAWER}px)` }, ml: { md: `${DRAWER}px` } }}>
        <Toolbar>
          <IconButton color="inherit" edge="start" onClick={() => setOpen(true)} sx={{ mr: 2, display: { md: 'none' } }}>
            <MenuIcon />
          </IconButton>
          <Typography variant="h6" noWrap sx={{ flexGrow: 1 }}>{title}</Typography>
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: DRAWER }, flexShrink: { md: 0 } }}>
        <Drawer
          variant="temporary"
          open={open}
          onClose={() => setOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{ display: { xs: 'block', md: 'none' }, '& .MuiDrawer-paper': { width: DRAWER } }}
        >
          {nav}
        </Drawer>
        <Drawer
          variant="permanent"
          open
          sx={{ display: { xs: 'none', md: 'block' }, '& .MuiDrawer-paper': { width: DRAWER, boxSizing: 'border-box' } }}
        >
          {nav}
        </Drawer>
      </Box>

      <Box component="main" sx={{ flexGrow: 1, p: { xs: 2, md: 3 }, minWidth: 0 }}>
        <Toolbar />
        <Outlet />
      </Box>
    </Box>
  );
}
