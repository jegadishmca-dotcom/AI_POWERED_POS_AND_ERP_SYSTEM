import React, { useEffect, useRef, useState } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/auth.store';
import axios from 'axios';
import { getServerUrl } from '../../../utils/api';

/**
 * ProtectedRoute — guards all authenticated routes.
 *
 * FEATURE 3 FIX (Login Session Fix):
 * ─────────────────────────────────────────────────────────────────────────────
 * Always attempts a /api/auth/refresh call on first SPA mount to validate the
 * session against the backend, regardless of the Zustand in-memory
 * `isAuthenticated` state.
 *
 * WHY: Zustand persist saves `user` to localStorage but NOT `isAuthenticated`.
 * After a browser close + reopen the HttpOnly refresh-token cookie is gone
 * (because it is now a session cookie — see AuthController.cs), so the refresh
 * call fails, clearAuth() is invoked, and the user is redirected to /login.
 * If the cookie is still alive (e.g. the browser tab was just sleeping), the
 * refresh succeeds silently and the user continues without interruption.
 *
 * SCOPE GUARD: `hasCheckedRef` ensures the check runs exactly ONCE per SPA
 * lifetime — on app-shell mount. Internal route navigations (e.g. /pos →
 * /settings → /pos) do NOT re-trigger the network round-trip.
 *
 * MULTI-TAB WARNING: Refresh tokens are rotating/single-use. If two tabs call
 * /api/auth/refresh at the same moment the second will receive a
 * "Token reuse detected" error (both tokens share a TokenFamily and the first
 * revokes the old one). The second tab will then be redirected to /login. To
 * avoid this, users should avoid opening the POS in multiple tabs and
 * force-refreshing them simultaneously. Staggered mounts (normal use) are fine
 * because the first tab will have already issued a new cookie.
 *
 * SESSION COOKIE NOTE: Making the cookie a session cookie narrows the bypass
 * window but does not fully eliminate it. Some browsers/OS session-restore
 * features (e.g. Chrome "Restore pages" on crash recovery) can keep session
 * cookies alive across a browser restart. This mount-time validation layer
 * handles that residual gap.
 */
export const ProtectedRoute: React.FC = () => {
  const { isAuthenticated, user, setAuth, clearAuth } = useAuthStore();
  const location = useLocation();

  // Always start in the "checking" state so we validate on every fresh SPA load.
  const [isChecking, setIsChecking] = useState(true);

  // Guard: ensures the refresh check runs only once per SPA lifetime, NOT on
  // every internal navigation (React Router keeps this component mounted).
  const hasCheckedRef = useRef(false);

  useEffect(() => {
    // Skip if we've already validated this SPA session.
    if (hasCheckedRef.current) return;
    hasCheckedRef.current = true;

    const validateSession = async () => {
      if (!user) {
        // No user persisted in store → nothing to restore → go to login.
        setIsChecking(false);
        return;
      }

      // Attempt a silent token refresh regardless of the in-memory
      // `isAuthenticated` flag.  The HttpOnly cookie is sent automatically.
      try {
        const res = await axios.post(
          `${getServerUrl()}/api/auth/refresh`,
          {},
          { withCredentials: true }
        );
        // Session still valid — update the in-memory access token.
        setAuth(user, res.data.accessToken);
      } catch {
        // Cookie gone or refresh token expired/revoked → force re-login.
        clearAuth();
      } finally {
        setIsChecking(false);
      }
    };

    validateSession();
  // Intentionally empty deps: run only on mount, not on navigation state changes.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (isChecking) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-900">
        <div className="flex flex-col items-center gap-3">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
          <p className="text-sm text-slate-500 font-medium">Validating session...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
};
