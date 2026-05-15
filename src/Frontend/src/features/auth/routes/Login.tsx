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

  const handleLogin = async (data: any) => {
    setIsLoading(true);
    setError(null);
    try {
      // Save terminal code for convenience
      localStorage.setItem('pos_terminal_code', data.terminalCode);
      
      const response = await login(data);
      setAuth(response.user, response.accessToken);
      navigate(from, { replace: true });
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
          Enterprise POS
        </h2>
        <p className="mt-2 text-center text-sm text-slate-600 dark:text-slate-400">
          Sign in to your terminal
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
