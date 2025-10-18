import React, { useEffect, useState } from "react";
import {
  Box, Chip, CircularProgress, Dialog, DialogContent, DialogTitle,
  Divider, List, ListItem, ListItemText, Stack, Typography
} from "@mui/material";
import { useSnackbar } from "notistack";

const API = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

type DetailsVm = {
  samAccountName: string;
  displayName: string;
  email: string;
  enabled: boolean;
  locked: boolean;
  distinguishedName: string;
  ouPath?: string;
  lastLogon?: string | null;
  passwordLastSet?: string | null;
  accountExpires?: string | null;
  groups: string[];
};

export default function UserDetailsModal({
  sam,
  onClose,
}: {
  sam: string | null;
  onClose: () => void;
}) {
  const { enqueueSnackbar } = useSnackbar();
  const [loading, setLoading] = useState(false);
  const [vm, setVm] = useState<DetailsVm | null>(null);

  useEffect(() => {
    const run = async () => {
      if (!sam) return;
      setLoading(true);
      setVm(null);
      try {
        const res = await fetch(`${API}/api/users/details?sam=${encodeURIComponent(sam)}`, {
          headers: { Accept: "application/json" },
          credentials: "include",
        });
        if (!res.ok) throw new Error(`API ${res.status}`);
        const data = await res.json();
        setVm(data);
      } catch (err: any) {
        enqueueSnackbar(`Failed to load details: ${err.message ?? err}`, { variant: "error" });
      } finally {
        setLoading(false);
      }
    };
    run();
  }, [sam, enqueueSnackbar]);

  return (
    <Dialog open={!!sam} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>User Properties</DialogTitle>
      <DialogContent dividers>
        {!vm || loading ? (
          <Stack alignItems="center" sx={{ py: 4 }}><CircularProgress /></Stack>
        ) : (
          <Stack spacing={2}>
            <Typography variant="h6">{vm.displayName || vm.samAccountName}</Typography>
            <Typography variant="body2" sx={{ wordBreak: "break-all" }}>{vm.distinguishedName}</Typography>

            <Stack direction="row" spacing={1}>
              <Chip label="user" size="small" />
              <Chip label={vm.enabled ? "enabled" : "disabled"} color={vm.enabled ? "success" : "warning"} size="small" />
              {vm.locked && <Chip label="locked" color="error" size="small" />}
            </Stack>

            <Divider />
            <List dense>
              <ListItem><ListItemText primary="SAM" secondary={vm.samAccountName} /></ListItem>
              <ListItem><ListItemText primary="Email" secondary={vm.email || "-"} /></ListItem>
              <ListItem><ListItemText primary="OU Path" secondary={vm.ouPath || "-"} /></ListItem>
              <ListItem><ListItemText primary="Last Logon" secondary={vm.lastLogon ? new Date(vm.lastLogon).toLocaleString() : "-"} /></ListItem>
              <ListItem><ListItemText primary="Password Last Set" secondary={vm.passwordLastSet ? new Date(vm.passwordLastSet).toLocaleString() : "-"} /></ListItem>
              <ListItem><ListItemText primary="Account Expires" secondary={vm.accountExpires ? new Date(vm.accountExpires).toLocaleString() : "-"} /></ListItem>
            </List>

            <Divider />
            <Typography variant="subtitle2">Groups</Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
              {(vm.groups || []).length === 0
                ? <Typography variant="body2">No groups</Typography>
                : vm.groups.map(g => <Chip key={g} label={g} size="small" />)
              }
            </Box>
          </Stack>
        )}
      </DialogContent>
    </Dialog>
  );
}
