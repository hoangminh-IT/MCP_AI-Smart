import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Box, Button, Chip, Dialog, DialogContent, DialogTitle, Divider, IconButton, LinearProgress, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material';
import CloseOutlined from '@mui/icons-material/CloseOutlined';
import CodeOutlined from '@mui/icons-material/CodeOutlined';
import RefreshOutlined from '@mui/icons-material/RefreshOutlined';
import ArrowForwardOutlined from '@mui/icons-material/ArrowForwardOutlined';
import { dateTime, errorMessage } from '../api';
import { EmptyState, PageHeading } from '../shared';
import { JsonPanel, ToolPolicy, useMcpStatus, useMcpTools } from '../mcp';
import type { McpTool } from '../mcp';

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
  const connected = status.isError ? undefined : status.data?.connected;
  const statusLabel = status.isLoading ? 'Đang kiểm tra…' : status.isError ? 'Không xác định' : connected ? 'Connected' : 'Disconnected';
  return <><PageHeading eyebrow="AI & MCP" title="MCP Client Lab" description="Quan sát kết nối và tool discovery của MCP Client viết bằng C#." actions={<Button variant="outlined" startIcon={<RefreshOutlined />} disabled={status.isFetching} onClick={() => void status.refetch()}>Kiểm tra kết nối</Button>} /><Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2.5, mb: 3 }}>{[
    { label: 'MCP SERVER STATUS', value: statusLabel, detail: status.data?.checkedAt ? `Kiểm tra ${dateTime(status.data.checkedAt)}` : 'Đang lấy dữ liệu thực tế từ API', color: connected ? '#0D9473' : '#78869B' },
    { label: 'TOOLS DISCOVERED', value: tools.data ? String(tools.data.length) : '—', detail: tools.data ? 'Kết quả ListTools từ MCP Server' : 'Chọn ListTools để khám phá', color: '#3468F6' },
    { label: 'TRANSPORT', value: status.data?.transport || '—', detail: 'C# Client → C# Server', color: '#18243B' },
  ].map((item) => <Paper variant="outlined" key={item.label} sx={{ p: 3 }}><Typography fontSize={10} fontWeight={750} letterSpacing={1.1} color="text.secondary">{item.label}</Typography><Typography sx={{ fontSize: { xs: 26, lg: 29 }, fontWeight: 800, color: item.color, mt: 1.5, overflowWrap: 'anywhere' }}>{item.value}</Typography><Typography fontSize={11} color="text.secondary" sx={{ mt: 1 }}>{item.detail}</Typography></Paper>)}</Box>
    {status.isError && <Alert severity="error" sx={{ mb: 3 }}>{errorMessage(status.error)}</Alert>}{status.data?.errorMessage && <Alert severity="warning" sx={{ mb: 3 }}>{status.data.errorMessage}</Alert>}
    <Paper variant="outlined" sx={{ p: 3, mb: 3 }}><Stack spacing={2.5}><Box><Typography variant="h6">Connection settings</Typography><Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>Endpoint được cấu hình ở backend. Trình duyệt không thay đổi địa chỉ MCP Server.</Typography></Box><Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '2fr 1fr 1fr' }, gap: 2 }}><TextField label="MCP Server URL" value={status.data?.serverUrl ?? ''} slotProps={{ input: { readOnly: true } }} /><TextField label="Server name" value={status.data?.serverName ?? 'Chưa có thông tin'} slotProps={{ input: { readOnly: true } }} /><TextField label="Server version" value={status.data?.serverVersion ?? 'Chưa có thông tin'} slotProps={{ input: { readOnly: true } }} /></Box><Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={2}><Stack direction="row" spacing={1} alignItems="center"><Chip size="small" variant="outlined" label="Official C# SDK" /><Typography variant="caption" color="text.secondary">Status cập nhật mỗi 10 giây khi màn hình đang mở.</Typography></Stack><Button startIcon={<CodeOutlined />} variant="contained" disabled={tools.isFetching} onClick={() => void tools.refetch()}>{tools.isFetching ? 'Đang ListTools…' : 'ListTools'}</Button></Stack></Stack></Paper>
    <Paper variant="outlined" sx={{ overflow: 'hidden', mb: 3 }}><Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ px: 3, py: 2.5 }}><Typography fontWeight={750}>Tools từ MCP Server</Typography><Button component={Link} to="/mcp/playground" endIcon={<ArrowForwardOutlined />}>Mở Playground</Button></Stack>{tools.isFetching && <LinearProgress />}{tools.isError ? <Alert severity="error" sx={{ m: 3 }}>{errorMessage(tools.error)}</Alert> : !tools.data?.length ? <EmptyState title={tools.data ? 'Server chưa đăng ký tool' : 'Sẵn sàng khám phá tools'} description="ListTools gửi yêu cầu qua MCP Client C# để lấy tên, mô tả và JSON Schema từ server thật." /> : <TableContainer><Table sx={{ minWidth: 780 }}><TableHead><TableRow><TableCell>TOOL NAME</TableCell><TableCell>DESCRIPTION</TableCell><TableCell>POLICY</TableCell><TableCell align="right">SCHEMA / EXECUTE</TableCell></TableRow></TableHead><TableBody>{tools.data.map((tool) => <TableRow hover key={tool.name}><TableCell sx={{ fontSize: 12, fontFamily: 'ui-monospace, monospace', fontWeight: 650 }}>{tool.name}</TableCell><TableCell sx={{ maxWidth: 480, color: 'text.secondary', fontSize: 12, lineHeight: 1.7 }}>{tool.description || '—'}</TableCell><TableCell><ToolPolicy tool={tool} /></TableCell><TableCell align="right" sx={{ whiteSpace: 'nowrap' }}><Button size="small" onClick={() => setSchema(tool)}>Schema</Button><Button size="small" component={Link} to={`/mcp/playground?tool=${encodeURIComponent(tool.name)}`}>Playground</Button></TableCell></TableRow>)}</TableBody></Table></TableContainer>}</Paper><JsonPanel title="C# CLIENT · LISTTOOLSASYNC / CALLTOOLASYNC" value={sdkExample} code />
    <Dialog open={Boolean(schema)} onClose={() => setSchema(null)} fullWidth maxWidth="md"><DialogTitle><Stack direction="row" justifyContent="space-between" alignItems="center"><Typography component="span" fontWeight={750}>{schema?.name}</Typography><IconButton aria-label="Đóng schema" onClick={() => setSchema(null)}><CloseOutlined /></IconButton></Stack></DialogTitle><Divider /><DialogContent><Stack spacing={2}><Typography color="text.secondary">{schema?.description}</Typography><JsonPanel title="INPUT SCHEMA" value={schema?.inputSchema} />{schema?.annotations && <JsonPanel title="TOOL ANNOTATIONS" value={schema.annotations} />}</Stack></DialogContent></Dialog>
  </>;
}
