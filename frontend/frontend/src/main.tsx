import { StrictMode, Suspense, lazy } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { LinearProgress } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider, useAuth } from './auth';
import { ToastProvider } from './shared';
import { Shell } from './shell';
import { AppearanceProvider } from './appearance';
import { WarehouseScopeProvider } from './warehouses';
import './styles.css';

const Login = lazy(() => import('./pages/Login'));
const Register = lazy(() => import('./pages/Register'));
const Products = lazy(() => import('./pages/Products'));
const Categories = lazy(() => import('./pages/Categories'));
const Suppliers = lazy(() => import('./pages/Suppliers'));
const Warehouses = lazy(() => import('./pages/Warehouses'));
const Transfers = lazy(() => import('./pages/Transfers'));
const Customers = lazy(() => import('./pages/Customers'));
const Users = lazy(() => import('./pages/Users'));
const Settings = lazy(() => import('./pages/Settings'));
const AuditLogs = lazy(() => import('./pages/AuditLogs'));
const PurchaseOrders = lazy(() => import('./pages/PurchaseOrders'));
const Inventory = lazy(() => import('./pages/Inventory'));
const StockMovements = lazy(() => import('./pages/StockMovements'));
const Profile = lazy(() => import('./pages/Profile'));
const McpLab = lazy(() => import('./pages/McpLab'));
const McpPlayground = lazy(() => import('./pages/McpPlayground'));
const McpTrace = lazy(() => import('./pages/McpTrace'));
const AiAssistant = lazy(() => import('./pages/AiAssistant'));
const SalesOrders = lazy(() => import('./pages/SalesOrders'));
const Reports = lazy(() => import('./pages/Reports'));
const Notifications = lazy(() => import('./pages/Notifications'));
const Dashboard = lazy(() => import('./pages/Dashboard'));
const queryClient = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: false, refetchOnWindowFocus: false } } });

// The server enforces these same roles; this guard also avoids mounting inventory pages for customers.
function StaffArea() {
  const { user } = useAuth();
  return user?.role === 'Customer' ? <Navigate to="/profile" replace /> : <Outlet />;
}
function AccountHome() {
  const { user } = useAuth();
  return <Navigate to={user?.role === 'Customer' ? '/profile' : '/dashboard'} replace />;
}

createRoot(document.getElementById('root')!).render(
  <StrictMode><AppearanceProvider><QueryClientProvider client={queryClient}><AuthProvider><WarehouseScopeProvider><ToastProvider><BrowserRouter>
    <Suspense fallback={<LinearProgress />}><Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route element={<Shell />}>
        <Route index element={<AccountHome />} />
        <Route path="/profile" element={<Profile />} />
        <Route element={<StaffArea />}>
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/reports" element={<Reports />} />
          <Route path="/notifications" element={<Notifications />} />
          <Route path="/inventory" element={<Inventory />} />
          <Route path="/stock-movements" element={<StockMovements />} />
          <Route path="/products" element={<Products />} />
          <Route path="/categories" element={<Categories />} />
          <Route path="/suppliers" element={<Suppliers />} />
          <Route path="/warehouses" element={<Warehouses />} />
          <Route path="/transfers" element={<Transfers />} />
          <Route path="/customers" element={<Customers />} />
          <Route path="/users" element={<Users />} />
          <Route path="/settings" element={<Settings />} />
          <Route path="/audit" element={<AuditLogs />} />
          <Route path="/purchase-orders" element={<PurchaseOrders />} />
          <Route path="/sales-orders" element={<SalesOrders />} />
          <Route path="/ai" element={<AiAssistant />} />
          <Route path="/mcp/lab" element={<McpLab />} />
          <Route path="/mcp/playground" element={<McpPlayground />} />
          <Route path="/mcp/trace" element={<McpTrace />} />
        </Route>
        <Route path="*" element={<AccountHome />} />
      </Route>
    </Routes></Suspense>
  </BrowserRouter></ToastProvider></WarehouseScopeProvider></AuthProvider></QueryClientProvider></AppearanceProvider></StrictMode>,
);
