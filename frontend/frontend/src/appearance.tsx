import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { alpha, createTheme, CssBaseline, IconButton, ListItemIcon, Menu, MenuItem, TextField, ThemeProvider, Tooltip, useMediaQuery } from '@mui/material';
import LightModeOutlined from '@mui/icons-material/LightModeOutlined';
import DarkModeOutlined from '@mui/icons-material/DarkModeOutlined';
import SettingsBrightnessOutlined from '@mui/icons-material/SettingsBrightnessOutlined';
import CheckOutlined from '@mui/icons-material/CheckOutlined';

type Appearance = 'light' | 'dark' | 'system';
const STORAGE_KEY = 'smart-inventory-appearance';
const options = [
  { value: 'light', label: 'Sáng', icon: <LightModeOutlined fontSize="small" /> },
  { value: 'dark', label: 'Tối', icon: <DarkModeOutlined fontSize="small" /> },
  { value: 'system', label: 'Theo hệ thống', icon: <SettingsBrightnessOutlined fontSize="small" /> },
] as const;
const valid = (value: unknown): value is Appearance => value === 'light' || value === 'dark' || value === 'system';
const AppearanceContext = createContext<{ preference: Appearance; setPreference: (value: Appearance) => void } | null>(null);
function storedAppearance(): Appearance {
  try { const value = localStorage.getItem(STORAGE_KEY); return valid(value) ? value : 'system'; } catch { return 'system'; }
}

