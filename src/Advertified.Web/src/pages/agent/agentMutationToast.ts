type ToastTone = 'success' | 'error' | 'info';
type ToastInput = string | { title: string; description?: string };
type PushToast = (input: ToastInput, tone?: ToastTone) => void;

export function pushAgentMutationError(
  pushToast: PushToast,
  title: string,
  error: unknown,
  fallbackDescription = 'Please try again.',
) {
  pushToast(
    {
      title,
      description: error instanceof Error ? error.message : fallbackDescription,
    },
    'error',
  );
}

