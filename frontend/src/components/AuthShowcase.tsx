import { useEffect, useState } from 'react';
import { Box, useMediaQuery, useTheme } from '@mui/material';
import { authSlides } from './authSlides';

export function AuthShowcase() {
  const theme = useTheme();
  const desktop = useMediaQuery(theme.breakpoints.up('md'));
  const reduceMotion = useMediaQuery('(prefers-reduced-motion: reduce)');
  const [active, setActive] = useState(0);
  const [visible, setVisible] = useState(() => document.visibilityState !== 'hidden');

  useEffect(() => {
    const update = () => setVisible(document.visibilityState !== 'hidden');
    document.addEventListener('visibilitychange', update);
    return () => document.removeEventListener('visibilitychange', update);
  }, []);

  const rotating = desktop && visible && !reduceMotion;
  useEffect(() => {
    if (!rotating) return;
    // Pointer hover and typing in the adjacent form do not interrupt playback.
    const timer = window.setTimeout(() => {
      setActive((index) => (index + 1) % authSlides.length);
    }, 6_000);
    return () => window.clearTimeout(timer);
  }, [active, rotating]);

  return <Box component="section" role="region" aria-roledescription="trình chiếu" aria-label="Ảnh kho hàng"
    aria-live="off" data-active-slide={authSlides[active].id}
    sx={{ position: 'absolute', inset: 0, display: { xs: 'none', md: 'block' }, overflow: 'hidden' }}>
    {authSlides.map((slide, index) => <Box key={slide.id} aria-hidden={index !== active} data-slide-id={slide.id}
      sx={{ position: 'absolute', inset: 0, opacity: index === active ? 1 : 0,
        visibility: index === active ? 'visible' : 'hidden',
        transition: reduceMotion ? 'none' : 'opacity 1000ms ease, visibility 1000ms ease' }}>
      <Box component="img" src={slide.src} alt={slide.alt} width={1440} height={1920}
        loading={desktop ? 'eager' : 'lazy'} decoding="async" fetchPriority={index === 0 ? 'high' : 'auto'}
        sx={{ display: 'block', width: '100%', height: '100%', objectFit: 'cover', objectPosition: slide.position }} />
    </Box>)}
    <Box aria-hidden="true" sx={{ position: 'absolute', inset: 0, pointerEvents: 'none',
      background: 'linear-gradient(180deg, rgba(6,18,29,.52) 0%, rgba(6,18,29,.08) 35%, rgba(6,18,29,.22) 52%, rgba(6,18,29,.88) 100%)' }} />
  </Box>;
}
