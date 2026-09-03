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
  Users,
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
  CalendarClock,
  Banknote,
  Tag,
  Activity,
  Trophy,
  BarChart2
} from 'lucide-react';
import { useAuthStore } from './features/auth/store/auth.store';
import { api } from './utils/api';
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
import { ProcurementDashboard } from './features/purchasing/components/ProcurementDashboard';
import { SupplierDashboard } from './features/purchasing/components/SupplierDashboard';
import { Settings } from './pages/Settings';
import { PriceChangeModule } from './features/catalog/components/PriceChangeModule';
import { OffersManager } from './features/offers/components/OffersManager';
import { Customers } from './features/crm/routes/Customers';
import { AiInvoiceImport } from './features/inventory/components/AiInvoiceImport';
import { AiChat } from './features/ai/routes/AiChat';
import { AiForecaster } from './features/ai/routes/AiForecaster';
import { AiMarkdowns } from './features/ai/routes/AiMarkdowns';
import { LossPreventionDashboard } from './features/analytics/components/LossPreventionDashboard';
import { ExecutiveDashboard } from './features/ai/routes/ExecutiveDashboard';
import { ForecastDashboard } from './features/ai/routes/ForecastDashboard';
import { StorePerformanceDashboard } from './features/ai/routes/StorePerformanceDashboard';
import { HealthDashboard } from './features/ai/routes/HealthDashboard';

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

  const [isUat, setIsUat] = React.useState(false);
  const [tenantName, setTenantName] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (user) {
      api.get('/api/environment/mode')
        .then(res => {
          setIsUat(!!res.data.isUat);
          setTenantName(res.data.tenantName || null);
        })
        .catch(err => {
          console.error("Failed to load environment status", err);
        });
    }
  }, [user, location.pathname]);

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
    // ERP Core Modules (Product Catalog & Operations)
    { path: '/dashboard', name: 'Dashboard', icon: LayoutDashboard, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/pos', name: 'POS Billing', icon: ShoppingCart, roles: ['Owner', 'Manager', 'Cashier'], category: 'general' },
    { path: '/shift-report', name: 'Shift & Sales Report', icon: ClipboardCheck, roles: ['Cashier'], category: 'general' },
    { path: '/eod', name: 'End of Day (EOD)', icon: CalendarClock, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/products', name: 'Product Catalog', icon: Package, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/price-management', name: 'Price Management', icon: Tag, roles: ['Owner', 'Manager'], category: 'general' }, // Moved under Product Catalog!
    { path: '/grn', name: 'Goods Receipt (GRN)', icon: ClipboardCheck, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/suppliers', name: 'Supplier Master', icon: UserIcon, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/purchase-orders', name: 'Purchase Orders', icon: ClipboardCheck, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/stock-adjustment', name: 'Stock Adjustment', icon: ArrowUpDown, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/stock-take', name: 'Stock Take', icon: ClipboardCheck, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/stock-ledger', name: 'Stock Ledger', icon: History, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/stock-position', name: 'Stock Position', icon: Layers, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/warehouses', name: 'Warehouses & Bins', icon: MapPin, roles: ['Owner', 'Manager'], category: 'general' },
    { path: '/reports', name: 'Reports & Insights', icon: BarChart3, roles: ['Owner', 'Manager'], category: 'general' },

    // CRM & Marketing
    { path: '/customers', name: 'CRM Master', icon: Users, roles: ['Owner', 'Manager'], category: 'crm' },
    { path: '/offers', name: 'Offers & Promotions', icon: Tag, roles: ['Owner', 'Manager'], category: 'crm' },

    // Finance & Accounting
    { path: '/finance/dashboard', name: 'Finance Dashboard', icon: PieChart, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/accounts', name: 'Chart of Accounts', icon: Landmark, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/journals', name: 'Journal Entries', icon: FileText, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/trial-balance', name: 'Trial Balance', icon: Wallet, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/profit-and-loss', name: 'Profit & Loss', icon: BarChart3, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/balance-sheet', name: 'Balance Sheet', icon: Landmark, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/supplier-bills', name: 'Supplier Bills (AP)', icon: FileText, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/supplier-payments', name: 'Supplier Payments', icon: Banknote, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/supplier-ledger', name: 'Supplier Ledger', icon: BookOpen, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/ap-aging', name: 'AP Aging', icon: CalendarClock, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/customer-receipts', name: 'Customer Receipts (AR)', icon: Banknote, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/customer-ledger', name: 'Customer Ledger', icon: BookOpen, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/ar-aging', name: 'AR Aging', icon: CalendarClock, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },
    { path: '/finance/credit-limit', name: 'Credit Monitoring', icon: ShieldAlert, roles: ['Owner', 'Manager', 'Accountant'], category: 'finance' },

    // AI Co-Pilot Hub
    { path: '/ai/executive', name: 'Executive Intelligence', icon: BarChart2, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/forecast-dashboard', name: 'Forecast Trends', icon: TrendingUp, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/store-performance', name: 'Store Benchmarks', icon: Trophy, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/health', name: 'System Health', icon: Activity, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/chat', name: 'AI Co-pilot Chat', icon: Bot, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/forecaster', name: 'AI Demand Forecaster', icon: TrendingUp, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/markdowns', name: 'AI Smart Markdowns', icon: TrendingDown, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/invoice-extractor', name: 'AI Invoice Extractor', icon: Sparkles, roles: ['Owner', 'Manager'], category: 'ai' },
    { path: '/ai/loss-prevention', name: 'AI Fraud Detection', icon: ShieldAlert, roles: ['Owner', 'Manager'], category: 'ai' },

    // System Administration (NEW MENU CATEGORY)
    { path: '/settings', name: 'System Settings', icon: SettingsIcon, roles: ['Owner', 'Manager'], category: 'admin' },
  ];

  const userRole = user?.role || 'Staff';
  const filteredNavItems = navItems.filter(item => 
    !user?.role || item.roles.includes(user.role)
  );

  const generalItems = filteredNavItems.filter(item => item.category === 'general');
  const crmItems = filteredNavItems.filter(item => item.category === 'crm');
  const financeItems = filteredNavItems.filter(item => item.category === 'finance');
  const aiItems = filteredNavItems.filter(item => item.category === 'ai');
  const adminItems = filteredNavItems.filter(item => item.category === 'admin');

  return (
    <div className="flex h-screen bg-slate-100 dark:bg-slate-900 font-sans transition-colors duration-200 overflow-hidden">
      {/* Sidebar */}
      <aside className={`transition-all duration-350 ${sidebarCollapsed ? 'w-0 overflow-hidden opacity-0' : 'w-64 opacity-100'} bg-gradient-to-b from-slate-950 via-slate-900 to-slate-950 text-white flex flex-col shadow-2xl z-20 border-r border-slate-800/80`}>
        {/* Brand Header */}
        <div className="h-16 flex items-center justify-between px-5 bg-slate-950/90 border-b border-slate-800/80 backdrop-blur-md">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-xl bg-gradient-to-tr from-indigo-600 to-violet-500 shadow-md shadow-indigo-500/20 text-white">
              <Terminal className="w-5 h-5" />
            </div>
            <div>
              <span className="font-black text-base bg-gradient-to-r from-white via-slate-200 to-indigo-200 bg-clip-text text-transparent block leading-tight">
                Supermarket ERP
              </span>
              <span className="text-[9px] font-extrabold uppercase tracking-widest text-indigo-400 block">
                Enterprise AI Edition
              </span>
            </div>
          </div>
        </div>

        {/* Sidebar Nav */}
        <nav className="flex-1 px-3 py-4 space-y-6 overflow-y-auto custom-scrollbar">
          {/* ERP Core Modules */}
          <div className="space-y-1">
            <div className="px-3 flex items-center justify-between mb-2">
              <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">
                ERP Core Modules
              </span>
              <span className="text-[9px] font-bold px-1.5 py-0.5 rounded bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                Core
              </span>
            </div>
            {generalItems.map(item => {
              const Icon = item.icon;
              const isActive = location.pathname === item.path;
              return (
                <Link
                  key={item.path}
                  to={item.path}
                  className={`group relative flex items-center px-3.5 py-2.5 rounded-xl text-xs font-bold transition-all duration-200 ${
                    isActive 
                      ? 'bg-gradient-to-r from-indigo-600 to-indigo-700 text-white shadow-lg shadow-indigo-600/30 font-black' 
                      : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-100'
                  }`}
                >
                  {isActive && (
                    <span className="absolute left-0 top-2 bottom-2 w-1 bg-indigo-300 rounded-r-md" />
                  )}
                  <Icon className={`w-4 h-4 mr-3 transition-transform group-hover:scale-110 ${isActive ? 'text-white' : 'text-slate-400 group-hover:text-indigo-300'}`} />
                  <span className="truncate">{item.name}</span>
                </Link>
              );
            })}
          </div>

          {/* CRM & Marketing Section */}
          {crmItems.length > 0 && (
            <div className="space-y-1">
              <div className="px-3 flex items-center justify-between mt-4 mb-2">
                <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">
                  CRM & Marketing
                </span>
                <span className="text-[9px] font-bold px-1.5 py-0.5 rounded bg-rose-500/10 text-rose-400 border border-rose-500/20">
                  Growth
                </span>
              </div>
              {crmItems.map(item => {
                const Icon = item.icon;
                const isActive = location.pathname.startsWith(item.path);
                return (
                  <Link
                    key={item.path}
                    to={item.path}
                    className={`group relative flex items-center px-3.5 py-2.5 rounded-xl text-xs font-bold transition-all duration-200 ${
                      isActive 
                        ? 'bg-gradient-to-r from-rose-600 to-rose-700 text-white shadow-lg shadow-rose-600/30 font-black' 
                        : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-100'
                    }`}
                  >
                    {isActive && (
                      <span className="absolute left-0 top-2 bottom-2 w-1 bg-rose-300 rounded-r-md" />
                    )}
                    <Icon className={`w-4 h-4 mr-3 transition-transform group-hover:scale-110 ${isActive ? 'text-white' : 'text-slate-400 group-hover:text-rose-300'}`} />
                    <span className="truncate">{item.name}</span>
                  </Link>
                );
              })}
            </div>
          )}

          {/* Finance Section */}
          {financeItems.length > 0 && (
            <div className="space-y-1">
              <div className="px-3 flex items-center justify-between mt-4 mb-2">
                <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">
                  Finance & Accounting
                </span>
                <span className="text-[9px] font-bold px-1.5 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                  Ledger
                </span>
              </div>
              {financeItems.map(item => {
                const Icon = item.icon;
                const isActive = location.pathname.startsWith(item.path);
                return (
                  <Link
                    key={item.path}
                    to={item.path}
                    className={`group relative flex items-center px-3.5 py-2 rounded-xl text-xs font-bold transition-all duration-200 ${
                      isActive 
                        ? 'bg-gradient-to-r from-emerald-600 to-teal-700 text-white shadow-lg shadow-emerald-600/30 font-black' 
                        : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-100'
                    }`}
                  >
                    {isActive && (
                      <span className="absolute left-0 top-2 bottom-2 w-1 bg-emerald-300 rounded-r-md" />
                    )}
                    <Icon className={`w-4 h-4 mr-3 transition-transform group-hover:scale-110 ${isActive ? 'text-white' : 'text-slate-400 group-hover:text-emerald-300'}`} />
                    <span className="truncate">{item.name}</span>
                  </Link>
                );
              })}
            </div>
          )}

          {/* AI Co-Pilot Section */}
          {aiItems.length > 0 && (
            <div className="space-y-1">
              <div className="px-3 flex items-center justify-between mt-4 mb-2">
                <div className="flex items-center gap-1.5">
                  <Sparkles className="w-3.5 h-3.5 text-indigo-400 animate-pulse" />
                  <span className="text-[10px] font-extrabold text-indigo-400 uppercase tracking-wider block">
                    AI Co-Pilot Hub
                  </span>
                </div>
                <span className="text-[9px] font-extrabold px-1.5 py-0.5 rounded bg-indigo-500/20 text-indigo-300 border border-indigo-400/30 animate-pulse">
                  Smart AI
                </span>
              </div>
              {aiItems.map(item => {
                const Icon = item.icon;
                const isActive = location.pathname === item.path;
                return (
                  <Link
                    key={item.path}
                    to={item.path}
                    className={`group relative flex items-center px-3.5 py-2.5 rounded-xl text-xs font-bold transition-all duration-200 ${
                      isActive 
                        ? 'bg-gradient-to-r from-indigo-600 via-purple-600 to-violet-600 text-white shadow-lg shadow-indigo-600/30 font-black' 
                        : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-100'
                    }`}
                  >
                    {isActive && (
                      <span className="absolute left-0 top-2 bottom-2 w-1 bg-violet-300 rounded-r-md" />
                    )}
                    <Icon className={`w-4 h-4 mr-3 transition-transform group-hover:scale-110 ${isActive ? 'text-white' : 'text-slate-400 group-hover:text-indigo-300'}`} />
                    <span className="truncate">{item.name}</span>
                  </Link>
                );
              })}
            </div>
          )}

          {/* System Administration (NEW PARENT MENU) */}
          {adminItems.length > 0 && (
            <div className="space-y-1 pt-2 border-t border-slate-800/80">
              <div className="px-3 flex items-center justify-between mt-2 mb-2">
                <span className="text-[10px] font-extrabold text-amber-400/90 uppercase tracking-wider block">
                  System Administration
                </span>
                <span className="text-[9px] font-bold px-1.5 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/20">
                  Admin
                </span>
              </div>
              {adminItems.map(item => {
                const Icon = item.icon;
                const isActive = location.pathname === item.path;
                return (
                  <Link
                    key={item.path}
                    to={item.path}
                    className={`group relative flex items-center px-3.5 py-2.5 rounded-xl text-xs font-bold transition-all duration-200 ${
                      isActive 
                        ? 'bg-gradient-to-r from-amber-600 to-amber-700 text-white shadow-lg shadow-amber-600/30 font-black' 
                        : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-100'
                    }`}
                  >
                    {isActive && (
                      <span className="absolute left-0 top-2 bottom-2 w-1 bg-amber-300 rounded-r-md" />
                    )}
                    <Icon className={`w-4 h-4 mr-3 transition-transform group-hover:scale-110 ${isActive ? 'text-white' : 'text-slate-400 group-hover:text-amber-300'}`} />
                    <span className="truncate">{item.name}</span>
                  </Link>
                );
              })}
            </div>
          )}
        </nav>

        {/* User Profile Footer */}
        <div className="p-3 border-t border-slate-800/80 bg-slate-950/90 backdrop-blur-md">
          <div className="flex items-center justify-between bg-slate-900/80 p-2 rounded-xl border border-slate-800">
            <div className="flex items-center min-w-0 gap-2.5">
              <div className="relative">
                <div className="w-8 h-8 rounded-lg bg-gradient-to-tr from-indigo-600 to-violet-600 flex items-center justify-center text-white font-extrabold text-xs shadow-md shrink-0">
                  {user?.fullName?.charAt(0) || 'U'}
                </div>
                <span className="absolute -bottom-0.5 -right-0.5 w-2.5 h-2.5 rounded-full bg-emerald-500 border-2 border-slate-950" />
              </div>
              <div className="min-w-0">
                <p className="text-xs font-extrabold text-slate-100 truncate leading-tight">{user?.fullName || 'Active User'}</p>
                <span className="text-[10px] font-bold text-indigo-400 bg-indigo-500/10 px-1.5 py-0.2 rounded border border-indigo-500/20 inline-block mt-0.5">
                  {user?.role || 'Staff'}
                </span>
              </div>
            </div>
            <button 
              onClick={handleLogout}
              className="p-1.5 text-slate-400 hover:text-rose-400 hover:bg-slate-800 rounded-lg transition-all duration-150"
              title="Sign Out"
            >
              <LogOut className="w-4 h-4" />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Flashing UAT safety banner */}
        {isUat && (
          <div className="bg-amber-500 text-slate-950 text-center py-1.5 px-4 text-[11px] font-extrabold uppercase tracking-widest animate-pulse border-b border-amber-600 shadow-sm z-30 select-none">
            ⚠️ WARNING: OPERATING IN UAT SANDBOX ENVIRONMENT {tenantName ? `— ${tenantName.toUpperCase()}` : ''} — NO TRANSACTION WILL POST TO LIVE LEDGER ⚠️
          </div>
        )}
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
              <span className="ml-3 text-xs font-black tracking-wider bg-red-600 text-white px-2.5 py-0.5 rounded shadow-sm border border-red-500">v1.0.0-rc2</span>
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
              {isUat && (
                <span className="text-xs font-black tracking-wider uppercase bg-amber-100 text-amber-800 px-2.5 py-1 rounded shadow-sm border border-amber-200 animate-pulse">
                  UAT {tenantName ? `— ${tenantName}` : ''}
                </span>
              )}
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
            <Route path="/price-management" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : (
                <div className="p-8">
                  <PriceChangeModule />
                </div>
              )
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
            <Route path="/ai/executive" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <ExecutiveDashboard />
            } />
            <Route path="/ai/forecast-dashboard" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <ForecastDashboard />
            } />
            <Route path="/ai/store-performance" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <StorePerformanceDashboard />
            } />
            <Route path="/ai/health" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <HealthDashboard />
            } />
            <Route path="/warehouses" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <WarehouseLocationsList />
            } />
            <Route path="/purchasing/procurement" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <ProcurementDashboard />
            } />
            <Route path="/purchasing/supplier-analytics" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <SupplierDashboard />
            } />
            <Route path="/settings" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Settings />
            } />
            <Route path="/offers" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <OffersManager />
            } />
            <Route path="/customers" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Customers />
            } />
            {/* Finance F1 Routes */}
            <Route path="/finance/dashboard" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <FinanceDashboard />} />
            <Route path="/finance/accounts" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <ChartOfAccounts />} />
            <Route path="/finance/journals" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <JournalEntries />} />
            <Route path="/finance/trial-balance" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <TrialBalance />} />
            <Route path="/finance/profit-and-loss" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <ProfitAndLoss />} />
            <Route path="/finance/balance-sheet" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <BalanceSheet />} />
            
            {/* Finance F2 Routes */}
            <Route path="/finance/supplier-bills" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <SupplierBills />} />
            <Route path="/finance/supplier-payments" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <SupplierPayments />} />
            <Route path="/finance/supplier-ledger" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <SupplierLedger />} />
            <Route path="/finance/ap-aging" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <APAging />} />
            <Route path="/finance/customer-receipts" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <CustomerReceipts />} />
            <Route path="/finance/customer-ledger" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <CustomerLedger />} />
            <Route path="/finance/ar-aging" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <ARAging />} />
            <Route path="/finance/credit-limit" element={user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <CreditLimitMonitoring />} />
            
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
