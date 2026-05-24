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
  Settings as SettingsIcon
} from 'lucide-react';
import { useAuthStore } from './features/auth/store/auth.store';
import { Login } from './features/auth/routes/Login';
import { ProtectedRoute } from './features/auth/components/ProtectedRoute';
import { Dashboard } from './features/analytics/components/Dashboard';
import { PosTerminal } from './features/pos/components/PosTerminal';
import { Products } from './features/catalog/routes/Products';
import { GrnForm } from './features/inventory/components/GrnForm';
import { StockAdjustmentForm } from './features/inventory/components/StockAdjustmentForm';
import { StockTakeForm } from './features/inventory/components/StockTakeForm';
import { StockLedgerView } from './features/inventory/components/StockLedgerView';
import { StockPositionReport } from './features/inventory/components/StockPositionReport';
import { WarehouseLocationsList } from './features/inventory/components/WarehouseLocationsList';
import { ShiftReport } from './features/pos/components/ShiftReport';
import { Suppliers } from './features/purchasing/routes/Suppliers';
import { Settings } from './pages/Settings';

const AppLayout: React.FC = () => {
  const { user, clearAuth } = useAuthStore();
  const location = useLocation();
  const navigate = useNavigate();

  const handleLogout = () => {
    clearAuth();
    navigate('/login');
  };

  const navItems = [
    { path: '/dashboard', name: 'Dashboard', icon: LayoutDashboard, roles: ['Owner', 'Manager'] },
    { path: '/pos', name: 'POS Billing', icon: ShoppingCart, roles: ['Owner', 'Manager', 'Cashier'] },
    { path: '/shift-report', name: 'Shift & Sales Report', icon: ClipboardCheck, roles: ['Cashier'] },
    { path: '/products', name: 'Product Catalog', icon: Package, roles: ['Owner', 'Manager'] },
    { path: '/grn', name: 'Goods Receipt (GRN)', icon: ClipboardCheck, roles: ['Owner', 'Manager'] },
    { path: '/suppliers', name: 'Supplier Master', icon: UserIcon, roles: ['Owner', 'Manager'] },
    { path: '/stock-adjustment', name: 'Stock Adjustment', icon: ArrowUpDown, roles: ['Owner', 'Manager'] },
    { path: '/stock-take', name: 'Stock Take', icon: ClipboardCheck, roles: ['Owner', 'Manager'] },
    { path: '/stock-ledger', name: 'Stock Ledger', icon: History, roles: ['Owner', 'Manager'] },
    { path: '/stock-position', name: 'Stock Position', icon: Layers, roles: ['Owner', 'Manager'] },
    { path: '/warehouses', name: 'Warehouses & Bins', icon: MapPin, roles: ['Owner', 'Manager'] },
    { path: '/settings', name: 'Settings', icon: SettingsIcon, roles: ['Owner', 'Manager'] },
  ];

  const filteredNavItems = navItems.filter(item => 
    !user?.role || item.roles.includes(user.role)
  );

  return (
    <div className="flex h-screen bg-slate-100 dark:bg-slate-900 font-sans transition-colors duration-200 overflow-hidden">
      {/* Sidebar */}
      <aside className="w-64 bg-slate-900 text-white flex flex-col shadow-xl z-20">
        {/* Brand Header */}
        <div className="h-16 flex items-center px-6 bg-slate-950 border-b border-slate-800">
          <Terminal className="w-6 h-6 mr-3 text-indigo-400" />
          <span className="font-extrabold text-lg bg-gradient-to-r from-white to-slate-400 bg-clip-text text-transparent">
            Supermarket ERP
          </span>
        </div>

        {/* Sidebar Nav */}
        <nav className="flex-1 px-4 py-6 space-y-1 overflow-y-auto">
          {filteredNavItems.map(item => {
            const Icon = item.icon;
            const isActive = location.pathname === item.path;
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`flex items-center px-4 py-3 rounded-lg text-sm font-semibold transition-all duration-200 ${
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
          <h1 className="text-xl font-extrabold text-slate-800 dark:text-white flex items-center">
            {navItems.find(item => item.path === location.pathname)?.name || 'ERP System'}
            <span className="ml-3 text-xs font-bold text-slate-500 bg-slate-200 dark:bg-slate-700 dark:text-slate-300 px-2 py-1 rounded">v1.2</span>
          </h1>
          <div className="flex items-center space-x-4">
            <div className="text-right hidden sm:block">
              <span className="text-xs font-semibold text-slate-500 dark:text-slate-400 block">Business Date</span>
              <span className="text-sm font-bold text-slate-800 dark:text-slate-200">{new Date().toLocaleDateString('en-IN', { dateStyle: 'medium' })}</span>
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
            <Route path="/pos" element={<PosTerminal />} />
            <Route path="/shift-report" element={<ShiftReport />} />
            <Route path="/products" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Products />
            } />
            <Route path="/grn" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <GrnForm />
            } />
            <Route path="/suppliers" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Suppliers />
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
            <Route path="/warehouses" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <WarehouseLocationsList />
            } />
            <Route path="/settings" element={
              user?.role === 'Cashier' ? <Navigate to="/pos" replace /> : <Settings />
            } />
            <Route path="/" element={<Navigate to={user?.role === 'Cashier' ? "/pos" : "/dashboard"} replace />} />
            <Route path="*" element={<Navigate to={user?.role === 'Cashier' ? "/pos" : "/dashboard"} replace />} />
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
