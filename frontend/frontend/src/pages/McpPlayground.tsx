import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Chip, CircularProgress, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material';
import PlayArrowOutlined from '@mui/icons-material/PlayArrowOutlined';
import RefreshOutlined from '@mui/icons-material/RefreshOutlined';
import { api, errorMessage } from '../api';
import { EmptyState, PageHeading } from '../shared';
import { ExecutionResult, isWriteTool, JsonPanel, ToolPolicy, useMcpTools } from '../mcp';
import type { McpExecution } from '../mcp';

function sampleArguments(schema: Record<string, unknown>): Record<string, unknown> {
  const properties = schema.properties as Record<string, Record<string, unknown>> | undefined;
  const required = Array.isArray(schema.required) ? schema.required : [];
  const result: Record<string, unknown> = {};
  for (const [name, field] of Object.entries(properties ?? {})) {
    if ('default' in field) { result[name] = field.default; continue; }
    if (!required.includes(name)) continue;
    const type = Array.isArray(field.type) ? field.type.find((value) => value !== 'null') : field.type;
    result[name] = field.enum instanceof Array ? field.enum[0] : type === 'number' || type === 'integer' ? field.minimum ?? 0 : type === 'boolean' ? false : type === 'array' ? [] : type === 'object' ? {} : '';
  }
  return result;
}

export default function McpPlayground() {
  const [parameters, setParameters] = useSearchParams();
  const tools = useMcpTools();
  const client = useQueryClient();
  const [argumentsText, setArgumentsText] = useState('{}');
  const [validation, setValidation] = useState('');
  const selectedName = parameters.get('tool') ?? '';
  const tool = tools.data?.find((item) => item.name === selectedName) ?? tools.data?.[0];
  const execution = useMutation({ mutationFn: async (arguments_: Record<string, unknown>) => (await api.post<McpExecution>(`/mcp/tools/${encodeURIComponent(tool!.name)}/execute`, { arguments: arguments_ }, { timeout: 45_000 })).data, onSuccess: () => { void client.invalidateQueries({ queryKey: ['mcp-executions'] }); void client.invalidateQueries({ queryKey: ['mcp-status'] }); } });
  useEffect(() => {
    setArgumentsText(JSON.stringify(sampleArguments(tool?.inputSchema ?? {}), null, 2)); setValidation(''); execution.reset();
  }, [tool?.name]);
  let request: unknown = { arguments: {} };
  try { request = { arguments: JSON.parse(argumentsText) }; } catch { request = { arguments: '(JSON chưa hợp lệ)' }; }
  function run() {
    setValidation('');
    try {
      const value: unknown = JSON.parse(argumentsText);
      if (!value || typeof value !== 'object' || Array.isArray(value)) { setValidation('Arguments phải là một JSON object, ví dụ { "threshold": 5 }.'); return; }
      execution.mutate(value as Record<string, unknown>);
    } catch { setValidation('JSON không hợp lệ. Kiểm tra dấu ngoặc, dấu phẩy và dấu nháy kép.'); }
  }
  return <><PageHeading eyebrow="AI & MCP" title="MCP Tool Playground" description="Gọi tool thật qua MCP Client C# và kiểm tra kết quả từ MCP Server." actions={<Chip label="MCP Direct Test Mode" variant="outlined" color="primary" />} /><Alert severity="info" sx={{ mb: 3 }}>Chế độ này kiểm thử giao thức MCP trực tiếp, không gọi LLM và không cần AI API key.</Alert>{tools.isLoading ? <Stack alignItems="center" sx={{ py: 7 }}><CircularProgress /></Stack> : tools.isError ? <Alert severity="error" action={<Button color="inherit" onClick={() => void tools.refetch()}>Thử lại</Button>}>{errorMessage(tools.error)}</Alert> : !tool ? <Paper variant="outlined"><EmptyState title="Chưa khám phá được tool" description="Kiểm tra MCP Server và thử tải lại danh sách." action={<Button onClick={() => void tools.refetch()}>ListTools</Button>} /></Paper> : <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', xl: 'minmax(0, 0.9fr) minmax(0, 1.1fr)' }, gap: 3, alignItems: 'start' }}><Stack spacing={3}><Paper variant="outlined" sx={{ p: 3 }}><Stack spacing={2.5}><Stack direction="row" alignItems="center" spacing={1.5}><TextField select fullWidth label="MCP Tool" value={tool.name} onChange={(event) => setParameters({ tool: event.target.value })} disabled={execution.isPending}>{tools.data?.map((item) => <MenuItem key={item.name} value={item.name}>{item.name}</MenuItem>)}</TextField><Button aria-label="Tải lại tools" variant="outlined" sx={{ minWidth: 42, px: 1 }} disabled={tools.isFetching || execution.isPending} onClick={() => void tools.refetch()}><RefreshOutlined /></Button></Stack><Box><ToolPolicy tool={tool} /><Typography color="text.secondary" sx={{ fontSize: 13, lineHeight: 1.8, mt: 1.2 }}>{tool.description || 'Server chưa cung cấp mô tả cho tool này.'}</Typography></Box>{isWriteTool(tool) && <Alert severity="warning">Tool thay đổi dữ liệu cần luồng xác nhận riêng. Playground hiện chỉ cho phép các tool đọc dữ liệu.</Alert>}<TextField label="Arguments · JSON object" value={argumentsText} onChange={(event) => setArgumentsText(event.target.value)} multiline minRows={8} maxRows={20} disabled={execution.isPending} spellCheck={false} slotProps={{ input: { sx: { fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace', fontSize: 13 } } }} />{validation && <Alert severity="warning">{validation}</Alert>}{execution.isError && <Alert severity="error">Lỗi HTTP hoặc kết nối: {errorMessage(execution.error)}</Alert>}<Button variant="contained" startIcon={<PlayArrowOutlined />} disabled={execution.isPending || isWriteTool(tool)} onClick={run}>{execution.isPending ? 'Đang gọi MCP Client…' : 'Run Tool'}</Button><Typography variant="caption" color="text.secondary">POST /api/mcp/tools/{tool.name}/execute → C# SDK → MCP Server → Business Service</Typography></Stack></Paper><JsonPanel title="INPUT SCHEMA · TỪ MCP SERVER" value={tool.inputSchema} /></Stack><Stack spacing={3}><JsonPanel title="HTTP REQUEST · JSON" value={request} />{execution.data ? <ExecutionResult execution={execution.data} /> : <Paper variant="outlined"><EmptyState title="Sẵn sàng thực thi" description="Nhập arguments theo JSON Schema rồi chọn Run Tool. Kết quả và thời gian thực tế sẽ xuất hiện tại đây." /></Paper>}</Stack></Box>}</>;
}
