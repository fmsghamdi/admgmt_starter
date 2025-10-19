import { useEffect, useMemo, useState } from "react";
import {
  Box, Card, CardContent, Typography, Stack, IconButton, Button,
  List, ListItemButton, ListItemIcon, ListItemText, Collapse, Divider, CircularProgress, Tooltip,
  TextField, InputAdornment, Dialog, DialogTitle, DialogContent, DialogActions, Chip, Alert, Checkbox, FormControlLabel
} from "@mui/material";
import DomainIcon from "@mui/icons-material/Domain";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import PeopleAltIcon from "@mui/icons-material/PeopleAlt";
import ComputerIcon from "@mui/icons-material/Computer";
import PersonIcon from "@mui/icons-material/Person";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import SearchIcon from "@mui/icons-material/Search";
import LockResetIcon from "@mui/icons-material/LockReset";
import ToggleOnIcon from "@mui/icons-material/ToggleOn";
import ToggleOffIcon from "@mui/icons-material/ToggleOff";
import LockOpenIcon from "@mui/icons-material/LockOpen";

type OUNode = {
  dn: string;
  name: string;
  loaded: boolean;
  open: boolean;
  children: OUNode[];
};

type ADObject = {
  name: string;
  distinguishedName: string;
  samAccountName?: string;
  objectClass: "user" | "group" | "computer" | string;
  disabled?: boolean | null;
};

type ADObjectDetails = {
  name: string;
  distinguishedName: string;
  samAccountName?: string;
  objectClass: string;
  email?: string | null;
  enabled?: boolean | null;
  locked?: boolean | null;
  lastLogonUtc?: string | null;
  extra?: Record<string, string | null>;
};

type PasswordPolicy = {
  minLength: number;
  requireUpper: boolean;
  requireLower: boolean;
  requireDigit: boolean;
  requireSpecial: boolean;
  forceChangeOnResetDefault: boolean;
  lockoutThreshold: number;
  lockoutMinutes: number;
};

const API = import.meta.env.VITE_API_URL || "http://localhost:5079";

