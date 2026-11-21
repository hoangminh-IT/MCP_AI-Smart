import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Accordion, AccordionDetails, AccordionSummary, Alert, Avatar, Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Divider, Drawer, IconButton, List, ListItemButton, ListItemText, Paper, Skeleton, Stack, TextField, Tooltip, Typography } from '@mui/material';
import AddOutlined from '@mui/icons-material/AddOutlined';
import EditOutlined from '@mui/icons-material/EditOutlined';
import DeleteOutline from '@mui/icons-material/DeleteOutline';
import SendOutlined from '@mui/icons-material/SendOutlined';
import SmartToyOutlined from '@mui/icons-material/SmartToyOutlined';
import ExpandMoreOutlined from '@mui/icons-material/ExpandMoreOutlined';
import CloseOutlined from '@mui/icons-material/CloseOutlined';
import TimelineOutlined from '@mui/icons-material/TimelineOutlined';
import StopCircleOutlined from '@mui/icons-material/StopCircleOutlined';
import { api, dateTime, errorMessage } from '../api';
import { ConfirmDialog, EmptyState, PageHeading, useToast } from '../shared';
import { JsonPanel, prettyJson, useMcpTools } from '../mcp';
import { aiProviderHint, executionStatus, toolPresentation } from '../mcpPresentation';
import { getAiProviderStatus } from '../settings';
import { useMcpWriteIntents } from '../mcpWrites';
import { McpWriteConfirmation } from '../components/McpWriteConfirmation';

interface Session { id: string; title: string; createdAt: string; updatedAt: string }
interface Message { id: string; role: string; content: string; status: string; createdAt: string; toolMetadata?: unknown }
interface Conversation { session: Session; messages: Message[] }
interface TraceStep { name: string; startedAt: string; durationMs: number; status: string; executionId?: number; toolName?: string; input?: unknown; output?: unknown }
interface Reply { sessionId: string; message: Message; trace: TraceStep[] }
interface ChatRequest {
  sessionId: string; message: string; startedAt: number; timeoutSeconds: number;
  controller: AbortController; previousMessageIds: ReadonlySet<string>;
}
interface CancelledRequest { request: ChatRequest; refreshUntil: number }

function metadata(value: unknown): Record<string, unknown> {
  if (typeof value === 'string') { try { return metadata(JSON.parse(value)); } catch { return {}; } }
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};
}
function traceSteps(value: unknown): TraceStep[] {
  return Array.isArray(value) ? value.filter((item): item is TraceStep => item && typeof item === 'object' && typeof item.name === 'string' && typeof item.startedAt === 'string' && typeof item.durationMs === 'number' && typeof item.status === 'string') : [];
}

