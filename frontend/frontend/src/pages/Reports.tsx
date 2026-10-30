import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Accordion, AccordionDetails, AccordionSummary, Alert, Box, Button, Chip, Divider, LinearProgress, MenuItem, Paper, Skeleton, Stack, Tab, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, Tabs, TextField, Typography } from '@mui/material';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import SwapVertRounded from '@mui/icons-material/SwapVertRounded';
import LocalShippingOutlined from '@mui/icons-material/LocalShippingOutlined';
import ShoppingBagOutlined from '@mui/icons-material/ShoppingBagOutlined';
import InsightsOutlined from '@mui/icons-material/InsightsOutlined';
import InfoOutlined from '@mui/icons-material/InfoOutlined';
import ExpandMoreRounded from '@mui/icons-material/ExpandMoreRounded';
import { dateTime, errorMessage, money } from '../api';
import { EmptyState, FilterBar, PageHeading } from '../shared';
import { getInventoryReport, getMovementReport, getProductPerformance, getPurchaseReport, getSalesReport } from '../reports';
import type { InventoryReportRow, ProductPerformanceRow, PurchaseReportRow, ReportPeriod, SalesReportRow, StockMovementReportRow } from '../reports';
import { useApplicationSettings } from '../settings';
import { ReportExport } from '../components/ReportExport';
import { useWarehouseScope, WarehouseScopeSelect, WarehouseScopeNote } from '../warehouses';

