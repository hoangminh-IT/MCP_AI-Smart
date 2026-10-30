import { aiProviderHint } from '../mcpPresentation';
import { useState } from 'react';
import type { FormEvent, ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Chip, Divider, LinearProgress, MenuItem, Paper, Skeleton, Stack, TextField, Typography } from '@mui/material';
import SaveOutlined from '@mui/icons-material/SaveOutlined';
import RefreshOutlined from '@mui/icons-material/RefreshOutlined';
import BusinessOutlined from '@mui/icons-material/BusinessOutlined';
import PaletteOutlined from '@mui/icons-material/PaletteOutlined';
import TuneOutlined from '@mui/icons-material/TuneOutlined';
import HubOutlined from '@mui/icons-material/HubOutlined';
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined';
import ArrowForwardRounded from '@mui/icons-material/ArrowForwardRounded';
import { Link } from 'react-router-dom';
import { dateTime, errorMessage } from '../api';
import { useAuth } from '../auth';
import { useMcpStatus } from '../mcp';
import { PageHeading, useToast } from '../shared';
import { getAiProviderStatus, saveApplicationSettings, saveUserPreferences, useApplicationSettings, useUserPreferences } from '../settings';
import type { ApplicationSettings, UserPreferences } from '../settings';
import { AppearanceSelect } from '../appearance';

function SettingsHeading({ icon, title, description }: { icon: ReactNode; title: string; description: string }) {
  return <Stack direction="row" spacing={1.25} alignItems="flex-start" sx={{ mb: 2.5 }}><Box sx={{ display: 'grid', placeItems: 'center', width: 34, height: 34, flexShrink: 0, bgcolor: 'action.hover', color: 'primary.main', borderRadius: 2, '& .MuiSvgIcon-root': { fontSize: 19 } }}>{icon}</Box><Box sx={{ minWidth: 0 }}><Typography component="h2" sx={{ fontSize: 14, fontWeight: 700 }}>{title}</Typography><Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: .3 }}>{description}</Typography></Box></Stack>;
}

export default function Settings() {
  const admin = useAuth().user?.role === 'Admin';
  const settings = useApplicationSettings(); const preferences = useUserPreferences();
  return <>
    <PageHeading eyebrow="HỆ THỐNG" title="Cài đặt" description="Thông tin đơn vị, giá trị mặc định và tùy chọn riêng của tài khoản." />
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 1.35fr) minmax(0, .9fr)' }, gap: 2, alignItems: 'start' }}>
      <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, minWidth: 0 }}>
        <SettingsHeading icon={<BusinessOutlined />} title="Thông tin đơn vị và mặc định" description="Thông tin chung và giá trị áp dụng khi tạo dữ liệu mới." />
        {!admin && <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 2 }}>Bạn có quyền xem. Chỉ quản trị viên được thay đổi cài đặt chung.</Typography>}
        {settings.isError && <Alert severity="error" sx={{ mb: 2 }} action={<Button color="inherit" onClick={() => void settings.refetch()}>Tải lại cài đặt</Button>}>{errorMessage(settings.error)}</Alert>}
        {settings.isLoading ? <Stack spacing={2}><Skeleton height={48} /><Skeleton height={48} /><Skeleton height={48} /></Stack> : settings.data && <CompanySettingsForm initial={settings.data} admin={admin} reload={async () => { const result = await settings.refetch(); return result.isSuccess ? result.data : undefined; }} />}
      </Paper>
      <Stack spacing={2} sx={{ minWidth: 0 }}>
      <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 } }}><SettingsHeading icon={<PaletteOutlined />} title="Giao diện" description="Chọn cách hiển thị phù hợp với bạn." /><Box sx={{ '& .MuiTextField-root': { width: '100%' } }}><AppearanceSelect /></Box><Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.25 }}>Lưu riêng trên trình duyệt này, áp dụng cả trang đăng nhập.</Typography></Paper>
      <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 } }}>
        <SettingsHeading icon={<TuneOutlined />} title="Tùy chọn của tôi" description="Số dòng mặc định khi mở các danh sách nghiệp vụ." />
        {preferences.isError && <Alert severity="error" action={<Button color="inherit" onClick={() => void preferences.refetch()}>Tải lại tùy chọn</Button>}>{errorMessage(preferences.error)}</Alert>}
        {preferences.isLoading ? <Skeleton height={48} /> : preferences.data && <PreferencesForm initial={preferences.data} reload={async () => { const result = await preferences.refetch(); return result.isSuccess ? result.data : undefined; }} />}
      </Paper>
      </Stack>
      <Box sx={{ gridColumn: '1 / -1', minWidth: 0 }}><RuntimeStatus /></Box>
    </Box>
  </>;
}

