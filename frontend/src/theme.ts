import { createTheme } from "@mui/material/styles";

declare module '@mui/material/styles' {
  interface Palette {
    brand: Palette['primary'];
  }
  interface PaletteOptions {
    brand?: PaletteOptions['primary'];
  }
}

export function createAppTheme(
  mode: "light" | "dark",
  dir: "ltr" | "rtl" = "ltr"
) {
  const isDark = mode === "dark";

  return createTheme({
    direction: dir,
    palette: {
      mode,
      primary: {
        main: isDark ? "#7aa2ff" : "#3f51b5",
      },
      secondary: {
        main: isDark ? "#64d2ff" : "#0097a7",
      },
      background: {
        default: isDark ? "#0f1420" : "#f7f8fb",
        paper: isDark ? "#131a2a" : "#ffffff",
      },
      brand: {
        main: isDark ? "#6c8af7" : "#3949ab",
      },
    },
    typography: {
      fontFamily:
        'Inter, "Segoe UI", Tahoma, Arial, "Helvetica Neue", sans-serif',
    },
    shape: {
      borderRadius: 10,
    },
    components: {
      MuiCard: {
        styleOverrides: {
          root: {
            boxShadow:
              "0 2px 8px rgba(0,0,0,.06), 0 1px 2px rgba(0,0,0,.05)",
          },
        },
      },
      MuiButton: {
        defaultProps: { variant: "contained" },
      },
    },
  });
}
