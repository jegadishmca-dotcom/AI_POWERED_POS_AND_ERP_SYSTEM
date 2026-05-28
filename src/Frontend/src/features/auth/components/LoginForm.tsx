import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Monitor, Building } from 'lucide-react';

const loginSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  terminalCode: z.string().optional(),
});

type LoginFormData = z.infer<typeof loginSchema>;

interface LoginFormProps {
  onSubmit: (data: any, loginType: 'cashier' | 'admin') => Promise<void>;
  isLoading: boolean;
  error?: string | null;
}

export const LoginForm: React.FC<LoginFormProps> = ({ onSubmit, isLoading, error }) => {
  const [loginType, setLoginType] = useState<'cashier' | 'admin'>('cashier');
  const [terminalError, setTerminalError] = useState<string | null>(null);

  const { register, handleSubmit, formState: { errors } } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      terminalCode: localStorage.getItem('pos_terminal_code') || '',
    }
  });

  const handleFormSubmit = (data: LoginFormData) => {
    setTerminalError(null);
    if (loginType === 'cashier' && (!data.terminalCode || data.terminalCode.trim() === '')) {
      setTerminalError('Terminal Code is required for POS cashier login');
      return;
    }
    onSubmit(data, loginType);
  };

  return (
    <div className="space-y-6">
      {/* Mode Selector Tabs */}
      <div className="flex bg-slate-100 dark:bg-slate-900 p-1.5 rounded-lg border border-slate-200 dark:border-slate-800">
        <button
          type="button"
          onClick={() => {
            setLoginType('cashier');
            setTerminalError(null);
          }}
          className={`flex-1 flex items-center justify-center py-2 px-3 rounded-md text-xs font-bold transition-all duration-200 ${
            loginType === 'cashier'
              ? 'bg-indigo-600 text-white shadow shadow-indigo-600/30'
              : 'text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200'
          }`}
        >
          <Monitor className="w-4 h-4 mr-2" />
          POS Cashier
        </button>
        <button
          type="button"
          onClick={() => {
            setLoginType('admin');
            setTerminalError(null);
          }}
          className={`flex-1 flex items-center justify-center py-2 px-3 rounded-md text-xs font-bold transition-all duration-200 ${
            loginType === 'admin'
              ? 'bg-indigo-600 text-white shadow shadow-indigo-600/30'
              : 'text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200'
          }`}
        >
          <Building className="w-4 h-4 mr-2" />
          ERP Back-Office
        </button>
      </div>

      <form onSubmit={handleSubmit(handleFormSubmit)} className="space-y-6">
        {error && (
          <div className="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-600 dark:text-red-400 p-3 rounded-md text-sm">
            {error}
          </div>
        )}

        {/* Terminal Code Field (Dynamic) */}
        {loginType === 'cashier' && (
          <div className="transition-all duration-300 ease-in-out">
            <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300">Terminal Code</label>
            <input 
              {...register('terminalCode')}
              type="text" 
              readOnly
              className="mt-1 block w-full rounded-md border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-500 dark:text-slate-400 shadow-sm sm:text-sm p-2.5 border cursor-not-allowed font-mono font-bold" 
              placeholder="NOT_REGISTERED" 
            />
            {localStorage.getItem('pos_terminal_code') ? (
              <p className="mt-1 text-[11px] text-slate-400">This device is persistently registered to this terminal counter.</p>
            ) : (
              <p className="mt-1 text-[11px] text-rose-500 font-semibold">Device is not registered. Please sign in to ERP Back-Office to bind a terminal.</p>
            )}
            {terminalError && <p className="mt-1 text-sm text-red-600 dark:text-red-400 font-bold">{terminalError}</p>}
          </div>
        )}

        <div>
          <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300">Username</label>
          <input 
            {...register('username')}
            type="text" 
            className="mt-1 block w-full rounded-md border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm p-2.5 border" 
            placeholder="Enter username" 
            autoComplete="username"
          />
          {errors.username && <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.username.message}</p>}
        </div>

        <div>
          <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300">Password</label>
          <input 
            {...register('password')}
            type="password" 
            className="mt-1 block w-full rounded-md border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm p-2.5 border" 
            placeholder="••••••••" 
            autoComplete="current-password"
          />
          {errors.password && <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.password.message}</p>}
        </div>

        <button 
          type="submit" 
          disabled={isLoading}
          className="w-full flex justify-center items-center py-2.5 px-4 border border-transparent rounded-md shadow-sm text-sm font-bold text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed transition-all duration-150"
        >
          {isLoading ? <Loader2 className="animate-spin h-5 w-5 mr-2" /> : `Sign In to ${loginType === 'cashier' ? 'POS Terminal' : 'ERP Back-Office'}`}
        </button>
      </form>
    </div>
  );
};