type ReportTab = 'inventory' | 'movements' | 'purchases' | 'sales' | 'performance';
function defaultDates(days = 30) { const now = new Date(); const from = new Date(now); from.setUTCDate(now.getUTCDate() - (days - 1)); return { from: from.toISOString().slice(0, 10), to: now.toISOString().slice(0, 10) }; }
function periodFrom(from: string, to: string): ReportPeriod { return { from: `${from}T00:00:00.000Z`, to: `${to}T23:59:59.999Z` }; }
export default function Reports() {
  const { warehouseId } = useWarehouseScope();
  const settings = useApplicationSettings(); const [datesEdited, setDatesEdited] = useState(false);
  const initial = defaultDates(settings.data?.reportDefaultDays); const [from, setFrom] = useState(initial.from); const [to, setTo] = useState(initial.to);
  useEffect(() => { if (!datesEdited && settings.data) { const defaults = defaultDates(settings.data.reportDefaultDays); setFrom(defaults.from); setTo(defaults.to); } }, [settings.data?.reportDefaultDays, datesEdited]);
  const [tab, setTab] = useState<ReportTab>('inventory'); const [search, setSearch] = useState(''); const [stockState, setStockState] = useState('all');
  const [page, setPage] = useState(0); const [pageSize, setPageSize] = useState(20);
  const period = useMemo(() => ({ ...periodFrom(from, to), warehouseId }), [from, to, warehouseId]);
  useEffect(() => setPage(0), [warehouseId]);
  const inventory = useQuery({ queryKey: ['report-inventory', warehouseId, search, stockState, page, pageSize], queryFn: ({ signal }) => getInventoryReport(page + 1, pageSize, search.trim(), stockState, signal, warehouseId), enabled: tab === 'inventory' });
  const sales = useQuery({ queryKey: ['report-sales', period, page, pageSize], queryFn: ({ signal }) => getSalesReport(period, page + 1, pageSize, signal), enabled: tab === 'sales' });
  const purchases = useQuery({ queryKey: ['report-purchases', period, page, pageSize], queryFn: ({ signal }) => getPurchaseReport(period, page + 1, pageSize, signal), enabled: tab === 'purchases' });
  const movements = useQuery({ queryKey: ['report-movements', period, page, pageSize], queryFn: ({ signal }) => getMovementReport(period, page + 1, pageSize, signal), enabled: tab === 'movements' });
  const performance = useQuery({ queryKey: ['report-performance', period], queryFn: ({ signal }) => getProductPerformance(period, 50, signal), enabled: tab === 'performance' });
  const activeQuery = tab === 'inventory' ? inventory : tab === 'sales' ? sales : tab === 'purchases' ? purchases : tab === 'movements' ? movements : performance;
  const rows = tab === 'inventory' ? inventory.data?.items : tab === 'sales' ? sales.data?.items : tab === 'purchases' ? purchases.data?.items : movements.data?.items;
  const totalCount = tab === 'inventory' ? inventory.data?.totalCount ?? 0 : tab === 'sales' ? sales.data?.totalCount ?? 0 : tab === 'purchases' ? purchases.data?.totalCount ?? 0 : movements.data?.totalCount ?? 0;
  const exportKind = tab === 'movements' ? 'stock-movements' : tab === 'performance' ? 'product-performance' : tab;
  const exportFilters = tab === 'inventory' ? { warehouseId, search: search.trim() || undefined, stockState } : tab === 'performance' ? { ...period, take: 50 } : period;
  const tabTitle = { inventory: 'Định giá tồn kho', movements: 'Giao dịch kho trong kỳ', purchases: 'Hoạt động mua hàng', sales: 'Hoạt động bán hàng', performance: 'Hiệu suất sản phẩm' }[tab];
  const reportNote = tab === 'inventory'
    ? 'Tồn kho phản ánh số lượng hiện tại, không áp dụng khoảng ngày. Tệp xuất dùng từ khóa và tình trạng tồn đang lọc, bao gồm các trang kết quả. Thu hẹp bộ lọc nếu vượt giới hạn xuất.'
    : tab === 'movements' ? 'Lọc theo thời điểm giao dịch trong khoảng ngày UTC. Thời gian trong bảng hiển thị theo múi giờ trình duyệt.'
      : 'Số lượng và giá trị đặt tính các đơn tạo trong kỳ UTC, kể cả nháp hoặc đã hủy. Giao, nhận chỉ tính phiếu phát sinh trong kỳ, bao gồm đơn tạo từ trước kỳ.';
  function changeTab(next: ReportTab) { setTab(next); setPage(0); }
  return <>
    <PageHeading eyebrow="PHÂN TÍCH" title="Báo cáo" description="Theo dõi hàng hóa, giao dịch và hiệu quả mua bán theo từng kho."
      actions={<ReportExport report={exportKind} filters={exportFilters} />} />
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
      <Tabs value={tab} onChange={(_, value: ReportTab) => changeTab(value)} variant="scrollable" allowScrollButtonsMobile sx={{ px: { xs: 1, md: 1.5 }, borderBottom: 1, borderColor: 'divider', '& .MuiTab-root': { minHeight: 52, fontSize: 12, gap: .5 }, '& .MuiSvgIcon-root': { fontSize: 17 } }}>
        <Tab value="inventory" icon={<Inventory2Outlined />} iconPosition="start" label="Tồn kho" /><Tab value="movements" icon={<SwapVertRounded />} iconPosition="start" label="Biến động kho" /><Tab value="purchases" icon={<LocalShippingOutlined />} iconPosition="start" label="Mua hàng" /><Tab value="sales" icon={<ShoppingBagOutlined />} iconPosition="start" label="Bán hàng" /><Tab value="performance" icon={<InsightsOutlined />} iconPosition="start" label="Sản phẩm" />
      </Tabs>
      <FilterBar sx={{ alignItems: 'flex-start' }}>
        <Box sx={{ flex: '1 1 230px', maxWidth: { xs: 'none', md: 290 }, minWidth: 0 }}><WarehouseScopeSelect /></Box>
        {tab === 'inventory' ? <>
          <TextField label="Tìm SKU hoặc sản phẩm" value={search} onChange={(event) => { setSearch(event.target.value); setPage(0); }} />
          <TextField select label="Tình trạng tồn" value={stockState} onChange={(event) => { setStockState(event.target.value); setPage(0); }}><MenuItem value="all">Tất cả</MenuItem><MenuItem value="low">Tồn thấp</MenuItem><MenuItem value="out">Hết hàng</MenuItem></TextField>
        </> : <>
          <TextField label="Từ ngày (UTC)" type="date" value={from} onChange={(event) => { setDatesEdited(true); setFrom(event.target.value); setPage(0); }} slotProps={{ inputLabel: { shrink: true } }} sx={{ maxWidth: { md: '210px !important' } }} />
          <TextField label="Đến ngày (UTC)" type="date" value={to} onChange={(event) => { setDatesEdited(true); setTo(event.target.value); setPage(0); }} slotProps={{ inputLabel: { shrink: true } }} sx={{ maxWidth: { md: '210px !important' } }} />
        </>}
      </FilterBar>
      <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} spacing={1} sx={{ px: { xs: 2, md: 2.5 }, pb: 1.75 }}><WarehouseScopeNote compact /><Chip size="small" variant="outlined" label={tab === 'inventory' ? 'Tồn kho hiện tại' : 'Kỳ tối đa 366 ngày'} sx={{ fontSize: 10.5 }} /></Stack>
      <Divider />
      <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1} sx={{ px: { xs: 2, md: 2.5 }, py: 1.75 }}><Typography component="h2" sx={{ fontSize: 13, fontWeight: 700 }}>{tabTitle}</Typography><Typography variant="caption" color="text.secondary" aria-live="polite">{activeQuery.isLoading ? 'Đang tải…' : tab === 'performance' ? `${performance.data?.length ?? 0} sản phẩm · tối đa 50` : `${totalCount.toLocaleString('vi-VN')} bản ghi`}</Typography></Stack>
      {activeQuery.isFetching && <LinearProgress />}
      {activeQuery.isError && <Alert severity="error" sx={{ m: 2 }} action={<Button color="inherit" onClick={() => void activeQuery.refetch()}>Thử lại</Button>}>{errorMessage(activeQuery.error)}</Alert>}
      {activeQuery.isLoading && <Stack spacing={.75} sx={{ px: 2.5, py: 1.5 }}>{[1, 2, 3].map((item) => <Skeleton key={item} height={46} />)}</Stack>}
      {tab === 'performance' ? performance.data?.length ? <PerformanceTable rows={performance.data} /> : !performance.isFetching && !performance.isError && <EmptyState title="Chưa có dữ liệu hiệu suất" description="Sản phẩm có đơn tạo hoặc được giao trong kỳ sẽ hiển thị tại đây." />
        : rows?.length ? <ReportTable tab={tab} rows={rows} /> : !activeQuery.isLoading && !activeQuery.isError && <EmptyState title="Không có dữ liệu trong bộ lọc" description="Thử đổi khoảng thời gian, từ khóa hoặc trạng thái tồn." />}
      {tab !== 'performance' && rows?.length ? <TablePagination component="div" count={totalCount} page={page} rowsPerPage={pageSize} rowsPerPageOptions={[10, 20, 50]} onPageChange={(_, value) => setPage(value)} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(0); }} labelRowsPerPage="Số dòng:" labelDisplayedRows={({ from: first, to: last, count }) => `${first}–${last} / ${count}`} /> : null}
    </Paper>
    <Accordion disableGutters elevation={0} sx={{ mt: 1.25, bgcolor: 'transparent', border: '0 !important', boxShadow: 'none !important', '&:before': { display: 'none' } }}>
      <AccordionSummary expandIcon={<ExpandMoreRounded fontSize="small" />} sx={{ px: .25, minHeight: 40 }}><Stack direction="row" alignItems="center" spacing={.75}><InfoOutlined sx={{ fontSize: 15, color: 'text.secondary' }} /><Typography variant="caption" color="text.secondary">Cách đọc báo cáo và phạm vi dữ liệu</Typography></Stack></AccordionSummary>
      <AccordionDetails sx={{ px: .25, pt: 0 }}><Stack spacing={1}><Typography variant="caption" color="text.secondary">{reportNote}</Typography><Typography variant="caption" color="text.secondary">Giá trị hàng đã giao là tổng giá bán theo số lượng giao, chưa trừ giá vốn, chiết khấu và hoàn trả. Chỉ số này chưa phải lợi nhuận. Giá vốn hàng đã nhận bằng đơn giá nhập đã thỏa thuận nhân số lượng nhận.</Typography></Stack></AccordionDetails>
    </Accordion>
  </>;
}
function PerformanceTable({ rows }: { rows: ProductPerformanceRow[] }) {
  return <TableContainer><Table sx={{ minWidth: 720 }}><TableHead><TableRow><TableCell>SẢN PHẨM</TableCell><TableCell align="right">ĐẶT TRONG KỲ</TableCell><TableCell align="right">GIAO TRONG KỲ</TableCell><TableCell align="right">GIÁ TRỊ GIAO TRONG KỲ</TableCell></TableRow></TableHead><TableBody>{rows.map(row => <TableRow key={row.productId} hover><TableCell><Typography fontWeight={650}>{row.productName}</Typography><Typography variant="caption" color="text.secondary">{row.sku}</Typography></TableCell><TableCell align="right">{row.orderedQuantity}</TableCell><TableCell align="right">{row.fulfilledQuantity}</TableCell><TableCell align="right">{money(row.fulfilledSalesValue)}</TableCell></TableRow>)}</TableBody></Table></TableContainer>;
}
function ReportTable({ tab, rows }: { tab: ReportTab; rows: Array<InventoryReportRow | SalesReportRow | PurchaseReportRow | StockMovementReportRow> }) {
  const head = tab === 'inventory' ? ['SKU', 'Sản phẩm', 'Danh mục', 'Tồn / Min', 'Giá vốn', 'Giá bán', 'Định giá vốn', 'Định giá bán lẻ']
    : tab === 'sales' ? ['Mã đơn', 'Khách hàng', 'Kho', 'Trạng thái', 'Ngày tạo (địa phương)', 'Đặt / Giao trong kỳ', 'Giá trị đơn tạo trong kỳ', 'Giá trị giao trong kỳ']
      : tab === 'purchases' ? ['Mã đơn', 'Nhà cung cấp', 'Kho', 'Trạng thái', 'Ngày tạo (địa phương)', 'Đặt / Nhận trong kỳ', 'Giá trị đơn tạo trong kỳ', 'Giá vốn nhận trong kỳ']
        : ['Thời gian địa phương', 'Sản phẩm', 'Kho', 'Loại', 'Tồn tại kho', 'Tổng toàn hệ thống', 'Lý do', 'Tham chiếu'];
  return <TableContainer><Table sx={{ minWidth: 960 }}><TableHead><TableRow>{head.map(label => <TableCell key={label}>{label}</TableCell>)}</TableRow></TableHead><TableBody>{rows.map((row) => {
    if (tab === 'inventory') { const item = row as InventoryReportRow; return <TableRow key={item.productId} hover><TableCell>{item.sku}</TableCell><TableCell>{item.productName}</TableCell><TableCell>{item.categoryName}</TableCell><TableCell>{item.quantity} / {item.minimumStock}</TableCell><TableCell>{money(item.costPrice)}</TableCell><TableCell>{money(item.sellingPrice)}</TableCell><TableCell>{money(item.costValuation)}</TableCell><TableCell>{money(item.retailValuation)}</TableCell></TableRow>; }
    if (tab === 'sales') { const item = row as SalesReportRow; return <TableRow key={item.orderId} hover><TableCell>{item.code}</TableCell><TableCell>{item.customerName}</TableCell><TableCell>{item.warehouseCode || `#${item.warehouseId}`}</TableCell><TableCell>{item.status}</TableCell><TableCell>{dateTime(item.createdAt)}</TableCell><TableCell>{item.orderedQuantity} / {item.fulfilledQuantity}</TableCell><TableCell>{money(item.orderValue)}</TableCell><TableCell>{money(item.fulfilledValue)}</TableCell></TableRow>; }
    if (tab === 'purchases') { const item = row as PurchaseReportRow; return <TableRow key={item.orderId} hover><TableCell>{item.code}</TableCell><TableCell>{item.supplierName}</TableCell><TableCell>{item.warehouseCode || `#${item.warehouseId}`}</TableCell><TableCell>{item.status}</TableCell><TableCell>{dateTime(item.createdAt)}</TableCell><TableCell>{item.orderedQuantity} / {item.receivedQuantity}</TableCell><TableCell>{money(item.orderedCost)}</TableCell><TableCell>{money(item.receivedCost)}</TableCell></TableRow>; }
    const item = row as StockMovementReportRow; return <TableRow key={item.id} hover><TableCell>{dateTime(item.createdAt)}</TableCell><TableCell>{item.sku} · {item.productName}</TableCell><TableCell>{item.warehouseCode || `#${item.warehouseId}`}</TableCell><TableCell>{item.type}</TableCell><TableCell>{item.warehouseQuantityBefore ?? '—'} → {item.warehouseQuantityAfter ?? '—'}</TableCell><TableCell>{item.quantityBefore ?? '—'} → {item.quantityAfter ?? '—'}</TableCell><TableCell>{item.reason || '—'}</TableCell><TableCell>{item.referenceType ? `${item.referenceType} ${item.referenceId ?? ''}` : '—'}</TableCell></TableRow>;
  })}</TableBody></Table></TableContainer>;
}
