import React, { useEffect, useMemo, useState } from "react";

type ADUser = {
  displayName: string;
  sam: string;
  email: string;
  distinguishedName: string;
  lastLogonUtc?: string | null;
  enabled: boolean;
};

const API = import.meta.env.VITE_API_URL ?? "http://localhost:5079";

export default function UsersPage() {
  const [q, setQ] = useState("");
  const [rows, setRows] = useState<ADUser[]>([]);
  const [loading, setLoading] = useState(false);
  const [take] = useState(100);
  const [skip] = useState(0);

  const auth = useMemo(() => {
    try {
      const t = localStorage.getItem("token");
      return t ? { Authorization: `Bearer ${t}` } : {};
    } catch {
      return {};
    }
  }, []);

  const fetchUsers = async () => {
    setLoading(true);
    try {
      const url = `${API}/api/users?q=${encodeURIComponent(q)}&take=${take}&skip=${skip}`;
      const res = await fetch(url, {
        headers: {
          Accept: "application/json",
          ...(auth.Authorization ? { Authorization: auth.Authorization } : {}),
        },
      });
      if (!res.ok) throw new Error(await res.text());
      const json = await res.json();
      setRows(Array.isArray(json) ? json : json.items ?? []);
    } catch (e: any) {
      console.error(e);
      alert(`Failed to load users: ${e.message ?? e}`);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    // اختياري: تحميل تلقائي عند فتح الصفحة
    // fetchUsers();
  }, []);

  return (
    <div className="p-6">
      <div className="flex gap-3 items-center mb-4">
        <input
          className="px-3 py-2 rounded w-full text-black"
          placeholder="Search users (displayName / SAM / email)"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <button onClick={fetchUsers} className="px-4 py-2 rounded bg-purple-600 text-white">
          SEARCH
        </button>
      </div>

      {loading ? (
        <div>Loading…</div>
      ) : rows.length === 0 ? (
        <div>No rows</div>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left">
              <th className="py-2">Display Name</th>
              <th>SAM</th>
              <th>Email</th>
              <th>Status</th>
              <th>Last Logon</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((u) => (
              <tr key={u.distinguishedName} className="border-t border-gray-700">
                <td className="py-2">{u.displayName}</td>
                <td>{u.sam}</td>
                <td>{u.email}</td>
                <td>{u.enabled ? "Enabled" : "Disabled"}</td>
                <td>{u.lastLogonUtc ? new Date(u.lastLogonUtc).toLocaleString() : "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
