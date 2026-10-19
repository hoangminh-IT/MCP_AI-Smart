import { Alert } from '@mui/material';

export default function ComingSoon({ task }: { task: string }) {
  return <Alert severity="info">Màn hình này làm ở {task} (Ngày 2).</Alert>;
}
