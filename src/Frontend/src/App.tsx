import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate, Link, useLocation, useNavigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { 
  LayoutDashboard, 
  ShoppingCart, 
  Package, 
  ClipboardCheck, 
  ArrowUpDown, 
  History, 
  Layers, 
  MapPin, 
  LogOut, 
  User as UserIcon, 
  Terminal,
  Settings as SettingsIcon,
  BarChart3,
  Sparkles,
  Bot,
  TrendingUp,
  TrendingDown,
  ShieldAlert,
  Palette,
  Wallet,
  Landmark,
  FileText,
  PieChart,
  BookOpen,
  CalendarClock
} from 'lucide-react';
import { useAuthStore } from './features/auth/store/auth.store';
import { Login } from './features/auth/routes/Login';
import { ProtectedRoute } from './features/auth/components/ProtectedRoute';
import { Dashboard } from './features/analytics/components/Dashboard';
import { ReportsHub } from './features/analytics/components/ReportsHub';
import { PosTerminal } from './features/pos/components/PosTerminal';
import { Products } from './features/catalog/routes/Products';
import { GrnForm } from './features/inventory/components/GrnForm';
import { StockAdjustmentForm } from './features/inventory/components/StockAdjustmentForm';
import { StockTakeForm } from './features/inventory/components/StockTakeForm';
import { StockLedgerView } from './features/inventory/components/StockLedgerView';
import { StockPositionReport } from './features/inventory/components/StockPositionReport';
import { WarehouseLocationsList } from './features/inventory/components/WarehouseLocationsList';
import { ShiftReport } from './features/pos/components/ShiftReport';
import { BusinessDateDashboard } from './features/pos/components/BusinessDateDashboard';
import { Suppliers } from './features/purchasing/routes/Suppliers';
import { PurchaseOrders } from './features/purchasing/routes/PurchaseOrders';
import { Settings } from './pages/Settings';
import { AiInvoiceImport } from './features/inventory/components/AiInvoiceImport';
import { AiChat } from './features/ai/routes/AiChat';
import { AiForecaster } from './features/ai/routes/AiForecaster';
import { AiMarkdowns } from './features/ai/routes/AiMarkdowns';
import { LossPreventionDashboard } from './features/analytics/components/LossPreventionDashboard';

// Finance Phase F1
import { FinanceDashboard } from './features/finance/components/FinanceDashboard';
import { ChartOfAccounts } from './features/finance/components/ChartOfAccounts';
import { JournalEntries } from './features/finance/components/JournalEntries';
import { TrialBalance } from './features/finance/components/TrialBalance';
import { ProfitAndLoss } from './features/finance/components/ProfitAndLoss';
import { BalanceSheet } from './features/finance/components/BalanceSheet';

// Finance Phase F2
import { SupplierBills } from './features/finance/components/SupplierBills';
import { SupplierPayments } from './features/finance/components/SupplierPayments';
import { SupplierLedger } from './features/finance/components/SupplierLedger';
import { APAging } from './features/finance/components/APAging';
import { CustomerReceipts } from './features/finance/components/CustomerReceipts';
import { CustomerLedger } from './features/finance/components/CustomerLedger';
import { ARAging } from './features/finance/components/ARAging';
import { CreditLimitMonitoring } from './features/finance/components/CreditLimitMonitoring';

