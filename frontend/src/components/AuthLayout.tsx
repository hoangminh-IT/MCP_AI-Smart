import type { ReactNode } from 'react';
import { Avatar, Box, Stack, Typography } from '@mui/material';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import { AppearanceMenu } from '../appearance';
import { AuthShowcase } from './AuthShowcase';

export function AuthLayout({ children, registering = false }: { children: ReactNode; registering?: boolean }) {
  return <Box sx={{ minHeight: '100dvh', display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'minmax(360px, .95fr) 1.05fr' }, gridTemplateRows: { xs: 'auto 1fr', md: '1fr' }, bgcolor: 'background.default' }}>
    <Stack component="aside" sx={{ position: 'relative', isolation: 'isolate', overflow: 'hidden', bgcolor: '#101F36', color: 'white',
      p: { xs: 3, sm: 4, md: 5, xl: 7 }, minHeight: { xs: 'auto', md: '100dvh' }, justifyContent: 'space-between' }}>
      <AuthShowcase />
      <Stack direction="row" spacing={1.5} alignItems="center" sx={{ position: 'relative', zIndex: 1, pr: { md: 7 } }}>
        <Avatar variant="rounded" sx={{ width: 46, height: 46, color: '#FFFFFF', bgcolor: 'rgba(255,255,255,.14)',
          border: '1px solid rgba(255,255,255,.28)', backdropFilter: 'blur(12px)', borderRadius: '14px' }}><Inventory2Outlined /></Avatar>
        <Box><Typography sx={{ fontWeight: 850, fontSize: 19, letterSpacing: -.4 }}>Smart Inventory<span style={{ color: '#8AE7DD' }}>.</span></Typography><Typography sx={{ fontSize: 11, color: 'rgba(255,255,255,.78)', mt: .25 }}>Quản lý kho hàng</Typography></Box>
      </Stack>
      <Box sx={{ position: 'relative', zIndex: 1, mt: 'auto', pt: 12, pb: { md: 5, xl: 6 }, display: { xs: 'none', md: 'block' } }}>
        <Typography component="h1" sx={{ fontWeight: 750, fontSize: { md: 34, lg: 44, xl: 54 }, letterSpacing: -1.6,
          lineHeight: 1.15, maxWidth: 580, textWrap: 'balance', textShadow: '0 2px 24px rgba(0,0,0,.18)' }}>
          {registering ? <>Chào mừng bạn.<br />Cùng bắt đầu nhé.</> : <>Bắt đầu ngày mới<br />cùng Smart Inventory.</>}
        </Typography>
        <Typography sx={{ color: 'rgba(255,255,255,.82)', fontSize: { md: 14, xl: 15 }, lineHeight: 1.8, maxWidth: 380, mt: 2.5 }}>
          {registering ? 'Tạo tài khoản để kết nối với Smart Inventory.' : 'Rất vui được gặp bạn. Đăng nhập để tiếp tục.'}
        </Typography>
      </Box>
      <Typography variant="caption" sx={{ position: 'relative', zIndex: 1, color: 'rgba(255,255,255,.62)', display: { xs: 'none', md: 'block' } }}>© {new Date().getFullYear()} Smart Inventory</Typography>
    </Stack>
    <Stack component="main" alignItems="center" justifyContent="center" sx={{ p: { xs: 3, sm: 4, md: 5 }, pt: { xs: 7, md: 7 }, position: 'relative' }}>
      <Box sx={{ position: 'absolute', top: 14, right: 18 }}><AppearanceMenu /></Box>
      <Box sx={{ width: '100%', maxWidth: registering ? 480 : 420 }}>{children}</Box>
    </Stack>
  </Box>;
}
