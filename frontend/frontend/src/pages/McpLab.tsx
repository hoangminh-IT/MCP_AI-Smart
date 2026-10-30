import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Accordion, AccordionDetails, AccordionSummary, Alert, Box, Button, Dialog, DialogContent, DialogTitle, Divider, IconButton, InputAdornment, LinearProgress, Paper, Stack, Tab, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Tabs, TextField, Typography } from '@mui/material';
import CloseOutlined from '@mui/icons-material/CloseOutlined';
import CodeOutlined from '@mui/icons-material/CodeOutlined';
import RefreshOutlined from '@mui/icons-material/RefreshOutlined';
import ArrowForwardOutlined from '@mui/icons-material/ArrowForwardOutlined';
import ExpandMoreOutlined from '@mui/icons-material/ExpandMoreOutlined';
import SearchOutlined from '@mui/icons-material/SearchOutlined';
import HubOutlined from '@mui/icons-material/HubOutlined';
import { dateTime, errorMessage } from '../api';
import { EmptyState, PageHeading } from '../shared';
import { isWriteTool, JsonPanel, ToolPolicy, useMcpStatus, useMcpTools } from '../mcp';
import type { McpTool } from '../mcp';
import { toolPresentation } from '../mcpPresentation';
import { McpResourcesPrompts } from '../components/McpResourcesPrompts';

const sdkExample = `using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// ASP.NET Core supplies the configured URI and validated caller identity.
// callerToken comes from the authenticated request, never a UI settings field.
static async Task<CallToolResult> ReadSummaryAsync(
    Uri configuredServerUri,
    string callerToken,
    CancellationToken cancellationToken)
{
    using var deadline = CancellationTokenSource
        .CreateLinkedTokenSource(cancellationToken);
    deadline.CancelAfter(TimeSpan.FromSeconds(30));

    await using var transport = new HttpClientTransport(
        new HttpClientTransportOptions
        {
            Endpoint = configuredServerUri,
            TransportMode = HttpTransportMode.StreamableHttp,
            EnableStandaloneGetStream = false,
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + callerToken
            }
        });

    await using var client = await McpClient.CreateAsync(
        transport,
        new McpClientOptions
        {
            InitializationTimeout = TimeSpan.FromSeconds(30)
        },
        cancellationToken: deadline.Token);

    var tools = await client.ListToolsAsync(
        cancellationToken: deadline.Token);

    var result = await client.CallToolAsync(
        "GetInventorySummary",
        new Dictionary<string, object?>(),
        cancellationToken: deadline.Token);

    return result; // Inspect IsError, Content and StructuredContent.
}
// Official C# SDK 2.3.0. React calls the API; this C# client owns MCP.`;

