import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Alert, Box, Button, Chip, Divider, LinearProgress, MenuItem, Paper, Stack, Tab, Tabs, TextField, Typography } from '@mui/material';
import { api, errorMessage } from '../api';
import { EmptyState } from '../shared';
import { JsonPanel } from '../mcp';

interface Resource { uri: string; name: string; description?: string; mimeType?: string }
interface Prompt { name: string; description?: string; arguments?: { name: string; description?: string; required?: boolean }[] }
interface ResourceResult { contents: { uri: string; mimeType?: string; text?: string }[] }
interface PromptResult { description?: string; messages: { role: string; content: unknown }[] }

export function McpResourcesPrompts() {
  const [tab, setTab] = useState<'resources' | 'prompts'>('resources');
  const resources = useQuery({ queryKey: ['mcp-resources'], enabled: tab === 'resources', queryFn: async ({ signal }) => (await api.get<Resource[]>('/mcp/resources', { signal })).data });
  const prompts = useQuery({ queryKey: ['mcp-prompts'], enabled: tab === 'prompts', queryFn: async ({ signal }) => (await api.get<Prompt[]>('/mcp/prompts', { signal })).data });
  const [promptName, setPromptName] = useState(''); const [arguments_, setArguments] = useState<Record<string, string>>({}); const [validation, setValidation] = useState('');
  const prompt = prompts.data?.find((item) => item.name === promptName) ?? prompts.data?.[0];
  const readResource = useMutation({ mutationFn: async (uri: string) => (await api.get<ResourceResult>('/mcp/resources/read', { params: { uri } })).data });
  const getPrompt = useMutation({ mutationFn: async (input: Record<string, string>) => (await api.post<PromptResult>(`/mcp/prompts/${encodeURIComponent(prompt!.name)}/get`, { arguments: input })).data });
  useEffect(() => { setArguments({}); setValidation(''); getPrompt.reset(); }, [prompt?.name]);
  function runPrompt() {
    setValidation(''); if (!prompt) return;
    const required = prompt.arguments?.filter((argument) => argument.required && !arguments_[argument.name]?.trim()) ?? [];
    if (required.length) { setValidation(`Nhập tham số bắt buộc: ${required.map((argument) => argument.name).join(', ')}.`); return; }
    getPrompt.mutate(Object.fromEntries(Object.entries(arguments_).map(([name, value]) => [name, value.trim()]).filter(([, value]) => value)));
  }
  return <Paper variant="outlined" sx={{ overflow: 'hidden' }}>
    <Box sx={{ px: 3, pt: 3 }}><Typography variant="h6">Tài liệu & mẫu hội thoại</Typography><Typography variant="body2" color="text.secondary" sx={{ mt: 1, mb: 2 }}>Resources chứa tài liệu tham khảo; Prompts cung cấp mẫu câu hỏi. Lấy mẫu hội thoại chưa gọi AI.</Typography></Box>
    <Tabs value={tab} onChange={(_, value: 'resources' | 'prompts') => setTab(value)}><Tab label="Tài liệu · Resources" value="resources" /><Tab label="Mẫu hội thoại · Prompts" value="prompts" /></Tabs><Divider />
    <Stack spacing={2.5} sx={{ p: 3 }}>
      {tab === 'resources' ? <>
        <Stack direction="row" justifyContent="space-between" alignItems="center"><Chip size="small" variant="outlined" label={`${resources.data?.length ?? '—'} tài liệu`} /><Button disabled={resources.isFetching} onClick={() => void resources.refetch()}>ListResources</Button></Stack>
        {resources.isFetching && <LinearProgress />}{resources.isError && <Alert severity="error">{errorMessage(resources.error)}</Alert>}
        {resources.data?.map((resource) => <Paper key={resource.uri} variant="outlined" sx={{ p: 2 }}><Stack spacing={1}><Stack direction="row" spacing={2} justifyContent="space-between" alignItems="center"><Typography fontWeight={750}>{resource.name}</Typography><Button size="small" variant="outlined" aria-label={`Đọc resource ${resource.name}`} disabled={readResource.isPending} onClick={() => readResource.mutate(resource.uri)}>Đọc tài liệu</Button></Stack><Typography variant="caption" color="primary" sx={{ overflowWrap: 'anywhere' }}>{resource.uri}</Typography><Typography variant="body2" color="text.secondary">{resource.description}</Typography>{resource.mimeType && <Typography variant="caption" color="text.secondary">{resource.mimeType}</Typography>}</Stack></Paper>)}
        {!resources.isLoading && !resources.isError && !resources.data?.length && <EmptyState title="Chưa có tài liệu" description="ListResources chỉ hiển thị nội dung do MCP Server thực tế công bố." />}
        {readResource.isPending && <LinearProgress />}{readResource.isError && <Alert severity="error">{errorMessage(readResource.error)}</Alert>}{readResource.data && <JsonPanel title="RESOURCE CONTENT · JSON" value={readResource.data} />}
      </> : <>
        <Stack direction="row" justifyContent="space-between" alignItems="center"><Chip size="small" variant="outlined" label={`${prompts.data?.length ?? '—'} mẫu hội thoại`} /><Button disabled={prompts.isFetching} onClick={() => void prompts.refetch()}>ListPrompts</Button></Stack>
        {prompts.isFetching && <LinearProgress />}{prompts.isError && <Alert severity="error">{errorMessage(prompts.error)}</Alert>}
        {prompt ? <><TextField label="MCP Prompt" select value={prompt.name} disabled={getPrompt.isPending} onChange={(event) => setPromptName(event.target.value)}>{prompts.data?.map((item) => <MenuItem key={item.name} value={item.name}>{item.name}</MenuItem>)}</TextField><Typography variant="body2" color="text.secondary">{prompt.description}</Typography>
          {prompt.arguments?.map((argument) => <TextField key={argument.name} label={`Prompt argument ${argument.name}`} helperText={argument.description} required={argument.required} value={arguments_[argument.name] ?? ''} disabled={getPrompt.isPending} onChange={(event) => { setArguments((current) => ({ ...current, [argument.name]: event.target.value })); setValidation(''); }} />)}
          {validation && <Alert severity="warning">{validation}</Alert>}{getPrompt.isError && <Alert severity="error">{errorMessage(getPrompt.error)}</Alert>}<Button variant="contained" disabled={getPrompt.isPending} onClick={runPrompt}>{getPrompt.isPending ? 'Đang lấy mẫu…' : 'GetPrompt'}</Button>
          <Typography variant="caption" color="text.secondary">Kết quả là mẫu hội thoại từ MCP Server; chưa phải câu trả lời hay phân tích của AI.</Typography>{getPrompt.data && <JsonPanel title="PROMPT TEMPLATE · JSON" value={getPrompt.data} />}
        </> : !prompts.isLoading && !prompts.isError && <EmptyState title="Chưa có mẫu hội thoại" description="ListPrompts chỉ hiển thị mẫu do MCP Server thực tế công bố." />}
      </>}
    </Stack>
  </Paper>;
}
