import { useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Accordion, AccordionDetails, AccordionSummary, alpha, Alert, Box, Button, Chip, Divider, LinearProgress, Paper, Skeleton, Stack, ToggleButton, ToggleButtonGroup, Typography, useTheme } from '@mui/material';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import CategoryOutlined from '@mui/icons-material/CategoryOutlined';
import LocalShippingOutlined from '@mui/icons-material/LocalShippingOutlined';
import PeopleOutline from '@mui/icons-material/PeopleOutline';
import ShoppingBagOutlined from '@mui/icons-material/ShoppingBagOutlined';
import SwapVertRounded from '@mui/icons-material/SwapVertRounded';
import WarningAmberOutlined from '@mui/icons-material/WarningAmberOutlined';
import ExpandMoreRounded from '@mui/icons-material/ExpandMoreRounded';
import ArrowForwardRounded from '@mui/icons-material/ArrowForwardRounded';
import InfoOutlined from '@mui/icons-material/InfoOutlined';
import TaskAltRounded from '@mui/icons-material/TaskAltRounded';
import { Link } from 'react-router-dom';
import { dateTime, errorMessage, money } from '../api';
import { PageHeading } from '../shared';
import { getDashboard } from '../reports';
import type { TrendPoint } from '../reports';
import { ReportExport } from '../components/ReportExport';
import { useWarehouseScope, WarehouseScopeSelect, WarehouseScopeNote } from '../warehouses';
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

function number(value: number) { return value.toLocaleString('vi-VN'); }
function dateInput(date: Date) { return date.toISOString().slice(0, 10); }
function range(period: '7' | '30' | '90') {
  const now = new Date(); const fromDate = new Date(now);
  fromDate.setUTCDate(now.getUTCDate() - (Number(period) - 1));
  return { from: `${dateInput(fromDate)}T00:00:00.000Z`, to: `${dateInput(now)}T23:59:59.999Z` };
}

function TrendChart({ data }: { data: TrendPoint[] }) {
  const theme = useTheme();
  const dayLabel = (value: string) => new Date(value).toLocaleDateString('vi-VN', { timeZone: 'UTC', day: '2-digit', month: '2-digit' });
  const series = [
    { key: 'salesOrdersValue', color: theme.palette.primary.main, label: 'Đơn bán' },
    { key: 'fulfilledSalesValue', color: theme.palette.success.main, label: 'Đã giao' },
    { key: 'purchaseOrdersValue', color: theme.palette.warning.main, label: 'Đơn mua' },
    { key: 'receivedPurchaseCost', color: theme.palette.info.main, label: 'Đã nhận' },
  ] as const;
  if (!data.length) return <Alert severity="info">Chưa có dữ liệu xu hướng trong kỳ đã chọn.</Alert>;
  const hasActivity = data.some((point) => series.some((item) => point[item.key] !== 0));
  return <Box component="figure" aria-label="Biểu đồ xu hướng đơn bán, đã giao và đơn mua" sx={{ m: 0, minWidth: 0 }}>
    <Box sx={{ width: '100%', height: { xs: 245, md: 270 }, minWidth: 0 }}>
      <ResponsiveContainer width="100%" height="100%" minWidth={0}>
        <LineChart data={data} accessibilityLayer margin={{ top: 16, right: 8, left: 0, bottom: 4 }}>
          <CartesianGrid stroke={theme.palette.divider} strokeDasharray="3 5" vertical={false} />
          <XAxis dataKey="date" tickFormatter={dayLabel} tick={{ fill: theme.palette.text.secondary, fontSize: 11 }} axisLine={false} tickLine={false} minTickGap={32} dy={8} />
          <YAxis width={58} domain={hasActivity ? [0, 'auto'] : [0, 1]} ticks={hasActivity ? undefined : [0]} tickFormatter={(value: number) => new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 }).format(value)} tick={{ fill: theme.palette.text.secondary, fontSize: 11 }} axisLine={false} tickLine={false} />
          <Tooltip formatter={(value) => money(Number(value))} labelFormatter={(value) => `${dayLabel(String(value))} · UTC`} contentStyle={{ backgroundColor: theme.palette.background.paper, borderColor: theme.palette.divider, borderRadius: 12, color: theme.palette.text.primary, fontSize: 12 }} labelStyle={{ color: theme.palette.text.primary, fontWeight: 700, marginBottom: 6 }} />
          <Legend iconType="circle" iconSize={7} wrapperStyle={{ fontSize: 11, paddingTop: 18 }} />
          {series.map((item) => <Line key={item.key} type="linear" dataKey={item.key} name={item.label} stroke={item.color} strokeWidth={2.5} dot={data.length === 1} activeDot={{ r: 4 }} isAnimationActive={false} />)}
        </LineChart>
      </ResponsiveContainer>
    </Box>
    <Typography component="figcaption" variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>
      {hasActivity ? 'Đơn vị: VND · Theo ngày UTC. Dùng phím mũi tên để xem từng ngày.' : 'Chưa phát sinh giá trị giao dịch trong khoảng thời gian này.'}
    </Typography>
  </Box>;
}