const AppLayout: React.FC = () => {
  const { user, clearAuth } = useAuthStore();
  const location = useLocation();
  const navigate = useNavigate();
  const [sidebarCollapsed, setSidebarCollapsed] = React.useState(false);
  const [theme, setTheme] = React.useState(() => {
    return localStorage.getItem('erp_theme') || 'blue';
  });
  const [showThemeDropdown, setShowThemeDropdown] = React.useState(false);

  React.useEffect(() => {
    const root = document.documentElement;
    root.classList.remove('theme-blue', 'theme-green', 'theme-orange', 'theme-purple', 'theme-obsidian');
    root.classList.add(`theme-${theme}`);
    localStorage.setItem('erp_theme', theme);
  }, [theme]);

  const handleLogout = () => {
    clearAuth();
    navigate('/login');
  };

  const navItems = [
    { path: '/dashboard', name: 'Dashboard', icon: LayoutDashboard, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/reports', name: 'Reports & Insights', icon: BarChart3, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/pos', name: 'POS Billing', icon: ShoppingCart, roles: ['Owner', 'Manager', 'Cashier'], category: 'general' },
    { path: '/shift-report', name: 'Shift & Sales Report', icon: ClipboardCheck, roles: ['Cashier'], category: 'general' },
    { path: '/eod', name: 'End of Day (EOD)', icon: ClipboardCheck, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/products', name: 'Product Catalog', icon: Package, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/grn', name: 'Goods Receipt (GRN)', icon: ClipboardCheck, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/suppliers', name: 'Supplier Master', icon: UserIcon, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/purchase-orders', name: 'Purchase Orders', icon: ClipboardCheck, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/stock-adjustment', name: 'Stock Adjustment', icon: ArrowUpDown, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/stock-take', name: 'Stock Take', icon: ClipboardCheck, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/stock-ledger', name: 'Stock Ledger', icon: History, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/stock-position', name: 'Stock Position', icon: Layers, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/warehouses', name: 'Warehouses & Bins', icon: MapPin, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/settings', name: 'Settings', icon: SettingsIcon, roles: ['Owner', 'Manager'], category: 'general' },

    // AI Co-Pilot Hub
    { path: '/ai/chat', name: 'AI Co-pilot Chat', icon: Bot, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/forecaster', name: 'AI Demand Forecaster', icon: TrendingUp, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/markdowns', name: 'AI Smart Markdowns', icon: TrendingDown, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/invoice-extractor', name: 'AI Invoice Extractor', icon: Sparkles, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/loss-prevention', name: 'AI Fraud Detection', icon: ShieldAlert, roles: ['Owner', 'Manager'], category: 'ai' },

    // Finance Phase F1
    { path: '/finance/dashboard', name: 'Finance Dashboard', icon: PieChart, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/accounts', name: 'Chart of Accounts', icon: Landmark, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/journals', name: 'Journal Entries', icon: FileText, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/trial-balance', name: 'Trial Balance', icon: Wallet, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/profit-and-loss', name: 'Profit & Loss', icon: BarChart3, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/balance-sheet', name: 'Balance Sheet', icon: Landmark, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },

    // Finance Phase F2 - AP
    { path: '/finance/supplier-bills', name: 'Supplier Bills (AP)', icon: FileText, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/supplier-payments', name: 'Supplier Payments', icon: Banknote, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/supplier-ledger', name: 'Supplier Ledger', icon: BookOpen, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/ap-aging', name: 'AP Aging', icon: CalendarClock, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },

    // Finance Phase F2 - AR
    { path: '/finance/customer-receipts', name: 'Customer Receipts (AR)', icon: Banknote, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/customer-ledger', name: 'Customer Ledger', icon: BookOpen, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/ar-aging', name: 'AR Aging', icon: CalendarClock, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/credit-limit', name: 'Credit Monitoring', icon: ShieldAlert, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
  ];

  const filteredNavItems = navItems.filter(item => 
    !user?.role || item.roles.includes(user.role)
  );

  const generalItems = filteredNavItems.filter(item => item.category === 'general');
  const aiItems = filteredNavItems.filter(item => item.category === 'ai');
  const financeItems = filteredNavItems.filter(item => item.category === 'finance');

  return (
    <div className="flex h-screen bg-slate-100 dark:bg-slate-900 font-sans transition-colors duration-200 overflow-hidden">
      {/* Sidebar */}
      <aside className={`transition-all duration-350 ${sidebarCollapsed ? 'w-0 overflow-hidden opacity-0' : 'w-64 opacity-100'} bg-slate-900 text-white flex flex-col shadow-xl z-20`}>
        {/* Brand Header */}
        <div className="h-16 flex items-center px-6 bg-slate-950 border-b border-slate-800">
          <Terminal className="w-6 h-6 mr-3 text-indigo-400" />
          <span className="font-extrabold text-lg bg-gradient-to-r from-white to-slate-400 bg-clip-text text-transparent">
            Supermarket ERP
          </span>
        </div>

        {/* Sidebar Nav */}
        <nav className="flex-1 px-4 py-4 space-y-6 overflow-y-auto">
          {/* General Section */}
          <div className="space-y-1">
            <span className="px-4 text-[10px] font-bold text-slate-500 uppercase tracking-wider block mb-2">
              ERP Core Modules
            </span>
            {generalItems.map(item => {
              const Icon = item.icon;
              const isActive = location.pathname === item.path;
              return (
                <Link
                  key={item.path}
                  to={item.path}
                  className={`flex items-center px-4 py-2.5 rounded-lg text-sm font-semibold transition-all duration-200 ${
                    isActive 
                      ? 'bg-indigo-600 text-white shadow-lg shadow-indigo-600/30' 
                      : 'text-slate-400 hover:bg-slate-800 hover:text-white'
                  }`}
                >
                  <Icon className={`w-5 h-5 mr-3 ${isActive ? 'text-white' : 'text-slate-400'}`} />
                  {item.name}
                </Link>
              );
            })}
          </div>

          {/* Finance Section */}
          {financeItems.length > 0 && (
            <div className="space-y-1">
              <span className="px-4 text-[10px] font-bold text-slate-500 uppercase tracking-wider block mt-4 mb-2">
                Finance & Accounting
              </span>
              {financeItems.map(item => {
                const Icon = item.icon;
                const isActive = location.pathname.startsWith(item.path);
                return (
                  <Link
                    key={item.path}
                    to={item.path}
                    className={`flex items-center px-4 py-2.5 rounded-lg text-sm font-semibold transition-all duration-200 ${
                      isActive 
                        ? 'bg-blue-600 text-white shadow-lg shadow-blue-600/30' 
                        : 'text-slate-400 hover:bg-slate-800 hover:text-white'
                    }`}
                  >
                    <Icon className={`w-5 h-5 mr-3 ${isActive ? 'text-white' : 'text-slate-400'}`} />
                    {item.name}
                  </Link>
                );
              })}
            </div>
          )}

          {/* AI Co-Pilot Section */}
          {aiItems.length > 0 && (
            <div className="space-y-1">
              <div className="px-4 flex items-center gap-1.5 mb-2">
                <Sparkles className="w-3.5 h-3.5 text-indigo-400 animate-pulse" />
                <span className="text-[10px] font-extrabold text-indigo-400 uppercase tracking-wider block">
                  AI Co-Pilot Hub
                </span>
              </div>
              {aiItems.map(item => {
                const Icon = item.icon;
                const isActive = location.pathname === item.path;
                return (
                  <Link
                    key={item.path}
                    to={item.path}
                    className={`flex items-center px-4 py-2.5 rounded-lg text-sm font-semibold transition-all duration-200 ${
                      isActive 
                        ? 'bg-gradient-to-r from-indigo-600 to-violet-600 text-white shadow-lg shadow-indigo-600/30' 
                        : 'text-slate-400 hover:bg-slate-800 hover:text-white'
                    }`}
                  >
                    <Icon className={`w-5 h-5 mr-3 ${isActive ? 'text-white' : 'text-slate-400'}`} />
                    {item.name}
                  </Link>
                );
              })}
            </div>
          )}
        </nav>


        {/* User Card */}
        <div className="p-4 border-t border-slate-800 bg-slate-950">
          <div className="flex items-center justify-between">
            <div className="flex items-center min-w-0">
              <div className="w-9 h-9 rounded-full bg-indigo-600 flex items-center justify-center text-white font-bold shrink-0 shadow">
                {user?.fullName?.charAt(0) || 'U'}
              </div>
              <div className="ml-3 min-w-0">
                <p className="text-sm font-bold text-white truncate">{user?.fullName || 'Active User'}</p>
                <p className="text-xs text-indigo-400 truncate">{user?.role || 'Staff'}</p>
              </div>
            </div>
            <button 
              onClick={handleLogout}
              className="p-2 text-slate-400 hover:text-red-400 hover:bg-slate-800 rounded-lg transition-all duration-150"
              title="Sign Out"
            >
              <LogOut className="w-5 h-5" />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Top Header */}
        <header className="h-16 bg-white dark:bg-slate-800 border-b border-slate-200 dark:border-slate-700 flex items-center justify-between px-8 shadow-sm transition-colors duration-200">
          <div className="flex items-center gap-4">
            <button
              onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
              className="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 transition-colors flex items-center justify-center border border-slate-200 dark:border-slate-700 shadow-sm"
              title={sidebarCollapsed ? "Expand Sidebar" : "Collapse Sidebar"}
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                {sidebarCollapsed ? (
                  <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
                ) : (
                  <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
                )}
              </svg>
            </button>
            <h1 className="text-xl font-extrabold text-slate-800 dark:text-white flex items-center">
              {navItems.find(item => item.path === location.pathname)?.name || 'ERP System'}
              <span className="ml-3 text-xs font-bold text-slate-500 bg-slate-200 dark:bg-slate-700 dark:text-slate-300 px-2 py-1 rounded">v1.3</span>
            </h1>
          </div>
          <div className="flex items-center space-x-4">
            <div className="text-right hidden sm:block">
              <span className="text-xs font-semibold text-slate-500 dark:text-slate-400 block">Business Date</span>
              <span className="text-sm font-bold text-slate-800 dark:text-slate-200">{new Date().toLocaleDateString('en-IN', { dateStyle: 'medium' })}</span>
            </div>
            <div className="h-8 w-px bg-slate-200 dark:bg-slate-700 hidden sm:block"></div>
            {/* Theme Selector */}
            <div className="relative">
              <button 
                onClick={() => setShowThemeDropdown(!showThemeDropdown)}
                className="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-500 dark:text-slate-400 transition-colors flex items-center justify-center border border-slate-200 dark:border-slate-700 shadow-sm gap-1.5"
                title="Change Theme Palette"
              >
                <Palette className="w-4 h-4 text-slate-500 dark:text-slate-400" />
                <span className="text-xs font-bold capitalize hidden md:inline">{theme} Theme</span>
              </button>

              {showThemeDropdown && (
                <>
                  <div className="fixed inset-0 z-40" onClick={() => setShowThemeDropdown(false)} />
                  <div className="absolute right-0 mt-2 w-48 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl shadow-lg py-2 z-50 animate-in fade-in slide-in-from-top-2 duration-150">
                    <div className="px-3 py-1.5 text-[10px] font-bold text-slate-400 dark:text-slate-500 uppercase tracking-wider">
                      Select System Theme
                    </div>
                    {[
                      { id: 'blue', name: 'Classic Royal Blue', colorClass: 'bg-blue-600' },
                      { id: 'green', name: 'Premium Emerald Mint', colorClass: 'bg-emerald-600' },
                      { id: 'orange', name: 'Sunset Terracotta', colorClass: 'bg-orange-600' },
                      { id: 'purple', name: 'Royal Velvet Purple', colorClass: 'bg-purple-600' },
                      { id: 'obsidian', name: 'Midnight Obsidian', colorClass: 'bg-slate-800' },
                    ].map(t => (
                      <button
                        key={t.id}
                        onClick={() => {
                          setTheme(t.id);
                          setShowThemeDropdown(false);
                        }}
                        className={`w-full px-4 py-2.5 text-left text-xs font-bold text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-700 flex items-center gap-3 transition-colors ${
                          theme === t.id ? 'bg-slate-50 dark:bg-slate-700' : ''
                        }`}
                      >
                        <span className={`w-3.5 h-3.5 rounded-full border border-black/10 ${t.colorClass}`} />
                        {t.name}
                      </button>
                    ))}
                  </div>
                </>
              )}
            </div>
            <div className="h-8 w-px bg-slate-200 dark:bg-slate-700 hidden sm:block"></div>
            <div className="flex items-center space-x-2 text-slate-700 dark:text-slate-200">
              <Terminal className="w-4 h-4 text-emerald-500" />
              <span className="text-xs font-black tracking-wider uppercase bg-emerald-100 dark:bg-emerald-950 text-emerald-800 dark:text-emerald-300 px-2.5 py-1 rounded">
                Terminal {localStorage.getItem('pos_terminal_code') || '01'}
              </span>
            </div>
          </div>
        </header>

        {/* Screen Content Wrapper */}
        <main className="flex-1 overflow-y-auto bg-slate-50 dark:bg-slate-900 transition-colors duration-200 relative">
          <Routes>
            <Route path="/dashboard" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Dashboard />
            } />
            <Route path="/reports" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <ReportsHub />
            } />
            <Route path="/pos" element={<PosTerminal />} />
            <Route path="/shift-report" element={<ShiftReport />} />
            <Route path="/eod" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <BusinessDateDashboard />
            } />
            <Route path="/products" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Products />
            } />
            <Route path="/grn" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <GrnForm />
            } />
            <Route path="/suppliers" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Suppliers />
            } />
            <Route path="/purchase-orders" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <PurchaseOrders />
            } />
            <Route path="/stock-adjustment" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <StockAdjustmentForm />
            } />
            <Route path="/stock-take" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <StockTakeForm />
            } />
            <Route path="/stock-ledger" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <StockLedgerView />
            } />
            <Route path="/stock-position" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <StockPositionReport />
            } />
            <Route path="/ai-invoice-import" element={
              <Navigate to="/ai/invoice-extractor" replace />
            } />
            <Route path="/ai/invoice-extractor" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <AiInvoiceImport />
            } />
            <Route path="/ai/chat" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <AiChat />
            } />
            <Route path="/ai/forecaster" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <AiForecaster />
            } />
            <Route path="/ai/markdowns" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <AiMarkdowns />
            } />
            <Route path="/ai/loss-prevention" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <LossPreventionDashboard />
            } />
            <Route path="/warehouses" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <WarehouseLocationsList />
            } />
            <Route path="/settings" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Settings />
            } />
            {/* Finance F1 Routes */}
            <Route path="/finance/dashboard" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><FinanceDashboard /></ProtectedRoute>} />
            <Route path="/finance/accounts" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><ChartOfAccounts /></ProtectedRoute>} />
            <Route path="/finance/journals" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><JournalEntries /></ProtectedRoute>} />
            <Route path="/finance/trial-balance" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><TrialBalance /></ProtectedRoute>} />
            <Route path="/finance/profit-and-loss" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><ProfitAndLoss /></ProtectedRoute>} />
            <Route path="/finance/balance-sheet" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><BalanceSheet /></ProtectedRoute>} />
            
            {/* Finance F2 Routes */}
            <Route path="/finance/supplier-bills" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><SupplierBills /></ProtectedRoute>} />
            <Route path="/finance/supplier-payments" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><SupplierPayments /></ProtectedRoute>} />
            <Route path="/finance/supplier-ledger" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><SupplierLedger /></ProtectedRoute>} />
            <Route path="/finance/ap-aging" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><APAging /></ProtectedRoute>} />
            <Route path="/finance/customer-receipts" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><CustomerReceipts /></ProtectedRoute>} />
            <Route path="/finance/customer-ledger" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><CustomerLedger /></ProtectedRoute>} />
            <Route path="/finance/ar-aging" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><ARAging /></ProtectedRoute>} />
            <Route path="/finance/credit-limit" element={<ProtectedRoute allowedRoles={['Owner', 'Manager', 'Accountant']}><CreditLimitMonitoring /></ProtectedRoute>} />
            
            <Route path="/" element={<Navigate to={user?.role === 'Cashier' ? "/pos" : "/finance/dashboard"} replace />} />
            <Route path="*" element={<Navigate to={user?.role === 'Cashier' ? "/pos" : "/finance/dashboard"} replace />} />
          </Routes>
        </main>
      </div>
    </div>
  );
};

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
});

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Router>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route element={<ProtectedRoute />}>
            <Route path="/*" element={<AppLayout />} />
          </Route>
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
      </Router>
    </QueryClientProvider>
  );
}

export default App;
