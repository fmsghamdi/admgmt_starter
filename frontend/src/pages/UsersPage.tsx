// frontend/src/pages/UsersPage.tsx
import React, { useCallback, useEffect, useMemo, useState } from "react";
import {
  Box,
  Stack,
  TextField,
  Button,
  Typography,
  IconButton,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RefreshIcon from "@mui/icons-material/Refresh";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import {
  DataGrid,
  GridColDef,
  GridRenderCellParams,
  GridPaginationModel,
  // ملاحظة: لا نستخدم GridValueFormatterParams / GridValueGetterParams
  // وإن احتجت تايبز: يوجد GridValueFormatter و GridValueGetter في v6
} from "@mui/x-data-grid";
import { useSnackbar } from "notistack";
import UserDetailsModal from "./components/UserDetailsModal";

// يضمن إضافة /api حتى لو متغير البيئة ما فيه /api
const RAW = import.meta.env.VITE_API_URL ?? "http://localhost:5000";
const API = (RAW.endsWith("/api") ? RAW : RAW.replace(/\/+$/, "") + "/api");


/* ================= Types ================= */
export type ADUserVm = {
  samAccountName: string;
  displayName: string;
  email?: string | null;
  enabled?: boolean | null;
  lastLogonUtc?: string | null;
};

type UsersApiResponse = {
  items: ADUserVm[];
  total: number;
  error?: string;
};

/* =============== Component =============== */
export default function UsersPage() {
  const { enqueueSnackbar } = useSnackbar();

  // البحث والفلاتر
  const [q, setQ] = useState("");
  const [status, setStatus] = useState<"any" | "enabled" | "disabled" | "locked">("any");
  const [ouDn, setOuDn] = useState("");

  // الجدول
  const [rows, setRows] = useState<ADUserVm[]>([]);
  const [rowCount, setRowCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [paginationModel, setPaginationModel] = useState<GridPaginationModel>({
    page: 0,
    pageSize: 100,
  });

  // نافذة التفاصيل
  const [detailsSam, setDetailsSam] = useState<string | null>(null);

  /* -------- الأعمدة -------- */
  const columns = useMemo<GridColDef<ADUserVm>[]>(
    () => [
      {
        field: "displayName",
        headerName: "Display Name",
        flex: 1,
        minWidth: 220,
        // لا نستخدم GridValueGetterParams؛ نكتب دالة مباشرة
        valueGetter: (_value, row) => row.displayName || row.samAccountName || "",
        renderCell: (p: GridRenderCellParams<ADUserVm, string>) => (
          <Stack direction="row" alignItems="center" spacing={1} sx={{ width: "100%" }}>
            <Typography
              variant="body2"
              sx={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis" }}
            >
              {p.value}
            </Typography>
            <IconButton
              size="small"
              onClick={(e) => {
                e.stopPropagation();
                setDetailsSam(p.row.samAccountName);
              }}
              title="User details"
            >
              <InfoOutlinedIcon fontSize="small" />
            </IconButton>
          </Stack>
        ),
      },
      { field: "samAccountName", headerName: "SAM", flex: 0.7, minWidth: 140 },
      {
        field: "email",
        headerName: "Email",
        flex: 1,
        minWidth: 220,
        // لا نستخدم GridValueFormatterParams
        valueFormatter: (value) => (value ?? "-") as string,
      },
      {
        field: "enabled",
        headerName: "Status",
        width: 120,
        type: "boolean",
        renderCell: (p: GridRenderCellParams<ADUserVm, boolean | null | undefined>) => (
          <Typography variant="body2">{p.value ? "enabled" : "disabled"}</Typography>
        ),
      },
      {
        field: "lastLogonUtc",
        headerName: "Last Logon",
        width: 180,
        valueFormatter: (value) =>
            value ? new Date(String(value)).toLocaleString() : "-",
      },
    ],
    []
  );

  /* -------- جلب المستخدمين -------- */
  const fetchUsers = useCallback(async () => {
    try {
      setLoading(true);

      const take = paginationModel.pageSize;
      const skip = paginationModel.page * paginationModel.pageSize;

      const url = new URL(`${API}/users`);
      const params = new URLSearchParams();
      if (q.trim()) params.set("q", q.trim());
      if (ouDn.trim()) params.set("ouDn", ouDn.trim());
      if (status !== "any") params.set("status", status);
      params.set("take", String(take));
      params.set("skip", String(skip));
      url.search = params.toString();

      const res = await fetch(url.toString(), { headers: { Accept: "application/json" } });
      if (!res.ok) throw new Error(`API ${res.status}`);

      const data: UsersApiResponse = await res.json();
      const items = Array.isArray(data.items) ? data.items : [];
      const total = Number.isFinite(data.total) ? data.total : items.length;

      setRows(items);
      setRowCount(total);
    } catch (err: any) {
      console.error(err);
      enqueueSnackbar(`Failed to load users: ${err.message ?? err}`, { variant: "error" });
      setRows([]);
      setRowCount(0);
    } finally {
      setLoading(false);
    }
  }, [API, q, ouDn, status, paginationModel.page, paginationModel.pageSize, enqueueSnackbar]);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  const onSearchClick = () => {
    setPaginationModel((prev) => ({ ...prev, page: 0 }));
    fetchUsers();
  };

  const onRefresh = () => fetchUsers();

  /* --------------- الواجهة --------------- */
  return (
    <Box sx={{ p: 2 }}>
      {/* البحث */}
      <Stack
        direction={{ xs: "column", md: "row" }}
        spacing={2}
        alignItems={{ xs: "stretch", md: "center" }}
        sx={{ mb: 2 }}
      >
        <TextField
          label="Search users (displayName / SAM / email)"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          fullWidth
          size="small"
        />

        <FormControl size="small" sx={{ minWidth: 160 }}>
          <InputLabel>Status</InputLabel>
          <Select
            label="Status"
            value={status}
            onChange={(e) => setStatus(e.target.value as any)}
          >
            <MenuItem value="any">Any</MenuItem>
            <MenuItem value="enabled">Enabled</MenuItem>
            <MenuItem value="disabled">Disabled</MenuItem>
            <MenuItem value="locked">Locked</MenuItem>
          </Select>
        </FormControl>

        <TextField
          label="OU DN (optional)"
          value={ouDn}
          onChange={(e) => setOuDn(e.target.value)}
          size="small"
          sx={{ minWidth: 260 }}
        />

        <Button variant="contained" startIcon={<SearchIcon />} onClick={onSearchClick} sx={{ minWidth: 140 }}>
          SEARCH
        </Button>

        <IconButton onClick={onRefresh} title="Refresh">
          <RefreshIcon />
        </IconButton>
      </Stack>

      {/* الجدول */}
      <Box sx={{ height: 560 }}>
        <DataGrid
          columns={columns}
          rows={rows}
          loading={loading}
          getRowId={(r) => r.samAccountName}
          paginationMode="server"
          rowCount={rowCount}
          paginationModel={paginationModel}
          onPaginationModelChange={setPaginationModel}
          pageSizeOptions={[25, 50, 100, 200]}
          disableRowSelectionOnClick
          density="compact"
        />
      </Box>

      {/* نافذة التفاصيل */}
      <UserDetailsModal
        open={!!detailsSam}
        sam={detailsSam ?? ""}
        onClose={() => setDetailsSam(null)}
      />
    </Box>
  );
}
