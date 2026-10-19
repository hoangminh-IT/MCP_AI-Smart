import { Card, CardContent, Skeleton, Typography } from '@mui/material';
import type { ReactNode } from 'react';

interface Props {
  label: string;
  value: string;
  icon: ReactNode;
  loading?: boolean;
  color?: string;
}

export default function StatCard({ label, value, icon, loading, color = 'primary.main' }: Props) {
  return (
    <Card variant="outlined">
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Typography component="span" sx={{ color, display: 'flex', fontSize: 40 }}>{icon}</Typography>
        <div>
          <Typography variant="body2" color="text.secondary">{label}</Typography>
          {loading ? <Skeleton width={90} height={36} /> : <Typography variant="h5" fontWeight={600}>{value}</Typography>}
        </div>
      </CardContent>
    </Card>
  );
}
