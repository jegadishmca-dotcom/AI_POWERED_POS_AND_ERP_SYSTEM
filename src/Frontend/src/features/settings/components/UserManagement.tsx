import React, { useState, useEffect } from 'react';
import { getUsers, createUser, updateUser, changeUserPassword, getRoles, UserSettingsDto, RoleDto } from '../api/settings.api';
import { Users, UserPlus, Key, Edit, Shield, Check, X, Loader2 } from 'lucide-react';

export const UserManagement: React.FC = () => {
  const [users, setUsers] = useState<UserSettingsDto[]>([]);
  const [roles, setRoles] = useState<RoleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Modals / forms state
  const [showAddModal, setShowAddModal] = useState(false);
  const [showPwdModal, setShowPwdModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  
  const [selectedUser, setSelectedUser] = useState<UserSettingsDto | null>(null);

  // Add User Form State
  const [username, setUsername] = useState('');
  const [fullName, setFullName] = useState('');
  const [roleId, setRoleId] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  
  // Edit User Form State
  const [editFullName, setEditFullName] = useState('');
  const [editRoleId, setEditRoleId] = useState('');
  const [editIsActive, setEditIsActive] = useState(true);

  // Change Password Form State
  const [newPassword, setNewPassword] = useState('');
  const [confirmNewPassword, setConfirmNewPassword] = useState('');

  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [uData, rData] = await Promise.all([getUsers(), getRoles()]);
      setUsers(uData);
      setRoles(rData);
      if (rData.length > 0) setRoleId(rData[0].id);
    } catch (err: any) {
      console.error('Failed to load user management data:', err);
      setError('Failed to load user list. Please verify your permissions and try again.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleAddUser = async (e: React.FormEvent) => {
    e.preventDefault();
    setActionError(null);

    if (password !== confirmPassword) {
      setActionError('Passwords do not match.');
      return;
    }

    setSaving(true);
    try {
      await createUser({
        username,
        fullName,
        roleId,
        password,
        storeId: null
      });
      setShowAddModal(false);
      setUsername('');
      setFullName('');
      setPassword('');
      setConfirmPassword('');
      loadData();
    } catch (err: any) {
      setActionError(err.response?.data?.message || 'Failed to create user.');
    } finally {
      setSaving(false);
    }
  };

  const handleEditUser = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedUser) return;
    
    setActionError(null);
    setSaving(true);
    try {
      await updateUser(selectedUser.id, {
        fullName: editFullName,
        roleId: editRoleId,
        isActive: editIsActive,
        storeId: null
      });
      setShowEditModal(false);
      loadData();
    } catch (err: any) {
      setActionError(err.response?.data?.message || 'Failed to update user.');
    } finally {
      setSaving(false);
    }
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedUser) return;

    if (newPassword !== confirmNewPassword) {
      setActionError('New passwords do not match.');
      return;
    }

    setActionError(null);
    setSaving(true);
    try {
      await changeUserPassword(selectedUser.id, { password: newPassword });
      setShowPwdModal(false);
      setNewPassword('');
      setConfirmNewPassword('');
      alert(`Password for ${selectedUser.username} has been updated.`);
    } catch (err: any) {
      setActionError(err.response?.data?.message || 'Failed to change password.');
    } finally {
      setSaving(false);
    }
  };

  const openEditModal = (user: UserSettingsDto) => {
    setSelectedUser(user);
    setEditFullName(user.fullName);
    setEditRoleId(user.roleId);
    setEditIsActive(user.isActive);
    setActionError(null);
    setShowEditModal(true);
  };

  const openPwdModal = (user: UserSettingsDto) => {
    setSelectedUser(user);
    setNewPassword('');
    setConfirmNewPassword('');
    setActionError(null);
    setShowPwdModal(true);
  };

  if (loading) {
    return (
      <div className="bg-white p-8 rounded-xl border border-slate-100 shadow-sm flex flex-col justify-center items-center h-80">
        <Loader2 className="w-8 h-8 animate-spin text-indigo-600 mb-2" />
        <p className="text-gray-400 font-medium text-sm">Loading staff directory...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-white p-8 rounded-xl border border-slate-100 shadow-sm text-center">
        <p className="text-red-500 font-bold mb-4">{error}</p>
        <button onClick={loadData} className="px-4 py-2 bg-indigo-600 text-white rounded-lg font-bold text-xs shadow">
          Retry
        </button>
      </div>
    );
  }

  return (
    <div className="bg-white p-6 rounded-xl border border-slate-100 shadow-sm">
      <div className="flex justify-between items-center mb-6">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
            <Users className="w-5 h-5" />
          </div>
          <div>
            <h3 className="font-bold text-slate-800">Cashier &amp; User Configuration</h3>
            <p className="text-xs text-slate-400">Manage cashier, supervisor, and back-office staff accounts</p>
          </div>
        </div>
        <button
          onClick={() => {
            setActionError(null);
            setShowAddModal(true);
          }}
          className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs rounded-xl shadow transition flex items-center gap-1.5"
        >
          <UserPlus className="w-4 h-4" /> Add Staff Account
        </button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b text-slate-400 uppercase text-xs font-bold">
              <th className="pb-3">Staff Details</th>
              <th className="pb-3">Username</th>
              <th className="pb-3">Role</th>
              <th className="pb-3 text-center">Status</th>
              <th className="pb-3 text-center">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y text-slate-700">
            {users.map((u) => (
              <tr key={u.id} className="hover:bg-slate-50 transition-colors">
                <td className="py-4 font-bold text-slate-800">{u.fullName}</td>
                <td className="py-4 font-semibold text-slate-500">{u.username}</td>
                <td className="py-4">
                  <span className={`px-2.5 py-1 rounded-full font-bold text-xs flex items-center w-fit gap-1 ${
                    u.roleName === 'Owner' || u.roleName === 'Manager'
                      ? 'bg-indigo-100 text-indigo-800'
                      : u.roleName === 'Supervisor'
                      ? 'bg-blue-100 text-blue-800'
                      : 'bg-emerald-100 text-emerald-800'
                  }`}>
                    <Shield className="w-3 h-3" />
                    {u.roleName}
                  </span>
                </td>
                <td className="py-4 text-center">
                  {u.isActive ? (
                    <span className="px-2.5 py-1 bg-emerald-50 text-emerald-700 rounded-full font-bold text-xs inline-flex items-center gap-1 border border-emerald-200">
                      <Check className="w-3.5 h-3.5" /> Active
                    </span>
                  ) : (
                    <span className="px-2.5 py-1 bg-slate-100 text-slate-500 rounded-full font-bold text-xs inline-flex items-center gap-1 border border-slate-200">
                      <X className="w-3.5 h-3.5" /> Disabled
                    </span>
                  )}
                </td>
                <td className="py-4 text-center">
                  <div className="flex justify-center gap-3">
                    <button
                      onClick={() => openEditModal(u)}
                      className="p-1.5 hover:bg-slate-100 text-slate-500 hover:text-slate-800 rounded transition"
                      title="Edit details"
                    >
                      <Edit className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => openPwdModal(u)}
                      className="p-1.5 hover:bg-slate-100 text-slate-500 hover:text-indigo-600 rounded transition"
                      title="Reset Password"
                    >
                      <Key className="w-4 h-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* ADD USER MODAL */}
      {showAddModal && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl border overflow-hidden p-6 space-y-4">
            <h3 className="text-lg font-bold text-slate-800">Add Staff Account</h3>
            {actionError && (
              <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-xl text-xs font-bold">
                {actionError}
              </div>
            )}
            <form onSubmit={handleAddUser} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Username / E-mail</label>
                <input
                  type="text"
                  required
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                  placeholder="e.g. cashier03@supermarket.local"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Full Name</label>
                <input
                  type="text"
                  required
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                  placeholder="e.g. John Doe"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Role Type</label>
                <select
                  value={roleId}
                  onChange={(e) => setRoleId(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm bg-white"
                >
                  {roles.map((r) => (
                    <option key={r.id} value={r.id}>{r.name} - {r.description}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Password</label>
                <input
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                  placeholder="••••••••"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Confirm Password</label>
                <input
                  type="password"
                  required
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                  placeholder="••••••••"
                />
              </div>
              <div className="flex gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs rounded-xl shadow transition"
                >
                  {saving ? 'Saving...' : 'Add Account'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* EDIT USER MODAL */}
      {showEditModal && selectedUser && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl border overflow-hidden p-6 space-y-4">
            <h3 className="text-lg font-bold text-slate-800">Edit Staff Account: {selectedUser.username}</h3>
            {actionError && (
              <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-xl text-xs font-bold">
                {actionError}
              </div>
            )}
            <form onSubmit={handleEditUser} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Full Name</label>
                <input
                  type="text"
                  required
                  value={editFullName}
                  onChange={(e) => setEditFullName(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Role Type</label>
                <select
                  value={editRoleId}
                  onChange={(e) => setEditRoleId(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm bg-white"
                >
                  {roles.map((r) => (
                    <option key={r.id} value={r.id}>{r.name}</option>
                  ))}
                </select>
              </div>
              <div className="flex items-center gap-3">
                <input
                  type="checkbox"
                  id="editIsActive"
                  checked={editIsActive}
                  onChange={(e) => setEditIsActive(e.target.checked)}
                  className="h-4.5 w-4.5 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                />
                <label htmlFor="editIsActive" className="text-sm font-bold text-slate-700">Account Active</label>
              </div>
              <div className="flex gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setShowEditModal(false)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs rounded-xl shadow transition"
                >
                  {saving ? 'Saving...' : 'Update Details'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* CHANGE PASSWORD MODAL */}
      {showPwdModal && selectedUser && (
        <div className="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-xl border overflow-hidden p-6 space-y-4">
            <h3 className="text-lg font-bold text-slate-800">Reset Password: {selectedUser.username}</h3>
            {actionError && (
              <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-xl text-xs font-bold">
                {actionError}
              </div>
            )}
            <form onSubmit={handleChangePassword} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">New Password</label>
                <input
                  type="password"
                  required
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                  placeholder="Min 8 chars, A-Z, a-z, 0-9, special"
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-slate-500 uppercase mb-1">Confirm New Password</label>
                <input
                  type="password"
                  required
                  value={confirmNewPassword}
                  onChange={(e) => setConfirmNewPassword(e.target.value)}
                  className="w-full px-4 py-2 border rounded-xl text-sm"
                  placeholder="••••••••"
                />
              </div>
              <div className="flex gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setShowPwdModal(false)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs rounded-xl transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs rounded-xl shadow transition"
                >
                  {saving ? 'Saving...' : 'Reset Password'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
