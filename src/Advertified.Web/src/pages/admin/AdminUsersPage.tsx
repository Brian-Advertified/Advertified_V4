import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, PlusCircle, Trash2, X } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import { ActionButton, ReadOnlyNotice, type AdminUserFormState, hasText } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, fmtDate, titleize, useAdminDashboardQuery } from './adminWorkspace';

export function AdminUsersPage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [userDialog, setUserDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [userForm, setUserForm] = useState<AdminUserFormState>({
    fullName: '',
    email: '',
    phone: '',
    password: '',
    role: 'client',
    accountStatus: 'PendingVerification',
    isSaCitizen: true,
    emailVerified: false,
    phoneVerified: false,
    assignedAreaCodes: [],
  });
  const userCreateIsValid = hasText(userForm.fullName) && hasText(userForm.email) && hasText(userForm.phone) && hasText(userForm.password);
  const userUpdateIsValid = hasText(userForm.fullName) && hasText(userForm.email) && hasText(userForm.phone);

  const resetUserForm = () => {
    setUserForm({
      fullName: '',
      email: '',
      phone: '',
      password: '',
      role: 'client',
      accountStatus: 'PendingVerification',
      isSaCitizen: true,
      emailVerified: false,
      phoneVerified: false,
      assignedAreaCodes: [],
    });
  };

  const createUserMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminUser(userForm),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setUserDialog(null);
      resetUserForm();
      pushToast({ title: 'User created.', description: 'The user account is now available in the live admin workspace.' });
    },
    onError: (error) => pushToast({ title: 'Could not create user.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateUserMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminUser(userDialog?.id ?? '', { ...userForm, password: userForm.password || undefined }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setUserDialog(null);
      resetUserForm();
      pushToast({ title: 'User updated.', description: 'The account details were saved successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not update user.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteUserMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminUser(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setUserDialog(null);
      resetUserForm();
      pushToast({ title: 'User deleted.', description: 'The account was removed from the system.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete user.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedUser = userDialog?.id ? dashboard.users.find((item) => item.id === userDialog.id) ?? null : null;
        const isReadOnly = userDialog?.mode === 'view';
        const openUserDialog = (mode: 'create' | 'view' | 'edit', user?: (typeof dashboard.users)[number]) => {
          if (mode === 'create' || !user) {
            resetUserForm();
            setUserDialog({ mode });
            return;
          }

          setUserForm({
            fullName: user.fullName,
            email: user.email,
            phone: user.phone,
            password: '',
            role: user.role,
            accountStatus: user.accountStatus,
            isSaCitizen: user.isSaCitizen,
            emailVerified: user.emailVerified,
            phoneVerified: user.phoneVerified,
            assignedAreaCodes: user.assignedAreaCodes,
          });
          setUserDialog({ mode, id: user.id });
        };

        return (
          <AdminPageShell title="Users and roles" description="Manage the live user base across admin, agent, and client roles without leaving the admin workspace.">
            <section className="space-y-6">
              <div className="panel p-6">
                <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Account management</h3>
                    <p className="mt-2 text-sm text-ink-soft">Create accounts, adjust role and verification state, and safely remove accounts that do not own live work yet.</p>
                  </div>
                  <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3" onClick={() => openUserDialog('create')}>
                    <PlusCircle className="size-4" />
                    Add user
                  </button>
                </div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Name</th><th className="px-4 py-4">Contact</th><th className="px-4 py-4">Role</th><th className="px-4 py-4">Coverage</th><th className="px-4 py-4">Status</th><th className="px-4 py-4">Verification</th><th className="px-4 py-4">Joined</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                  <tbody>
                    {dashboard.users.map((item) => <tr key={item.id} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{item.fullName}</p><p className="text-xs text-ink-soft">{item.isSaCitizen ? 'SA citizen' : 'International user'}</p></td><td className="px-4 py-4 text-ink-soft"><div>{item.email}</div><div className="text-xs">{item.phone}</div></td><td className="px-4 py-4 text-ink-soft">{titleize(item.role)}</td><td className="px-4 py-4 text-ink-soft">{item.role === 'agent' ? item.assignedAreaLabels.length > 0 ? item.assignedAreaLabels.join(', ') : 'No areas assigned' : item.role === 'creative_director' ? 'Creative studio access' : 'Not applicable'}</td><td className="px-4 py-4 text-ink-soft">{titleize(item.accountStatus)}</td><td className="px-4 py-4 text-ink-soft"><div>Email: {item.emailVerified ? 'Verified' : 'Pending'}</div><div className="text-xs">Phone: {item.phoneVerified ? 'Verified' : 'Pending'}</div></td><td className="px-4 py-4 text-ink-soft">{fmtDate(item.createdAt)}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View ${item.fullName}`} icon={Eye} onClick={() => openUserDialog('view', item)} /><ActionButton label={`Edit ${item.fullName}`} icon={Pencil} onClick={() => openUserDialog('edit', item)} /><ActionButton label={`Delete ${item.fullName}`} icon={Trash2} variant="danger" disabled={deleteUserMutation.isPending} onClick={() => { if (window.confirm(`Delete ${item.fullName}? Accounts with linked campaigns, recommendations, or orders cannot be deleted.`)) { deleteUserMutation.mutate(item.id); } }} /></div></td></tr>)}
                  </tbody>
                </table>
              </div>

              {userDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{userDialog.mode === 'create' ? 'Add user' : userDialog.mode === 'view' ? 'View user' : 'Edit user'}</h3><button type="button" className="button-secondary p-3" onClick={() => { setUserDialog(null); resetUserForm(); }}><X className="size-4" /></button></div>
                    {isReadOnly ? <ReadOnlyNotice label="This account is open in view mode. Switch to edit mode to change role, status, verification state, or credentials." /> : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2">
                      <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Full name" value={userForm.fullName} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, fullName: event.target.value }))} />
                      <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Email" value={userForm.email} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, email: event.target.value }))} />
                      <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Phone" value={userForm.phone} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, phone: event.target.value }))} />
                      <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder={userDialog.mode === 'edit' ? 'New password (optional)' : 'Password'} type="password" value={userForm.password ?? ''} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, password: event.target.value }))} />
                      <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={userForm.role} onChange={(event) => setUserForm((current: AdminUserFormState) => {
                        const nextRole = event.target.value as AdminUserFormState['role'];
                        return { ...current, role: nextRole, assignedAreaCodes: nextRole === 'agent' ? current.assignedAreaCodes : [] };
                      })}>
                        <option value="client">Client</option>
                        <option value="agent">Agent</option>
                        <option value="creative_director">Creative director</option>
                        <option value="admin">Admin</option>
                      </select>
                      <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={userForm.accountStatus} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, accountStatus: event.target.value }))}>
                        <option value="PendingVerification">Pending verification</option>
                        <option value="Active">Active</option>
                        <option value="Suspended">Suspended</option>
                      </select>
                    </div>
                    <div className="mt-4 space-y-3">
                      <div>
                        <p className="text-sm font-semibold text-ink">Assigned areas</p>
                        <p className="mt-1 text-sm text-ink-soft">Area routing is reserved for agent accounts. Creative directors work in the production studio after recommendation approval.</p>
                      </div>
                      {userForm.role === 'agent' ? (
                        <div className="grid gap-3 md:grid-cols-2">
                          {dashboard.areas.map((area) => (
                            <label key={area.code} className="inline-flex items-center gap-3 rounded-[18px] border border-line px-4 py-3 text-sm text-ink-soft">
                              <input
                                disabled={isReadOnly}
                                type="checkbox"
                                checked={userForm.assignedAreaCodes.includes(area.code)}
                                onChange={(event) => setUserForm((current: AdminUserFormState) => ({
                                  ...current,
                                  assignedAreaCodes: event.target.checked
                                    ? [...current.assignedAreaCodes, area.code]
                                    : current.assignedAreaCodes.filter((code) => code !== area.code),
                                }))}
                              />
                              <span>
                                <span className="font-semibold text-ink">{area.label}</span>
                                <span className="block text-xs">{area.code}</span>
                              </span>
                            </label>
                          ))}
                        </div>
                      ) : (
                        <ReadOnlyNotice label="Area routing is only used for agent accounts. Switching this user away from the agent role clears any existing area ownership." />
                      )}
                    </div>
                    <div className="mt-4 flex flex-wrap items-center gap-5 text-sm text-ink-soft">
                      <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={userForm.isSaCitizen} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, isSaCitizen: event.target.checked }))} /> South African citizen</label>
                      <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={userForm.emailVerified} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, emailVerified: event.target.checked }))} /> Email verified</label>
                      <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={userForm.phoneVerified} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, phoneVerified: event.target.checked }))} /> Phone verified</label>
                      {selectedUser ? <span>Updated {fmtDate(selectedUser.updatedAt)}</span> : null}
                    </div>
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => { setUserDialog(null); resetUserForm(); }}>Close</button>
                      {userDialog.mode === 'view' && selectedUser ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openUserDialog('edit', selectedUser)}>Edit user</button> : null}
                      {userDialog.mode === 'edit' && selectedUser ? <button type="button" className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50" disabled={deleteUserMutation.isPending} onClick={() => { if (window.confirm(`Delete ${selectedUser.fullName}? Accounts with linked campaigns, recommendations, or orders cannot be deleted.`)) { deleteUserMutation.mutate(selectedUser.id); } }}>Delete user</button> : null}
                      {userDialog.mode === 'create' && !userCreateIsValid ? <p className="text-sm text-rose-600">Full name, email, phone, and password are required to create a user.</p> : null}
                      {userDialog.mode === 'edit' && !userUpdateIsValid ? <p className="text-sm text-rose-600">Full name, email, and phone are required to update a user.</p> : null}
                      {userDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" onClick={() => createUserMutation.mutate()} disabled={createUserMutation.isPending || !userCreateIsValid}>Save user</button> : null}
                      {userDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" onClick={() => updateUserMutation.mutate()} disabled={updateUserMutation.isPending || !userUpdateIsValid}>Update user</button> : null}
                    </div>
                  </div>
                </div>
              ) : null}
            </section>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}
