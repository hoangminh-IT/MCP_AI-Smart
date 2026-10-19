import { Box, Button, Typography } from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';

export default function NotFound() {
  return (
    <Box sx={{ textAlign: 'center', mt: 8 }}>
      <Typography variant="h2" fontWeight={700}>404</Typography>
      <Typography sx={{ mb: 3 }} color="text.secondary">Trang bạn tìm không tồn tại.</Typography>
      <Button variant="contained" component={RouterLink} to="/">Về trang tổng quan</Button>
    </Box>
  );
}
