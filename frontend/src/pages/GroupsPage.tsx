import { useEffect, useMemo, useRef, useState } from "react";
import {
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import FilterAltIcon from "@mui/icons-material/FilterAlt";
import PlayCircleIcon from "@mui/icons-material/PlayCircle";
import DownloadIcon from "@mui/icons-material/Download";
import AddIcon from "@mui/icons-material/Add";
import RemoveIcon from "@mui/icons-material/Remove";
import GroupIcon from "@mui/icons-material/Groups";
import InfoIcon from "@mui/icons-material/Info";

const API = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

type GroupRow = {
  name: string;                // Sam/Name
  description?: string;
  type?: "security" | "distribution" | "unknown";
  memberCount?: number;        // إذا توفّر من الـ API (اختياري)
};

type MemberVm = {
  samAccountName: string;
  displayName?: string;
  type: "user" | "group" | "computer" | "unknown";
};

type TypeFilter = "any" | "security" | "distribution";
type SizeFilter = "any" | "empty" | "lt10" | "lt50" | "gte50";

export default function GroupsPage() {
  // ========== الأساس ==========
  const [rows, setRows] = useState<GroupRow[]>([]);
  const [total, setTotal] = useState(0);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(false);

  // ========== الأعضاء/العد ==========
  const [members, setMembers] = useState<Record<string, MemberVm[]>>({});
  const [loadingCounts, setLoadingCounts] = useState(false);
  const triedMembersForKey = useRef<string>("");

  // ========== التحديد ==========
  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const selectedCount = useMemo(
    () => Object.values(selected).filter(Boolean).length,
    [selected]
  );
  const picked = useMemo(
    () => rows.filter((g) => selected[g.name]),
    [rows, selected]
  );

  // ========== الفلاتر ==========
  const [typeFilter, setTypeFilter] = useState<TypeFilter>("any");
  const [sizeFilter, setSizeFilter] = useState<SizeFilter>("any");
  const [missingDescOnly, setMissingDescOnly] = useState(false);

  // ========== النوافذ ==========
  const [openAdd, setOpenAdd] = useState(false);
  const [openRemove, setOpenRemove] = useState(false);
  const [userSam, setUserSam] = useState("");

  // ---------- جلب المجموعات ----------
  const fetchGroups = async () => {
    setLoading(true);
    try {
      const res = await fetch(
        `${API}/api/groups?search=${encodeURIComponent(q)}&take=200&skip=0`,
        { credentials: "include" }
      );
      const json = await res.json();
      const list: GroupRow[] = json.items ?? json ?? [];
      setRows(list);
      setTotal(json.total ?? list.length);

      const sel: Record<string, boolean> = {};
      list.forEach((g) => (sel[g.name] = false));
      setSelected(sel);

      triedMembersForKey.current = ""; // ابطل الكاش للعد
    } catch {
      alert("Failed to load groups");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchGroups();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ---------- تحميل الأعضاء (لحساب العدّ عند الحاجة) ----------
  const ensureCountsIfNeeded = async () => {
    // نحتاج العد إذا لم يكن الفلتر "any"
    const needsCounts = sizeFilter !== "any";
    if (!needsCounts) return;

    const key = JSON.stringify(rows.map((r) => r.name));
    if (triedMembersForKey.current === key) return;

    setLoadingCounts(true);
    const map: Record<string, MemberVm[]> = { ...members };
    try {
      const names = rows.map((g) => g.name);
      const chunk = 10;

      for (let i = 0; i < names.length; i += chunk) {
        const slice = names.slice(i, i + chunk);
        await Promise.allSettled(
          slice.map(async (name) => {
            try {
              // GET /api/groups/members?name=<group>
              const r = await fetch(
                `${API}/api/groups/members?name=${encodeURIComponent(name)}`,
                { credentials: "include" }
              );
              if (!r.ok) return;
              const arr: MemberVm[] = await r.json();
              map[name] = Array.isArray(arr) ? arr : [];
            } catch {
              // تجاهل الخطأ لكل مجموعة
            }
          })
        );
      }

      setMembers(map);
      triedMembersForKey.current = key;
    } finally {
      setLoadingCounts(false);
    }
  };

  useEffect(() => {
    ensureCountsIfNeeded();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sizeFilter, rows]);

  // ---------- الفلترة ----------
  const filtered = useMemo(() => {
    let list = rows;

    if (typeFilter !== "any") {
      list = list.filter((g) =>
        g.type ? g.type === typeFilter : true
      );
    }

    if (missingDescOnly) {
      list = list.filter((g) => !g.description || g.description.trim() === "");
    }

    if (sizeFilter !== "any") {
      list = list.filter((g) => {
        const count =
          g.memberCount ??
          (members[g.name] ? members[g.name].length : undefined);

        if (count === undefined) return true;

        switch (sizeFilter) {
          case "empty":
            return count === 0;
          case "lt10":
            return count < 10;
          case "lt50":
            return count < 50;
          case "gte50":
            return count >= 50;
          default:
            return true;
        }
      });
    }

    return list;
  }, [rows, typeFilter, missingDescOnly, sizeFilter, members]);

  // ---------- تحديد الكل ----------
  const toggleAll = (value: boolean) => {
    const copy: Record<string, boolean> = {};
    filtered.forEach((g) => (copy[g.name] = value));
    setSelected(copy);
  };

  // ---------- عمليات جماعية ----------
  const bulkAddUser = async () => {
    if (!picked.length || !userSam) return;
    const tasks = picked.map((g) =>
      fetch(`${API}/api/groups/add-user`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ groupName: g.name, samAccountName: userSam }),
        credentials: "include",
      })
    );
    await Promise.allSettled(tasks);
    setOpenAdd(false);
    setUserSam("");
    triedMembersForKey.current = "";
    alert("Add to group: done (check server logs for any failures).");
  };

  const bulkRemoveUser = async () => {
    if (!picked.length || !userSam) return;
    const tasks = picked.map((g) =>
      fetch(`${API}/api/groups/remove-user`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ groupName: g.name, samAccountName: userSam }),
        credentials: "include",
      })
    );
    await Promise.allSettled(tasks);
    setOpenRemove(false);
    setUserSam("");
    triedMembersForKey.current = "";
    alert("Remove from group: done.");
  };

  // ---------- تصدير CSV ----------
  const exportCsv = () => {
    const header = ["name", "description", "type", "memberCount"];
    const lines = [header.join(",")].concat(
      filtered.map((g) => {
        const count =
          g.memberCount ??
          (members[g.name] ? members[g.name].length : undefined);

        const values = [
          g.name,
          g.description ?? "",
          g.type ?? "unknown",
          count ?? "",
        ];
        return values
          .map((v) => `"${(v ?? "").toString().replace(/"/g, '""')}"`)
          .join(",");
      })
    );
    const blob = new Blob([lines.join("\n")], {
      type: "text/csv;charset=utf-8",
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `groups_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  // ---------- UI ----------
  return (
    <Card>
      <CardContent>
        {/* الشريط العلوي */}
        <Stack direction="row" alignItems="center" spacing={2} sx={{ mb: 2 }}>
          <TextField
            size="small"
            placeholder="Search groups (name/desc)"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && fetchGroups()}
            sx={{ width: 360 }}
          />
          <Button
            onClick={fetchGroups}
            startIcon={<PlayCircleIcon />}
            variant="contained"
            disabled={loading}
          >
            Search
          </Button>

          <Divider orientation="vertical" flexItem />

          <Button
            startIcon={<AddIcon />}
            disabled={!selectedCount}
            onClick={() => setOpenAdd(true)}
          >
            Add User to selected
          </Button>
          <Button
            startIcon={<RemoveIcon />}
            disabled={!selectedCount}
            onClick={() => setOpenRemove(true)}
          >
            Remove User from selected
          </Button>

          <Tooltip title="Export filtered groups as CSV">
            <IconButton onClick={exportCsv} sx={{ ml: "auto" }}>
              <DownloadIcon />
            </IconButton>
          </Tooltip>
        </Stack>

        {/* الفلاتر */}
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={2}
          alignItems="center"
          sx={{ mb: 2 }}
        >
          <Chip icon={<FilterAltIcon />} label="Filters" variant="outlined" />

          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel>Type</InputLabel>
            <Select
              label="Type"
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value as TypeFilter)}
            >
              <MenuItem value="any">Any</MenuItem>
              <MenuItem value="security">Security</MenuItem>
              <MenuItem value="distribution">Distribution</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 200 }}>
            <InputLabel>Size</InputLabel>
            <Select
              label="Size"
              value={sizeFilter}
              onChange={(e) => setSizeFilter(e.target.value as SizeFilter)}
            >
              <MenuItem value="any">Any</MenuItem>
              <MenuItem value="empty">Empty only</MenuItem>
              <MenuItem value="lt10">&lt; 10 members</MenuItem>
              <MenuItem value="lt50">&lt; 50 members</MenuItem>
              <MenuItem value="gte50">&ge; 50 members</MenuItem>
            </Select>
          </FormControl>

          <Stack direction="row" alignItems="center" spacing={1}>
            <Checkbox
              checked={missingDescOnly}
              onChange={(e) => setMissingDescOnly(e.target.checked)}
            />
            <Typography>Missing description only</Typography>
          </Stack>

          {loadingCounts && (
            <Stack direction="row" alignItems="center" spacing={1}>
              <CircularProgress size={18} />
              <Typography variant="body2" color="text.secondary">
                Loading member counts…
              </Typography>
            </Stack>
          )}
        </Stack>

        {/* ملخص التحديد */}
        <Box sx={{ mb: 1, display: "flex", alignItems: "center", gap: 2 }}>
          <Checkbox
            checked={selectedCount > 0 && selectedCount === filtered.length}
            indeterminate={selectedCount > 0 && selectedCount < filtered.length}
            onChange={(e) => toggleAll(e.target.checked)}
          />
          <Typography variant="body2">
            {selectedCount} selected / {filtered.length} shown (of {total} total)
          </Typography>

          <Stack direction="row" alignItems="center" spacing={1} sx={{ ml: 2 }}>
            <InfoIcon fontSize="small" color="disabled" />
            <Typography variant="body2" color="text.secondary">
              حجم المجموعة يعتمد على{" "}
              <code>/api/groups/members?name=</code>. إذا غير موجود، قد تظهر “—”.
            </Typography>
          </Stack>
        </Box>

        {/* الجدول */}
        <Box
          sx={{
            border: "1px solid #e0e0e0",
            borderRadius: 1,
            overflow: "hidden",
          }}
        >
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: "200px 1fr 180px 140px",
              bgcolor: "#fafafa",
              p: 1,
              fontWeight: 600,
            }}
          >
            <div />
            <div>Group (name)</div>
            <div>Description</div>
            <div>Members</div>
          </Box>

          {filtered.map((g) => {
            const count =
              g.memberCount ??
              (members[g.name] ? members[g.name].length : undefined);
            return (
              <Box
                key={g.name}
                sx={{
                  display: "grid",
                  gridTemplateColumns: "200px 1fr 180px 140px",
                  alignItems: "center",
                  p: 1,
                  borderTop: "1px solid #eee",
                }}
              >
                <Checkbox
                  checked={!!selected[g.name]}
                  onChange={(e) =>
                    setSelected((s) => ({ ...s, [g.name]: e.target.checked }))
                  }
                />
                <Stack direction="row" spacing={1} alignItems="center">
                  <GroupIcon fontSize="small" />
                  <Typography>{g.name}</Typography>
                </Stack>
                <Typography color="text.secondary">
                  {g.description || "—"}
                </Typography>
                <Typography>{count ?? "—"}</Typography>
              </Box>
            );
          })}
        </Box>
      </CardContent>

      {/* إضافة مستخدم */}
      <Dialog open={openAdd} onClose={() => setOpenAdd(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Add user to selected groups</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            label="User SAM"
            value={userSam}
            onChange={(e) => setUserSam(e.target.value)}
            placeholder="e.g. jdoe"
            sx={{ mt: 1 }}
          />
          <Typography variant="body2" sx={{ mt: 1.5 }} color="text.secondary">
            Groups: {picked.map((p) => p.name).join(", ") || "—"}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenAdd(false)}>Cancel</Button>
          <Button onClick={bulkAddUser} variant="contained" disabled={!userSam || !selectedCount}>
            Add
          </Button>
        </DialogActions>
      </Dialog>

      {/* إزالة مستخدم */}
      <Dialog open={openRemove} onClose={() => setOpenRemove(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Remove user from selected groups</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            label="User SAM"
            value={userSam}
            onChange={(e) => setUserSam(e.target.value)}
            placeholder="e.g. jdoe"
            sx={{ mt: 1 }}
          />
          <Typography variant="body2" sx={{ mt: 1.5 }} color="text.secondary">
            Groups: {picked.map((p) => p.name).join(", ") || "—"}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenRemove(false)}>Cancel</Button>
          <Button onClick={bulkRemoveUser} variant="contained" color="error" disabled={!userSam || !selectedCount}>
            Remove
          </Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
}
