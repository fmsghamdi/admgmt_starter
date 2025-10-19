import React, { useEffect, useMemo, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Stack,
  Typography,
  Chip,
  CircularProgress,
  Divider,
} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { useSnackbar } from "notistack";

// يضمن إضافة /api حتى لو متغير البيئة ما فيه /api
const RAW = import.meta.env.VITE_API_URL ?? "http://localhost:5000";
const API = (RAW.endsWith("/api") ? RAW : RAW.replace(/\/+$/, "") + "/api");


type Props = {
  open: boolean;
  /** ابعث واحد فقط: sam أو dn */
  sam?: string;
  dn?: string;
  onClose: () => void;
};

type ADObjectDetailsVm = {
  name: string;
  distinguishedName: string;
  samAccountName?: string | null;
  objectClass: "user" | "group" | "computer" | "other";
  email?: string | null;
  enabled?: boolean | null;
  locked?: boolean | null;
  lastLogonUtc?: string | null;
  // ممكن يجي حقول إضافية داخل Extra
  extra?: Record<string, unknown>;
};

export default function UserDetailsModal({ open, sam, dn, onClose }: Props) {
  const { enqueueSnackbar } = useSnackbar();
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<ADObjectDetailsVm | null>(null);

  const queryString = useMemo(() => {
    const u = new URLSearchParams();
    if (sam) u.set("sam", sam);
    else if (dn) u.set("dn", dn);
    return u.toString();
  }, [sam, dn]);

  useEffect(() => {
    let alive = true;
    async function run() {
      if (!open || !queryString) {
        setData(null);
        return;
      }
      try {
        setLoading(true);
        const res = await fetch(`${API}/users/details?${queryString}`, {
          headers: { Accept: "application/json" },
        });
        if (!res.ok) throw new Error(`API ${res.status}`);
        const json: ADObjectDetailsVm = await res.json();
        if (alive) setData(json);
      } catch (e: any) {
        console.error(e);
        enqueueSnackbar(`Failed to load details: ${e.message ?? e}`, { variant: "error" });
        if (alive) setData(null);
      } finally {
        if (alive) setLoading(false);
      }
    }
    run();
    return () => {
      alive = false;
    };
  }, [open, queryString, enqueueSnackbar]);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        <InfoOutlinedIcon fontSize="small" />
        User Properties
      </DialogTitle>

      <DialogContent dividers>
        {loading ? (
          <Stack py={4} alignItems="center" justifyContent="center">
            <CircularProgress size={24} />
          </Stack>
        ) : data ? (
          <Stack spacing={1.5}>
            <Typography variant="h6">{data.name}</Typography>
            <Typography variant="body2" sx={{ opacity: 0.8, wordBreak: "break-all" }}>
              {data.distinguishedName}
            </Typography>

            <Stack direction="row" spacing={1} mt={0.5}>
              <Chip size="small" label={data.objectClass} />
              <Chip
                size="small"
                color={data.enabled ? "success" : "default"}
                label={data.enabled ? "enabled" : "disabled"}
              />
              {data.locked ? <Chip size="small" color="error" label="locked" /> : null}
            </Stack>

            <Divider sx={{ my: 1 }} />

            <Row label="SAM" value={data.samAccountName ?? "-"} />
            <Row label="Email" value={data.email ?? "-"} />
            <Row label="Last Logon" value={data.lastLogonUtc ? new Date(data.lastLogonUtc).toLocaleString() : "-"} />

            {/* قيم إضافية إن وجدت داخل Extra */}
            {data.extra ? (
              <>
                <Divider sx={{ my: 1 }} />
                {Object.entries(data.extra).map(([k, v]) => (
                  <Row key={k} label={k} value={formatValue(v)} />
                ))}
              </>
            ) : null}
          </Stack>
        ) : (
          <Typography variant="body2">No data.</Typography>
        )}
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}

/* ============== Helpers ============== */
function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
      <Typography variant="body2" sx={{ minWidth: 140, fontWeight: 600 }}>
        {label}
      </Typography>
      <Typography variant="body2" sx={{ wordBreak: "break-all" }}>
        {value}
      </Typography>
    </Stack>
  );
}

function formatValue(v: unknown): string {
  if (v == null) return "-";
  if (Array.isArray(v)) return v.join(", ");
  if (typeof v === "object") return JSON.stringify(v);
  return String(v);
}
