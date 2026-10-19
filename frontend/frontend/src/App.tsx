import { Route, Routes } from 'react-router-dom';
import Layout from './components/Layout';
import ComingSoon from './pages/ComingSoon';
import Dashboard from './pages/Dashboard';
import NotFound from './pages/NotFound';
import Products from './pages/Products';

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Dashboard />} />
        <Route path="products" element={<Products />} />
        <Route path="inventory" element={<ComingSoon task="T053–T054" />} />
        <Route path="chat" element={<ComingSoon task="T055" />} />
        <Route path="login" element={<ComingSoon task="T056" />} />
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  );
}
