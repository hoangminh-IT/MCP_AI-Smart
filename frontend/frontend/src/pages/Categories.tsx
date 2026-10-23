import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, InputAdornment, LinearProgress, Paper, Skeleton, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TableSortLabel, TextField, Tooltip, Typography } from '@mui/material';
import AddOutlined from '@mui/icons-material/AddOutlined';
import SearchOutlined from '@mui/icons-material/SearchOutlined';
import EditOutlined from '@mui/icons-material/EditOutlined';
import DeleteOutline from '@mui/icons-material/DeleteOutline';
import CategoryOutlined from '@mui/icons-material/CategoryOutlined';
import { api, dateTime, errorMessage } from '../api';
import { useAuth } from '../auth';
import { ConfirmDialog, EmptyState, PageHeading, useDebounced, useToast } from '../shared';
import type { Category, Page } from '../types';

export default function Categories() {
  const admin = useAuth().user?.role === 'Admin';
  const client = useQueryClient();
  const toast = useToast();
  const [search, setSearch] = useState('');
  const debounced = useDebounced(search.trim());
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [sortBy, setSortBy] = useState('name');
  const [descending, setDescending] = useState(false);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<Category | null>(null);
  const [deleting, setDeleting] = useState<Category | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  useEffect(() => setPage(0), [debounced]);
  const params = { search: debounced, page: page + 1, pageSize, sortBy, descending };
  const query = useQuery({ queryKey: ['categories', params], queryFn: async ({ signal }) => (await api.get<Page<Category>>('/categories', { params, signal })).data, placeholderData: keepPreviousData });
  async function refresh() { await Promise.all([client.invalidateQueries({ queryKey: ['categories'] }), client.invalidateQueries({ queryKey: ['category-options'] }), client.invalidateQueries({ queryKey: ['products'] })]); }
  const save = useMutation({ mutationFn: () => editing ? api.put(`/categories/${editing.id}`, { name: name.trim(), description: description.trim() }) : api.post('/categories', { name: name.trim(), description: description.trim() }), onSuccess: async () => { setEditorOpen(false); toast(editing ? 'Đã cập nhật danh mục.' : 'Đã thêm danh mục.'); await refresh(); } });
  const remove = useMutation({ mutationFn: () => api.delete(`/categories/${deleting!.id}`), onSuccess: async () => { setDeleting(null); toast('Đã xóa danh mục.'); await refresh(); } });
  function openEditor(item: Category | null) { save.reset(); setEditing(item); setName(item?.name ?? ''); setDescription(item?.description ?? ''); setEditorOpen(true); }
  function sort(field: string) { setDescending(sortBy === field ? !descending : false); setSortBy(field); setPage(0); }
  return <><PageHeading eyebrow="QUẢN LÝ KHO" title="Danh mục sản phẩm" description="Tổ chức hàng hóa thành các nhóm rõ ràng và dễ tìm kiếm." actions={admin && <Button startIcon={<AddOutlined />} variant="contained" onClick={() => openEditor(null)}>Thêm danh mục</Button>} />
    <Paper variant="outlined" sx={{ overflow: 'hidden' }}><Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} justifyContent="space-between" alignItems={{ xs: 'stretch', sm: 'center' }} sx={{ p: 2.5 }}><TextField placeholder="Tìm danh mục…" aria-label="Tìm danh mục" value={search} onChange={(event) => setSearch(event.target.value)} sx={{ width: { sm: 340 } }} slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchOutlined fontSize="small" /></InputAdornment> } }} /><Chip variant="outlined" label={`${query.data?.totalCount ?? 0} danh mục theo bộ lọc`} sx={{ alignSelf: { xs: 'start', sm: 'center' } }} /></Stack>{query.isFetching && <LinearProgress />}{query.isError ? <Alert severity="error" action={<Button color="inherit" onClick={() => void query.refetch()}>Thử lại</Button>} sx={{ m: 2.5 }}>{errorMessage(query.error)}</Alert> : query.isLoading ? <Stack spacing={1} sx={{ p: 3 }}>{[1, 2, 3, 4].map((value) => <Skeleton key={value} height={65} />)}</Stack> : query.data?.items.length ? <><TableContainer><Table sx={{ minWidth: 680 }}><TableHead><TableRow><TableCell><TableSortLabel active={sortBy === 'name'} direction={descending ? 'desc' : 'asc'} onClick={() => sort('name')}>TÊN DANH MỤC</TableSortLabel></TableCell><TableCell>MÔ TẢ</TableCell><TableCell align="right"><TableSortLabel active={sortBy === 'productCount'} direction={descending ? 'desc' : 'asc'} onClick={() => sort('productCount')}>SẢN PHẨM</TableSortLabel></TableCell><TableCell>NGÀY TẠO</TableCell>{admin && <TableCell align="right">THAO TÁC</TableCell>}</TableRow></TableHead><TableBody>{query.data.items.map((item) => <TableRow hover key={item.id}><TableCell><Stack direction="row" spacing={1.5} alignItems="center"><Box sx={{ bgcolor: '#EAF0FF', color: 'primary.main', p: 1, borderRadius: 2, display: 'flex' }}><CategoryOutlined fontSize="small" /></Box><Typography fontSize={14} fontWeight={650}>{item.name}</Typography></Stack></TableCell><TableCell sx={{ maxWidth: 300, color: 'text.secondary' }}>{item.description || '—'}</TableCell><TableCell align="right"><Chip label={item.productCount} size="small" sx={{ bgcolor: '#F1F5FC' }} /></TableCell><TableCell sx={{ whiteSpace: 'nowrap', color: 'text.secondary', fontSize: 12 }}>{dateTime(item.createdAt)}</TableCell>{admin && <TableCell align="right"><Tooltip title="Sửa danh mục"><IconButton size="small" aria-label={`Sửa ${item.name}`} onClick={() => openEditor(item)}><EditOutlined fontSize="small" /></IconButton></Tooltip><Tooltip title="Xóa danh mục"><IconButton size="small" aria-label={`Xóa ${item.name}`} onClick={() => { remove.reset(); setDeleting(item); }}><DeleteOutline fontSize="small" /></IconButton></Tooltip></TableCell>}</TableRow>)}</TableBody></Table></TableContainer><TablePagination component="div" count={query.data.totalCount} page={page} rowsPerPage={pageSize} rowsPerPageOptions={[10, 25, 50]} onPageChange={(_, value) => setPage(value)} onRowsPerPageChange={(event) => { setPageSize(Number(event.target.value)); setPage(0); }} labelRowsPerPage="Số dòng:" labelDisplayedRows={({ from, to, count }) => `${from}–${to} / ${count}`} /></> : <EmptyState title="Chưa có danh mục phù hợp" description={search ? 'Thử tên khác hoặc xóa nội dung tìm kiếm.' : 'Tạo danh mục đầu tiên để sắp xếp sản phẩm trong kho.'} action={admin && !search && <Button onClick={() => openEditor(null)} startIcon={<AddOutlined />}>Thêm danh mục</Button>} />}</Paper>
    <Alert severity="info" variant="outlined" sx={{ mt: 3 }}>Danh mục đang có sản phẩm được bảo vệ khỏi thao tác xóa. Hãy chuyển sản phẩm sang danh mục khác trước.</Alert>
    <Dialog open={editorOpen} onClose={save.isPending ? undefined : () => setEditorOpen(false)} fullWidth maxWidth="sm"><Box component="form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}><DialogTitle>{editing ? 'Chỉnh sửa danh mục' : 'Thêm danh mục mới'}</DialogTitle><DialogContent><Stack spacing={2.5} sx={{ pt: 1 }}>{save.isError && <Alert severity="error">{errorMessage(save.error)}</Alert>}<TextField autoFocus label="Tên danh mục" value={name} onChange={(event) => setName(event.target.value)} required slotProps={{ htmlInput: { maxLength: 120 } }} /><TextField label="Mô tả" value={description} onChange={(event) => setDescription(event.target.value)} multiline minRows={3} slotProps={{ htmlInput: { maxLength: 2000 } }} /></Stack></DialogContent><DialogActions sx={{ p: 2.5 }}><Button disabled={save.isPending} onClick={() => setEditorOpen(false)}>Hủy</Button><Button variant="contained" type="submit" disabled={save.isPending || !name.trim()}>{save.isPending ? 'Đang lưu…' : 'Lưu danh mục'}</Button></DialogActions></Box></Dialog>
    <ConfirmDialog open={Boolean(deleting)} title="Xóa danh mục?" description={`Danh mục “${deleting?.name ?? ''}” sẽ được xóa nếu không còn sản phẩm liên quan.`} confirmLabel="Xóa danh mục" pending={remove.isPending} error={remove.isError ? errorMessage(remove.error) : undefined} onClose={() => setDeleting(null)} onConfirm={() => remove.mutate()} />
  </>;
}
