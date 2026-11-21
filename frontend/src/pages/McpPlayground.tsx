import { useEffect, useState } from 'react';
import axios from 'axios';
import { Link, useSearchParams } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Accordion, AccordionDetails, AccordionSummary, Alert, Box, Button, Chip, CircularProgress, MenuItem, Paper, Stack, Tab, Tabs, TextField, Typography } from '@mui/material';
import PlayArrowOutlined from '@mui/icons-material/PlayArrowOutlined';
import RefreshOutlined from '@mui/icons-material/RefreshOutlined';
import ExpandMoreOutlined from '@mui/icons-material/ExpandMoreOutlined';
import { api, dateTime, errorMessage } from '../api';
import { EmptyState, PageHeading } from '../shared';
import { ExecutionResult, isWriteTool, JsonPanel, ToolPolicy, useMcpTools } from '../mcp';
import type { McpExecution } from '../mcp';
import { schemaSample, validateArguments } from '../mcpSchema';
import { prepareMcpWrite, useMcpWriteIntents, writeStatusLabels } from '../mcpWrites';
import { McpArgumentForm } from '../components/McpArgumentForm';
import { McpWriteConfirmation } from '../components/McpWriteConfirmation';
import { toolPresentation } from '../mcpPresentation';

export default function McpPlayground() {
  const [parameters, setParameters] = useSearchParams(); const tools = useMcpTools(); const client = useQueryClient();
  const [argumentsText, setArgumentsText] = useState('{}'); const [validation, setValidation] = useState('');
  const [inputMode, setInputMode] = useState<'form' | 'json'>('form');
  const [writeBusy, setWriteBusy] = useState(false); const [prepareUnknown, setPrepareUnknown] = useState(false); const [reviewIntentId, setReviewIntentId] = useState('');
  const selectedName = parameters.get('tool') ?? ''; const intentId = parameters.get('intentId') ?? '';
  const tool = selectedName ? tools.data?.find((item) => item.name === selectedName) : tools.data?.find((item) => !isWriteTool(item)) ?? tools.data?.[0];
  const write = Boolean(tool && isWriteTool(tool));
  const intents = useMcpWriteIntents(undefined, write || Boolean(intentId));
  const execution = useMutation({ retry: false, mutationFn: async (arguments_: Record<string, unknown>) => (await api.post<McpExecution>(`/mcp/tools/${encodeURIComponent(tool!.name)}/execute`, { arguments: arguments_ }, { timeout: 45_000 })).data,
    onSettled: () => { void client.invalidateQueries({ queryKey: ['mcp-executions'] }); void client.invalidateQueries({ queryKey: ['mcp-status'] }); },
  });
  const prepare = useMutation({ retry: false, mutationFn: (input: { toolName: string; arguments: Record<string, unknown> }) => prepareMcpWrite(input.toolName, input.arguments),
    onSuccess: (intent) => { client.setQueryData(['mcp-write-intent', intent.id], intent); setPrepareUnknown(false); setReviewIntentId(intent.id); setParameters({ tool: intent.toolName, intentId: intent.id }); },
    onError: (error) => { if (axios.isAxiosError(error) && !error.response) setPrepareUnknown(true); },
    onSettled: () => { void client.invalidateQueries({ queryKey: ['mcp-write-intents'] }); },
  });
  const busy = execution.isPending || prepare.isPending || writeBusy;
  function resetArguments() { setArgumentsText(JSON.stringify(schemaSample(tool?.inputSchema ?? {}), null, 2)); setValidation(''); execution.reset(); prepare.reset(); }
  useEffect(resetArguments, [tool?.name]);
  let request: unknown;
  try { const arguments_ = JSON.parse(argumentsText); request = write ? { toolName: tool?.name, arguments: arguments_ } : { arguments: arguments_ }; }
  catch { request = { arguments: '(JSON chưa hợp lệ)' }; }
  function run() {
    setValidation(''); if (!tool || busy || write && prepareUnknown) return;
    try {
      const value: unknown = JSON.parse(argumentsText); const errors = validateArguments(value, tool.inputSchema);
      if (errors.length) { setValidation(errors.join(' ')); return; }
      if (write) prepare.mutate({ toolName: tool.name, arguments: value as Record<string, unknown> }); else execution.mutate(value as Record<string, unknown>);
    } catch { setValidation('JSON không hợp lệ. Kiểm tra dấu ngoặc, dấu phẩy và dấu nháy kép.'); }
  }
  function newDraft() { setParameters({ tool: tool?.name ?? '' }); setReviewIntentId(''); setPrepareUnknown(false); prepare.reset(); }
  return <>
    <PageHeading eyebrow="AI & MCP" title="MCP Tool Playground" description="Thử công cụ, xem dữ liệu đầu vào và kiểm tra kết quả." actions={<Chip size="small" label="MCP Direct Test Mode" variant="outlined" color="primary" />} />
    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" spacing={1} sx={{ mb: 2.5 }}><Typography fontSize={12} color="text.secondary">Kết nối trực tiếp qua MCP Client C# · Không cần khóa AI</Typography><Button size="small" component={Link} to="/mcp/trace">Mở lịch sử MCP</Button></Stack>
    {tools.isLoading ? <Stack alignItems="center" sx={{ py: 7 }}><CircularProgress /></Stack>
      : tools.isError ? <Alert severity="error" action={<Button color="inherit" onClick={() => void tools.refetch()}>Thử lại</Button>}>{errorMessage(tools.error)}</Alert>
        : !tool ? <Paper variant="outlined"><EmptyState title={selectedName ? `Không tìm thấy tool ${selectedName}` : 'Chưa khám phá được tool'} description="Tải lại danh sách từ MCP Server hoặc chọn một công cụ hiện có." action={<Stack direction="row" spacing={1}><Button onClick={() => void tools.refetch()}>ListTools</Button>{selectedName && <Button onClick={() => setParameters({})}>Chọn tool có sẵn</Button>}</Stack>} /></Paper>
          : <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'minmax(0, 1fr) minmax(0, 1fr)' }, gap: 2.5, alignItems: 'start' }}>
            <Stack spacing={2.5} sx={{ minWidth: 0 }}>
              <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
                <Box sx={{ p: 2.5, pb: 2, borderBottom: 1, borderColor: 'divider' }}><Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}><Chip label="01" size="small" sx={{ fontWeight: 750, bgcolor: 'action.selected', color: 'primary.main' }} /><Typography fontSize={14} fontWeight={750}>Chọn công cụ & nhập tham số</Typography></Stack><Stack direction="row" alignItems="center" spacing={1}><TextField select fullWidth label="MCP Tool" value={tool.name} onChange={(event) => setParameters({ tool: event.target.value })} disabled={busy}>{tools.data?.map((item) => <MenuItem key={item.name} value={item.name}>{item.name}</MenuItem>)}</TextField><Button aria-label="Tải lại tools" variant="outlined" sx={{ minWidth: 42, px: 1 }} disabled={tools.isFetching || busy} onClick={() => void tools.refetch()}><RefreshOutlined /></Button></Stack><Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1} sx={{ mt: 2 }}><Typography fontSize={14} fontWeight={650}>{toolPresentation(tool).title}</Typography><ToolPolicy tool={tool} /></Stack><Typography color="text.secondary" sx={{ fontSize: 12, lineHeight: 1.7, mt: 1 }}>{toolPresentation(tool).description}</Typography></Box>
                <Box sx={{ p: 2.5 }}><Stack spacing={2}>
                  {write && <Alert severity="info" sx={{ fontSize: 12 }}>Chuẩn bị đề nghị → xem lại nội dung → xác nhận thực thi. Chỉ bước cuối mới thay đổi dữ liệu.</Alert>}
                  <Tabs value={inputMode} onChange={(_, value: 'form' | 'json') => setInputMode(value)} aria-label="Cách nhập tham số" sx={{ minHeight: 36, '& .MuiTab-root': { minHeight: 36, px: 1.5 } }}><Tab value="form" label="Biểu mẫu tham số" /><Tab value="json" label="Chỉnh sửa JSON" /></Tabs>
                  {inputMode === 'form' ? <McpArgumentForm schema={tool.inputSchema} text={argumentsText} disabled={busy || write && Boolean(intentId)} onChange={(text) => { setArgumentsText(text); setValidation(''); }} /> : <TextField label="Arguments · JSON object" value={argumentsText} onChange={(event) => { setArgumentsText(event.target.value); setValidation(''); }} multiline minRows={6} maxRows={16} disabled={busy || write && Boolean(intentId)} spellCheck={false} slotProps={{ input: { sx: { fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace', fontSize: 12 } } }} />}
                  <Box><Button size="small" disabled={busy || write && Boolean(intentId)} onClick={resetArguments}>Khởi tạo arguments từ schema</Button></Box>
                  {validation && <Alert severity="warning">{validation}</Alert>}{execution.isError && <Alert severity="error">Lỗi HTTP hoặc kết nối: {errorMessage(execution.error)} Kiểm tra lịch sử nếu chưa xác định được kết quả.</Alert>}{prepare.isError && <Alert severity="error">{errorMessage(prepare.error)}</Alert>}
                  {prepareUnknown && <Alert severity="warning">Chưa xác định đề nghị đã được lưu hay chưa. Làm mới danh sách và chọn đề nghị đã lưu; không tự động gửi lại yêu cầu chuẩn bị.</Alert>}
                  <Button variant="contained" startIcon={<PlayArrowOutlined />} disabled={busy || write && (Boolean(intentId) || prepareUnknown)} onClick={run}>{prepare.isPending ? 'Đang lưu đề nghị…' : execution.isPending ? 'Đang gọi MCP Client…' : write ? 'Chuẩn bị đề nghị thay đổi' : 'Run Tool'}</Button>
                  {write && (intentId || prepareUnknown) && <Button disabled={busy} onClick={newDraft}>Soạn đề nghị khác</Button>}
                </Stack></Box>
              </Paper>
              <Accordion disableGutters elevation={0} sx={{ border: 1, borderColor: 'divider', borderRadius: '12px !important', '&::before': { display: 'none' } }}><AccordionSummary expandIcon={<ExpandMoreOutlined />}><Box><Typography fontSize={13} fontWeight={650}>Thông tin kỹ thuật</Typography><Typography fontSize={12} color="text.secondary" sx={{ mt: .5 }}>Schema, mô tả gốc và JSON request</Typography></Box></AccordionSummary><AccordionDetails><Stack spacing={2}><Typography fontSize={12} color="text.secondary" sx={{ lineHeight: 1.7 }}>{tool.description || 'Server chưa cung cấp mô tả cho tool này.'}</Typography><JsonPanel title="INPUT SCHEMA · TỪ MCP SERVER" value={tool.inputSchema} /><JsonPanel title={write ? 'HTTP PREPARE REQUEST · JSON' : 'HTTP REQUEST · JSON'} value={request} /><Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>{write ? 'POST /api/mcp/write-intents → đề nghị đã lưu → xác nhận → MCP Client C#' : `POST /api/mcp/tools/${tool.name}/execute → C# SDK → MCP Server → Business Service`}</Typography></Stack></AccordionDetails></Accordion>
            </Stack>
            <Stack spacing={2.5} sx={{ minWidth: 0 }}>
              <Box sx={{ px: .5, py: 1 }}><Stack direction="row" spacing={1} alignItems="center"><Chip label="02" size="small" sx={{ fontWeight: 750, bgcolor: 'action.selected', color: 'primary.main' }} /><Typography fontSize={14} fontWeight={750}>{write ? 'Xem lại & xác nhận' : 'Kết quả thực thi'}</Typography></Stack></Box>
              {intentId && <McpWriteConfirmation key={intentId} intentId={intentId} openReviewInitially={reviewIntentId === intentId} onBusyChange={setWriteBusy} />}
              {!write && (execution.data ? <><ExecutionResult execution={execution.data} /><Button variant="outlined" component={Link} to={`/mcp/trace?executionId=${execution.data.executionId}`}>Mở execution #{execution.data.executionId}</Button></> : <Paper variant="outlined"><EmptyState title="Kết quả sẽ xuất hiện tại đây" description="Điền tham số rồi chọn Run Tool để xem kết quả và thời gian thực thi từ máy chủ." /></Paper>)}
              {write && !intentId && <Paper variant="outlined"><EmptyState title="Sẵn sàng tạo đề nghị" description="Chuẩn bị đề nghị ở bên trái. Bạn sẽ được xem lại nội dung trước khi xác nhận thay đổi dữ liệu." /></Paper>}
              {write && <Paper variant="outlined" sx={{ p: 2.5 }}><Stack spacing={1.5}><Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}><Typography fontSize={13} fontWeight={750}>Đề nghị đã lưu của tôi</Typography><Button size="small" disabled={intents.isFetching || busy} onClick={() => void intents.refetch()}>Làm mới đề nghị</Button></Stack>
                {intents.isError && <Alert severity="error">{errorMessage(intents.error)}</Alert>}{intents.isLoading && <CircularProgress size={20} />}
                {intents.data?.map((intent) => <Button key={intent.id} variant={intent.id === intentId ? 'outlined' : 'text'} aria-label={`Xem đề nghị ${intent.id}`} disabled={busy} onClick={() => { setReviewIntentId(''); setPrepareUnknown(false); setParameters({ tool: intent.toolName, intentId: intent.id }); }} sx={{ textAlign: 'left', justifyContent: 'flex-start', py: 1.25 }}><Stack spacing={.5}><Typography fontSize={12} fontWeight={650}>{toolPresentation({ name: intent.toolName }).title} · {writeStatusLabels[intent.status]}</Typography><Typography variant="caption" color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>{intent.id} · {dateTime(intent.createdAt)}</Typography></Stack></Button>)}
                {!intents.isLoading && !intents.isError && !intents.data?.length && <Typography variant="body2" color="text.secondary">Chưa có đề nghị. Hiển thị tối đa 50 đề nghị gần nhất.</Typography>}
              </Stack></Paper>}
            </Stack>
          </Box>}
  </>;
}