export default function OUsTreePage() {
  const [root, setRoot] = useState<OUNode>({ dn: "", name: "Domain Root", loaded: false, open: true, children: [] });
  const [loadingNode, setLoadingNode] = useState<string | null>(null);
  const [selectedDn, setSelectedDn] = useState<string>("");
  const [objects, setObjects] = useState<ADObject[]>([]);
  const [loadingObjects, setLoadingObjects] = useState(false);

  const [search, setSearch] = useState("");
  const [searchDebounced, setSearchDebounced] = useState("");

  // modal (properties)
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [details, setDetails] = useState<ADObjectDetails | null>(null);
  const [actionMsg, setActionMsg] = useState<string | null>(null);

  // reset password modal
  const [resetOpen, setResetOpen] = useState(false);
  const [newPass, setNewPass] = useState("");
  const [newPass2, setNewPass2] = useState("");
  const [resetLoading, setResetLoading] = useState(false);
  const [forceChange, setForceChange] = useState(true);
  const [unlockIfLocked, setUnlockIfLocked] = useState(false);
  const [policy, setPolicy] = useState<PasswordPolicy | null>(null);
  const [policyErrors, setPolicyErrors] = useState<string[]>([]);

  // debounce
  useEffect(() => {
    const t = setTimeout(() => setSearchDebounced(search.trim()), 350);
    return () => clearTimeout(t);
  }, [search]);

  const auth = useMemo(() => {
    const t = localStorage.getItem("token");
    return t ? { Authorization: `Bearer ${t}` } : {};
  }, []);

  // load root once
  useEffect(() => { if (!root.loaded) loadChildren(root); /* eslint-disable-next-line */ }, []);

  // refetch objects on search change
  useEffect(() => { if (selectedDn) loadObjects(selectedDn, searchDebounced); /* eslint-disable-next-line */ }, [searchDebounced]);

  async function fetchChildren(dn?: string) {
    const url = dn ? `${API}/api/ous/children?parentDn=${encodeURIComponent(dn)}` : `${API}/api/ous/children`;
    const headers: Record<string, string> = { "Accept": "application/json" };
    if (auth.Authorization) {
      headers.Authorization = auth.Authorization;
    }
    const res = await fetch(url, { headers });
    if (!res.ok) throw new Error(await res.text());
    return (await res.json()) as { name: string; distinguishedName: string }[];
  }

  async function fetchObjects(dn: string, searchTerm?: string) {
    const qs = new URLSearchParams({ ouDn: dn, take: "200" });
    if (searchTerm) qs.append("search", searchTerm);
    const url = `${API}/api/ous/objects?${qs.toString()}`;
    const headers: Record<string, string> = { "Accept": "application/json" };
    if (auth.Authorization) {
      headers.Authorization = auth.Authorization;
    }
    const res = await fetch(url, { headers });
    if (!res.ok) throw new Error(await res.text());
    return (await res.json()) as ADObject[];
  }

  async function fetchObjectDetails(dn: string) {
    const url = `${API}/api/ous/object?dn=${encodeURIComponent(dn)}`;
    const headers: Record<string, string> = { "Accept": "application/json" };
    if (auth.Authorization) {
      headers.Authorization = auth.Authorization;
    }
    const res = await fetch(url, { headers });
    if (!res.ok) throw new Error(await res.text());
    return (await res.json()) as ADObjectDetails;
  }

  async function fetchPolicy() {
    const headers: Record<string, string> = { "Accept": "application/json" };
    if (auth.Authorization) {
      headers.Authorization = auth.Authorization;
    }
    const res = await fetch(`${API}/api/policy/password`, { headers });
    if (!res.ok) return null;
    const p = await res.json();
    setPolicy(p);
    setForceChange(p.forceChangeOnResetDefault);
    return p as PasswordPolicy;
  }

  function validateAgainstPolicy(pwd: string, pol: PasswordPolicy | null): string[] {
    const errs: string[] = [];
    if (!pol) return errs;
    if (pwd.length < pol.minLength) errs.push(`Minimum length is ${pol.minLength}.`);
    if (pol.requireUpper && !/[A-Z]/.test(pwd)) errs.push("At least one uppercase letter is required.");
    if (pol.requireLower && !/[a-z]/.test(pwd)) errs.push("At least one lowercase letter is required.");
    if (pol.requireDigit && !/[0-9]/.test(pwd)) errs.push("At least one digit is required.");
    if (pol.requireSpecial && !/[^A-Za-z0-9]/.test(pwd)) errs.push("At least one special character is required.");
    return errs;
  }

  async function loadChildren(node: OUNode) {
    try {
      setLoadingNode(node.dn || "(root)");
      const children = await fetchChildren(node.dn || undefined);
      node.children = children.map((c) => ({ dn: c.distinguishedName, name: c.name, open: false, loaded: false, children: [] }));
      node.loaded = true; setRoot((r) => ({ ...r }));
    } catch (e) { console.error(e); alert("Failed to load OUs"); }
    finally { setLoadingNode(null); }
  }

  async function loadObjects(dn: string, searchTerm?: string) {
    setLoadingObjects(true);
    try { setObjects(await fetchObjects(dn, searchTerm)); }
    catch (e) { console.error(e); setObjects([]); }
    finally { setLoadingObjects(false); }
  }

  async function onToggle(node: OUNode) {
    node.open = !node.open; setRoot((r) => ({ ...r }));
    if (node.open && !node.loaded) await loadChildren(node);
  }

  async function onSelect(node: OUNode) {
    setSelectedDn(node.dn); await loadObjects(node.dn, searchDebounced);
  }

  function iconFor(obj: ADObject | { objectClass: string }) {
    const oc = (obj.objectClass || "").toLowerCase();
    if (oc === "user") return <PersonIcon fontSize="small" />;
    if (oc === "group") return <PeopleAltIcon fontSize="small" />;
    if (oc === "computer") return <ComputerIcon fontSize="small" />;
    return <InfoOutlinedIcon fontSize="small" />;
  }

  async function openDetails(dn: string) {
    setDetailsOpen(true); setDetailsLoading(true); setActionMsg(null);
    try {
      const d = await fetchObjectDetails(dn);
      setDetails(d);
      if (!policy) await fetchPolicy();
    } catch (e) { console.error(e); setDetails(null); }
    finally { setDetailsLoading(false); }
  }

  async function refreshDetails() {
    if (details?.distinguishedName) await openDetails(details.distinguishedName);
  }

  // --- Actions: Enable/Disable ---
  async function setEnabled(enabled: boolean) {
    if (!details?.samAccountName) return;
    setDetailsLoading(true); setActionMsg(null);
    try {
      const headers: Record<string, string> = { "Content-Type": "application/json" };
      if (auth.Authorization) {
        headers.Authorization = auth.Authorization;
      }
      const res = await fetch(`${API}/api/users/set-enabled`, {
        method: "POST", headers,
        body: JSON.stringify({ samAccountName: details.samAccountName, enabled })
      });
      const data = await res.json();
      if (!res.ok || data.success !== true) throw new Error(data.error || "Failed");
      setActionMsg(enabled ? "User enabled successfully." : "User disabled successfully.");
      await refreshDetails();
      if (selectedDn) loadObjects(selectedDn, searchDebounced);
    } catch (e: any) { setActionMsg(e?.message || "Operation failed."); }
    finally { setDetailsLoading(false); }
  }

  // --- Unlock ---
  async function unlockNow() {
    if (!details?.samAccountName) return;
    setDetailsLoading(true); setActionMsg(null);
    try {
      const headers: Record<string, string> = { "Content-Type": "application/json" };
      if (auth.Authorization) {
        headers.Authorization = auth.Authorization;
      }
      const res = await fetch(`${API}/api/users/unlock`, {
        method: "POST", headers,
        body: JSON.stringify({ samAccountName: details.samAccountName })
      });
      const data = await res.json();
      if (!res.ok || data.success !== true) throw new Error(data.error || "Failed");
      setActionMsg("User unlocked successfully."); await refreshDetails();
    } catch (e: any) { setActionMsg(e?.message || "Unlock failed."); }
    finally { setDetailsLoading(false); }
  }

  // --- Reset Password ---
  useEffect(() => {
    setPolicyErrors(validateAgainstPolicy(newPass, policy));
  }, [newPass, policy]);

  async function doResetPassword() {
    if (!details?.samAccountName) return;
    if (!newPass || newPass !== newPass2) { setActionMsg("Passwords do not match."); return; }
    const errs = validateAgainstPolicy(newPass, policy);
    if (errs.length > 0) { setPolicyErrors(errs); return; }

    setResetLoading(true); setActionMsg(null);
    try {
      const headers: Record<string, string> = { "Content-Type": "application/json" };
      if (auth.Authorization) {
        headers.Authorization = auth.Authorization;
      }
      const res = await fetch(`${API}/api/users/reset-password`, {
        method: "POST",
        headers,
        body: JSON.stringify({
          samAccountName: details.samAccountName,
          newPassword: newPass,
          forceChangeAtNextLogon: forceChange,
          unlockIfLocked
        })
      });
      const data = await res.json();
      if (!res.ok || data.success !== true) throw new Error((data.errors && data.errors.join?.(", ")) || data.error || "Failed");
      setActionMsg("Password reset successfully.");
      setResetOpen(false); setNewPass(""); setNewPass2(""); setPolicyErrors([]);
      await refreshDetails();
    } catch (e: any) { setActionMsg(e?.message || "Reset password failed."); }
    finally { setResetLoading(false); }
  }

  function renderNode(node: OUNode, level = 0) {
    const isLoading = loadingNode === (node.dn || "(root)");
    return (
      <Box key={node.dn || "ROOT"}>
        <ListItemButton onClick={() => onSelect(node)} sx={{ pl: 2 + level * 2, bgcolor: selectedDn === node.dn ? "action.selected" : undefined }}>
          <ListItemIcon sx={{ minWidth: 32 }}>
            <IconButton size="small" onClick={(e) => { e.stopPropagation(); onToggle(node); }}>
              {node.open ? <ExpandMoreIcon /> : <ChevronRightIcon />}
            </IconButton>
          </ListItemIcon>
          <ListItemIcon sx={{ minWidth: 28 }}><DomainIcon fontSize="small" /></ListItemIcon>
          <ListItemText primary={node.name} secondary={node.dn} />
          {isLoading && <CircularProgress size={16} />}
        </ListItemButton>

        <Collapse in={node.open} timeout="auto" unmountOnExit>
          {node.loaded ? (
            <List disablePadding>
              {node.children.map((c) => renderNode(c, level + 1))}
              {node.children.length === 0 && (
                <Typography variant="body2" sx={{ pl: 6, py: 1, color: "text.secondary" }}>(No child OUs)</Typography>
              )}
            </List>
          ) : isLoading && (
            <Box sx={{ pl: 6, py: 1 }}><CircularProgress size={18} /></Box>
          )}
        </Collapse>
      </Box>
    );
  }

  return (
    <Card>
      <CardContent>
        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
          <Typography variant="h6">Organizational Units</Typography>
          <Button variant="contained" onClick={() => { setRoot({ dn: "", name: "Domain Root", open: true, loaded: false, children: [] }); setObjects([]); setSelectedDn(""); setSearch(""); }}>
            LOAD ROOT
          </Button>
        </Stack>

        <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
          {/* Tree */}
          <Box sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, height: 520, overflow: "auto" }}>
            <List disablePadding>{renderNode(root, 0)}</List>
          </Box>

          {/* Objects Panel */}
          <Box sx={{ border: "1px solid", borderColor: "divider", borderRadius: 1, height: 520, display: "flex", flexDirection: "column" }}>
            <Box sx={{ p: 1 }}>
              <Typography variant="subtitle1">Objects in OU</Typography>
              <Typography variant="caption" color="text.secondary">{selectedDn || "(nothing selected)"}</Typography>
              <Box sx={{ mt: 1 }}>
                <TextField size="small" fullWidth placeholder="Search users / groups / computers"
                  value={search} onChange={(e) => setSearch(e.target.value)}
                  InputProps={{ startAdornment: (<InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment>) }} />
              </Box>
            </Box>
            <Divider />
            <Box sx={{ flex: 1, overflow: "auto" }}>
              {loadingObjects ? (
                <Box sx={{ p: 2, display: "flex", alignItems: "center", gap: 1 }}>
                  <CircularProgress size={18} /><Typography variant="body2">Loading...</Typography>
                </Box>
              ) : (
                <List dense disablePadding>
                  {objects.map((o) => (
                    <ListItemButton key={o.distinguishedName} sx={{ px: 1.5 }} onClick={() => openDetails(o.distinguishedName)}>
                      <ListItemIcon sx={{ minWidth: 28 }}>{iconFor(o)}</ListItemIcon>
                      <ListItemText
                        primary={<Stack direction="row" alignItems="center" gap={1}><span>{o.name}</span>{o.disabled && <Chip size="small" label="disabled" color="warning" />}</Stack>}
                        secondary={o.samAccountName ? `${o.objectClass} · ${o.samAccountName}` : o.objectClass}
                      />
                      <Tooltip title="Properties"><IconButton edge="end" size="small"><InfoOutlinedIcon fontSize="small" /></IconButton></Tooltip>
                    </ListItemButton>
                  ))}
                  {objects.length === 0 && <Typography variant="body2" sx={{ px: 2, py: 1, color: "text.secondary" }}>(No objects)</Typography>}
                </List>
              )}
            </Box>
          </Box>
        </Box>
      </CardContent>

      {/* Details Modal */}
      <Dialog open={detailsOpen} onClose={() => setDetailsOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Properties</DialogTitle>
        <DialogContent dividers>
          {actionMsg && <Alert severity="info" sx={{ mb: 1 }}>{actionMsg}</Alert>}
          {detailsLoading ? (
            <Box sx={{ py: 2, display: "flex", gap: 1, alignItems: "center" }}>
              <CircularProgress size={18} /> <Typography>Loading...</Typography>
            </Box>
          ) : details ? (
            <Stack spacing={1}>
              <Typography variant="subtitle1">{details.name}</Typography>
              <Typography variant="body2" color="text.secondary">{details.distinguishedName}</Typography>
              <Divider sx={{ my: 1 }} />
              <Stack direction="row" spacing={1} alignItems="center">
                <Chip label={details.objectClass} />
                {details.enabled !== undefined && <Chip label={details.enabled ? "enabled" : "disabled"} color={details.enabled ? "success" : "warning"} />}
                {details.locked && <Chip label="locked" color="error" />}
              </Stack>
              {details.samAccountName && <Typography><b>SAM:</b> {details.samAccountName}</Typography>}
              {details.email && <Typography><b>Email:</b> {details.email}</Typography>}
              {details.lastLogonUtc && <Typography><b>Last logon:</b> {new Date(details.lastLogonUtc).toLocaleString()}</Typography>}
              {details.extra && details.extra.parentDn && <Typography><b>Parent DN:</b> {details.extra.parentDn}</Typography>}

              {/* Actions for user */}
              {details.objectClass.toLowerCase() === "user" && details.samAccountName && (
                <>
                  <Divider sx={{ my: 1 }} />
                  <Stack direction="row" spacing={1} flexWrap="wrap">
                    <Button variant="contained" color={details.enabled ? "warning" : "success"}
                      startIcon={details.enabled ? <ToggleOffIcon /> : <ToggleOnIcon />} onClick={() => setEnabled(!details.enabled)}>
                      {details.enabled ? "Disable" : "Enable"}
                    </Button>
                    <Button variant="outlined" startIcon={<LockResetIcon />} onClick={() => { setResetOpen(true); setActionMsg(null); }}>
                      Reset Password
                    </Button>
                    {details.locked && (
                      <Button variant="outlined" color="error" startIcon={<LockOpenIcon />} onClick={unlockNow}>
                        Unlock
                      </Button>
                    )}
                  </Stack>
                </>
              )}
            </Stack>
          ) : (<Typography color="text.secondary">No data</Typography>)}
        </DialogContent>
        <DialogActions><Button onClick={() => setDetailsOpen(false)}>Close</Button></DialogActions>
      </Dialog>

      {/* Reset Password Modal */}
      <Dialog open={resetOpen} onClose={() => setResetOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Reset Password</DialogTitle>
        <DialogContent dividers>
          {policy && (
            <Alert severity="info" sx={{ mb: 2 }}>
              Policy: length ≥ {policy.minLength}
              {policy.requireUpper ? " · upper" : ""}{policy.requireLower ? " · lower" : ""}
              {policy.requireDigit ? " · digit" : ""}{policy.requireSpecial ? " · special" : ""}
            </Alert>
          )}
          {policyErrors.length > 0 && <Alert severity="warning" sx={{ mb: 2 }}>{policyErrors.join(" ")}</Alert>}
          <Stack spacing={2}>
            <TextField label="New password" type="password" value={newPass} onChange={(e) => setNewPass(e.target.value)} fullWidth />
            <TextField label="Confirm new password" type="password" value={newPass2} onChange={(e) => setNewPass2(e.target.value)} fullWidth />
            <FormControlLabel control={<Checkbox checked={forceChange} onChange={(e) => setForceChange(e.target.checked)} />} label="Force change at next logon" />
            <FormControlLabel control={<Checkbox checked={unlockIfLocked} onChange={(e) => setUnlockIfLocked(e.target.checked)} />} label="Unlock account if locked" />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetOpen(false)} disabled={resetLoading}>Cancel</Button>
          <Button onClick={doResetPassword} variant="contained"
            disabled={resetLoading || !newPass || newPass !== newPass2 || validateAgainstPolicy(newPass, policy).length > 0}>
            {resetLoading ? "Working..." : "Reset"}
          </Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
}
