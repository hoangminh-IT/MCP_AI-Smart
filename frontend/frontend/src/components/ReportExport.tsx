import { useEffect, useId, useRef, useState } from 'react';
import axios from 'axios';
import { Alert, Box, Button, ButtonGroup, Divider, Menu, MenuItem, Stack, Typography } from '@mui/material';
import FileDownloadOutlined from '@mui/icons-material/FileDownloadOutlined';
import KeyboardArrowDownRounded from '@mui/icons-material/KeyboardArrowDownRounded';
import CheckRounded from '@mui/icons-material/CheckRounded';
import { errorMessage } from '../api';
import { useToast } from '../shared';
import { exportReportFile } from '../reports';
import type { ReportExportFilters, ReportExportFormat, ReportExportKind } from '../reports';

async function exportError(error: unknown): Promise<string> {
  if (axios.isAxiosError(error) && error.response?.data instanceof Blob) {
    try {
      const problem: unknown = JSON.parse(await error.response.data.text());
      if (problem && typeof problem === 'object') {
        const details = problem as { detail?: unknown; title?: unknown; errors?: unknown };
        if (details.errors && typeof details.errors === 'object') {
          const messages = Object.values(details.errors).flat().filter((value): value is string => typeof value === 'string');
          if (messages.length) return messages.join(' ');
        }
        if (typeof details.detail === 'string') return details.detail;
        if (typeof details.title === 'string') return details.title;
      }
    } catch { /* Preserve the normal HTTP/connectivity error below. */ }
  }
  return errorMessage(error);
}

export function ReportExport({ report, filters }: { report: ReportExportKind; filters: ReportExportFilters }) {
  const toast = useToast(); const [format, setFormat] = useState<ReportExportFormat>('csv'); const [exporting, setExporting] = useState(false); const [error, setError] = useState('');
  const [anchor, setAnchor] = useState<HTMLElement | null>(null); const menuId = useId();
  const controller = useRef<AbortController | null>(null); const filterKey = JSON.stringify(filters);
  useEffect(() => { setError(''); }, [report, format, filterKey]);
  useEffect(() => () => controller.current?.abort(), []);
  async function download() {
    if (exporting) return;
    const request = new AbortController(); controller.current = request; setExporting(true); setError('');
    const fileName = `smart-inventory-${report}.${format}`;
    try {
      const blob = await exportReportFile(report, format, { ...filters }, request.signal);
      if (request.signal.aborted) return;
      const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = fileName; anchor.click(); window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      toast(`Đã tải ${fileName} từ dữ liệu máy chủ.`);
    } catch (failure) { if (!axios.isCancel(failure)) setError(await exportError(failure)); }
    finally { controller.current = null; setExporting(false); }
  }
  return <Stack spacing={1} sx={{ alignItems: 'flex-start', maxWidth: '100%' }}>
    <ButtonGroup variant="contained" disableElevation disabled={exporting} aria-label="Xuất báo cáo">
      <Button startIcon={<FileDownloadOutlined sx={{ fontSize: 18 }} />} onClick={() => void download()} sx={{ whiteSpace: 'nowrap' }}>{exporting ? 'Đang xuất…' : `Xuất ${format.toUpperCase()}`}</Button>
      <Button title="Chọn định dạng tải xuống" aria-label="Định dạng xuất" aria-haspopup="listbox" aria-expanded={Boolean(anchor)} aria-controls={anchor ? menuId : undefined} onClick={(event) => setAnchor(event.currentTarget)} sx={{ px: 1, minWidth: '36px !important' }}><KeyboardArrowDownRounded fontSize="small" /></Button>
    </ButtonGroup>
    <Menu id={menuId} anchorEl={anchor} open={Boolean(anchor)} onClose={() => setAnchor(null)} MenuListProps={{ role: 'listbox', 'aria-label': 'Định dạng xuất' }} slotProps={{ paper: { sx: { minWidth: 230, mt: .75 } } }}>
      {([{ value: 'csv', label: 'CSV' }, { value: 'xlsx', label: 'Excel (XLSX)' }, { value: 'pdf', label: 'PDF' }] as const).map((item) => <MenuItem key={item.value} role="option" aria-label={item.label} aria-selected={format === item.value} selected={format === item.value} onClick={() => { setFormat(item.value); setAnchor(null); }} sx={{ gap: 2, justifyContent: 'space-between' }}>{item.label}{format === item.value && <CheckRounded sx={{ fontSize: 17, color: 'primary.main' }} />}</MenuItem>)}
      <Divider />
      <Box component="li" role="presentation" sx={{ px: 2, py: 1, maxWidth: 270 }}><Typography component="p" variant="caption" color="text.secondary">Tải theo bộ lọc hiện tại.<br />CSV, Excel: tối đa 10.000 dòng.<br />PDF: tối đa 1.000 dòng.</Typography></Box>
    </Menu>
    {error && <Alert severity="error" onClose={() => setError('')} sx={{ maxWidth: 360, overflowWrap: 'anywhere' }}>{error}</Alert>}
  </Stack>;
}
