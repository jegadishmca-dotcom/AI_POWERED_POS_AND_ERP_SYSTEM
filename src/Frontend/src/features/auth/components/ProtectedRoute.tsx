import React, { useEffect, useState } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/auth.store';
import axios from 'axios';
import { getServerUrl } from '../../../utils/api';

export const ProtectedRoute: React.FC = () => {
  const { isAuthenticated, user, setAuth, clearAuth } = useAuthStore();
  const location = useLocation();
  const [isChecking, setIsChecking] = useState(!isAuthenticated && !!user);

  useEffect(() => {
    const restoreSession = async () => {
      if (!isAuthenticated && user) {
        try {
          const res = await axios.post(
            `${getServerUrl()}/api/auth/refresh`,
            {},
            { withCredentials: true }
          );
          setAuth(user, res.data.accessToken);
        } catch (e) {
          clearAuth();
        } finally {
          setIsChecking(false);
        }
      }
    };
    restoreSession();
  }, [isAuthenticated, user, setAuth, clearAuth]);

  if (isChecking) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-900">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-650"></div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
};
