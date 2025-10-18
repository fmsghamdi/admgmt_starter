import React, { useEffect, useState } from 'react';
import { Card, CardContent, Typography, TextField, Button, Stack, Table, TableHead, TableRow, TableCell, TableBody, Divider } from '@mui/material';
import axios from 'axios';

interface OUVm {
  name: string;
  distinguishedName: string;
  parentDn?: string;
  description?: string;
  childCount: number;
}

const API = (import.meta as any).env.VITE_API_URL || 'http://localhost:5079';

export default function OUsPage(){
  const [ous, setOus] = useState<OUVm[]>([]);
  const [baseDn, setBaseDn] = useState<string>('');
  const [parentDn, setParentDn] = useState<string>('');
  const [ouName, setOuName] = useState<string>('');
  const [ouDesc, setOuDesc] = useState<string>('');
  const [renameDn, setRenameDn] = useState<string>('');
  const [newName, setNewName] = useState<string>('');
  const [deleteDn, setDeleteDn] = useState<string>('');
  const [moveObjectDn, setMoveObjectDn] = useState<string>('');
  const [targetOuDn, setTargetOuDn] = useState<string>('');
  const [samForMove, setSamForMove] = useState<string>('');
  const [refreshFlag, setRefreshFlag] = useState(0);

  const headers = () => {
    const t = localStorage.getItem('token');
    return { Authorization: `Bearer ${t}` };
  };

  const load = async () => {
    const r = await axios.get(`${API}/api/ous`, { params: { baseDn: baseDn || undefined }, headers: headers() });
    setOus(r.data);
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [refreshFlag]);

  return (
    <Card>
      <CardContent>
        <Typography variant="h6" gutterBottom>Organizational Units</Typography>

        <Stack direction="row" spacing={2} sx={{ my: 2 }}>
          <TextField label="Base DN (optional)" value={baseDn} onChange={e => setBaseDn(e.target.value)} size="small" fullWidth />
          <Button variant="contained" onClick={() => setRefreshFlag(x => x + 1)}>Load</Button>
        </Stack>

        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>DN</TableCell>
              <TableCell>Parent DN</TableCell>
              <TableCell>Children</TableCell>
              <TableCell>Description</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {ous.map(o => (
              <TableRow key={o.distinguishedName}>
                <TableCell>{o.name}</TableCell>
                <TableCell>{o.distinguishedName}</TableCell>
                <TableCell>{o.parentDn}</TableCell>
                <TableCell>{o.childCount}</TableCell>
                <TableCell>{o.description}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        <Divider sx={{ my: 3 }} />

        <Typography variant="subtitle1" gutterBottom>Create OU</Typography>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 2 }}>
          <TextField label="Parent DN" value={parentDn} onChange={e => setParentDn(e.target.value)} fullWidth />
          <TextField label="OU Name" value={ouName} onChange={e => setOuName(e.target.value)} fullWidth />
          <TextField label="Description (optional)" value={ouDesc} onChange={e => setOuDesc(e.target.value)} fullWidth />
          <Button variant="contained" onClick={async () => {
            if (!parentDn || !ouName) return alert('ParentDn and Name are required');
            const r = await axios.post(`${API}/api/ous`, { parentDn, name: ouName, description: ouDesc || null }, { headers: headers() });
            if (r.data?.success) { setRefreshFlag(x => x + 1); setOuName(''); setOuDesc(''); } else alert('Create failed');
          }}>Create</Button>
        </Stack>

        <Typography variant="subtitle1" gutterBottom>Rename OU</Typography>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 2 }}>
          <TextField label="OU DN" value={renameDn} onChange={e => setRenameDn(e.target.value)} fullWidth />
          <TextField label="New Name" value={newName} onChange={e => setNewName(e.target.value)} fullWidth />
          <Button variant="contained" onClick={async () => {
            if (!renameDn || !newName) return alert('DN and NewName are required');
            const r = await axios.put(`${API}/api/ous/rename`, { dn: renameDn, newName }, { headers: headers() });
            if (r.data?.success) { setRefreshFlag(x => x + 1); setNewName(''); } else alert('Rename failed');
          }}>Rename</Button>
        </Stack>

        <Typography variant="subtitle1" gutterBottom>Delete OU</Typography>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 2 }}>
          <TextField label="OU DN (must be empty)" value={deleteDn} onChange={e => setDeleteDn(e.target.value)} fullWidth />
          <Button color="error" variant="contained" onClick={async () => {
            if (!deleteDn) return alert('DN is required');
            const r = await axios.delete(`${API}/api/ous`, { params: { dn: deleteDn }, headers: headers() });
            if (r.data?.success) { setRefreshFlag(x => x + 1); setDeleteDn(''); } else alert('Delete failed (OU must be empty)');
          }}>Delete</Button>
        </Stack>

        <Typography variant="subtitle1" gutterBottom>Move Object by DN → Target OU</Typography>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 2 }}>
          <TextField label="Object DN" value={moveObjectDn} onChange={e => setMoveObjectDn(e.target.value)} fullWidth />
          <TextField label="Target OU DN" value={targetOuDn} onChange={e => setTargetOuDn(e.target.value)} fullWidth />
          <Button variant="contained" onClick={async () => {
            if (!moveObjectDn || !targetOuDn) return alert('ObjectDn and TargetOuDn are required');
            const r = await axios.post(`${API}/api/ous/move-object`, { objectDn: moveObjectDn, targetOuDn }, { headers: headers() });
            if (r.data?.success) { setRefreshFlag(x => x + 1); } else alert('Move failed');
          }}>Move</Button>
        </Stack>

        <Typography variant="subtitle1" gutterBottom>Move User by SAM → Target OU</Typography>
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
          <TextField label="User SAM" value={samForMove} onChange={e => setSamForMove(e.target.value)} fullWidth />
          <TextField label="Target OU DN" value={targetOuDn} onChange={e => setTargetOuDn(e.target.value)} fullWidth />
          <Button variant="contained" onClick={async () => {
            if (!samForMove || !targetOuDn) return alert('Sam and TargetOuDn are required');
            const r = await axios.post(`${API}/api/ous/move-user-by-sam`, { samAccountName: samForMove, targetOuDn }, { headers: headers() });
            if (r.data?.success) { setRefreshFlag(x => x + 1); setSamForMove(''); } else alert('Move failed');
          }}>Move</Button>
        </Stack>
      </CardContent>
    </Card>
  );
}
