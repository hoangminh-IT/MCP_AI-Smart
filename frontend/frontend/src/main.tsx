import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { CssBaseline, LinearProgress, ThemeProvider, createTheme } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './auth';
import { ToastProvider } from './shared';
import { Shell } from './shell';
import './styles.css';

const Login = lazy(() => import('./pages/Login'));
const Products = lazy(() => import('./pages/Products'));
const Categories = lazy(() => import('./pages/Categories'));
const Inventory = lazy(() => import('./pages/Inventory'));
const StockMovements = lazy(() => import('./pages/StockMovements'));
const Profile = lazy(() => import('./pages/Profile'));
const McpLab = lazy(() => import('./pages/McpLab'));
const McpPlayground = lazy(() => import('./pages/McpPlayground'));
const McpTrace = lazy(() => import('./pages/McpTrace'));
const AiAssistant = lazy(() => import('./pages/AiAssistant'));
const queryClient = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: false, refetchOnWindowFocus: false } } });
const theme = createTheme({
  palette: { primary: { main: '#3468F6' }, secondary: { main: '#0A1830' }, background: { default: '#F6F8FD', paper: '#FFFFFF' }, text: { primary: '#18243B', secondary: '#78869B' }, success: { main: '#0D9473' } },
  typography: { fontFamily: 'Inter, "Noto Sans", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif', h4: { fontSize: '1.85rem', fontWeight: 800, letterSpacing: '-0.6px' }, h5: { fontWeight: 750 }, h6: { fontWeight: 700 }, button: { textTransform: 'none', fontWeight: 650 } },
  shape: { borderRadius: 12 },
  components: {
    MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { borderRadius: 10, minHeight: 40, paddingInline: 18 } } },
    MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { outlined: { borderColor: '#E3E9F1', borderRadius: 16 } } },
    MuiTableCell: { styleOverrides: { head: { color: '#78869B', fontSize: 11, fontWeight: 750, letterSpacing: '.6px', background: '#FAFBFE', whiteSpace: 'nowrap' }, body: { borderColor: '#EEF1F6' } } },
    MuiTextField: { defaultProps: { size: 'small' } },
    MuiChip: { styleOverrides: { root: { fontWeight: 650, borderRadius: 7 } } },
  },
});

createRoot(document.getElementById('root')!).render(<StrictMode><ThemeProvider theme={theme}><CssBaseline /><QueryClientProvider client={queryClient}><AuthProvider><ToastProvider><BrowserRouter><Suspense fallback={<LinearProgress />}><Routes><Route path="/login" element={<Login />} /><Route element={<Shell />}><Route index element={<Navigate to="/inventory" replace />} /><Route path="/inventory" element={<Inventory />} /><Route path="/stock-movements" element={<StockMovements />} /><Route path="/products" element={<Products />} /><Route path="/categories" element={<Categories />} /><Route path="/profile" element={<Profile />} /><Route path="/ai" element={<AiAssistant />} /><Route path="/mcp/lab" element={<McpLab />} /><Route path="/mcp/playground" element={<McpPlayground />} /><Route path="/mcp/trace" element={<McpTrace />} /><Route path="*" element={<Navigate to="/inventory" replace />} /></Route></Routes></Suspense></BrowserRouter></ToastProvider></AuthProvider></QueryClientProvider></ThemeProvider></StrictMode>);
