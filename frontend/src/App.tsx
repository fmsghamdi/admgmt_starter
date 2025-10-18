import React, { useMemo, useState, useEffect } from "react";
import { createTheme, ThemeProvider, CssBaseline } from "@mui/material";
import {
  AppBar,
  Toolbar,
  Typography,
  IconButton,
  Button,
  Box,
  Stack,
} from "@mui/material";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import LightModeIcon from "@mui/icons-material/LightMode";
import { SnackbarProvider } from "notistack";
import { BrowserRouter, Routes, Route, Link, useLocation } from "react-router-dom";

// صفحاتك الحالية (موجودة بالمشروع)
import OUsTreePage from "./pages/OUsTreePage";
import GroupsPage from "./pages/GroupsPage";
// الصفحة اللي رجعناها الآن
import UsersPage from "./pages/UsersPage";

function TopBar({
  mode,
  toggleMode,
}: {
  mode: "light" | "dark";
  toggleMode: () => void;
}) {
  const location = useLocation();
  const active = (path: string) => location.pathname === path;

  return (
    <AppBar position="sticky" color="primary" enableColorOnDark>
      <Toolbar>
        <Typography sx={{ flexGrow: 1, fontWeight: 700 }}>AD Management</Typography>
        <Stack direction="row" spacing={1}>
          <Button
            component={Link}
            to="/"
            variant={active("/") ? "contained" : "text"}
            color="inherit"
          >
            OUs
          </Button>
          <Button
            component={Link}
            to="/users"
            variant={active("/users") ? "contained" : "text"}
            color="inherit"
          >
            Users
          </Button>
          <Button
            component={Link}
            to="/groups"
            variant={active("/groups") ? "contained" : "text"}
            color="inherit"
          >
            Groups
          </Button>

          <IconButton
            aria-label="toggle theme"
            onClick={toggleMode}
            color="inherit"
            sx={{ ml: 1 }}
          >
            {mode === "dark" ? <LightModeIcon /> : <DarkModeIcon />}
          </IconButton>
        </Stack>
      </Toolbar>
    </AppBar>
  );
}

export default function App() {
  // حفظ واسترجاع الثيم
  const [mode, setMode] = useState<"light" | "dark">(() => {
    const saved = localStorage.getItem("themeMode");
    return (saved as "light" | "dark") || "light";
  });

  useEffect(() => {
    localStorage.setItem("themeMode", mode);
  }, [mode]);

  const theme = useMemo(
    () =>
      createTheme({
        palette: {
          mode,
          primary: { main: mode === "dark" ? "#5c6bc0" : "#3f51b5" },
          background: {
            default: mode === "dark" ? "#0f1115" : "#fafafa",
            paper: mode === "dark" ? "#151821" : "#fff",
          },
        },
        shape: { borderRadius: 10 },
        components: {
          MuiCard: {
            styleOverrides: { root: { boxShadow: "0 6px 18px rgba(0,0,0,.06)" } },
          },
        },
      }),
    [mode]
  );

  const toggleMode = () => setMode((m) => (m === "dark" ? "light" : "dark"));

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <SnackbarProvider maxSnack={3} autoHideDuration={2500} anchorOrigin={{ vertical: "bottom", horizontal: "right" }}>
        <BrowserRouter>
          <TopBar mode={mode} toggleMode={toggleMode} />
          <Box sx={{ p: 2 }}>
            <Routes>
              <Route path="/" element={<OUsTreePage />} />
              <Route path="/users" element={<UsersPage />} />
              <Route path="/groups" element={<GroupsPage />} />
            </Routes>
          </Box>
        </BrowserRouter>
      </SnackbarProvider>
    </ThemeProvider>
  );
}
