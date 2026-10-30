import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { api, errorMessage } from './api';
import { useAuth } from './auth';
import type { Page, StockOperation } from './types';

export interface Warehouse { id: number; code: string; name: string; address: string | null; isActive: boolean; isDefault: boolean; createdAt: string; updatedAt: string }
export interface WarehouseInput { code: string; name: string; address: string | null; isActive: boolean }
export type WarehouseSort = 'code' | 'name' | 'createdAt' | 'updatedAt';
export interface WarehouseQuery { search?: string; isActive?: boolean; page: number; pageSize: number; sortBy: WarehouseSort; descending: boolean }
export const warehouseInput = (item: Warehouse): WarehouseInput => ({ code: item.code, name: item.name, address: item.address, isActive: item.isActive });
export const listWarehouses = async (params: WarehouseQuery, signal?: AbortSignal) => (await api.get<Page<Warehouse>>('/warehouses', { params, signal })).data;
export const getWarehouse = async (id: number, signal?: AbortSignal) => (await api.get<Warehouse>(`/warehouses/${id}`, { signal })).data;
export const saveWarehouse = async (input: WarehouseInput, id?: number) => id ? (await api.put<Warehouse>(`/warehouses/${id}`, input)).data : (await api.post<Warehouse>('/warehouses', input)).data;
export const createWarehouse = (input: WarehouseInput) => saveWarehouse(input);
export const updateWarehouse = (id: number, input: WarehouseInput) => saveWarehouse(input, id);
export const deactivateWarehouse = async (id: number) => { await api.delete(`/warehouses/${id}`); };
async function warehouseOptions(signal?: AbortSignal) {
  const items: Warehouse[] = [];
  for (let page = 1; ; page++) {
    const result = await listWarehouses({ page, pageSize: 100, sortBy: 'name', descending: false }, signal); items.push(...result.items);
    if (!result.items.length || items.length >= result.totalCount) return items;
  }
}
export function useWarehouseOptions() {
  const { user } = useAuth();
  return useQuery({ queryKey: ['warehouse-options'], enabled: user?.role === 'Admin' || user?.role === 'Staff',
    queryFn: ({ signal }) => warehouseOptions(signal) });
}
const ScopeContext = createContext<{ warehouseId: number | undefined; setWarehouseId: (value: number | undefined) => void } | null>(null);
export function WarehouseScopeProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth(); const [warehouseId, setWarehouseId] = useState<number>();
  useEffect(() => setWarehouseId(undefined), [user?.id]);
  return <ScopeContext.Provider value={{ warehouseId, setWarehouseId }}>{children}</ScopeContext.Provider>;
}
export function useWarehouseScope() { const value = useContext(ScopeContext); if (!value) throw new Error('WarehouseScopeProvider is required.'); return value; }
export function WarehouseSelect({ value, onChange, label = 'Kho', allowAll = false, activeOnly = false, disabled = false, defaultToMain = true }: {
  value?: number; onChange: (value: number | undefined) => void; label?: string; allowAll?: boolean; activeOnly?: boolean; disabled?: boolean; defaultToMain?: boolean;
}) {
  const options = useWarehouseOptions();
  useEffect(() => { if (defaultToMain && !allowAll && value === undefined && options.data) { const initial = options.data.find((item) => item.isDefault && item.isActive) ?? options.data.find((item) => item.isActive); if (initial) onChange(initial.id); } }, [defaultToMain, allowAll, value, options.data, onChange]);
  const visible = options.data?.filter((item) => !activeOnly || item.isActive || item.id === value) ?? [];
  return <Stack spacing={1} sx={{ minWidth: 0, width: '100%' }}>
    <TextField select fullWidth label={label} value={value ?? ''} slotProps={{ inputLabel: { shrink: true }, select: { displayEmpty: true } }} disabled={disabled || options.isLoading || options.isError} required={!allowAll} onChange={(event) => onChange(event.target.value ? Number(event.target.value) : undefined)}>
      <MenuItem value="">{options.isLoading ? 'Đang tải kho…' : allowAll ? 'Tất cả kho · tổng hợp' : 'Chọn kho'}</MenuItem>
      {value !== undefined && !visible.some((item) => item.id === value) && <MenuItem value={value} disabled>Kho #{value} · {options.isLoading ? 'đang tải' : 'chưa tải được thông tin'}</MenuItem>}
      {visible.map((item) => <MenuItem key={item.id} value={item.id} disabled={activeOnly && !item.isActive}>{item.code} · {item.name}{item.isActive ? '' : ' · ngừng hoạt động'}</MenuItem>)}
    </TextField>
    {options.isError && <Alert severity="error" action={<Button color="inherit" onClick={() => void options.refetch()}>Tải lại kho</Button>}>{errorMessage(options.error)}</Alert>}
  </Stack>;
}
export function WarehouseScopeSelect() {
  const { warehouseId, setWarehouseId } = useWarehouseScope();
  return <WarehouseSelect label="Phạm vi kho" value={warehouseId} onChange={setWarehouseId} allowAll />;
}
export function WarehouseScopeNote({ compact = false }: { compact?: boolean }) {
  const { warehouseId } = useWarehouseScope(); const options = useWarehouseOptions(); const selected = options.data?.find((item) => item.id === warehouseId);
  return <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: compact ? 0 : 1.5 }}>{warehouseId ? `Dữ liệu tại ${selected ? `${selected.code} · ${selected.name}` : `kho #${warehouseId}`}.` : 'Dữ liệu tổng hợp từ tất cả kho.'}</Typography>;
}
export interface TransferInput { productId: number; sourceWarehouseId: number; destinationWarehouseId: number; quantity: number; reason: string; requestId: string }
export interface StockTransfer { transferId: string; requestId: string; replayed: boolean; productId: number; sourceWarehouseId: number; destinationWarehouseId: number; quantity: number; reason: string; createdBy: number; createdAt: string; movements: StockOperation[] }
export const transferStock = async (input: TransferInput) => (await api.post<StockTransfer>('/inventory/transfers', input)).data;
export const listTransfers = async (params: { warehouseId?: number; productId?: number; requestId?: string; page: number; pageSize: number }, signal?: AbortSignal) => (await api.get<Page<StockTransfer>>('/inventory/transfers', { params, signal })).data;