export default function McpLab() {
  const status = useMcpStatus();
  const tools = useMcpTools(false);
  const [schema, setSchema] = useState<McpTool | null>(null);
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState<'all' | 'read' | 'write'>('all');
  const visibleTools = tools.data?.filter((tool) => {
    const display = toolPresentation(tool);
    const matchesSearch = `${tool.name} ${tool.description ?? ''} ${display.title} ${display.description}`.toLocaleLowerCase().includes(search.trim().toLocaleLowerCase());
    return matchesSearch && (category === 'all' || (category === 'write' ? isWriteTool(tool) : !isWriteTool(tool)));
  });
  const connected = status.isError ? undefined : status.data?.connected;
  const statusLabel = status.isLoading ? 'Đang kiểm tra…' : status.isError ? 'Chưa xác định' : connected ? 'Đã kết nối' : 'Chưa kết nối';
  const writes = tools.data?.filter(isWriteTool).length ?? 0;
  return <>
    <PageHeading eyebrow="AI & MCP" title="MCP Client Lab" description="Kết nối, khám phá và thử công cụ dữ liệu kho." actions={<Button variant="outlined" startIcon={<RefreshOutlined />} disabled={status.isFetching} onClick={() => void status.refetch()}>Kiểm tra kết nối</Button>} />
    <Paper variant="outlined" sx={{ mb: 2.5, overflow: 'hidden' }}>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2.5} alignItems={{ xs: 'stretch', md: 'center' }} justifyContent="space-between" sx={{ p: 2.5 }}>
        <Stack direction="row" spacing={1.5} alignItems="center">
          <Box sx={{ width: 44, height: 44, display: 'grid', placeItems: 'center', bgcolor: 'action.selected', color: 'primary.main', borderRadius: 2.5 }}><HubOutlined /></Box>
          <Box><Typography fontWeight={750} fontSize={14}>MCP Client C#</Typography><Stack direction="row" spacing={.8} alignItems="center" sx={{ mt: .5 }}><Box component="span" sx={{ width: 7, height: 7, borderRadius: '50%', bgcolor: connected ? 'success.main' : 'text.disabled' }} /><Typography fontSize={12} color="text.secondary">{statusLabel}</Typography><Typography fontSize={12} color="text.disabled">·</Typography><Typography fontSize={12} color="text.secondary">{status.data?.transport || '—'}</Typography></Stack></Box>
        </Stack>
        <Stack direction="row" spacing={3} alignItems="center" flexWrap="wrap" useFlexGap>
          <Box><Typography fontWeight={750} fontSize={20}>{tools.data?.length ?? '—'}<Typography component="span" fontSize={12} color="text.secondary" sx={{ ml: .8 }}>công cụ</Typography></Typography><Typography fontSize={11} color="text.secondary">{tools.data ? `${tools.data.length - writes} chỉ xem · ${writes} thay đổi dữ liệu` : 'Khám phá từ máy chủ'}</Typography></Box>
          <Button startIcon={<CodeOutlined />} variant="contained" disabled={tools.isFetching} onClick={() => void tools.refetch()}>{tools.isFetching ? 'Đang ListTools…' : 'ListTools'}</Button>
        </Stack>
      </Stack>
      <Divider />
      <Accordion disableGutters elevation={0} sx={{ bgcolor: 'transparent', '&::before': { display: 'none' } }}>
        <AccordionSummary expandIcon={<ExpandMoreOutlined />} sx={{ px: 2.5, minHeight: 44 }}><Typography fontSize={12} color="text.secondary">Thông tin kết nối <Typography component="span" fontSize={11} sx={{ ml: 1.5, display: { xs: 'none', sm: 'inline' } }}>{status.data?.checkedAt ? `Cập nhật ${dateTime(status.data.checkedAt)}` : ''}</Typography></Typography></AccordionSummary>
        <AccordionDetails sx={{ px: 2.5, pb: 2.5 }}><Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '2fr 1fr 1fr' }, gap: 2 }}><TextField label="MCP Server URL" value={status.data?.serverUrl ?? ''} slotProps={{ input: { readOnly: true } }} /><TextField label="Server name" value={status.data?.serverName ?? 'Chưa có thông tin'} slotProps={{ input: { readOnly: true } }} /><TextField label="Server version" value={status.data?.serverVersion ?? 'Chưa có thông tin'} slotProps={{ input: { readOnly: true } }} /></Box><Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>Official C# SDK · Kết nối được cấu hình tại máy chủ và kiểm tra lại mỗi 10 giây.</Typography></AccordionDetails>
      </Accordion>
    </Paper>
    {status.isError && <Alert severity="error" sx={{ mb: 2.5 }}>{errorMessage(status.error)}</Alert>}{status.data?.errorMessage && <Alert severity="warning" sx={{ mb: 2.5 }}>{status.data.errorMessage}</Alert>}
    <Paper variant="outlined" sx={{ overflow: 'hidden', mb: 2.5 }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} sx={{ px: 2.5, pt: 2.5, pb: 2 }}><Box><Typography fontWeight={750}>Thư viện công cụ</Typography><Typography fontSize={12} color="text.secondary" sx={{ mt: .5 }}>Chọn công cụ phù hợp rồi mở Playground để thử.</Typography></Box><Button component={Link} to="/mcp/playground" endIcon={<ArrowForwardOutlined />}>Mở Playground</Button></Stack>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ xs: 'stretch', md: 'center' }} justifyContent="space-between" sx={{ px: 2.5, pb: 2 }}>
        <Tabs value={category} onChange={(_, value: 'all' | 'read' | 'write') => setCategory(value)} aria-label="Loại công cụ" sx={{ minHeight: 38, '& .MuiTab-root': { minHeight: 38, px: 1.5 } }}><Tab value="all" label="Tất cả" /><Tab value="read" label="Tra cứu" /><Tab value="write" label="Thay đổi dữ liệu" /></Tabs>
        <TextField label="Tìm MCP tool" placeholder="Tên hoặc chức năng…" value={search} onChange={(event) => setSearch(event.target.value)} slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined fontSize="small" /></InputAdornment> } }} sx={{ width: { xs: '100%', md: 280 } }} />
      </Stack>
      {category === 'write' && <Box sx={{ px: 2.5, pb: 2 }}><Typography fontSize={12} color="text.secondary">Các công cụ này cần bạn xem lại và xác nhận đề nghị trước khi thực thi.</Typography></Box>}
      {tools.isFetching && <LinearProgress />}
      {tools.isError ? <Alert severity="error" sx={{ m: 2.5 }}>{errorMessage(tools.error)}</Alert> : !tools.data?.length ? <EmptyState title={tools.data ? 'Server chưa đăng ký tool' : 'Sẵn sàng khám phá tools'} description="Chọn ListTools để lấy danh sách công cụ và tham số từ MCP Server." /> : !visibleTools?.length ? <EmptyState title="Không có tool khớp bộ lọc" description="Thử từ khóa hoặc nhóm công cụ khác." /> : <TableContainer><Table sx={{ minWidth: 760 }}><TableHead><TableRow><TableCell sx={{ width: '29%' }}>Công cụ</TableCell><TableCell>Chức năng</TableCell><TableCell sx={{ width: 170 }}>Loại thao tác</TableCell><TableCell align="right" sx={{ width: 190 }}>Thao tác</TableCell></TableRow></TableHead><TableBody>{visibleTools.map((tool) => {
        const display = toolPresentation(tool);
        return <TableRow hover key={tool.name}><TableCell aria-label={tool.name}><Typography fontSize={13} fontWeight={650}>{display.title}</Typography><Typography fontSize={11} color="text.secondary" sx={{ mt: .5, fontFamily: 'ui-monospace, monospace' }}>{tool.name}</Typography></TableCell><TableCell sx={{ maxWidth: 390, color: 'text.secondary', fontSize: 12, lineHeight: 1.6 }}>{display.description}</TableCell><TableCell><ToolPolicy tool={tool} /></TableCell><TableCell align="right" sx={{ whiteSpace: 'nowrap' }}><Button size="small" onClick={() => setSchema(tool)}>Schema</Button><Button size="small" component={Link} to={`/mcp/playground?tool=${encodeURIComponent(tool.name)}`}>Playground</Button></TableCell></TableRow>;
      })}</TableBody></Table></TableContainer>}
      {tools.data && <Box sx={{ px: 2.5, py: 1.5, borderTop: 1, borderColor: 'divider' }}><Typography fontSize={11} color="text.secondary">Hiển thị {visibleTools?.length ?? 0} / {tools.data.length} công cụ</Typography></Box>}
    </Paper>
    <Accordion disableGutters elevation={0} sx={{ mb: 2, border: 1, borderColor: 'divider', borderRadius: '12px !important', '&::before': { display: 'none' } }}><AccordionSummary expandIcon={<ExpandMoreOutlined />}><Box><Typography fontSize={13} fontWeight={650}>Tài liệu & mẫu hội thoại</Typography><Typography fontSize={12} color="text.secondary" sx={{ mt: .5 }}>Khám phá MCP Resources và Prompts</Typography></Box></AccordionSummary><AccordionDetails><McpResourcesPrompts /></AccordionDetails></Accordion>
    <Accordion disableGutters elevation={0} sx={{ border: 1, borderColor: 'divider', borderRadius: '12px !important', '&::before': { display: 'none' } }}><AccordionSummary expandIcon={<ExpandMoreOutlined />}><Box><Typography fontSize={13} fontWeight={650}>Ví dụ MCP Client bằng C#</Typography><Typography fontSize={12} color="text.secondary" sx={{ mt: .5 }}>ListToolsAsync · CallToolAsync</Typography></Box></AccordionSummary><AccordionDetails><JsonPanel title="C# CLIENT · LISTTOOLSASYNC / CALLTOOLASYNC" value={sdkExample} code /></AccordionDetails></Accordion>
    <Dialog open={Boolean(schema)} onClose={() => setSchema(null)} fullWidth maxWidth="md"><DialogTitle><Stack direction="row" justifyContent="space-between" alignItems="center"><Box><Typography fontWeight={750}>{schema && toolPresentation(schema).title}</Typography><Typography component="span" fontSize={12} color="text.secondary" fontFamily="ui-monospace, monospace">{schema?.name}</Typography></Box><IconButton aria-label="Đóng schema" onClick={() => setSchema(null)}><CloseOutlined /></IconButton></Stack></DialogTitle><Divider /><DialogContent><Stack spacing={2}><Typography fontSize={13} color="text.secondary">{schema && toolPresentation(schema).description}</Typography>{schema && <ToolPolicy tool={schema} />}<Typography variant="caption" color="text.secondary">Mô tả gốc từ MCP Server</Typography><Typography fontSize={12} color="text.secondary" sx={{ lineHeight: 1.7 }}>{schema?.description}</Typography><JsonPanel title="INPUT SCHEMA" value={schema?.inputSchema} />{schema?.annotations && <JsonPanel title="TOOL ANNOTATIONS" value={schema.annotations} />}</Stack></DialogContent></Dialog>
  </>;
}
