import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Alert, Box, Button, Chip, Divider, Paper, Stack, Typography } from '@mui/material';
import ContentCopyOutlined from '@mui/icons-material/ContentCopyOutlined';
import { api, dateTime } from './api';
import { useToast } from './shared';

export interface McpStatus {
  connected: boolean; transport: string; serverUrl: string; serverName?: string;
  serverVersion?: string; errorMessage?: string; checkedAt: string;
}
export interface McpTool {
  name: string; description?: string; inputSchema: Record<string, unknown>;
  annotations?: { title?: string; readOnlyHint?: boolean; destructiveHint?: boolean; idempotentHint?: boolean };
}
export interface McpExecution {
  executionId: number; toolName: string; input: unknown; output?: unknown;
  isError: boolean; durationMs: number; createdAt: string; errorMessage?: string; status?: string;
}

export function useMcpStatus() {
  return useQuery({ queryKey: ['mcp-status'], queryFn: async ({ signal }) => (await api.get<McpStatus>('/mcp/status', { signal })).data, refetchInterval: 10_000, refetchIntervalInBackground: false });
}
export function useMcpTools(enabled = true) {
  return useQuery({ queryKey: ['mcp-tools'], enabled, queryFn: async ({ signal }) => (await api.get<McpTool[]>('/mcp/tools', { signal })).data });
}
export function prettyJson(value: unknown): string {
  if (typeof value === 'string') {
    try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
  }
  return JSON.stringify(value ?? null, null, 2);
}
export function isWriteTool(tool: McpTool) {
  return tool.annotations?.readOnlyHint === false || tool.annotations?.destructiveHint === true;
}
export function ToolPolicy({ tool }: { tool: McpTool }) {
  return tool.annotations?.readOnlyHint === true
    ? <Chip size="small" label="READ ONLY" sx={{ bgcolor: '#E6F8F2', color: '#0D9473', fontSize: 10 }} />
    : isWriteTool(tool) ? <Chip size="small" label="WRITE · CẦN XÁC NHẬN" color="warning" /> : null;
}
export function JsonPanel({ title, value, code = false }: { title: string; value: unknown; code?: boolean }) {
  const toast = useToast();
  const [copyError, setCopyError] = useState(false);
  const text = code && typeof value === 'string' ? value : prettyJson(value);
  return <Paper variant="outlined" sx={{ overflow: 'hidden' }}><Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ px: 2.5, py: 1.5 }}><Typography fontSize={12} fontWeight={750} letterSpacing={0.5}>{title}</Typography><Button size="small" startIcon={<ContentCopyOutlined fontSize="small" />} onClick={() => { if (!navigator.clipboard) { setCopyError(true); return; } void navigator.clipboard.writeText(text).then(() => { setCopyError(false); toast('Đã sao chép nội dung.'); }).catch(() => setCopyError(true)); }}>Sao chép</Button></Stack><Divider />{copyError && <Alert severity="info">Trình duyệt không cho phép sao chép. Bạn có thể chọn văn bản bên dưới.</Alert>}<Box component="pre" sx={{ m: 0, p: 2.5, bgcolor: '#F8FAFE', color: '#304260', overflow: 'auto', maxHeight: 440, fontSize: 12, lineHeight: 1.8, fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace' }}>{text}</Box></Paper>;
}
export function ExecutionResult({ execution }: { execution: McpExecution }) {
  return <Stack spacing={2}><Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap><Chip size="small" color={execution.isError ? 'error' : 'success'} label={execution.isError ? execution.status === 'ToolError' ? 'Tool báo lỗi' : 'Thực thi thất bại' : 'Thực thi thành công'} />{execution.status && <Chip size="small" variant="outlined" label={execution.status} />}<Typography variant="caption" color="text.secondary">#{execution.executionId} · {execution.durationMs} ms · {dateTime(execution.createdAt)}</Typography></Stack>{execution.isError && <Alert severity="error">{execution.errorMessage || 'MCP execution trả về isError=true. Xem kết quả giao thức bên dưới.'}</Alert>}<JsonPanel title="MCP RESULT · JSON" value={execution.output} /></Stack>;
}
