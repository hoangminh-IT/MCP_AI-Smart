import { Fragment } from 'react';
import type { ReactNode } from 'react';
import { Alert, Button, LinearProgress, Skeleton, Stack, Table, TableBody, TableContainer, TableHead, TablePagination } from '@mui/material';
import { EmptyState } from '../shared';

// Query state and pagination shared by the supplier and customer catalogs.
// The caller supplies domain-specific headers and cells, keeping actions typed.
export function ServerDataTable<T>({
  label, minWidth = 900, rows, rowKey, head, renderRow, loading, fetching, error, onRetry,
  totalCount, page, pageSize, onPageChange, onPageSizeChange, emptyTitle, emptyDescription, emptyAction,
}: {
  label: string; minWidth?: number; rows: readonly T[]; rowKey: (row: T) => string | number;
  head: ReactNode; renderRow: (row: T) => ReactNode; loading: boolean; fetching: boolean;
  error?: string; onRetry: () => void; totalCount: number; page: number; pageSize: number;
  onPageChange: (page: number) => void; onPageSizeChange: (pageSize: number) => void;
  emptyTitle: string; emptyDescription: string; emptyAction?: ReactNode;
}) {
  return <>
    {fetching && <LinearProgress />}
    {error ? <Alert severity="error" sx={{ m: 2.5 }} action={<Button color="inherit" onClick={onRetry}>Thử lại</Button>}>{error}</Alert>
      : loading ? <Stack spacing={1} sx={{ p: 3 }}>{[1, 2, 3, 4, 5].map((value) => <Skeleton key={value} height={48} />)}</Stack>
      : rows.length ? <>
        <TableContainer><Table sx={{ minWidth }} aria-label={label} size="small">
          <TableHead>{head}</TableHead>
          <TableBody>{rows.map((row) => <Fragment key={rowKey(row)}>{renderRow(row)}</Fragment>)}</TableBody>
        </Table></TableContainer>
        <TablePagination component="div" count={totalCount} page={page} rowsPerPage={pageSize} rowsPerPageOptions={[10, 25, 50]}
          onPageChange={(_, value) => onPageChange(value)} onRowsPerPageChange={(event) => onPageSizeChange(Number(event.target.value))}
          labelRowsPerPage="Số dòng:" labelDisplayedRows={({ from, to, count }) => `${from}–${to} / ${count}`} />
      </> : <EmptyState title={emptyTitle} description={emptyDescription} action={emptyAction} />}
  </>;
}
