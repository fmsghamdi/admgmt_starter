import React, { useEffect, useMemo, useRef, useState } from "react";

type GroupRow = {
  name: string;
  sam: string;
  distinguishedName: string;
  description?: string | null;
  memberCount: number;
};

const API = import.meta.env.VITE_API_URL ?? "http://localhost:5079";

export default function GroupsPage() {
  const [q, setQ] = useState("");
  const [rows, setRows] = useState<GroupRow[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const triedMembersForKey = useRef<string>("");

  const auth = useMemo(() => {
    try {
      const t = localStorage.getItem("token");
      return t ? { Authorization: `Bearer ${t}` } : {};
    } catch {
      return {};
    }
  }, []);

  const fetchGroups = async () => {
    setLoading(true);
    try {
      const url = `${API}/api/groups?search=${encodeURIComponent(q)}&take=200&skip=0`;
      const res = await fetch(url, {
        headers: {
          Accept: "application/json",
          ...(auth.Authorization ? { Authorization: auth.Authorization } : {}),
        },
      });
      if (!res.ok) throw new Error(await res.text());
      const json = await res.json();
      const list: GroupRow[] = (json?.items ?? json ?? []) as GroupRow[];
      setRows(list);
      setTotal(json?.total ?? list.length);
      triedMembersForKey.current = "";
    } catch (e: any) {
      console.error(e);
      alert(`Failed to load groups: ${e.message ?? e}`);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    // fetchGroups(); // لو تريد تحميل تلقائي
  }, []);

  return (
    <div className="p-6">
      <div className="flex gap-3 items-center mb-4">
        <input
          className="px-3 py-2 rounded w-full text-black"
          placeholder="Search groups (name/desc)"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <button onClick={fetchGroups} className="px-4 py-2 rounded bg-purple-600 text-white">
          SEARCH
        </button>
        <div className="opacity-70">{total > 0 ? `${rows.length} shown of ${total}` : "0"}</div>
      </div>

      {loading ? (
        <div>Loading…</div>
      ) : rows.length === 0 ? (
        <div>No groups</div>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left">
              <th className="py-2">Group (name)</th>
              <th>SAM</th>
              <th>Description</th>
              <th>Members</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((g) => (
              <tr key={g.distinguishedName} className="border-t border-gray-700">
                <td className="py-2">{g.name}</td>
                <td>{g.sam}</td>
                <td>{g.description ?? ""}</td>
                <td>{g.memberCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
