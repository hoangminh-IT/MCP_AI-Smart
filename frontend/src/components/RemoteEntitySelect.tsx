import { useState } from 'react';
import { useInfiniteQuery } from '@tanstack/react-query';
import { Autocomplete, CircularProgress, TextField } from '@mui/material';
import { errorMessage } from '../api';
import { useDebounced } from '../shared';
import type { Page } from '../types';

// Reused for supplier and product lookup: server search + scroll pagination,
// so selection is never limited to the first catalog page.
export function RemoteEntitySelect<T extends { id: number }>({
  label, value, onChange, optionLabel, loadPage, queryKey, disabled = false, required = false,
}: {
  label: string; value: T | null; onChange: (value: T | null) => void; optionLabel: (value: T) => string;
  loadPage: (search: string, page: number, signal?: AbortSignal) => Promise<Page<T>>;
  queryKey: string; disabled?: boolean; required?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounced(search.trim());
  const pages = useInfiniteQuery({
    queryKey: [queryKey, debouncedSearch], enabled: open, initialPageParam: 1, staleTime: 0,
    queryFn: ({ pageParam, signal }) => loadPage(debouncedSearch, pageParam, signal),
    getNextPageParam: (last) => last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
  });
  const options = pages.data?.pages.flatMap((page) => page.items) ?? [];
  return <Autocomplete<T> open={open} onOpen={() => { setSearch(''); setOpen(true); }} onClose={() => setOpen(false)}
    value={value} onChange={(_, selected) => onChange(selected)} disabled={disabled} fullWidth
    options={options} getOptionLabel={optionLabel} isOptionEqualToValue={(option, selected) => option.id === selected.id}
    filterOptions={(items) => items} loading={pages.isFetching} loadingText="Đang tìm dữ liệu…"
    noOptionsText={pages.isError ? errorMessage(pages.error) : 'Không tìm thấy kết quả'}
    onInputChange={(_, text, reason) => { if (reason === 'input' || reason === 'clear') setSearch(text); }}
    slotProps={{ listbox: { onScroll: (event) => {
      const list = event.currentTarget;
      if (list.scrollTop + list.clientHeight >= list.scrollHeight - 40 && pages.hasNextPage && !pages.isFetchingNextPage) void pages.fetchNextPage();
    } } }}
    renderInput={(parameters) => <TextField {...parameters} label={label} required={required} inputProps={{ ...parameters.inputProps, 'aria-label': label }}
      error={pages.isError} placeholder="Nhập tên hoặc mã để tìm…" helperText={pages.isError ? errorMessage(pages.error) : undefined}
      slotProps={{ input: { ...parameters.InputProps, endAdornment: <>{pages.isFetching && <CircularProgress size={16} />}{parameters.InputProps.endAdornment}</> } }} />} />;
}
