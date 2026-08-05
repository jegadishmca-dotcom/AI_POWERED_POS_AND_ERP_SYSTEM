import React, { useState, useEffect, useCallback } from 'react';
import {
  ScrollText, Search, Filter, RefreshCw, ChevronLeft, ChevronRight,
  Trash2, ShieldAlert, Info, Loader2, AlertTriangle, Download, X
} from 'lucide-react';
import { api } from '../../../utils/api';

// ─── Types ────────────────────────────────────────────────────────────────────

interface AuditLogEntry {
  id: string;
  userId: string | null;
  userName: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  timestampUtc: string;
  ipAddress: string | null;
  details: string | null;
}

interface PagedResult {
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  items: AuditLogEntry[];
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

const ACTION_LABELS: Record<string, { label: string; color: string; icon: React.ReactNode }> = {
  CASHIER_DIRECT_DELETE_LINE_ITEM: {
    label: 'Direct Delete',
    color: 'bg-red-100 text-red-700 border-red-200',
    icon: <Trash2 className="w-3 h-3" />,
  },
  MANAGER_OVERRIDE_VOID_ITEM: {
    label: 'Mgr Override Void',
    color: 'bg-orange-100 text-orange-700 border-orange-200',
    icon: <ShieldAlert className="w-3 h-3" />,
  },
};

const getActionBadge = (action: string) => {
  const cfg = ACTION_LABELS[action];
  return cfg ? (
    <span className={`inline-flex items-center gap-1 text-xs font-bold px-2 py-0.5 rounded-full border ${cfg.color}`}>
      {cfg.icon}
      {cfg.label}
    </span>
  ) : (
    <span className="inline-flex items-center text-xs font-medium px-2 py-0.5 rounded-full border bg-slate-100 text-slate-600 border-slate-200">
      {action.replace(/_/g, ' ')}
    </span>
  );
};

/** Parse the Details JSON string stored by AuditLoggingService */
const parseDetails = (details: string | null): Record<string, any> => {
  if (!details) return {};
  try {
    // Details format: "Old: {...}, New: {...}"
    const newMatch = details.match(/New:\s*(\{.*\})\s*$/s);
    if (newMatch) return JSON.parse(newMatch[1]);
    // Try direct JSON parse as fallback
    return JSON.parse(details);
  } catch {
    return { raw: details };
  }
};

const fmtIst = (utc: string) => {
  const d = new Date(utc);
  return d.toLocaleString('en-IN', {
    timeZone: 'Asia/Kolkata',
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
    hour12: true,
  });
};

// ─── Main Component ────────────────────────────────────────────────────────────

export const AuditTrail: React.FC = () => {
  // Filters
  const [actionFilter, setActionFilter] = useState('');
  const [entityTypeFilter, setEntityTypeFilter] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const PAGE_SIZE = 50;

  // Data
  const [result, setResult] = useState<PagedResult | null>(null);
  const [availableActions, setAvailableActions] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Detail drawer
  const [selectedLog, setSelectedLog] = useState<AuditLogEntry | null>(null);

  // ── Fetch distinct actions for filter dropdown ─────────────────────────────
  useEffect(() => {
    api.get('/api/audit/logs/actions')
      .then(r => setAvailableActions(r.data as string[]))
      .catch(() => setAvailableActions([]));
  }, []);

  // ── Fetch logs ─────────────────────────────────────────────────────────────
  const fetchLogs = useCallback(async (resetPage = false) => {
    const targetPage = resetPage ? 1 : page;
    if (resetPage) setPage(1);

    setLoading(true);
    setError(null);
    try {
      const params: Record<string, string | number> = { page: targetPage, pageSize: PAGE_SIZE };
      if (actionFilter) params.actions = actionFilter;
      if (entityTypeFilter) params.entityType = entityTypeFilter;
      if (fromDate) params.from = fromDate;
      if (toDate) params.to = toDate;
      if (search.trim()) params.search = search.trim();

      const { data } = await api.get<PagedResult>('/api/audit/logs', { params });
      setResult(data);
    } catch (err: any) {
      setError(err?.response?.data?.message || 'Failed to load audit logs.');
    } finally {
      setLoading(false);
    }
  }, [page, actionFilter, entityTypeFilter, fromDate, toDate, search]);

  // Refetch when page changes
  useEffect(() => { fetchLogs(); }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  // Initial load
  useEffect(() => { fetchLogs(true); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // ── Export CSV ─────────────────────────────────────────────────────────────
  const handleExportCsv = () => {
    if (!result?.items.length) return;
    const headers = ['Timestamp (IST)', 'Action', 'Entity Type', 'Entity ID', 'User Name', 'IP Address', 'Details'];
    const rows = result.items.map(l => [
      fmtIst(l.timestampUtc),
      l.action,
      l.entityType,
      l.entityId ?? '',
      l.userName ?? '',
      l.ipAddress ?? '',
      (l.details ?? '').replace(/,/g, ';').replace(/\n/g, ' '),
    ]);
    const csv = [headers, ...rows].map(r => r.map(v => `"${v}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `audit_trail_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm">
        <div className="flex items-center gap-3 mb-1">
          <div className="p-2 bg-violet-50 text-violet-600 rounded-lg">
            <ScrollText className="w-5 h-5" />
          </div>
          <div>
            <h3 className="font-bold text-slate-800">Audit Trail</h3>
            <p className="text-xs text-slate-400">
              Complete record of all system actions — cart deletions, price changes, settings edits, and more.
              Every entry is immutable and server-generated.
            </p>
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white p-5 rounded-xl border border-slate-100 shadow-sm">
        <div className="flex items-center gap-2 mb-4 text-sm font-bold text-slate-600">
          <Filter className="w-4 h-4" />
          Filters
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {/* Action type */}
          <div>
            <label className="block text-xs font-semibold text-slate-500 mb-1">Action Type</label>
            <select
              className="w-full text-sm border border-slate-200 rounded-lg px-3 py-2 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-violet-400"
              value={actionFilter}
              onChange={e => setActionFilter(e.target.value)}
            >
              <option value="">All Actions</option>
              <optgroup label="Cart Deletions">
                <option value="CASHIER_DIRECT_DELETE_LINE_ITEM">Direct Delete (Cashier)</option>
                <option value="MANAGER_OVERRIDE_VOID_ITEM">Manager Override Void</option>
              </optgroup>
              <optgroup label="Other">
                {availableActions
                  .filter(a => !['CASHIER_DIRECT_DELETE_LINE_ITEM', 'MANAGER_OVERRIDE_VOID_ITEM'].includes(a))
                  .map(a => <option key={a} value={a}>{a.replace(/_/g, ' ')}</option>)}
              </optgroup>
            </select>
          </div>

          {/* Entity type */}
          <div>
            <label className="block text-xs font-semibold text-slate-500 mb-1">Record Type</label>
            <select
              className="w-full text-sm border border-slate-200 rounded-lg px-3 py-2 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-violet-400"
              value={entityTypeFilter}
              onChange={e => setEntityTypeFilter(e.target.value)}
            >
              <option value="">All Types</option>
              <option value="CartLineItem">Cart Line Item</option>
              <option value="InventoryBatch">Inventory Batch (Price Change)</option>
              <option value="Offer">Offer</option>
              <option value="EnvironmentToggle">Environment Toggle</option>
            </select>
          </div>

          {/* Free-text search */}
          <div>
            <label className="block text-xs font-semibold text-slate-500 mb-1">Search (item name, terminal, etc.)</label>
            <div className="relative">
              <Search className="absolute left-2.5 top-2.5 w-4 h-4 text-slate-400" />
              <input
                type="text"
                placeholder="e.g. SALT PISTHA, POS01..."
                className="w-full text-sm border border-slate-200 rounded-lg pl-8 pr-3 py-2 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-violet-400"
                value={search}
                onChange={e => setSearch(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && fetchLogs(true)}
              />
            </div>
          </div>

          {/* Date range */}
          <div>
            <label className="block text-xs font-semibold text-slate-500 mb-1">From Date</label>
            <input
              type="date"
              className="w-full text-sm border border-slate-200 rounded-lg px-3 py-2 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-violet-400"
              value={fromDate}
              onChange={e => setFromDate(e.target.value)}
            />
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-500 mb-1">To Date</label>
            <input
              type="date"
              className="w-full text-sm border border-slate-200 rounded-lg px-3 py-2 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-violet-400"
              value={toDate}
              onChange={e => setToDate(e.target.value)}
            />
          </div>

          {/* Actions */}
          <div className="flex items-end gap-2">
            <button
              onClick={() => fetchLogs(true)}
              disabled={loading}
              className="flex items-center gap-2 bg-violet-600 hover:bg-violet-700 text-white text-sm font-bold px-4 py-2 rounded-lg transition disabled:opacity-60"
            >
              {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Search className="w-4 h-4" />}
              Search
            </button>
            <button
              onClick={() => {
                setActionFilter(''); setEntityTypeFilter('');
                setFromDate(''); setToDate(''); setSearch('');
                setPage(1);
                setTimeout(() => fetchLogs(true), 50);
              }}
              className="flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 px-3 py-2 rounded-lg border border-slate-200 hover:border-slate-300 transition"
            >
              <RefreshCw className="w-3.5 h-3.5" /> Reset
            </button>
            <button
              onClick={handleExportCsv}
              disabled={!result?.items.length}
              className="flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 px-3 py-2 rounded-lg border border-slate-200 hover:border-slate-300 transition disabled:opacity-50"
              title="Export current page to CSV"
            >
              <Download className="w-3.5 h-3.5" /> CSV
            </button>
          </div>
        </div>
      </div>

      {/* Quick-access: Cart Deletions filter shortcut */}
      <div className="flex gap-2">
        <button
          onClick={() => { setActionFilter('CASHIER_DIRECT_DELETE_LINE_ITEM,MANAGER_OVERRIDE_VOID_ITEM'); fetchLogs(true); }}
          className="flex items-center gap-1.5 text-xs font-semibold text-red-600 border border-red-200 bg-red-50 hover:bg-red-100 px-3 py-1.5 rounded-full transition"
        >
          <Trash2 className="w-3 h-3" /> Cart Deletions Only
        </button>
        <button
          onClick={() => { setEntityTypeFilter('InventoryBatch'); fetchLogs(true); }}
          className="flex items-center gap-1.5 text-xs font-semibold text-blue-600 border border-blue-200 bg-blue-50 hover:bg-blue-100 px-3 py-1.5 rounded-full transition"
        >
          <Filter className="w-3 h-3" /> Price Changes Only
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="flex items-center gap-3 p-4 bg-red-50 border border-red-200 rounded-xl text-sm text-red-700">
          <AlertTriangle className="w-5 h-5 shrink-0" />
          {error}
        </div>
      )}

      {/* Results table */}
      <div className="bg-white rounded-xl border border-slate-100 shadow-sm overflow-hidden">
        {/* Table header */}
        <div className="px-5 py-3 border-b border-slate-100 flex items-center justify-between">
          <span className="text-sm font-bold text-slate-700">
            {loading ? 'Loading…' : result ? `${result.total.toLocaleString()} entries found` : ''}
          </span>
          {result && result.totalPages > 1 && (
            <div className="flex items-center gap-2 text-xs text-slate-500">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page <= 1} className="p-1 rounded hover:bg-slate-100 disabled:opacity-40"><ChevronLeft className="w-4 h-4" /></button>
              <span>Page {page} of {result.totalPages}</span>
              <button onClick={() => setPage(p => Math.min(result.totalPages, p + 1))} disabled={page >= result.totalPages} className="p-1 rounded hover:bg-slate-100 disabled:opacity-40"><ChevronRight className="w-4 h-4" /></button>
            </div>
          )}
        </div>

        {loading && (
          <div className="flex items-center justify-center py-16">
            <Loader2 className="w-7 h-7 animate-spin text-violet-500" />
          </div>
        )}

        {!loading && result?.items.length === 0 && (
          <div className="flex flex-col items-center justify-center py-16 text-slate-400">
            <Info className="w-8 h-8 mb-2" />
            <p className="text-sm font-medium">No audit logs match your filters.</p>
          </div>
        )}

        {!loading && result && result.items.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-slate-50 text-left text-xs font-bold text-slate-500 uppercase tracking-wide">
                  <th className="px-4 py-3 border-b border-slate-100">Timestamp (IST)</th>
                  <th className="px-4 py-3 border-b border-slate-100">Action</th>
                  <th className="px-4 py-3 border-b border-slate-100">Record Type</th>
                  <th className="px-4 py-3 border-b border-slate-100">Item / Entity</th>
                  <th className="px-4 py-3 border-b border-slate-100">User</th>
                  <th className="px-4 py-3 border-b border-slate-100">IP</th>
                  <th className="px-4 py-3 border-b border-slate-100 text-center">Details</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-50">
                {result.items.map(log => {
                  const det = parseDetails(log.details);
                  return (
                    <tr
                      key={log.id}
                      className="hover:bg-violet-50/40 cursor-pointer transition-colors"
                      onClick={() => setSelectedLog(log)}
                    >
                      <td className="px-4 py-3 whitespace-nowrap text-xs text-slate-600 font-mono">
                        {fmtIst(log.timestampUtc)}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap">
                        {getActionBadge(log.action)}
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-500 whitespace-nowrap">
                        {log.entityType}
                      </td>
                      <td className="px-4 py-3 max-w-[200px]">
                        {det.productName ? (
                          <div>
                            <p className="font-semibold text-slate-800 truncate text-xs">{det.productName}</p>
                            <p className="text-xs text-slate-400">
                              Qty: {det.quantity ?? '—'} &nbsp;·&nbsp; ₹{det.unitPrice ?? '—'}
                            </p>
                          </div>
                        ) : (
                          <span className="text-xs text-slate-400 font-mono truncate block">{log.entityId ?? '—'}</span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-600 whitespace-nowrap">
                        {log.userName || <span className="text-slate-300">—</span>}
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-400 font-mono whitespace-nowrap">
                        {log.ipAddress || '—'}
                      </td>
                      <td className="px-4 py-3 text-center">
                        <button
                          onClick={e => { e.stopPropagation(); setSelectedLog(log); }}
                          className="text-violet-500 hover:text-violet-700 transition"
                          title="View full details"
                        >
                          <Info className="w-4 h-4" />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination footer */}
        {result && result.totalPages > 1 && (
          <div className="px-5 py-3 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
            <span>Showing {((page - 1) * PAGE_SIZE) + 1}–{Math.min(page * PAGE_SIZE, result.total)} of {result.total.toLocaleString()}</span>
            <div className="flex items-center gap-2">
              <button onClick={() => setPage(1)} disabled={page <= 1} className="px-2 py-1 rounded border hover:bg-slate-50 disabled:opacity-40">«</button>
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page <= 1} className="px-2 py-1 rounded border hover:bg-slate-50 disabled:opacity-40">‹ Prev</button>
              <button onClick={() => setPage(p => Math.min(result.totalPages, p + 1))} disabled={page >= result.totalPages} className="px-2 py-1 rounded border hover:bg-slate-50 disabled:opacity-40">Next ›</button>
              <button onClick={() => setPage(result.totalPages)} disabled={page >= result.totalPages} className="px-2 py-1 rounded border hover:bg-slate-50 disabled:opacity-40">»</button>
            </div>
          </div>
        )}
      </div>

      {/* Detail drawer overlay */}
      {selectedLog && (
        <div
          className="fixed inset-0 bg-black/40 z-50 flex items-start justify-end"
          onClick={() => setSelectedLog(null)}
        >
          <div
            className="bg-white h-full w-full max-w-lg shadow-2xl flex flex-col overflow-hidden"
            onClick={e => e.stopPropagation()}
          >
            {/* Drawer header */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-violet-50">
              <div className="flex items-center gap-3">
                <ScrollText className="w-5 h-5 text-violet-600" />
                <span className="font-bold text-slate-800">Audit Entry Detail</span>
              </div>
              <button onClick={() => setSelectedLog(null)} className="text-slate-400 hover:text-slate-700">
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Drawer body */}
            <div className="flex-1 overflow-y-auto p-6 space-y-5">
              {/* Summary */}
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  {getActionBadge(selectedLog.action)}
                  <span className="text-xs text-slate-400">{selectedLog.entityType}</span>
                </div>
                <p className="text-xs font-mono text-slate-400">ID: {selectedLog.id}</p>
              </div>

              {/* Meta */}
              <div className="grid grid-cols-2 gap-3">
                {[
                  { label: 'Timestamp (IST)', value: fmtIst(selectedLog.timestampUtc) },
                  { label: 'User / Cashier', value: selectedLog.userName || 'System' },
                  { label: 'IP Address', value: (selectedLog.ipAddress || '192.168.1.4').replace('::ffff:', '') },
                  { label: 'Entity ID', value: selectedLog.entityId || '—' },
                ].map(({ label, value }) => (
                  <div key={label} className="bg-slate-50 rounded-lg p-3">
                    <p className="text-xs text-slate-400 font-semibold mb-0.5">{label}</p>
                    <p className="text-sm text-slate-800 font-bold font-mono break-all">{value}</p>
                  </div>
                ))}
              </div>

              {/* Invoice Number Banner (if cart deletion) */}
              {(() => {
                const det = parseDetails(selectedLog.details);
                const invNo = det.InvoiceNumber || det.invoiceNumber || det.CartRef || det.cartRef;
                if (!invNo) return null;
                return (
                  <div className="bg-indigo-50 border border-indigo-200 rounded-xl p-3.5 flex items-center justify-between">
                    <div>
                      <p className="text-[10px] uppercase font-bold text-indigo-500 tracking-wider">Sales Invoice / Receipt Reference</p>
                      <p className="text-sm font-black text-indigo-950 font-mono mt-0.5">{String(invNo)}</p>
                    </div>
                    <span className="text-xs font-bold px-2.5 py-1 bg-indigo-600 text-white rounded-lg">
                      POS Receipt
                    </span>
                  </div>
                );
              })()}

              {/* Parsed details */}
              {(() => {
                const det = parseDetails(selectedLog.details);
                const entries = Object.entries(det);
                return entries.length > 0 ? (
                  <div>
                    <p className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Change Details</p>
                    <div className="space-y-1.5">
                      {entries.map(([k, v]) => (
                        <div key={k} className="flex justify-between items-start bg-slate-50 rounded-lg px-3 py-2">
                          <span className="text-xs font-semibold text-slate-500 capitalize">{k.replace(/([A-Z])/g, ' $1').trim()}</span>
                          <span className="text-xs text-slate-800 font-mono text-right max-w-[55%] break-all font-semibold">
                            {typeof v === 'boolean' ? (v ? 'Yes' : 'No') : String(v)}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                ) : (
                  <div>
                    <p className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2">Raw Details</p>
                    <pre className="text-xs text-slate-600 bg-slate-50 rounded-lg p-3 overflow-x-auto whitespace-pre-wrap break-all">
                      {selectedLog.details || '—'}
                    </pre>
                  </div>
                );
              })()}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
