import { useState } from 'react';
import { Alert, Box, IconButton, Menu, MenuItem, Stack, TextField, Tooltip, Typography } from '@mui/material';
import InfoOutlined from '@mui/icons-material/InfoOutlined';
import MoreHorizOutlined from '@mui/icons-material/MoreHorizOutlined';
import { allowsNull, fieldType, resolveField, schemaProperties } from '../mcpSchema';
import type { JsonSchema } from '../mcpSchema';

export function McpArgumentForm({ schema, text, disabled, onChange }: { schema: JsonSchema; text: string; disabled: boolean; onChange: (text: string) => void }) {
  const [menu, setMenu] = useState<{ name: string; element: HTMLElement } | null>(null);
  let arguments_: Record<string, unknown>;
  try { const value: unknown = JSON.parse(text); if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error(); arguments_ = value as Record<string, unknown>; }
  catch { return <Alert severity="info">Mở tab Chỉnh sửa JSON và nhập một object hợp lệ để dùng biểu mẫu tham số.</Alert>; }
  const properties = schemaProperties(schema); const required = Array.isArray(schema.required) ? schema.required : [];
  function update(name: string, value: unknown, remove = false) { const next = { ...arguments_ }; if (remove) delete next[name]; else next[name] = value; onChange(JSON.stringify(next, null, 2)); }
  return <Box sx={{ containerType: 'inline-size' }}><Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 2, '@container (min-width: 440px)': { gridTemplateColumns: 'repeat(2, minmax(0, 1fr))' } }}>
    {!Object.keys(properties).length && <Typography variant="body2" color="text.secondary" sx={{ gridColumn: '1 / -1' }}>Tool này không yêu cầu tham số.</Typography>}
    {Object.entries(properties).map(([name, raw]) => {
      const field = resolveField(raw, schema); const type = fieldType(field); const present = name in arguments_; const value = arguments_[name];
      const scalar = type === 'string' || type === 'integer' || type === 'number' || type === 'boolean';
      const nullable = allowsNull(field); const enumValues = Array.isArray(field.enum) ? field.enum : undefined;
      const helper = [typeof field.description === 'string' ? field.description : '', typeof field.minimum === 'number' ? `Min ${field.minimum}` : '', typeof field.maximum === 'number' ? `Max ${field.maximum}` : ''].filter(Boolean).join(' · ');
      const hasActions = !required.includes(name) && present || nullable && value !== null || 'default' in field;
      const menuOpen = menu?.name === name;
      return <Box key={name} sx={{ minWidth: 0, ...(!scalar ? { gridColumn: '1 / -1' } : {}) }}><Stack direction="row" spacing={.5} alignItems="center" justifyContent="space-between" sx={{ mb: .75, minHeight: 26 }}><Typography fontSize={11} color="text.secondary">{type ?? 'JSON'} · {required.includes(name) ? 'Bắt buộc' : 'Tùy chọn'}{present && value === null ? ' · null' : ''}</Typography><Stack direction="row" spacing={.25}>
        {helper && <Tooltip title={<Typography fontSize={12} sx={{ lineHeight: 1.7 }}>{helper}</Typography>} arrow><IconButton size="small" aria-label={`Thông tin tham số ${name}`} sx={{ width: 26, height: 26, color: 'text.secondary' }}><InfoOutlined sx={{ fontSize: 16 }} /></IconButton></Tooltip>}
        {hasActions && <IconButton size="small" aria-label={`Tùy chọn tham số ${name}`} aria-haspopup="menu" aria-expanded={menuOpen ? 'true' : undefined} disabled={disabled} onClick={(event) => setMenu({ name, element: event.currentTarget })} sx={{ width: 26, height: 26, color: 'text.secondary' }}><MoreHorizOutlined sx={{ fontSize: 18 }} /></IconButton>}
        </Stack></Stack>
        {scalar ? type === 'boolean' || enumValues ? <TextField fullWidth select label={`Tham số ${name}`} value={!present ? '__omit' : JSON.stringify(value)} disabled={disabled} onChange={(event) => event.target.value === '__omit' ? update(name, undefined, true) : update(name, JSON.parse(event.target.value))}>
          <MenuItem value="__omit">Không gửi tham số</MenuItem>{nullable && !enumValues?.includes(null) && <MenuItem value="null">null</MenuItem>}{(enumValues ?? [true, false]).map((option) => <MenuItem key={JSON.stringify(option)} value={JSON.stringify(option)}>{String(option)}</MenuItem>)}
        </TextField> : <TextField fullWidth label={`Tham số ${name}`} type={type === 'string' ? 'text' : 'number'} value={!present || value === null ? '' : String(value)} disabled={disabled} onChange={(event) => { const input = event.target.value; if (type !== 'string' && !input) update(name, undefined, true); else update(name, type === 'string' ? input : Number(input)); }} slotProps={{ htmlInput: { ...(typeof field.minimum === 'number' ? { min: field.minimum } : {}), ...(typeof field.maximum === 'number' ? { max: field.maximum } : {}), step: type === 'integer' ? 1 : 'any', ...(typeof field.maxLength === 'number' ? { maxLength: field.maxLength } : {}) } }} />
          : <Typography variant="body2" color="text.secondary">Tham số phức hợp: chỉnh trực tiếp trong tab Chỉnh sửa JSON.</Typography>}
        <Menu anchorEl={menuOpen ? menu.element : null} open={menuOpen} onClose={() => setMenu(null)}>{!required.includes(name) && present && <MenuItem disabled={disabled} onClick={() => { update(name, undefined, true); setMenu(null); }}>Bỏ {name}</MenuItem>}{nullable && value !== null && <MenuItem disabled={disabled} onClick={() => { update(name, null); setMenu(null); }}>Gửi null cho {name}</MenuItem>}{'default' in field && <MenuItem disabled={disabled} onClick={() => { update(name, field.default); setMenu(null); }}>Mặc định của {name}: {JSON.stringify(field.default)}</MenuItem>}</Menu>
      </Box>;
    })}
  </Box></Box>;
}
