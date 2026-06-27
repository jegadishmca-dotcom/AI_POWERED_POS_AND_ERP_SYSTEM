import React, { useState, useCallback } from 'react';
import { Users, Plus, Search, Phone, Star, CreditCard, RefreshCw, UserCheck, X } from 'lucide-react';
import { searchCustomers, registerCustomer, CustomerDto } from '../api/crm.api';
import { CustomerRegistrationModal } from '../components/CustomerRegistrationModal';

export const Customers = () => {
  const [searchQuery, setSearchQuery] = useState('');
  const [customers, setCustomers] = useState<CustomerDto[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [isAddModalOpen, setAddModalOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleSearch = useCallback(async (q: string) => {
    setIsSearching(true);
    setHasSearched(true);
    setErrorMessage(null);
    try {
      const results = await searchCustomers(q);
      setCustomers(results);
    } catch (err: any) {
      setErrorMessage('Failed to search customers. Please try again.');
      setCustomers([]);
    } finally {
      setIsSearching(false);
    }
  }, []);

  const handleSearchInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setSearchQuery(val);
    if (val.length === 0) {
      // Show all on empty
      handleSearch('');
    } else if (val.length >= 2) {
      handleSearch(val);
    }
  };

  const handleRegister = async (payload: any) => {
    const result = await registerCustomer({
      phone: payload.phone,
      name: payload.name,
      tamilName: payload.tamilName,
      dob: payload.dob,
      marketingConsent: payload.marketingConsent,
    });
    setSuccessMessage(`Customer "${payload.name}" registered successfully!`);
    setTimeout(() => setSuccessMessage(null), 4000);
    // Refresh list
    handleSearch(searchQuery || '');
    return result;
  };

  // Load all customers on mount
  React.useEffect(() => {
    handleSearch('');
  }, []);

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <Users className="w-6 h-6 text-rose-600" />
            CRM Master
          </h1>
          <p className="text-slate-500 dark:text-slate-400 mt-1">
            Manage customers, loyalty points, and CRM data
          </p>
        </div>
        <button
          onClick={() => setAddModalOpen(true)}
          className="bg-rose-600 hover:bg-rose-700 text-white px-4 py-2.5 rounded-lg font-semibold flex items-center gap-2 transition-colors shadow-sm"
        >
          <Plus className="w-4 h-4" />
          Add Customer
        </button>
      </div>

      {/* Success/Error messages */}
      {successMessage && (
        <div className="bg-emerald-50 dark:bg-emerald-900/30 border border-emerald-200 dark:border-emerald-700 text-emerald-700 dark:text-emerald-300 px-4 py-3 rounded-lg flex items-center justify-between">
          <span className="flex items-center gap-2"><UserCheck className="w-4 h-4" /> {successMessage}</span>
          <button onClick={() => setSuccessMessage(null)}><X className="w-4 h-4" /></button>
        </div>
      )}
      {errorMessage && (
        <div className="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 text-red-700 dark:text-red-300 px-4 py-3 rounded-lg flex items-center justify-between">
          <span>{errorMessage}</span>
          <button onClick={() => setErrorMessage(null)}><X className="w-4 h-4" /></button>
        </div>
      )}

      {/* Search Bar */}
      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-4">
        <div className="flex gap-3">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <input
              type="text"
              placeholder="Search by name, phone number, or ID..."
              value={searchQuery}
              onChange={handleSearchInput}
              onKeyDown={(e) => e.key === 'Enter' && handleSearch(searchQuery)}
              className="w-full pl-9 pr-4 py-2.5 border border-slate-200 dark:border-slate-700 rounded-lg text-sm bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-rose-500"
            />
          </div>
          <button
            onClick={() => handleSearch(searchQuery)}
            disabled={isSearching}
            className="px-4 py-2.5 bg-rose-600 hover:bg-rose-700 text-white rounded-lg font-medium text-sm flex items-center gap-2 transition-colors disabled:opacity-50"
          >
            {isSearching ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Search className="w-4 h-4" />}
            Search
          </button>
        </div>
      </div>

      {/* Customer Table */}
      <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
        {isSearching ? (
          <div className="flex items-center justify-center py-16">
            <RefreshCw className="w-8 h-8 animate-spin text-rose-500 mr-3" />
            <span className="text-slate-500 dark:text-slate-400">Loading customers...</span>
          </div>
        ) : customers.length === 0 && hasSearched ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <Users className="w-16 h-16 text-slate-200 dark:text-slate-700 mb-4" />
            <h3 className="text-lg font-bold text-slate-600 dark:text-slate-300 mb-2">
              {searchQuery ? 'No customers found' : 'No customers yet'}
            </h3>
            <p className="text-slate-400 dark:text-slate-500 text-sm max-w-xs">
              {searchQuery
                ? `No results for "${searchQuery}". Try a different search term.`
                : 'Click "+ Add Customer" to register your first customer.'}
            </p>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-slate-200 dark:divide-slate-800">
            <thead className="bg-slate-50 dark:bg-slate-800/50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">Customer</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">Phone</th>
                <th className="px-6 py-3 text-left text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">Tier</th>
                <th className="px-6 py-3 text-right text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">Loyalty Points</th>
                <th className="px-6 py-3 text-right text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">Wallet Balance</th>
              </tr>
            </thead>
            <tbody className="bg-white dark:bg-slate-900 divide-y divide-slate-100 dark:divide-slate-800">
              {customers.map((customer) => (
                <tr key={customer.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors">
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-3">
                      <div className="w-9 h-9 rounded-full bg-rose-100 dark:bg-rose-900/30 flex items-center justify-center text-rose-600 dark:text-rose-400 font-bold text-sm">
                        {customer.name.charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <p className="font-semibold text-slate-900 dark:text-white text-sm">{customer.name}</p>
                        <p className="text-xs text-slate-400 dark:text-slate-500 font-mono">{customer.id.slice(0, 8).toUpperCase()}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <span className="flex items-center gap-1.5 text-sm text-slate-600 dark:text-slate-300">
                      <Phone className="w-3.5 h-3.5 text-slate-400" />
                      {customer.phone}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold ${
                      customer.tierName === 'Gold' ? 'bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-400' :
                      customer.tierName === 'Silver' ? 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300' :
                      customer.tierName === 'Platinum' ? 'bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-400' :
                      'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400'
                    }`}>
                      {customer.tierName || 'Standard'}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right">
                    <span className="flex items-center justify-end gap-1 text-sm font-semibold text-amber-600 dark:text-amber-400">
                      <Star className="w-3.5 h-3.5" />
                      {customer.loyaltyPoints?.toFixed(0) ?? '0'}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right">
                    <span className="flex items-center justify-end gap-1 text-sm font-semibold text-emerald-600 dark:text-emerald-400">
                      <CreditCard className="w-3.5 h-3.5" />
                      ₹{customer.walletBalance?.toFixed(2) ?? '0.00'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {customers.length > 0 && (
          <div className="px-6 py-3 bg-slate-50 dark:bg-slate-800/50 border-t border-slate-100 dark:border-slate-800">
            <p className="text-xs text-slate-400 dark:text-slate-500">
              Showing {customers.length} customer{customers.length !== 1 ? 's' : ''}
              {searchQuery && ` for "${searchQuery}"`}
            </p>
          </div>
        )}
      </div>

      {/* Add Customer Modal */}
      <CustomerRegistrationModal
        isOpen={isAddModalOpen}
        onClose={() => setAddModalOpen(false)}
        onRegister={handleRegister}
        initialPhone=""
      />
    </div>
  );
};
