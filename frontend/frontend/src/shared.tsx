import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, Snackbar, Stack, Typography } from '@mui/material';
import type { SxProps, Theme } from '@mui/material';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';

type ToastSeverity = 'success' | 'error' | 'warning' | 'info';
const ToastContext = createContext<(message: string, severity?: ToastSeverity) => void>(() => {});
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<{ message: string; severity: ToastSeverity } | null>(null);
  const showToast = useCallback((message: string, severity: ToastSeverity = 'success') => setToast({ message, severity }), []);
  return <ToastContext.Provider value={showToast}>{children}<Snackbar open={Boolean(toast)} autoHideDuration={5000} onClose={() => setToast(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}><Alert severity={toast?.severity ?? 'success'} variant="filled" onClose={() => setToast(null)}>{toast?.message}</Alert></Snackbar></ToastContext.Provider>;
}
export const useToast = () => useContext(ToastContext);
export function useDebounced<T,>(value: T, delay = 300) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => { const timer = window.setTimeout(() => setDebounced(value), delay); return () => window.clearTimeout(timer); }, [value, delay]);
  return debounced;
}
export function PageHeading({ eyebrow, title, description, actions }: { eyebrow: string; title: string; description: string; actions?: ReactNode }) {
  return <Box component="header" className="page-heading" data-section={eyebrow}
    sx={{ display: 'flex', flexDirection: { xs: 'column', lg: 'row' }, justifyContent: 'space-between',
      alignItems: { xs: 'stretch', lg: 'center' }, gap: 2, mb: 3 }}>
    <Box sx={{ minWidth: 0, maxWidth: 720 }}>
      <Typography component="h1" variant="h4" sx={{ fontSize: { xs: 23, sm: 26 }, fontWeight: 750, letterSpacing: '-.75px' }}>{title}</Typography>
      <Typography color="text.secondary" sx={{ mt: .75, fontSize: 13, lineHeight: 1.65 }}>{description}</Typography>
    </Box>
    {actions && <Box sx={{ flexShrink: 0, '& > .MuiStack-root': { flexWrap: 'wrap', alignItems: 'center', rowGap: 1 } }}>{actions}</Box>}
  </Box>;
}
export function FilterBar({ children, sx }: { children: ReactNode; sx?: SxProps<Theme> }) {
  return <Box className="module-filters" sx={[
    { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 1.5, px: { xs: 2, md: 2.5 }, py: 2,
      '& > .MuiTextField-root': { flex: '1 1 170px', minWidth: 0, maxWidth: { xs: 'none', md: 260 } },
      '& > .MuiTextField-root:first-of-type': { flex: '2 1 240px', maxWidth: { xs: 'none', md: 400 } },
      '& > .MuiFormControlLabel-root': { ml: 0, mr: 0 },
    }, ...(Array.isArray(sx) ? sx : sx ? [sx] : []),
  ]}>{children}</Box>;
}
export function EmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return <Stack alignItems="center" spacing={1.25} sx={{ py: 6, px: 3, textAlign: 'center' }}><Box sx={{ bgcolor: 'action.hover', color: 'primary.main', width: 52, height: 52, display: 'grid', placeItems: 'center', mb: .5, borderRadius: '14px', border: 1, borderColor: 'divider' }}><Inventory2Outlined sx={{ fontSize: 25 }} /></Box><Typography sx={{ fontSize: 16, fontWeight: 700 }}>{title}</Typography><Typography color="text.secondary" sx={{ maxWidth: 400, fontSize: 13, lineHeight: 1.75 }}>{description}</Typography>{action && <Box sx={{ pt: .75 }}>{action}</Box>}</Stack>;
}
export function ConfirmDialog({ open, title, description, confirmLabel = 'Xác nhận', pending = false, error, onClose, onConfirm }: { open: boolean; title: string; description: string; confirmLabel?: string; pending?: boolean; error?: string; onClose: () => void; onConfirm: () => void }) {
  return <Dialog open={open} onClose={pending ? undefined : onClose} fullWidth maxWidth="xs"><DialogTitle>{title}</DialogTitle><DialogContent><DialogContentText>{description}</DialogContentText>{error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}</DialogContent><DialogActions sx={{ p: 2.5 }}><Button onClick={onClose} disabled={pending}>Hủy</Button><Button onClick={onConfirm} variant="contained" color="error" disabled={pending}>{pending ? 'Đang xử lý…' : confirmLabel}</Button></DialogActions></Dialog>;
}