export default function AiAssistant() {
  const client = useQueryClient();
  const toast = useToast();
  const [parameters, setParameters] = useSearchParams();
  const sessionId = parameters.get('session') ?? '';
  const [draft, setDraft] = useState('');
  const [title, setTitle] = useState('');
  const [editor, setEditor] = useState<{ session: Session | null } | null>(null);
  const [deleting, setDeleting] = useState<Session | null>(null);
  const [trace, setTrace] = useState<TraceStep[] | null>(null);
  const [pendingRequest, setPendingRequest] = useState<ChatRequest | null>(null);
  const [cancelRequested, setCancelRequested] = useState(false);
  const [cancelledRequest, setCancelledRequest] = useState<CancelledRequest | null>(null);
  const [clock, setClock] = useState(Date.now);
  const [activeWriteIds, setActiveWriteIds] = useState<ReadonlySet<string>>(() => new Set());
  const onWriteBusy = useCallback((busy: boolean, id: string) => setActiveWriteIds((current) => {
    if (current.has(id) === busy) return current;
    const next = new Set(current); if (busy) next.add(id); else next.delete(id); return next;
  }), []);
  const confirmingWrite = activeWriteIds.size > 0;
  const activeRequest = useRef<ChatRequest | null>(null);
  const currentSession = useRef(sessionId);
  currentSession.current = sessionId;
  const mounted = useRef(true);
  const isSending = pendingRequest?.sessionId === sessionId;
  const end = useRef<HTMLDivElement>(null);
  const provider = useQuery({ queryKey: ['ai-status'], queryFn: ({ signal }) => getAiProviderStatus(signal) });
  const sessions = useQuery({ queryKey: ['ai-sessions'], queryFn: async ({ signal }) => (await api.get<Session[]>('/ai/sessions', { signal })).data });
  const conversation = useQuery({
    queryKey: ['ai-conversation', sessionId], enabled: Boolean(sessionId),
    queryFn: async ({ signal }) => (await api.get<Conversation>(`/ai/sessions/${encodeURIComponent(sessionId)}`, { signal })).data,
    refetchInterval: isSending || (cancelledRequest?.request.sessionId === sessionId && clock < cancelledRequest.refreshUntil) ? 2_000 : false,
  });
  const writeIntents = useMcpWriteIntents(sessionId || undefined, Boolean(sessionId));
  const saveTitle = useMutation({ mutationFn: async () => (editor?.session ? await api.put<Session>(`/ai/sessions/${editor.session.id}`, { title: title.trim() }) : await api.post<Session>('/ai/sessions', { title: title.trim() })).data, onSuccess: (session) => { setEditor(null); setParameters({ session: session.id }); void client.invalidateQueries({ queryKey: ['ai-sessions'] }); void client.invalidateQueries({ queryKey: ['ai-conversation', session.id] }); toast('Đã lưu cuộc trò chuyện.'); } });
  const remove = useMutation({ mutationFn: () => api.delete(`/ai/sessions/${deleting!.id}`), onSuccess: () => { if (deleting?.id === sessionId) setParameters({}); setDeleting(null); void client.invalidateQueries({ queryKey: ['ai-sessions'] }); toast('Đã xóa cuộc trò chuyện.'); } });
  const send = useMutation({
    retry: false,
    mutationFn: async (request: ChatRequest) => (await api.post<Reply>(`/ai/sessions/${encodeURIComponent(request.sessionId)}/messages`, { message: request.message }, {
      signal: request.controller.signal, timeout: request.timeoutSeconds * 1_000 + 15_000,
    })).data,
    onSuccess: async (reply, request) => {
      if (!isCurrentRequest(request)) return;
      // A polling GET started before the reply must not replace the completed answer.
      await client.cancelQueries({ queryKey: ['ai-conversation', request.sessionId], exact: true });
      if (!isCurrentRequest(request)) return;
      client.setQueryData<Conversation>(['ai-conversation', request.sessionId], (current) => current ? {
        ...current, messages: [...current.messages.filter((message) => message.id !== reply.message.id), reply.message],
      } : undefined);
      setDraft('');
    },
    onSettled: (_reply, _error, request) => {
      if (!isCurrentRequest(request)) return;
      activeRequest.current = null; setPendingRequest(null); setCancelRequested(false);
      if (request.controller.signal.aborted) {
        const now = Date.now(); setClock(now);
        setCancelledRequest({ request, refreshUntil: now + 10_000 });
      }
      void client.invalidateQueries({ queryKey: ['ai-conversation', request.sessionId] });
      void client.invalidateQueries({ queryKey: ['ai-sessions'] });
      void client.invalidateQueries({ queryKey: ['mcp-executions'] });
      void client.invalidateQueries({ queryKey: ['mcp-status'] });
      void client.invalidateQueries({ queryKey: ['mcp-write-intents'] });
    },
  });
  function isCurrentRequest(request: ChatRequest) {
    return mounted.current && currentSession.current === request.sessionId && activeRequest.current === request;
  }
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; activeRequest.current?.controller.abort(); activeRequest.current = null; };
  }, []);
  useEffect(() => {
    setDraft(''); setPendingRequest(null); setCancelRequested(false); setCancelledRequest(null); send.reset();
    return () => {
      const request = activeRequest.current;
      if (request?.sessionId === sessionId) { activeRequest.current = null; request.controller.abort(); }
    };
  }, [sessionId]);
  useEffect(() => {
    if (!pendingRequest && (!cancelledRequest || Date.now() >= cancelledRequest.refreshUntil)) return;
    const timer = window.setInterval(() => setClock(Date.now()), 1_000);
    return () => window.clearInterval(timer);
  }, [pendingRequest, cancelledRequest, clock >= (cancelledRequest?.refreshUntil ?? 0)]);
  useEffect(() => { end.current?.scrollIntoView({ behavior: 'smooth', block: 'end' }); }, [conversation.data?.messages.length, isSending]);
  const configured = provider.data?.configured === true && !provider.isError;
  const availableTools = useMcpTools(configured);
  const questionSuggestions = [
    ['GetInventorySummary', 'Tóm tắt tồn kho hiện tại.'],
    ['GetLowStockProducts', 'Liệt kê sản phẩm đang tồn thấp hơn mức tối thiểu.'],
    ['GetSalesSummary', 'Tóm tắt đơn bán và hàng đã giao trong 30 ngày qua.'],
    ['GetPurchaseSummary', 'Tóm tắt đơn mua và hàng đã nhận trong 30 ngày qua.'],
  ].filter(([name]) => availableTools.data?.some((tool) => tool.name === name));
  function openEditor(session: Session | null) { saveTitle.reset(); setTitle(session?.title ?? 'Cuộc trò chuyện mới'); setEditor({ session }); }
  const cancellationSaved = cancelledRequest && conversation.data?.messages.some((message) => message.role === 'assistant' && !cancelledRequest.request.previousMessageIds.has(message.id));
  const recoveringCancellation = cancelledRequest?.request.sessionId === sessionId && clock < cancelledRequest.refreshUntil && !cancellationSaved;
  const currentUserMessage = pendingRequest && conversation.data?.messages.slice().reverse().find((message) => message.role === 'user' && !pendingRequest.previousMessageIds.has(message.id));
  const pendingMetadata = metadata(currentUserMessage?.toolMetadata);
  const progress = metadata(pendingMetadata.progress);
  const observedSteps = traceSteps(pendingMetadata.trace);
  const elapsedSeconds = pendingRequest ? Math.max(0, Math.floor((clock - pendingRequest.startedAt) / 1_000)) : 0;
  const retryAt = typeof progress.updatedAt === 'string' && typeof progress.retryAfterSeconds === 'number'
    ? Date.parse(progress.updatedAt) + Math.max(0, progress.retryAfterSeconds) * 1_000 : Number.NaN;
  const retrySeconds = Number.isFinite(retryAt) ? Math.max(0, Math.ceil((retryAt - clock) / 1_000)) : undefined;
  const progressText = progress.stage === 'DiscoveringTools' ? 'Đang kết nối các công cụ dữ liệu…'
    : progress.stage === 'CallingLlm' ? 'Đang chờ AI phản hồi…'
    : progress.stage === 'RunningTool' ? `Đang truy vấn dữ liệu${typeof progress.toolName === 'string' ? ` · ${progress.toolName}` : ''}…`
    : progress.stage === 'RateLimited' ? `Nhà cung cấp đang giới hạn tốc độ${retrySeconds ? ` · thử lại sau ${retrySeconds} giây` : ' · đang chờ tiếp tục'}…`
    : 'Máy chủ đang xử lý yêu cầu…';
  function submit() {
    if (!draft.trim() || !configured || !sessionId || activeRequest.current || recoveringCancellation || confirmingWrite || conversation.isLoading || conversation.isError) return;
    const configuredTimeout = provider.data?.requestTimeoutSeconds;
    const timeoutSeconds = typeof configuredTimeout === 'number' && Number.isFinite(configuredTimeout) ? Math.min(120, Math.max(10, configuredTimeout)) : 120;
    const request: ChatRequest = { sessionId, message: draft.trim(), startedAt: Date.now(), timeoutSeconds,
      controller: new AbortController(), previousMessageIds: new Set(conversation.data?.messages.map((message) => message.id)) };
    activeRequest.current = request; setPendingRequest(request); setClock(request.startedAt); setCancelledRequest(null); setCancelRequested(false);
    send.mutate(request);
  }
  function stop() { setCancelRequested(true); activeRequest.current?.controller.abort(); }

  return <><PageHeading eyebrow="AI & MCP" title="AI Assistant" description="Tra cứu và phân tích dữ liệu kho qua cuộc trò chuyện." actions={<Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap><Tooltip title={configured ? `${provider.data?.provider} · ${provider.data?.model}` : 'Kiểm tra cấu hình nhà cung cấp AI'}><Chip size="small" color={configured ? 'success' : 'default'} variant="outlined" label={provider.isLoading ? 'Đang kiểm tra AI…' : configured ? 'Đã cấu hình AI' : 'AI chưa sẵn sàng'} /></Tooltip><Button variant="outlined" onClick={() => void provider.refetch()}>Kiểm tra AI</Button></Stack>} />
    {provider.isError && <Alert severity="error" sx={{ mb: 3 }}>{errorMessage(provider.error)}</Alert>}{provider.data && !provider.data.configured && <Alert severity="info" sx={{ mb: 3 }} action={<Button color="inherit" component={Link} to="/mcp/playground">MCP Direct Test</Button>}>{aiProviderHint(provider.data.reason)} Bạn vẫn có thể kiểm thử MCP trực tiếp.</Alert>}
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: '240px minmax(0, 1fr)' }, gap: 2.5, alignItems: 'start' }}><Paper component="aside" variant="outlined" sx={{ overflow: 'hidden', position: { lg: 'sticky' }, top: 0 }}><Box sx={{ p: 2 }}><Typography fontSize={13} fontWeight={750} sx={{ mb: 1.5 }}>Cuộc trò chuyện</Typography><Button fullWidth variant="contained" startIcon={<AddOutlined />} onClick={() => openEditor(null)} disabled={isSending || confirmingWrite}>Cuộc trò chuyện mới</Button></Box><Divider />{sessions.isLoading ? <Stack spacing={1} sx={{ p: 2 }}><Skeleton /><Skeleton /><Skeleton /></Stack> : sessions.isError ? <Alert severity="error" sx={{ m: 2 }} action={<Button color="inherit" onClick={() => void sessions.refetch()}>Thử lại</Button>}>{errorMessage(sessions.error)}</Alert> : !sessions.data?.length ? <Typography color="text.secondary" variant="body2" sx={{ p: 3, lineHeight: 1.8 }}>Chưa có lịch sử. Tạo một cuộc trò chuyện để bắt đầu.</Typography> : <List disablePadding sx={{ maxHeight: { xs: 220, lg: 520 }, overflow: 'auto', py: 1 }}>{sessions.data.map((item) => <ListItemButton key={item.id} selected={item.id === sessionId} disabled={isSending || confirmingWrite} onClick={() => setParameters({ session: item.id })} sx={{ mx: 1, mb: .5, borderRadius: 2, py: 1.25, '&.Mui-selected': { bgcolor: 'action.selected', color: 'primary.main' } }}><ListItemText primary={item.title} secondary={dateTime(item.updatedAt)} slotProps={{ primary: { fontSize: 13, fontWeight: 650, noWrap: true }, secondary: { fontSize: 10, sx: { mt: 0.7 } } }} /></ListItemButton>)}</List>}</Paper>
    <Paper variant="outlined" sx={{ overflow: 'hidden', minWidth: 0 }}><Stack direction="row" spacing={1} justifyContent="space-between" alignItems="center" sx={{ px: 3, py: 2 }}><Stack direction="row" spacing={1.5} alignItems="center"><Avatar sx={{ bgcolor: 'action.selected', color: 'primary.main', width: 40, height: 40, borderRadius: 2.5 }}><SmartToyOutlined fontSize="small" /></Avatar><Box><Typography fontWeight={750} fontSize={14}>{conversation.data?.session.title ?? 'Trợ lý dữ liệu kho'}</Typography><Typography variant="caption" color="text.secondary">Trả lời dựa trên dữ liệu kho qua MCP</Typography></Box></Stack>{conversation.data && <Stack direction="row"><Tooltip title="Đổi tên"><IconButton aria-label="Đổi tên cuộc trò chuyện" disabled={isSending || confirmingWrite} onClick={() => openEditor(conversation.data.session)}><EditOutlined fontSize="small" /></IconButton></Tooltip><Tooltip title="Xóa cuộc trò chuyện"><IconButton aria-label="Xóa cuộc trò chuyện" disabled={isSending || confirmingWrite} onClick={() => { remove.reset(); setDeleting(conversation.data.session); }}><DeleteOutline fontSize="small" /></IconButton></Tooltip></Stack>}</Stack><Divider />
    <Box sx={{ minHeight: 380, height: { xs: 420, xl: 510 }, overflow: 'auto', p: { xs: 2, sm: 3 }, bgcolor: 'background.paper' }}>{!sessionId ? <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', px: 2 }}><Avatar sx={{ width: 58, height: 58, bgcolor: 'action.selected', color: 'primary.main', borderRadius: 4, mb: 2.5 }}><SmartToyOutlined sx={{ fontSize: 30 }} /></Avatar><Typography fontWeight={750} fontSize={22}>Bạn muốn tìm hiểu điều gì?</Typography><Typography fontSize={13} color="text.secondary" sx={{ mt: 1, maxWidth: 340, lineHeight: 1.8 }}>Tìm sản phẩm, kiểm tra tồn kho hoặc phân tích đơn hàng trong một cuộc trò chuyện.</Typography><Button variant="outlined" startIcon={<AddOutlined />} onClick={() => openEditor(null)} disabled={isSending || confirmingWrite} sx={{ mt: 2.5 }}>Tạo cuộc trò chuyện</Button></Box> : conversation.isLoading ? <Stack spacing={2}><Skeleton height={70} /><Skeleton height={100} /></Stack> : conversation.isError && !conversation.data ? <Alert severity="error" action={<Button color="inherit" onClick={() => void conversation.refetch()}>Thử lại</Button>}>{errorMessage(conversation.error)}</Alert> : <Stack spacing={2.5}>{conversation.isError && <Alert severity="warning">Chưa cập nhật được tiến độ. {errorMessage(conversation.error)}</Alert>}{!conversation.data?.messages.length && <EmptyState title="Cuộc trò chuyện đang trống" description={configured ? 'Ví dụ: “Cho tôi danh sách sản phẩm sắp hết.”' : 'AI chưa được cấu hình. Lịch sử và MCP Direct Test vẫn có thể sử dụng.'} />}{conversation.data?.messages.map((message) => {
      const meta = metadata(message.toolMetadata);
      const steps = traceSteps(meta.trace);
      const executionId = typeof meta.executionId === 'number' ? meta.executionId : undefined;
      if (message.role === 'tool') return <Accordion key={message.id} disableGutters elevation={0} sx={{ border: 1, borderColor: 'divider', borderRadius: '12px !important', '&::before': { display: 'none' } }}><AccordionSummary expandIcon={<ExpandMoreOutlined />}><Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap><Chip label="MCP TOOL" size="small" variant="outlined" /><Typography fontSize={12} fontWeight={650}>{typeof meta.toolName === 'string' ? toolPresentation({ name: meta.toolName }).title : 'Công cụ dữ liệu'}</Typography><Chip size="small" label={executionStatus(message.status)} color={message.status === 'Succeeded' ? 'success' : 'error'} variant="outlined" />{typeof meta.durationMs === 'number' && <Typography variant="caption" color="text.secondary">{meta.durationMs} ms</Typography>}</Stack></AccordionSummary><AccordionDetails><Stack spacing={2}><JsonPanel title="TOOL METADATA · INPUT" value={meta} /><JsonPanel title="TOOL OUTPUT" value={typeof message.content === 'string' ? prettyJson(message.content) : message.content} />{executionId && <Button component={Link} to={`/mcp/trace?executionId=${executionId}`}>Mở MCP execution #{executionId}</Button>}</Stack></AccordionDetails></Accordion>;
      return <Box key={message.id} sx={{ maxWidth: message.role === 'user' ? '86%' : '96%', alignSelf: message.role === 'user' ? 'flex-end' : 'flex-start', minWidth: 0 }}><Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.8, textAlign: message.role === 'user' ? 'right' : 'left' }}>{message.role === 'user' ? 'Bạn' : 'AI Assistant'} · {dateTime(message.createdAt)}</Typography>{message.status === 'AwaitingConfirmation' ? <Alert severity="info">{message.content || 'AI đã đề xuất thay đổi.'} Trạng thái hiện tại và nút xác nhận nằm ở thẻ đề nghị đã lưu bên dưới.</Alert> : message.status !== 'Succeeded' ? <Alert severity="warning">Yêu cầu {message.status}. Chưa tạo được câu trả lời hoàn chỉnh.</Alert> : <Paper elevation={0} sx={{ p: 2, bgcolor: message.role === 'user' ? 'primary.main' : 'action.hover', color: message.role === 'user' ? 'primary.contrastText' : 'text.primary', borderRadius: message.role === 'user' ? '16px 16px 4px 16px' : '4px 16px 16px 16px' }}><Typography fontSize={14} sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', lineHeight: 1.8 }}>{message.content}</Typography></Paper>}{(steps.length > 0 || Boolean(message.toolMetadata)) && <Stack direction="row" spacing={1} sx={{ mt: 1 }}><Button size="small" startIcon={<TimelineOutlined />} disabled={steps.length === 0} onClick={() => setTrace(steps)}>Xem Agent Trace</Button>{message.status !== 'Succeeded' && <Typography variant="caption" color="text.secondary">Kiểm tra trace và trạng thái MCP trước khi gửi lại.</Typography>}</Stack>}</Box>;
    })}{writeIntents.isError && <Alert severity="error" action={<Button color="inherit" onClick={() => void writeIntents.refetch()}>Tải lại đề nghị</Button>}>{errorMessage(writeIntents.error)}</Alert>}{writeIntents.data && writeIntents.data.length > 0 && <Stack spacing={2.5}><Typography variant="h6">Đề nghị thay đổi trong cuộc trò chuyện</Typography><Alert severity="info">Xác nhận thực thi tool MCP thực tế bằng đề nghị đã lưu. Không tự động gọi thêm lượt LLM sau khi xác nhận.</Alert>{writeIntents.data.map((intent) => <McpWriteConfirmation key={intent.id} intentId={intent.id} initialIntent={intent} onBusyChange={onWriteBusy} />)}</Stack>}{isSending && <Stack direction="row" spacing={1.5} alignItems="flex-start" sx={{ p: 2, bgcolor: 'action.selected', borderRadius: 3 }}><CircularProgress size={18} sx={{ mt: 0.5 }} /><Box role="status" aria-live="polite"><Typography fontSize={13} fontWeight={650}>{cancelRequested ? 'Đang dừng yêu cầu…' : progressText}</Typography><Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>Đã chờ {elapsedSeconds} giây · Giới hạn phản hồi {(pendingRequest?.timeoutSeconds ?? 120) + 15} giây</Typography><Typography variant="caption" color="text.secondary" sx={{ display: 'block', overflowWrap: 'anywhere', mt: 0.5 }}>{pendingRequest?.message}</Typography>{elapsedSeconds >= 20 && !cancelRequested && <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>Nhà cung cấp AI đang phản hồi chậm. Bạn có thể bấm Dừng; yêu cầu sẽ không tự gửi lại.</Typography>}{observedSteps.length > 0 && <Button size="small" startIcon={<TimelineOutlined />} onClick={() => setTrace(observedSteps)}>Xem các bước đã thực hiện</Button>}</Box></Stack>}<div ref={end} /></Stack>}</Box><Divider /><Box component="form" onSubmit={(event) => { event.preventDefault(); submit(); }} sx={{ p: 2.5 }}>{cancelledRequest && <Alert severity="info" sx={{ mb: 2 }} role="status">{recoveringCancellation ? 'Đã gửi yêu cầu dừng. Đang cập nhật trạng thái đã lưu…' : 'Đã dừng chờ phản hồi. Kiểm tra trạng thái trong lịch sử và các công cụ đã thực thi trước khi gửi lại.'}</Alert>}{send.isError && !cancelledRequest && <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(send.error)} Kiểm tra lịch sử trước khi gửi lại.</Alert>}<Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mb: questionSuggestions.length && configured && sessionId ? 2 : 0 }}>{configured && sessionId && questionSuggestions.map(([name, question]) => <Button key={name} size="small" variant="outlined" sx={{ fontSize: 11, borderColor: 'divider', color: 'text.secondary', borderRadius: 2 }} disabled={isSending || recoveringCancellation || confirmingWrite || conversation.isLoading || conversation.isError} onClick={() => setDraft(question)}>{question}</Button>)}</Stack><Stack direction="row" spacing={1.5} alignItems="flex-end"><TextField label="Câu hỏi về kho hàng" value={draft} onChange={(event) => setDraft(event.target.value)} multiline minRows={2} maxRows={6} fullWidth disabled={!sessionId || !configured || isSending || recoveringCancellation || confirmingWrite || conversation.isError || conversation.isLoading} slotProps={{ htmlInput: { maxLength: 4000 } }} onKeyDown={(event) => { if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); submit(); } }} />{isSending ? <Button color="error" variant="outlined" startIcon={<StopCircleOutlined />} disabled={cancelRequested} onClick={stop}>{cancelRequested ? 'Đang dừng…' : 'Dừng'}</Button> : <Button type="submit" variant="contained" startIcon={<SendOutlined />} disabled={!sessionId || !configured || recoveringCancellation || confirmingWrite || !draft.trim() || conversation.isError || conversation.isLoading}>Gửi</Button>}</Stack><Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 1.5 }}>Ctrl/⌘ + Enter để gửi · Tối đa 4.000 ký tự · Mọi thay đổi dữ liệu đều cần bạn xác nhận.</Typography></Box></Paper></Box>
    <Dialog open={Boolean(editor)} onClose={saveTitle.isPending ? undefined : () => setEditor(null)} fullWidth maxWidth="sm"><Box component="form" onSubmit={(event) => { event.preventDefault(); saveTitle.mutate(); }}><DialogTitle>{editor?.session ? 'Đổi tên cuộc trò chuyện' : 'Cuộc trò chuyện mới'}</DialogTitle><DialogContent><Stack spacing={2} sx={{ pt: 1 }}>{saveTitle.isError && <Alert severity="error">{errorMessage(saveTitle.error)}</Alert>}<TextField label="Tên cuộc trò chuyện" autoFocus fullWidth value={title} onChange={(event) => setTitle(event.target.value)} required slotProps={{ htmlInput: { maxLength: 120 } }} /></Stack></DialogContent><DialogActions sx={{ p: 2.5 }}><Button disabled={saveTitle.isPending} onClick={() => setEditor(null)}>Hủy</Button><Button type="submit" variant="contained" disabled={saveTitle.isPending || !title.trim()}>{saveTitle.isPending ? 'Đang lưu…' : 'Lưu cuộc trò chuyện'}</Button></DialogActions></Box></Dialog>
    <ConfirmDialog open={Boolean(deleting)} title="Xóa cuộc trò chuyện?" description={`Lịch sử “${deleting?.title ?? ''}” và các tin nhắn liên quan sẽ bị xóa.`} confirmLabel="Xóa cuộc trò chuyện" pending={remove.isPending} error={remove.isError ? errorMessage(remove.error) : undefined} onClose={() => setDeleting(null)} onConfirm={() => remove.mutate()} />
    <Drawer anchor="right" open={trace !== null} onClose={() => setTrace(null)} sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', md: 680 } } }}><Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ p: 3 }}><Box><Typography variant="overline" color="primary">CÁC BƯỚC ĐÃ GHI NHẬN</Typography><Typography variant="h6">AI Agent Trace</Typography></Box><IconButton aria-label="Đóng Agent Trace" onClick={() => setTrace(null)}><CloseOutlined /></IconButton></Stack><Divider /><Stack spacing={3} sx={{ p: 3 }}>{trace?.map((step, index) => <Box key={`${step.startedAt}-${index}`} sx={{ borderLeft: 3, borderColor: 'primary.main', pl: 2 }}><Stack direction="row" spacing={1} justifyContent="space-between" alignItems="center"><Typography fontWeight={750} fontSize={14}>{step.toolName || step.name}</Typography><Chip size="small" label={executionStatus(step.status)} color={step.status === 'Succeeded' ? 'success' : step.status === 'AwaitingConfirmation' ? 'warning' : 'error'} variant="outlined" /></Stack><Typography variant="caption" color="text.secondary">{dateTime(step.startedAt)} · {step.durationMs} ms{step.executionId ? ` · MCP execution #${step.executionId}` : ''}</Typography>{step.input !== undefined && <JsonPanel title="MCP TOOL INPUT · JSON" value={step.input} />}{step.output !== undefined && <JsonPanel title="MCP TOOL OUTPUT · JSON" value={step.output} />}{step.executionId && <Button size="small" component={Link} to={`/mcp/trace?executionId=${step.executionId}`}>Mở MCP execution #{step.executionId}</Button>}</Box>)}<Alert severity="info">Tool name, JSON, status và duration được lấy từ các kết quả MCP đã thực thi và lưu bởi backend.</Alert></Stack></Drawer>
  </>;
}