function CompanySettingsForm({ initial, admin, reload }: { initial: ApplicationSettings; admin: boolean; reload: () => Promise<ApplicationSettings | undefined> }) {
  const cache = useQueryClient(); const toast = useToast(); const [form, setForm] = useState(initial); const [validation, setValidation] = useState(''); const [reloading, setReloading] = useState(false);
  const save = useMutation({ mutationFn: () => saveApplicationSettings({ companyName: form.companyName.trim(), contactEmail: form.contactEmail?.trim() || null, phone: form.phone?.trim() || null, address: form.address?.trim() || null, defaultMinStock: form.defaultMinStock, reportDefaultDays: form.reportDefaultDays, revision: form.revision }),
    onSuccess: (updated) => { setForm(updated); cache.setQueryData(['application-settings'], updated); toast('Đã lưu cài đặt đơn vị.'); },
  });
  function field<K extends keyof ApplicationSettings>(key: K, value: ApplicationSettings[K]) { setForm((current) => ({ ...current, [key]: value })); }
  function submit(event: FormEvent) {
    event.preventDefault(); setValidation('');
    if (!admin) return;
    if (!form.companyName.trim()) { setValidation('Nhập tên đơn vị.'); return; }
    if (!Number.isInteger(form.defaultMinStock) || form.defaultMinStock < 0 || form.defaultMinStock > 2_147_483_647) { setValidation('Tồn tối thiểu mặc định phải là số nguyên từ 0 đến 2.147.483.647.'); return; }
    if (!Number.isInteger(form.reportDefaultDays) || form.reportDefaultDays < 1 || form.reportDefaultDays > 366) { setValidation('Kỳ báo cáo mặc định phải từ 1 đến 366 ngày.'); return; }
    save.mutate();
  }
  async function loadLatest() {
    setReloading(true); const latest = await reload(); if (latest) { setForm(latest); save.reset(); setValidation(''); } setReloading(false);
  }
  const locked = !admin || save.isPending || reloading;
  return <Box component="form" onSubmit={submit}><Stack spacing={2}>
    {(validation || save.isError) && <Alert severity="error">{validation || errorMessage(save.error)}{save.isError && ' Nếu bản dữ liệu đã thay đổi, tải bản mới từ máy chủ trước khi lưu lại.'}</Alert>}
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}>
      <TextField label="Tên đơn vị" value={form.companyName} required disabled={save.isPending || reloading} onChange={(event) => field('companyName', event.target.value)} slotProps={{ input: { readOnly: !admin }, htmlInput: { maxLength: 200 } }} />
      <TextField label="Email liên hệ" type="email" value={form.contactEmail ?? ''} disabled={save.isPending || reloading} onChange={(event) => field('contactEmail', event.target.value)} slotProps={{ input: { readOnly: !admin }, htmlInput: { maxLength: 254 } }} />
      <TextField label="Điện thoại đơn vị" value={form.phone ?? ''} disabled={save.isPending || reloading} onChange={(event) => field('phone', event.target.value)} slotProps={{ input: { readOnly: !admin }, htmlInput: { maxLength: 40 } }} />
      <TextField label="Tồn tối thiểu mặc định" type="number" value={form.defaultMinStock} required disabled={save.isPending || reloading} onChange={(event) => field('defaultMinStock', Number(event.target.value))} slotProps={{ input: { readOnly: !admin }, htmlInput: { min: 0, max: 2_147_483_647, step: 1 } }} />
      <TextField label="Kỳ báo cáo mặc định (ngày)" type="number" value={form.reportDefaultDays} required disabled={save.isPending || reloading} onChange={(event) => field('reportDefaultDays', Number(event.target.value))} slotProps={{ input: { readOnly: !admin }, htmlInput: { min: 1, max: 366, step: 1 } }} />
    </Box>
    <TextField label="Địa chỉ đơn vị" value={form.address ?? ''} multiline minRows={2} disabled={save.isPending || reloading} onChange={(event) => field('address', event.target.value)} slotProps={{ input: { readOnly: !admin }, htmlInput: { maxLength: 1000 } }} />
    <Typography variant="caption" color="text.secondary">Tồn tối thiểu áp dụng cho sản phẩm mới. Kỳ báo cáo khởi tạo bộ lọc ngày; dữ liệu đã lưu giữ nguyên.</Typography>
    <Divider />
    <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap><Chip label={`Phiên bản ${form.revision}`} size="small" variant="outlined" /><Typography variant="caption" color="text.secondary">{form.updatedAt ? `Cập nhật ${dateTime(form.updatedAt)}` : ''}</Typography></Stack>
    <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap><Button variant="outlined" disabled={save.isPending || reloading} startIcon={<RefreshOutlined sx={{ fontSize: 17 }} />} onClick={() => void loadLatest()}>Tải bản cài đặt mới</Button>{admin && <Button type="submit" variant="contained" startIcon={<SaveOutlined sx={{ fontSize: 17 }} />} disabled={locked}>{save.isPending ? 'Đang lưu…' : 'Lưu cài đặt đơn vị'}</Button>}</Stack>
    <Typography variant="caption" color="text.secondary">Tải bản mới sẽ thay thế các thay đổi chưa lưu.</Typography>
  </Stack></Box>;
}