export function AppearanceProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<Appearance>(storedAppearance);
  const systemDark = useMediaQuery('(prefers-color-scheme: dark)');
  const mode = preference === 'system' ? systemDark ? 'dark' : 'light' : preference;
  const setPreference = (value: Appearance) => {
    setPreferenceState(value);
    try { localStorage.setItem(STORAGE_KEY, value); } catch { /* Current-tab selection still works without storage. */ }
  };
  useEffect(() => {
    document.documentElement.dataset.theme = mode;
    document.documentElement.style.colorScheme = mode;
  }, [mode]);
  useEffect(() => {
    const receive = (event: StorageEvent) => { if (event.key === STORAGE_KEY) setPreferenceState(valid(event.newValue) ? event.newValue : 'system'); };
    window.addEventListener('storage', receive); return () => window.removeEventListener('storage', receive);
  }, []);
  const theme = useMemo(() => createTheme({
    palette: {
      mode,
      primary: { main: mode === 'dark' ? '#91B1FF' : '#3564E5', contrastText: mode === 'dark' ? '#0B1830' : '#FFFFFF' },
      secondary: { main: mode === 'dark' ? '#6BD2CA' : '#0B8582' },
      background: { default: mode === 'dark' ? '#0E1624' : '#F5F7FB', paper: mode === 'dark' ? '#172235' : '#FFFFFF' },
      text: { primary: mode === 'dark' ? '#E8EEF9' : '#1C2A40', secondary: mode === 'dark' ? '#A6B7CD' : '#65758D' },
      divider: mode === 'dark' ? '#2A3A50' : '#E6EBF3',
      success: { main: mode === 'dark' ? '#62DDB3' : '#087D60' },
      warning: { main: mode === 'dark' ? '#FFC078' : '#A95E05' },
      error: { main: mode === 'dark' ? '#FF969C' : '#C13439' },
      info: { main: mode === 'dark' ? '#8BC8FF' : '#2368AC' },
    },
    typography: {
      fontFamily: 'Inter, "Noto Sans", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
      fontSize: 14,
      h4: { fontSize: '1.625rem', fontWeight: 750, letterSpacing: '-.75px', lineHeight: 1.3 },
      h5: { fontWeight: 700, letterSpacing: '-.5px' },
      h6: { fontSize: '1rem', fontWeight: 700, letterSpacing: '-.25px' },
      body1: { fontSize: '.875rem', lineHeight: 1.7 },
      body2: { fontSize: '.8125rem', lineHeight: 1.65 },
      caption: { fontSize: '.725rem', lineHeight: 1.6 },
      button: { textTransform: 'none', fontWeight: 650, letterSpacing: 0 },
    },
    shape: { borderRadius: 8 },
    components: {
      MuiCssBaseline: { styleOverrides: (theme) => ({ '::selection': { backgroundColor: alpha(theme.palette.primary.main, .28), color: theme.palette.text.primary }, body: { backgroundImage: 'none' } }) },
      MuiButtonBase: { styleOverrides: { root: ({ theme }) => ({ '&.Mui-focusVisible': { outline: `2px solid ${theme.palette.primary.main}`, outlineOffset: 2 } }) } },
      MuiButton: { defaultProps: { disableElevation: true }, styleOverrides: { root: { borderRadius: 8, minHeight: 38, paddingInline: 15, fontSize: 13, fontWeight: 650, boxShadow: 'none' }, outlined: ({ theme }) => ({ borderColor: theme.palette.divider, backgroundColor: theme.palette.background.paper, color: theme.palette.text.primary, '&:hover': { borderColor: theme.palette.primary.main, backgroundColor: alpha(theme.palette.primary.main, .04) } }), sizeSmall: { minHeight: 32, paddingInline: 11, fontSize: 12 } } },
      MuiButtonGroup: { styleOverrides: { root: { boxShadow: 'none' }, grouped: { boxShadow: 'none !important' } } },
      MuiIconButton: { styleOverrides: { root: ({ theme }) => ({ color: theme.palette.text.secondary, borderRadius: 8 }) } },
      MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { backgroundImage: 'none' }, outlined: ({ theme }) => ({ borderColor: theme.palette.divider, borderRadius: 12, boxShadow: theme.palette.mode === 'light' ? '0 2px 5px rgba(24,43,72,.025)' : 'none' }) } },
      MuiTableCell: { styleOverrides: { root: { paddingBlock: 12, paddingInline: 16 }, head: ({ theme }) => ({ color: theme.palette.text.secondary, fontSize: 11, fontWeight: 700, letterSpacing: '.25px', backgroundColor: theme.palette.mode === 'light' ? '#F8FAFD' : alpha(theme.palette.background.default, .45), whiteSpace: 'nowrap', borderColor: theme.palette.divider }), body: ({ theme }) => ({ fontSize: 13, borderColor: theme.palette.divider }) } },
      MuiTableRow: { styleOverrides: { root: { '&:last-child td': { borderBottom: 0 } }, hover: ({ theme }) => ({ '&:hover': { backgroundColor: alpha(theme.palette.primary.main, .025) } }) } },
      MuiTextField: { defaultProps: { size: 'small' } },
      MuiOutlinedInput: { styleOverrides: { root: ({ theme }) => ({ borderRadius: 8, fontSize: 13, backgroundColor: theme.palette.background.paper, '& .MuiOutlinedInput-notchedOutline': { borderColor: theme.palette.mode === 'light' ? '#DCE4EF' : theme.palette.divider }, '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: theme.palette.mode === 'light' ? '#B8C7DC' : '#526982' } }) } },
      MuiInputLabel: { styleOverrides: { root: { fontSize: 13 } } },
      MuiChip: { styleOverrides: { root: { fontWeight: 600, borderRadius: 6, fontSize: 11.5 }, sizeSmall: { height: 23 } } },
      MuiDialog: { styleOverrides: { paper: { borderRadius: 16 } } },
      MuiDialogTitle: { styleOverrides: { root: { fontSize: 19, fontWeight: 700, letterSpacing: '-.4px', padding: '24px 24px 18px' } } },
      MuiDialogActions: { styleOverrides: { root: ({ theme }) => ({ borderTop: `1px solid ${theme.palette.divider}`, gap: 4 }) } },
      MuiTablePagination: { styleOverrides: { toolbar: { minHeight: 52 }, selectLabel: { fontSize: 12 }, displayedRows: { fontSize: 12 } } },
      MuiTab: { styleOverrides: { root: { textTransform: 'none', minHeight: 42, fontSize: 13 } } },
      MuiTabs: { styleOverrides: { root: { minHeight: 42 } } },
      MuiAccordion: { styleOverrides: { root: { boxShadow: 'none', '&:before': { display: 'none' } } } },
      MuiMenu: { styleOverrides: { paper: ({ theme }) => ({ border: `1px solid ${theme.palette.divider}`, borderRadius: 12, boxShadow: theme.palette.mode === 'light' ? '0 12px 40px rgba(24, 43, 72, .12)' : '0 12px 40px rgba(0,0,0,.3)' }) } },
      MuiMenuItem: { styleOverrides: { root: { fontSize: 13, minHeight: 40 } } },
      MuiAlert: { styleOverrides: { root: { borderRadius: 8, fontSize: 12.5 }, message: { minWidth: 0 }, icon: { fontSize: 20 } } },
      MuiSkeleton: { styleOverrides: { root: { borderRadius: 6 } } },
      MuiTooltip: { styleOverrides: { tooltip: { fontSize: 11.5, borderRadius: 6, padding: '7px 10px' } } },
    },
  }), [mode]);
  return <AppearanceContext.Provider value={{ preference, setPreference }}><ThemeProvider theme={theme}><CssBaseline enableColorScheme />{children}</ThemeProvider></AppearanceContext.Provider>;
}

function useAppearance() { const value = useContext(AppearanceContext); if (!value) throw new Error('AppearanceProvider is required.'); return value; }
export function AppearanceMenu() {
  const { preference, setPreference } = useAppearance(); const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const current = options.find((option) => option.value === preference)!;
  return <><Tooltip title={`Giao diện: ${current.label}`}><IconButton id="appearance-menu-button" aria-label="Chọn giao diện" aria-haspopup="menu" aria-controls={anchor ? 'appearance-menu' : undefined} aria-expanded={Boolean(anchor)} onClick={(event) => setAnchor(event.currentTarget)}>{current.icon}</IconButton></Tooltip>
    <Menu id="appearance-menu" anchorEl={anchor} open={Boolean(anchor)} onClose={() => setAnchor(null)} slotProps={{ list: { 'aria-labelledby': 'appearance-menu-button' } }}>
      {options.map((option) => <MenuItem key={option.value} selected={preference === option.value} onClick={() => { setPreference(option.value); setAnchor(null); }}><ListItemIcon>{preference === option.value ? <CheckOutlined fontSize="small" /> : option.icon}</ListItemIcon>{option.label}</MenuItem>)}
    </Menu></>;
}
export function AppearanceSelect() {
  const { preference, setPreference } = useAppearance();
  return <TextField select label="Giao diện trình duyệt" value={preference} onChange={(event) => { if (valid(event.target.value)) setPreference(event.target.value); }} sx={{ width: { xs: '100%', sm: 260 } }}>{options.map((option) => <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>)}</TextField>;
}