function MetricCard({ label, value, detail, icon, color }: { label: string; value: string; detail: string; icon: ReactNode; color: string }) {
  return <Paper variant="outlined" sx={{ p: { xs: 1.75, md: 2.25 }, minWidth: 0 }}>
    <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={1}>
      <Typography sx={{ fontSize: 12, color: 'text.secondary', fontWeight: 550 }}>{label}</Typography>
      <Box sx={{ display: 'grid', placeItems: 'center', width: 33, height: 33, flexShrink: 0, borderRadius: 2, bgcolor: alpha(color, .08), color, '& .MuiSvgIcon-root': { fontSize: 19 } }}>{icon}</Box>
    </Stack>
    <Typography sx={{ mt: 1.25, fontWeight: 750, fontSize: { xs: 25, md: 30 }, letterSpacing: '-1px', lineHeight: 1.2, fontVariantNumeric: 'tabular-nums', overflowWrap: 'anywhere' }}>{value}</Typography>
    <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: .75 }}>{detail}</Typography>
  </Paper>;
}

function DirectoryLink({ icon, label, value, to }: { icon: ReactNode; label: string; value: number; to: string }) {
  return <Box component={Link} to={to} sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 1.25, px: { xs: 1.5, md: 2 }, minWidth: 0, textDecoration: 'none', color: 'text.secondary', borderRadius: 1.5, '&:hover': { bgcolor: 'action.hover', color: 'primary.main' }, '&:focus-visible': { outline: '2px solid', outlineColor: 'primary.main', outlineOffset: -2 } }}>
    <Box sx={{ display: 'flex', '& .MuiSvgIcon-root': { fontSize: 18, opacity: .75 } }}>{icon}</Box>
    <Typography sx={{ fontSize: 12, flex: 1 }}>{label}</Typography><Typography sx={{ fontSize: 13, fontWeight: 700, color: 'text.primary', fontVariantNumeric: 'tabular-nums' }}>{number(value)}</Typography>
  </Box>;
}

function ValueRow({ label, value, color }: { label: string; value: number; color?: string }) {
  return <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={2} sx={{ minWidth: 0 }}>
    <Stack direction="row" spacing={1} alignItems="center" sx={{ minWidth: 0 }}>{color && <Box sx={{ width: 7, height: 7, bgcolor: color, flexShrink: 0, borderRadius: '50%' }} />}<Typography variant="body2" color="text.secondary" sx={{ fontSize: 12 }}>{label}</Typography></Stack>
    <Typography sx={{ fontSize: 13, fontWeight: 650, textAlign: 'right', fontVariantNumeric: 'tabular-nums', overflowWrap: 'anywhere' }}>{money(value)}</Typography>
  </Stack>;
}

