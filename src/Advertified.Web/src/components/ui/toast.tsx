import {
  createContext,
  useContext,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react';
import { CheckCircle2, CircleAlert, Info, X } from 'lucide-react';

type ToastTone = 'success' | 'error' | 'info';
type ToastInput = string | { title: string; description?: string };
type ToastItem = { id: string; title: string; description?: string; tone: ToastTone };
type ToastContextValue = { pushToast: (input: ToastInput, tone?: ToastTone) => void };

const ToastContext = createContext<ToastContextValue | undefined>(undefined);

export function ToastProvider({ children }: PropsWithChildren) {
  const [items, setItems] = useState<ToastItem[]>([]);

  const value = useMemo<ToastContextValue>(
    () => ({
      pushToast(input, tone = 'success') {
        const id = crypto.randomUUID();
        const payload = typeof input === 'string'
          ? { title: input }
          : input;
        setItems((current) => [...current, { id, title: payload.title, description: payload.description, tone }]);
        window.setTimeout(() => {
          setItems((current) => current.filter((item) => item.id !== id));
        }, 3500);
      },
    }),
    [],
  );

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div className="fixed right-4 top-4 z-50 flex w-full max-w-sm flex-col gap-3">
        {items.map((item) => (
          <div
            key={item.id}
            className={`panel flex items-start gap-3 px-4 py-4 ${
              item.tone === 'error'
                ? 'border-rose-200 bg-rose-50'
                : item.tone === 'info'
                  ? 'border-brand/20 bg-brand-soft'
                  : 'border-brand/20 bg-white'
            }`}
          >
            {item.tone === 'error' ? (
              <CircleAlert className="mt-0.5 size-5 text-rose-600" />
            ) : item.tone === 'info' ? (
              <Info className="mt-0.5 size-5 text-brand" />
            ) : (
              <CheckCircle2 className="mt-0.5 size-5 text-brand" />
            )}
            <div className="flex-1 space-y-1">
              <p className="text-sm font-semibold text-ink">{item.title}</p>
              {item.description ? <p className="text-sm leading-6 text-ink-soft">{item.description}</p> : null}
            </div>
            <button
              type="button"
              className="text-slate-400 transition hover:text-slate-600"
              onClick={() => setItems((current) => current.filter((entry) => entry.id !== item.id))}
            >
              <X className="size-4" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error('useToast must be used within ToastProvider');
  }

  return context;
}
