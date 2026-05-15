import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';

const loginSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  terminalCode: z.string().min(1, 'Terminal Code is required'),
});

type LoginFormData = z.infer<typeof loginSchema>;

interface LoginFormProps {
  onSubmit: (data: LoginFormData) => Promise<void>;
  isLoading: boolean;
  error?: string | null;
}

export const LoginForm: React.FC<LoginFormProps> = ({ onSubmit, isLoading, error }) => {
  const { register, handleSubmit, formState: { errors } } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      terminalCode: localStorage.getItem('pos_terminal_code') || '',
    }
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {error && (
        <div className="bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 text-red-600 dark:text-red-400 p-3 rounded-md text-sm">
          {error}
        </div>
      )}

      <div>
        <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Terminal Code</label>
        <input 
          {...register('terminalCode')}
          type="text" 
          className="mt-1 block w-full rounded-md border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 border" 
          placeholder="TERM-01" 
          onKeyDown={(e) => { if (e.key === 'Enter') e.currentTarget.blur(); }}
        />
        {errors.terminalCode && <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.terminalCode.message}</p>}
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Username</label>
        <input 
          {...register('username')}
          type="text" 
          className="mt-1 block w-full rounded-md border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 border" 
          placeholder="Enter username" 
          autoComplete="username"
        />
        {errors.username && <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.username.message}</p>}
      </div>

      <div>
        <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Password</label>
        <input 
          {...register('password')}
          type="password" 
          className="mt-1 block w-full rounded-md border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-sm focus:border-blue-500 focus:ring-blue-500 sm:text-sm p-2.5 border" 
          placeholder="â€¢â€¢â€¢â€¢â€¢â€¢â€¢â€¢" 
          autoComplete="current-password"
        />
        {errors.password && <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.password.message}</p>}
      </div>

      <button 
        type="submit" 
        disabled={isLoading}
        className="w-full flex justify-center items-center py-2.5 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {isLoading ? <Loader2 className="animate-spin h-5 w-5 mr-2" /> : 'Sign In'}
      </button>
    </form>
  );
};