export default function Dashboard() {
  const theme = useTheme(); const { warehouseId } = useWarehouseScope();
  const [period, setPeriod] = useState<'7' | '30' | '90'>('30');
  const parameters = useMemo(() => ({ ...range(period), warehouseId }), [period, warehouseId]);
  const query = useQuery({ queryKey: ['dashboard-report', parameters], queryFn: ({ signal }) => getDashboard(parameters, signal) });
  const report = query.data;
  const pendingOrders = report ? report.pendingPurchaseOrders + report.pendingSalesOrders : 0;
  const metrics = report ? [
    { label: 'Tổng lượng tồn', value: number(report.inventory.totalQuantity), detail: 'Số lượng hiện có trong kho', icon: <Inventory2Outlined />, color: theme.palette.primary.main },
    { label: 'Sản phẩm hoạt động', value: number(report.inventory.totalProducts), detail: 'Danh mục hàng hóa đang kinh doanh', icon: <CategoryOutlined />, color: theme.palette.secondary.main },
    { label: 'Đơn hàng chờ xử lý', value: number(pendingOrders), detail: `${number(report.pendingPurchaseOrders)} đơn mua · ${number(report.pendingSalesOrders)} đơn bán`, icon: <ShoppingBagOutlined />, color: theme.palette.info.main },
    { label: 'Tồn thấp / hết hàng', value: `${number(report.inventory.lowStockCount)} / ${number(report.inventory.outOfStockCount)}`, detail: 'Số sản phẩm cần kiểm tra tồn kho', icon: <WarningAmberOutlined />, color: report.inventory.lowStockCount || report.inventory.outOfStockCount ? theme.palette.warning.main : theme.palette.success.main },
  ] : [];

  return <>
    <PageHeading eyebrow="TỔNG QUAN" title="Tổng quan kho hàng" description="Nắm bắt tồn kho, theo dõi đơn hàng và ưu tiên công việc hôm nay."
      actions={<Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap><Button component={Link} to="/reports" variant="outlined">Mở báo cáo</Button><Button component={Link} to="/inventory" variant="contained" endIcon={<ArrowForwardRounded sx={{ fontSize: 17 }} />}>Quản lý tồn kho</Button></Stack>} />

    <Paper variant="outlined" sx={{ p: { xs: 1.5, md: 1.75 }, mb: 2.5 }}>
      <Stack direction={{ xs: 'column', lg: 'row' }} alignItems={{ xs: 'stretch', lg: 'center' }} justifyContent="space-between" spacing={1.5}>
        <Box sx={{ width: { xs: '100%', lg: 270 }, flexShrink: 0 }}><WarehouseScopeSelect /></Box>
        <Stack direction={{ xs: 'column', sm: 'row' }} alignItems={{ xs: 'stretch', sm: 'center' }} spacing={1.5} sx={{ minWidth: 0 }}>
          <ToggleButtonGroup size="small" exclusive value={period} aria-label="Khoảng thời gian báo cáo" onChange={(_, value: '7' | '30' | '90' | null) => { if (value) setPeriod(value); }} sx={{ bgcolor: 'action.hover', borderRadius: 2, p: .4, '& .MuiToggleButton-root': { border: 0, borderRadius: '7px !important', px: { xs: 1.75, sm: 2 }, py: .65, fontSize: 11.5, color: 'text.secondary', fontWeight: 650 }, '& .Mui-selected': { bgcolor: 'background.paper', color: 'primary.main', boxShadow: '0 1px 4px #142A4512', '&:hover': { bgcolor: 'background.paper' } } }}>
            {(['7', '30', '90'] as const).map((value) => <ToggleButton key={value} value={value}>{value} ngày</ToggleButton>)}
          </ToggleButtonGroup>
          <ReportExport report="dashboard" filters={parameters} />
        </Stack>
      </Stack>
      <Box sx={{ mt: 1.1, pl: .25 }}><WarehouseScopeNote compact /></Box>
    </Paper>

    {query.isFetching && <LinearProgress aria-label="Đang cập nhật tổng quan" sx={{ mb: 2, borderRadius: 2, height: 2 }} />}
    {query.isError && <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" onClick={() => void query.refetch()}>Thử lại</Button>}>{errorMessage(query.error)}</Alert>}

    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'repeat(2,minmax(0,1fr))', lg: 'repeat(4,minmax(0,1fr))' }, gap: { xs: 1.25, md: 2 }, mb: 1.5 }}>
      {query.isLoading ? Array.from({ length: 4 }, (_, index) => <Paper key={index} variant="outlined" sx={{ p: 2.25 }}><Skeleton width="70%" /><Skeleton width="45%" height={48} /><Skeleton width="85%" /></Paper>) : metrics.map((item) => <MetricCard key={item.label} {...item} />)}
    </Box>

    {report && <>
      <Paper variant="outlined" sx={{ display: 'grid', gridTemplateColumns: { xs: 'repeat(2,minmax(0,1fr))', md: 'repeat(4,minmax(0,1fr))' }, px: .75, py: .25, mb: 2.5 }}>
        <DirectoryLink icon={<CategoryOutlined />} label="Danh mục" value={report.inventory.totalCategories} to="/categories" />
        <DirectoryLink icon={<LocalShippingOutlined />} label="Nhà cung cấp" value={report.totalSuppliers} to="/suppliers" />
        <DirectoryLink icon={<PeopleOutline />} label="Khách hàng" value={report.totalCustomers} to="/customers" />
        <DirectoryLink icon={<SwapVertRounded />} label="Lượt biến động" value={report.inventory.stockMovementCount} to="/stock-movements" />
      </Paper>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 1.8fr) minmax(280px, .9fr)' }, gap: 2, mb: 2 }}>
        <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, minWidth: 0 }}>
          <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={1} sx={{ mb: 2 }}>
            <Box><Typography component="h2" fontWeight={700} sx={{ fontSize: 14 }}>Xu hướng đơn hàng</Typography><Typography variant="caption" color="text.secondary">Giá trị đơn bán, đơn mua và giao nhận</Typography></Box>
            <Chip size="small" variant="outlined" label={`${period} ngày gần nhất`} sx={{ alignSelf: 'flex-start', fontSize: 10.5 }} />
          </Stack>
          <TrendChart data={report.trend} />
        </Paper>

        <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
          <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={1}>
            <Box><Typography component="h2" fontWeight={700} sx={{ fontSize: 14 }}>Giá trị tồn kho</Typography><Typography variant="caption" color="text.secondary">Định giá theo hàng đang có</Typography></Box>
            <Inventory2Outlined sx={{ fontSize: 21, color: 'secondary.main' }} />
          </Stack>
          <Box sx={{ bgcolor: alpha(theme.palette.secondary.main, .055), border: '1px solid', borderColor: alpha(theme.palette.secondary.main, .1), borderRadius: 2.5, p: 2, mt: 2.5, mb: 2.5 }}>
            <Typography variant="caption" color="text.secondary">Theo giá vốn</Typography>
            <Typography sx={{ fontWeight: 750, fontSize: { xs: 25, md: 28 }, mt: .75, lineHeight: 1.3, letterSpacing: '-.6px', color: 'text.primary', overflowWrap: 'anywhere', fontVariantNumeric: 'tabular-nums' }}>{money(report.inventory.inventoryCostValue)}</Typography>
          </Box>
          <ValueRow label="Theo giá bán lẻ" value={report.inventory.inventoryRetailValue} />
          <Divider sx={{ my: 2 }} />
          <Typography variant="caption" color="text.secondary" sx={{ mb: 2 }}>Giá trị hàng hiện có trong phạm vi kho đã chọn. Số liệu này không phải doanh thu.</Typography>
          <Button component={Link} to="/reports" color="secondary" size="small" endIcon={<ArrowForwardRounded sx={{ fontSize: 16 }} />} sx={{ alignSelf: 'flex-start', mt: 'auto', px: 0 }}>Xem báo cáo tồn kho</Button>
        </Paper>
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 1fr) minmax(0, 1fr)' }, gap: 2 }}>
        <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, minWidth: 0 }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1} sx={{ mb: 2 }}><Typography component="h2" fontWeight={700} sx={{ fontSize: 14 }}>Ưu tiên vận hành</Typography><Chip size="small" label="Hiện tại" variant="outlined" sx={{ fontSize: 10.5 }} /></Stack>
          <Stack spacing={.5}>
            <Box component={Link} to="/inventory" sx={{ px: 1.5, py: 1.25, display: 'flex', alignItems: 'center', gap: 1.25, textDecoration: 'none', color: 'inherit', borderRadius: 2, bgcolor: alpha(theme.palette.warning.main, .055), '&:hover': { bgcolor: alpha(theme.palette.warning.main, .1) } }}>
              <WarningAmberOutlined sx={{ fontSize: 20, color: 'warning.main' }} /><Box sx={{ flex: 1, minWidth: 0 }}><Typography sx={{ fontSize: 12, fontWeight: 650 }}>Kiểm tra hàng cần bổ sung</Typography><Typography variant="caption" color="text.secondary">{number(report.inventory.lowStockCount)} tồn thấp · {number(report.inventory.outOfStockCount)} hết hàng</Typography></Box><ArrowForwardRounded sx={{ fontSize: 16, color: 'text.secondary' }} />
            </Box>
            <Box component={Link} to="/purchase-orders" sx={{ px: 1.5, py: 1.25, display: 'flex', alignItems: 'center', gap: 1.25, textDecoration: 'none', color: 'inherit', borderRadius: 2, '&:hover': { bgcolor: 'action.hover' } }}><LocalShippingOutlined sx={{ fontSize: 20, color: 'text.secondary' }} /><Typography sx={{ fontSize: 12, flex: 1 }}>Đơn mua chờ xử lý</Typography><Typography sx={{ fontSize: 13, fontWeight: 700 }}>{number(report.pendingPurchaseOrders)}</Typography><ArrowForwardRounded sx={{ fontSize: 16, color: 'text.secondary' }} /></Box>
            <Box component={Link} to="/sales-orders" sx={{ px: 1.5, py: 1.25, display: 'flex', alignItems: 'center', gap: 1.25, textDecoration: 'none', color: 'inherit', borderRadius: 2, '&:hover': { bgcolor: 'action.hover' } }}><ShoppingBagOutlined sx={{ fontSize: 20, color: 'text.secondary' }} /><Typography sx={{ fontSize: 12, flex: 1 }}>Đơn bán chờ xử lý</Typography><Typography sx={{ fontSize: 13, fontWeight: 700 }}>{number(report.pendingSalesOrders)}</Typography><ArrowForwardRounded sx={{ fontSize: 16, color: 'text.secondary' }} /></Box>
          </Stack>
          {!pendingOrders && !report.inventory.lowStockCount && !report.inventory.outOfStockCount && <Stack direction="row" alignItems="center" spacing={.75} sx={{ mt: 1.5, color: 'success.main' }}><TaskAltRounded sx={{ fontSize: 16 }} /><Typography variant="caption">Không có cảnh báo tồn kho hoặc đơn hàng chờ xử lý.</Typography></Stack>}
        </Paper>

        <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, minWidth: 0 }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1} sx={{ mb: 2.5 }}><Typography component="h2" fontWeight={700} sx={{ fontSize: 14 }}>Giá trị giao dịch trong kỳ</Typography><Typography variant="caption" color="text.secondary">{period} ngày</Typography></Stack>
          <Stack spacing={1.75}>
            <ValueRow label="Tổng giá trị đơn bán" value={report.salesOrdersValue} color={theme.palette.primary.main} />
            <ValueRow label="Giá trị số lượng đã giao" value={report.fulfilledSalesValue} color={theme.palette.success.main} />
            <Divider />
            <ValueRow label="Tổng giá trị đơn mua" value={report.purchaseOrdersValue} color={theme.palette.warning.main} />
            <ValueRow label="Giá vốn hàng đã nhận" value={report.receivedPurchaseCost} color={theme.palette.info.main} />
          </Stack>
        </Paper>
      </Box>

      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 2.25, px: .25 }}>Cập nhật {dateTime(report.inventory.asOf)}</Typography>
    </>}

    {!query.isLoading && !query.isError && !report && <Alert severity="info">Chưa có dữ liệu tổng quan.</Alert>}
    <Accordion disableGutters elevation={0} sx={{ mt: 1, bgcolor: 'transparent', border: '0 !important', boxShadow: 'none !important', '&:before': { display: 'none' }, '&.Mui-expanded': { mt: 1 } }}>
      <AccordionSummary expandIcon={<ExpandMoreRounded fontSize="small" />} sx={{ px: .25, minHeight: 38, '& .MuiAccordionSummary-content': { my: 1 } }}><Stack direction="row" spacing={.75} alignItems="center"><InfoOutlined sx={{ fontSize: 14, color: 'text.secondary' }} /><Typography variant="caption" color="text.secondary">Phạm vi dữ liệu và cách tính</Typography></Stack></AccordionSummary>
      <AccordionDetails sx={{ px: .25, pt: 0 }}><Stack spacing={1}><Typography variant="caption" color="text.secondary">Sản phẩm, danh mục, nhà cung cấp và khách hàng là danh mục chung. Tồn kho, giá trị, cảnh báo và đơn hàng theo kho đã chọn. Định mức tối thiểu dùng chung cho sản phẩm; thông báo hệ thống theo tổng tồn.</Typography><Typography variant="caption" color="text.secondary">Tồn kho và đơn chờ xử lý phản ánh hiện tại. Giá trị đơn theo ngày tạo trong kỳ UTC; giá trị giao, nhận theo ngày phát sinh phiếu, kể cả đơn tạo trước kỳ.</Typography><Typography variant="caption" color="text.secondary">Các nhóm tồn thấp và hết hàng có thể trùng nhau. Giá trị giao và nhận được thống kê độc lập, chưa dùng để tính lợi nhuận kế toán.</Typography></Stack></AccordionDetails>
    </Accordion>
  </>;
}
