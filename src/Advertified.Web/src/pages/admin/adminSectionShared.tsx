import type { ReactNode } from 'react';
import { Eye } from 'lucide-react';

type ActionButtonProps = {
  label: string;
  onClick?: () => void;
  icon: typeof Eye;
  variant?: 'default' | 'danger';
  disabled?: boolean;
};

export function ActionButton({ label, onClick, icon: Icon, variant = 'default', disabled = false }: ActionButtonProps) {
  const baseClassName = variant === 'danger'
    ? 'rounded-full border border-rose-200 bg-white p-2 text-rose-600 transition hover:bg-rose-50'
    : 'button-secondary p-2';

  return (
    <button type="button" className={baseClassName} onClick={onClick} title={label} aria-label={label} disabled={disabled}>
      <Icon className="size-4" />
    </button>
  );
}

export function ReadOnlyNotice({ label }: { label: string }) {
  return (
    <div className="rounded-[24px] border border-dashed border-line bg-white/75 px-4 py-3 text-sm text-ink-soft">
      {label}
    </div>
  );
}

export function EmptyTableState({ message, action }: { message: string; action?: ReactNode }) {
  return (
    <div className="rounded-[24px] border border-dashed border-line bg-white px-6 py-10 text-center">
      <p className="text-sm text-ink-soft">{message}</p>
      {action ? <div className="mt-4 flex justify-center">{action}</div> : null}
    </div>
  );
}

export type AdminUserFormState = {
  fullName: string;
  email: string;
  phone: string;
  password: string;
  role: 'client' | 'agent' | 'creative_director' | 'admin';
  accountStatus: string;
  isSaCitizen: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  assignedAreaCodes: string[];
};

export const hasText = (value: string) => value.trim().length > 0;