function PreferencesForm({ initial, reload }: { initial: UserPreferences; reload: () => Promise<UserPreferences | undefined> }) {
  const cache = useQueryClient(); const toast = useToast(); const [form, setForm] = useState(initial); const [reloading, setReloading] = useState(false);
  const save = useMutation({ mutationFn: () => saveUserPreferences({ defaultPageSize: form.defaultPageSize, revision: form.revision }), onSuccess: (updated) => { setForm(updated); cache.setQueryData(['user-preferences'], updated); toast('Đã lưu tùy chọn của bạn.'); } });
  async function loadLatest() { setReloading(true); const latest = await reload(); if (latest) { setForm(latest); save.reset(); } setReloading(false); }
  return <Box component="form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}><Stack spacing={2}>
    {save.isError && <Alert severity="error">{errorMessage(save.error)} Tải bản tùy chọn mới nếu phiên bản đã thay đổi.</Alert>}
    <TextField select fullWidth label="Số dòng mặc định" value={form.defaultPageSize} disabled={save.isPending || reloading} onChange={(event) => setForm((current) => ({ ...current, defaultPageSize: Number(event.target.value) as UserPreferences['defaultPageSize'] }))}>{[10, 25, 50].map((size) => <MenuItem key={size} value={size}>{size} dòng</MenuItem>)}</TextField>
    <Stack direction="row" alignItems="center" justifyContent="space-between" spacing={1}><Chip label={`Phiên bản ${form.revision}`} size="small" variant="outlined" /><Typography variant="caption" color="text.secondary">Áp dụng khi mở danh sách.</Typography></Stack>
    <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap><Button variant="outlined" disabled={save.isPending || reloading} onClick={() => void loadLatest()}>Tải bản tùy chọn mới</Button><Button type="submit" variant="contained" disabled={save.isPending || reloading}>{save.isPending ? 'Đang lưu…' : 'Lưu tùy chọn của tôi'}</Button></Stack>
    <Typography variant="caption" color="text.secondary">Tải bản mới sẽ thay thế các thay đổi chưa lưu.</Typography>
  </Stack></Box>;
}

