import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material';

interface Props {
  open: boolean;
  title: string;
  message: string;
  confirmText?: string;
  busy?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}

export default function ConfirmDialog({ open, title, message, confirmText = 'Xác nhận', busy, onConfirm, onClose }: Props) {
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose}>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <DialogContentText>{message}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Hủy</Button>
        <Button onClick={onConfirm} color="error" variant="contained" disabled={busy}>{confirmText}</Button>
      </DialogActions>
    </Dialog>
  );
}
