import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { LoginForm } from '../components/LoginForm';
import { login } from '../api/auth.api';
import { useAuthStore } from '../store/auth.store';

export const Login = () => {
  const setAuth = useAuthStore((state) => state.setAuth);
  const navigate = useNavigate();
  const location = useLocation();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const from = location.state?.from?.pathname || '/dashboard';

  const handleLogin = async (data: any, loginType: 'cashier' | 'admin') => {
    setIsLoading(true);
    setError(null);
    try {
      if (loginType === 'cashier') {
        localStorage.setItem('pos_terminal_code', data.terminalCode);
      } else {
        localStorage.removeItem('pos_terminal_code');
      }
      
      const response = await login({
        username: data.username,
        password: data.password,
        terminalCode: loginType === 'cashier' ? data.terminalCode : 'BACK-OFFICE'
      });
      
      setAuth(response.user, response.accessToken);
      
      // Dynamic Redirect: Cashiers straight to POS, Admins/Managers to Back-office Dashboard
      const defaultPath = response.user.role === 'Cashier' ? '/pos' : '/dashboard';
      const redirectPath = location.state?.from?.pathname || defaultPath;
      navigate(redirectPath, { replace: true });
    } catch (err: any) {
      setError(err.response?.data?.message || 'Invalid username or password. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-900 flex flex-col justify-center py-12 sm:px-6 lg:px-8 transition-colors duration-200">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <h2 className="mt-6 text-center text-3xl font-extrabold text-slate-900 dark:text-white">
          Supermarket POS & ERP
        </h2>
        <p className="mt-2 text-center text-sm text-slate-600 dark:text-slate-400">
          Access the point-of-sale terminal or central ERP platform
        </p>
      </div>

      <div className="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
        <div className="bg-white dark:bg-slate-800 py-8 px-4 shadow sm:rounded-lg sm:px-10 border border-slate-200 dark:border-slate-700">
          <LoginForm onSubmit={handleLogin} isLoading={isLoading} error={error} />
        </div>
      </div>
    </div>
  );
};