function RuntimeStatus() {
  const mcp = useMcpStatus(); const ai = useQuery({ queryKey: ['ai-status'], queryFn: ({ signal }) => getAiProviderStatus(signal) });
  return <Box>
    <Typography component="h2" sx={{ fontSize: 14, fontWeight: 700, mb: 1.5, mt: .5 }}>Trạng thái MCP và AI</Typography>
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
      <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}><SettingsHeading icon={<HubOutlined />} title="MCP Client C#" description="Kết nối tới máy chủ công cụ." /><Chip size="small" variant="outlined" color={!mcp.isError && mcp.data?.connected ? 'success' : 'default'} label={mcp.isLoading ? 'Đang kiểm tra…' : mcp.isError ? 'Không xác định' : mcp.data?.connected ? 'Đã kết nối' : 'Chưa kết nối'} /></Stack>
        {mcp.isFetching && <LinearProgress sx={{ mb: 1.5 }} />}
        {mcp.isError && <Alert severity="error" sx={{ mb: 1.5 }}>{errorMessage(mcp.error)}</Alert>}{!mcp.isError && mcp.data?.errorMessage && <Alert severity="warning" sx={{ mb: 1.5 }}>{mcp.data.errorMessage}</Alert>}
        {mcp.data && <Stack spacing={.75} sx={{ mb: 2, overflowWrap: 'anywhere' }}><Typography variant="body2" sx={{ fontSize: 12.5 }}>{mcp.data.serverName || 'Chưa có tên máy chủ'}{mcp.data.serverVersion ? ` · ${mcp.data.serverVersion}` : ''}</Typography><Typography variant="caption" color="text.secondary">{mcp.data.transport} · {mcp.data.serverUrl}</Typography><Typography variant="caption" color="text.secondary">Kiểm tra {dateTime(mcp.data.checkedAt)}</Typography></Stack>}
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mt: 'auto', pt: .5 }}><Button variant="outlined" disabled={mcp.isFetching} startIcon={<RefreshOutlined sx={{ fontSize: 17 }} />} onClick={() => void mcp.refetch()}>Kiểm tra MCP</Button><Button component={Link} to="/mcp/lab" endIcon={<ArrowForwardRounded sx={{ fontSize: 16 }} />}>Mở MCP Client Lab</Button></Stack>
      </Paper>
      <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}><SettingsHeading icon={<SmartToyOutlined />} title="AI Provider" description="Nhà cung cấp cho trợ lý AI." /><Chip size="small" variant="outlined" color={!ai.isError && ai.data?.configured ? 'success' : 'default'} label={ai.isLoading ? 'Đang kiểm tra…' : ai.isError ? 'Không xác định' : ai.data?.configured ? 'Đã cấu hình' : 'Chưa cấu hình'} /></Stack>
        {ai.isFetching && <LinearProgress sx={{ mb: 1.5 }} />}
        {ai.isError && <Alert severity="error" sx={{ mb: 1.5 }}>{errorMessage(ai.error)}</Alert>}
        {ai.data && <Stack spacing={.75} sx={{ mb: 2, overflowWrap: 'anywhere' }}><Typography variant="body2" sx={{ fontSize: 12.5 }}>{ai.data.provider} · {ai.data.model}</Typography>{ai.data.reason && <Typography variant="caption" color="text.secondary">{aiProviderHint(ai.data.reason)}</Typography>}<Typography variant="caption" color="text.secondary">Thông tin xác thực được quản lý riêng trên máy chủ.</Typography></Stack>}
        <Button variant="outlined" disabled={ai.isFetching} startIcon={<RefreshOutlined sx={{ fontSize: 17 }} />} onClick={() => void ai.refetch()} sx={{ mt: 'auto', alignSelf: 'flex-start' }}>Kiểm tra AI</Button>
      </Paper>
    </Box>
  </Box>;
}
