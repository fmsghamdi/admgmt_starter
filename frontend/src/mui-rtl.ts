// Simple RTL setup for MUI
import { createTheme } from '@mui/material/styles';

export const theme = (dir: 'ltr' | 'rtl') => createTheme({
  direction: dir
});
