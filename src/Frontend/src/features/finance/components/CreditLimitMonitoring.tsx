import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getCreditMonitoring, CreditMonitoring } from '../services/finance.service';
import { exportToCsv } from '../../../utils/exportToCsv';
import { 
  ShieldAlert, 
  Search, 
  Download, 
  ArrowUpDown, 
  ChevronLeft, 
  ChevronRight, 
  AlertTriangle,
  CheckCircle,
  XCircle
} from 'lucide-react';
import { formatCurrency } from '../../../utils/formatters';

export const CreditLimitMonitoring: React.FC = () => {
  const [search, setSearch] = useState('');
  const [riskFilter, setRiskFilter] = useState('');
  const [sortBy, setSortBy] = useState<keyof CreditMonitoring>('utilizationPercentage');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const { data: monitors = [], isLoading, error } = useQuery({
    queryKey: ['creditMonitoring'],
    queryFn: () => getCreditMonitoring()
  });

  const handleSort = (field: keyof CreditMonitoring) => {
    if (sortBy === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortOrder('desc');
    }
  };

  const handleExport = () => {
    exportToCsv(filteredMonitors, 'Credit_Limit_Monitoring_Report', [
      { key: 'customerName', label: 'Customer Name' },
      { key: 'phone', label: 'Phone' },
      { key: 'creditLimit', label: 'Credit Limit (₹)' },
      { key: 'outstandingBalance', label: 'Outstanding Balance (₹)' },
      { key: 'availableCredit', label: 'Available Credit (₹)' },
      { key: 'utilizationPercentage', label: 'Utilization %' },
      { key: 'overdueDays', label: 'Overdue Days' },
      { key: 'riskLevel', label: 'Risk Level' },
      { key: 'isBlocked', label: 'Status' },
    ]);
  };

  // Filter records
  const filteredMonitors = monitors.filter(item => {
    const matchesSearch = 
      item.customerName.toLowerCase().includes(search.toLowerCase()) ||
      item.phone.includes(search);
    
    const matchesRisk = riskFilter === '' || item.riskLevel === riskFilter;
    
    return matchesSearch && matchesRisk;
  });

  // Sort records
  const sortedMonitors = [...filteredMonitors].sort((a, b) => {
    let aVal = a[sortBy] ?? '';
    let bVal = b[sortBy] ?? '';
    
    if (typeof aVal === 'string') {
      return sortOrder === 'asc' 
        ? aVal.localeCompare(bVal as string) 
        : (bVal as string).localeCompare(aVal);
    } else if (typeof aVal === 'boolean') {
      return sortOrder === 'asc'
        ? (aVal ? 1 : 0) - (bVal ? 1 : 0)
        : (bVal ? 1 : 0) - (aVal ? 1 : 0);
    } else {
      return sortOrder === 'asc'
        ? (aVal as number) - (bVal as number)
        : (bVal as number) - (aVal as number);
    }
  });

  // Paginate records
  const totalItems = sortedMonitors.length;
  const totalPages = Math.ceil(totalItems / pageSize);
  const paginatedMonitors = sortedMonitors.slice((page - 1) * pageSize, page * pageSize);

  const getRiskBadgeClass = (level: string) => {
    switch (level) {
      case 'CRITICAL':
        return 'bg-rose-100 text-rose-800 dark:bg-rose-950/40 dark:text-rose-400 border-rose-200 dark:border-rose-800/40';
      case 'WARNING':
        return 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-400 border-amber-200 dark:border-amber-800/40';
      case 'SAFE':
      default:
        return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/40';
    }
  };

  const getProgressBarColor = (percentage: number) => {
    if (percentage >= 90) return 'bg-rose-600';
    if (percentage >= 75) return 'bg-amber-500';
    return 'bg-emerald-500';
  };

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <ShieldAlert className="w-7 h-7 text-indigo-600" />
            Credit Limit Monitoring (AR)
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Track credit exposure, B2B customer limits, and outstanding risk profiles</p>
        </div>
        <div>
          <button 
            onClick={handleExport}
            className="bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-200 px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-sm transition-all text-sm cursor-pointer"
          >
            <Download className="w-4 h-4" />
            Export Credit Report
          </button>
        </div>
      </div>

      {/* Filters and Search */}
      <div className="bg-white dark:bg-slate-900 p-4 rounded-xl border border-slate-200 dark:border-slate-800 flex flex-col md:flex-row gap-4 justify-between items-center shadow-sm">
        <div className="relative w-full md:max-w-md">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Search by customer name or phone number..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full pl-10 pr-4 py-2.5 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg text-sm text-slate-800 dark:text-white placeholder-slate-400 focus:bg-white dark:focus:bg-slate-900 focus:ring-2 focus:ring-indigo-500 outline-none transition-all"
          />
        </div>
        <div className="w-full md:w-[180px]">
          <select
            value={riskFilter}
            onChange={(e) => { setRiskFilter(e.target.value); setPage(1); }}
            className="w-full px-3 py-2.5 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 rounded-lg text-sm font-bold text-slate-700 dark:text-slate-200 focus:ring-2 focus:ring-indigo-500 outline-none transition-all"
          >
            <option value="">All Risk Levels</option>
            <option value="SAFE">Safe Risk Profile</option>
            <option value="WARNING">Warning Profile</option>
            <option value="CRITICAL">Critical Profile</option>
          </select>
        </div>
      </div>

      {/* Main Table Grid */}
      {isLoading ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-12 text-center text-slate-400 font-bold">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600 mx-auto mb-4"></div>
          Loading credit monitoring details...
        </div>
      ) : error ? (
        <div className="bg-rose-50 dark:bg-rose-950/20 text-rose-600 p-6 rounded-xl border border-rose-200 dark:border-rose-800">
          <h3 className="font-extrabold text-lg flex items-center gap-2">
            <AlertTriangle className="w-5 h-5" />
            Error loading credit details
          </h3>
          <p className="text-sm mt-1">{(error as any)?.message}</p>
        </div>
      ) : paginatedMonitors.length === 0 ? (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-12 text-center text-slate-400">
          No customer credit monitoring accounts found.
        </div>
      ) : (
        <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50 text-slate-500 dark:text-slate-400 font-bold">
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('customerName')}>
                    <span className="flex items-center gap-1.5">
                      Customer / Phone
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('creditLimit')}>
                    <span className="flex items-center gap-1.5">
                      Credit Limit
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('outstandingBalance')}>
                    <span className="flex items-center gap-1.5">
                      Outstanding
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('availableCredit')}>
                    <span className="flex items-center gap-1.5">
                      Available Credit
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('utilizationPercentage')}>
                    <span className="flex items-center gap-1.5">
                      Credit Utilization
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('overdueDays')}>
                    <span className="flex items-center gap-1.5">
                      Overdue Days
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('riskLevel')}>
                    <span className="flex items-center justify-center gap-1.5">
                      Risk Profile
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                  <th className="p-4 cursor-pointer hover:bg-slate-100 dark:hover:bg-slate-800" onClick={() => handleSort('isBlocked')}>
                    <span className="flex items-center justify-center gap-1.5">
                      Status
                      <ArrowUpDown className="w-3.5 h-3.5" />
                    </span>
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-150 dark:divide-slate-800/80">
                {paginatedMonitors.map(item => (
                  <tr key={item.customerId} className="hover:bg-slate-50 dark:hover:bg-slate-800/30 transition-colors">
                    <td className="p-4">
                      <div className="font-bold text-slate-800 dark:text-slate-100">{item.customerName}</div>
                      <div className="text-xs text-slate-400 mt-0.5">{item.phone}</div>
                    </td>
                    <td className="p-4 text-slate-700 dark:text-slate-300 font-bold">
                      {formatCurrency(item.creditLimit)}
                    </td>
                    <td className="p-4 text-rose-600 dark:text-rose-450 font-black">
                      {formatCurrency(item.outstandingBalance)}
                    </td>
                    <td className="p-4 text-emerald-600 dark:text-emerald-450 font-black">
                      {formatCurrency(item.availableCredit)}
                    </td>
                    <td className="p-4 min-w-[150px]">
                      <div className="flex items-center gap-3">
                        <div className="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-2">
                          <div 
                            className={`h-2 rounded-full transition-all ${getProgressBarColor(item.utilizationPercentage)}`}
                            style={{ width: `${Math.min(100, item.utilizationPercentage)}%` }}
                          ></div>
                        </div>
                        <span className="text-xs font-bold text-slate-600 dark:text-slate-400">
                          {Math.round(item.utilizationPercentage)}%
                        </span>
                      </div>
                    </td>
                    <td className="p-4 text-center font-mono">
                      {item.overdueDays > 0 ? (
                        <span className="text-rose-600 dark:text-rose-400 font-bold">{item.overdueDays} Days</span>
                      ) : (
                        <span className="text-slate-400">-</span>
                      )}
                    </td>
                    <td className="p-4 text-center">
                      <span className={`inline-block px-2.5 py-1 rounded-full text-xs font-black uppercase tracking-wider border ${getRiskBadgeClass(item.riskLevel)}`}>
                        {item.riskLevel}
                      </span>
                    </td>
                    <td className="p-4 text-center">
                      {item.isBlocked ? (
                        <span className="inline-flex items-center gap-1 text-xs font-bold text-rose-600 bg-rose-50 dark:bg-rose-950/20 border border-rose-200 dark:border-rose-800/40 px-2.5 py-1 rounded-full uppercase tracking-wider">
                          <XCircle className="w-3.5 h-3.5" />
                          Blocked
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-xs font-bold text-emerald-600 bg-emerald-50 dark:bg-emerald-950/20 border border-emerald-200 dark:border-emerald-800/40 px-2.5 py-1 rounded-full uppercase tracking-wider">
                          <CheckCircle className="w-3.5 h-3.5" />
                          Active
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="p-4 border-t border-slate-200 dark:border-slate-800 flex justify-between items-center bg-slate-50 dark:bg-slate-900/50">
              <span className="text-xs font-bold text-slate-500">
                Showing {((page - 1) * pageSize) + 1} to {Math.min(page * pageSize, totalItems)} of {totalItems} entries
              </span>
              <div className="flex gap-2">
                <button
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="p-2 border border-slate-300 dark:border-slate-700 rounded-lg text-slate-600 dark:text-slate-400 disabled:opacity-50 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>
                <span className="px-4 py-2 text-sm font-bold text-slate-700 dark:text-slate-300 flex items-center">
                  Page {page} of {totalPages}
                </span>
                <button
                  onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                  className="p-2 border border-slate-300 dark:border-slate-700 rounded-lg text-slate-600 dark:text-slate-400 disabled:opacity-50 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
