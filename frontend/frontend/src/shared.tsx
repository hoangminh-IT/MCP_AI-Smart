import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, Snackbar, Stack, Typography } from '@mui/material';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';

const ToastContext = createContext<(message: string) => void>(() => {});
export function ToastProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState('');
  return <ToastContext.Provider value={setMessage}>{children}<Snackbar open={Boolean(message)} autoHideDuration={5000} onClose={() => setMessage('')} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}><Alert severity="success" variant="filled" onClose={() => setMessage('')}>{message}</Alert></Snackbar></ToastContext.Provider>;
}
export const useToast = () => useContext(ToastContext);
export function useDebounced<T,>(value: T, delay = 300) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => { const timer = window.setTimeout(() => setDebounced(value), delay); return () => window.clearTimeout(timer); }, [value, delay]);
  return debounced;
}
export function PageHeading({ eyebrow, title, description, actions }: { eyebrow: string; title: string; description: string; actions?: ReactNode }) {
  return <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" alignItems={{ xs: 'stretch', md: 'center' }} spacing={2} sx={{ mb: 3 }}><Box><Typography variant="overline" color="primary" sx={{ fontWeight: 800, letterSpacing: 1.5 }}>{eyebrow}</Typography><Typography variant="h4" sx={{ mt: 0.5 }}>{title}</Typography><Typography color="text.secondary" sx={{ mt: 1 }}>{description}</Typography></Box>{actions}</Stack>;
}
export function EmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return <Stack alignItems="center" spacing={1.5} sx={{ py: 8, px: 3, textAlign: 'center' }}><Box sx={{ bgcolor: '#EAF0FF', color: 'primary.main', p: 2, borderRadius: 4 }}><Inventory2Outlined sx={{ fontSize: 32 }} /></Box><Typography variant="h6">{title}</Typography><Typography color="text.secondary" sx={{ maxWidth: 440 }}>{description}</Typography>{action}</Stack>;
}
export function ConfirmDialog({ open, title, description, confirmLabel = 'Xác nhận', pending = false, error, onClose, onConfirm }: { open: boolean; title: string; description: string; confirmLabel?: string; pending?: boolean; error?: string; onClose: () => void; onConfirm: () => void }) {
  return <Dialog open={open} onClose={pending ? undefined : onClose} fullWidth maxWidth="xs"><DialogTitle>{title}</DialogTitle><DialogContent><DialogContentText>{description}</DialogContentText>{error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}</DialogContent><DialogActions sx={{ p: 2.5 }}><Button onClick={onClose} disabled={pending}>Hủy</Button><Button onClick={onConfirm} variant="contained" color="error" disabled={pending}>{pending ? 'Đang xử lý…' : confirmLabel}</Button></DialogActions></Dialog>;
}
