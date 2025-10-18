import React, { useEffect, useMemo, useState, useCallback } from "react";
import {
  Box, Button, Card, CardContent, Chip, FormControl, InputLabel,
  MenuItem, Select, Stack, TextField, Typography, IconButton
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import SearchIcon from "@mui/icons-material/Search";
import InfoIcon from "@mui/icons-material/Info";
import { DataGrid, GridColDef, GridPaginationModel, GridSortModel } from "@mui/x-data-grid";
import { useSnackbar } from "notistack";
import UserDetailsModal from "./components/UserDetailsModal";

type ADUserVm = {
  samAccountName: string;
  displayName: string;
  email: string;
  enabled: boolean;
  lastLogon?: string | null;
};

const API = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

export default function UsersPage() {
  const { enqueueSnackbar } = useSnackbar();

  // filters
  const [q, setQ] = useState<string>("");
  const [status, setStatus] = useState<"any" | "enabled" | "disabled" | "locked">("any");
  const [ouDn, setOuDn] = useState<string>("");

  // grid
  const [rows, setRows] = useState<ADUserVm[]>([]);
  const [rowCount, setRowCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(false);
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({ page: 0, pageSize: 100 });
  const [sortModel, setSortModel] = useState<GridSortModel>([{ field: "displayName", sort: "asc" }]);

  // details modal
  const [detailsSam, setDetailsSam] = useState<string | null>(null);

  const columns: GridColDef<ADUserVm>[] = useMemo(
    () => [
      {
        field: "displayName",
        headerName: "Display Name",
        flex: 1,
        minWidth: 220,
        renderCell: (p) => (
          <Stack direction="row" spacing={1} alignItems="center" sx={{ width: "100%" }}>
            <Typography variant="body2" sx={{ flexGrow: 1 }}>
              {p.row.displayName || p.row.samAccountName}
            </Typography>
            <IconButton
              size="small"
              onClick={(e) => {
                e.stopPropagation();
                setDetailsSam(p.row.samAccountName);
              }}
            >
              <InfoIcon fontSize="small" />
            </IconButton>
          </Stack>
        ),
      },
      { field: "samAccountName", headerName: "SAM", minWidth: 140, flex: 0.6 },
      {
        field: "email",
        headerName: "Email",
        flex: 1,
        minWidth: 220,
        // v7: valueFormatter(value) — لا نستخدم p.value
        valueFormatter: (value) => (value ? String(value) : "-"),
      },
      {
        field: "enabled",
        headerName: "Status",
        width: 120,
        type: "boolean",
        renderCell: (p) => (
          <Chip
            label={p.row.enabled ? "enabled" : "disabled"}
            color={p.row.enabled ? "success" : "warning"}
            size="small"
            sx={{ fontWeight: 600 }}
          />
        ),
      },
      {
        field: "lastLogon",
        headerName: "Last Logon",
        flex: 1,
        minWidth: 180,
        valueFormatter: (value) => (value ? new Date(String(value)).toLocaleString() : "-"),
      },
    ],
    []
  );

  const runSearch = useCallback(async () => {
    setLoading(true);
    try {
      const page = paginationModel.page;
      const pageSize = paginationModel.pageSize;
      const sortBy = sortModel[0]?.field ?? "displayName";
      const sortDir = sortModel[0]?.sort ?? "asc";

      const params = new URLSearchParams();
      if (q) params.set("q", q);
      if (ouDn) params.set("ouDn", ouDn);
      if (status === "enabled") params.set("enabled", "true");
      else if (status === "disabled") params.set("enabled", "false");
      else if (status === "locked") params.set("locked", "true");

      params.set("skip", String(page * pageSize));
      params.set("take", String(pageSize));
      params.set("sortBy", sortBy);
      params.set("sortDir", sortDir);

      const res = await fetch(`${API}/api/users?${params.toString()}`, {
        headers: { Accept: "application/json" },
        credentials: "include",
      });
      if (!res.ok) throw new Error(`API ${res.status}`);

      const data = await res.json(); // { items, total }
      setRows(Array.isArray(data.items) ? data.items : []);
      setRowCount(Number(data.total) || 0);
    } catch (err: any) {
      enqueueSnackbar(`Failed to load users: ${err.message ?? err}`, { variant: "error" });
      setRows([]);
      setRowCount(0);
    } finally {
      setLoading(false);
    }
  }, [API, q, ouDn, status, paginationModel, sortModel, enqueueSnackbar]);

  useEffect(() => {
    runSearch();
  }, [runSearch]);

  return (
    <Box sx={{ p: 2 }}>
      <Card variant="outlined">
        <CardContent>
          <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2, flexWrap: "wrap" }}>
            <TextField
              size="small"
              fullWidth
              label="Search users (displayName / SAM / email)"
              value={q}
              onChange={(e) => setQ(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && runSearch()}
            />
            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel>Status</InputLabel>
              <Select label="Status" value={status} onChange={(e) => setStatus(e.target.value as any)}>
                <MenuItem value="any">Any</MenuItem>
                <MenuItem value="enabled">Enabled</MenuItem>
                <MenuItem value="disabled">Disabled</MenuItem>
                <MenuItem value="locked">Locked</MenuItem>
              </Select>
            </FormControl>
            <TextField
              size="small"
              label="OU DN (optional)"
              value={ouDn}
              onChange={(e) => setOuDn(e.target.value)}
              sx={{ minWidth: 320 }}
              placeholder="OU=Colleges,DC=UQU,DC=LOCAL"
            />
            <Button
              variant="contained"
              startIcon={<SearchIcon />}
              onClick={() => {
                // رجّع للصفحة الأولى عند تغيير الفلاتر
                setPaginationModel((m) => ({ ...m, page: 0 }));
                runSearch();
              }}
            >
              Search
            </Button>
            <IconButton onClick={runSearch}><RefreshIcon /></IconButton>
          </Stack>

          <div style={{ height: 560, width: "100%" }}>
            <DataGrid<ADUserVm>
              rows={rows}
              getRowId={(r) => r.samAccountName}
              columns={columns}
              rowCount={rowCount}
              loading={loading}
              paginationMode="server"
              sortingMode="server"
              paginationModel={paginationModel}
              onPaginationModelChange={setPaginationModel}
              sortModel={sortModel}
              onSortModelChange={setSortModel}
              disableRowSelectionOnClick
              onRowDoubleClick={(p) => setDetailsSam(p.row.samAccountName)}
            />
          </div>
        </CardContent>
      </Card>

      <UserDetailsModal sam={detailsSam} onClose={() => setDetailsSam(null)} />
    </Box>
  );
}
