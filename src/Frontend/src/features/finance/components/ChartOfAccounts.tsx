import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getAccounts, Account } from '../services/finance.service';
import { Landmark, Plus, Search, ChevronRight, ChevronDown, CheckCircle2, XCircle } from 'lucide-react';

export const ChartOfAccounts: React.FC = () => {
  const [searchTerm, setSearchTerm] = useState('');
  const [expandedNodes, setExpandedNodes] = useState<Set<string>>(new Set());

  const { data: accounts, isLoading } = useQuery({
    queryKey: ['accounts'],
    queryFn: () => getAccounts(false, true)
  });

  const toggleNode = (id: string) => {
    const next = new Set(expandedNodes);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setExpandedNodes(next);
  };

  const renderTree = (nodes: Account[], depth = 0) => {
    if (!nodes) return null;
    return nodes.map(node => {
      const hasChildren = node.children && node.children.length > 0;
      const isExpanded = expandedNodes.has(node.id);
      
      // Filter logic: if search term exists, expand all to show matches (simplified here for brevity)
      const nodeName = node.name || '';
      const nodeCode = node.accountCode || '';
      if (searchTerm && !nodeName.toLowerCase().includes(searchTerm.toLowerCase()) && !nodeCode.includes(searchTerm)) {
        if (!hasChildren) return null; // hide leaf if no match
      }

      return (
        <React.Fragment key={node.id}>
          <div 
            className={`flex items-center p-3 border-b border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors ${depth === 0 ? 'bg-slate-50/50 dark:bg-slate-900/50 font-semibold' : ''}`}
            style={{ paddingLeft: `${depth * 24 + 12}px` }}
          >
            <div className="flex-1 flex items-center">
              {hasChildren ? (
                <button onClick={() => toggleNode(node.id)} className="p-1 text-slate-400 hover:text-indigo-600 transition-colors mr-1">
                  {isExpanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
                </button>
              ) : (
                <div className="w-6" /> // spacer
              )}
              <span className="text-indigo-600 font-mono text-sm font-bold mr-3">{node.accountCode}</span>
              <span className="text-slate-700 dark:text-slate-200">{node.name}</span>
            </div>
            <div className="w-48 text-sm text-slate-500">
              <span className={`px-2 py-1 rounded text-xs font-bold ${
                node.accountType === 'ASSET' ? 'bg-emerald-100 text-emerald-800' :
                node.accountType === 'LIABILITY' ? 'bg-rose-100 text-rose-800' :
                node.accountType === 'EQUITY' ? 'bg-purple-100 text-purple-800' :
                node.accountType === 'REVENUE' ? 'bg-blue-100 text-blue-800' :
                'bg-amber-100 text-amber-800'
              }`}>
                {node.accountType}
              </span>
            </div>
            <div className="w-24 text-center">
              {node.isActive ? <CheckCircle2 className="w-5 h-5 text-emerald-500 mx-auto" /> : <XCircle className="w-5 h-5 text-slate-300 mx-auto" />}
            </div>
          </div>
          {hasChildren && (isExpanded || searchTerm) && renderTree(node.children!, depth + 1)}
        </React.Fragment>
      );
    });
  };

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h2 className="text-2xl font-extrabold text-slate-800 dark:text-white flex items-center gap-3">
            <Landmark className="w-7 h-7 text-indigo-600" />
            Chart of Accounts
          </h2>
          <p className="text-slate-500 dark:text-slate-400 mt-1">Manage the ERP general ledger hierarchy</p>
        </div>
        <button className="bg-indigo-600 hover:bg-indigo-700 text-white px-4 py-2.5 rounded-lg font-bold flex items-center gap-2 shadow-lg shadow-indigo-600/30 transition-all">
          <Plus className="w-5 h-5" />
          Add Account
        </button>
      </div>

      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
        <div className="p-4 border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-950 flex gap-4">
          <div className="relative flex-1 max-w-md">
            <Search className="w-5 h-5 absolute left-3 top-2.5 text-slate-400" />
            <input 
              type="text"
              placeholder="Search by code or name..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-10 pr-4 py-2 bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 rounded-lg text-sm text-slate-900 dark:text-white focus:ring-2 focus:ring-indigo-500 outline-none"
            />
          </div>
        </div>

        <div className="flex p-3 bg-slate-100 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800 text-xs font-bold text-slate-500 uppercase tracking-wider">
          <div className="flex-1 pl-12">Account Code & Name</div>
          <div className="w-48">Type</div>
          <div className="w-24 text-center">Status</div>
        </div>

        <div className="min-h-[400px]">
          {isLoading ? (
            <div className="flex justify-center items-center h-48 text-slate-400">Loading accounts...</div>
          ) : accounts?.length ? (
            renderTree(accounts)
          ) : (
            <div className="text-center p-12 text-slate-500">No accounts found. Please seed the database.</div>
          )}
        </div>
      </div>
    </div>
  );
};
