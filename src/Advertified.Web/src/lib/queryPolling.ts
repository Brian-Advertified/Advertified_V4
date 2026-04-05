type PollableRecord = {
  paymentStatus?: string;
};

export function shouldPollWhenVisible() {
  if (typeof document === 'undefined') {
    return true;
  }

  return document.visibilityState === 'visible';
}

export function getPendingPaymentPollInterval(items?: PollableRecord[] | null, intervalMs = 15_000) {
  if (!shouldPollWhenVisible()) {
    return false;
  }

  return items?.some((item) => item.paymentStatus !== 'paid') ? intervalMs : false;
}

export function getActiveJobPollInterval(status?: string | null, intervalMs = 3_000) {
  if (!shouldPollWhenVisible()) {
    return false;
  }

  const normalizedStatus = status?.toLowerCase();
  return normalizedStatus === 'completed' || normalizedStatus === 'failed' ? false : intervalMs;
}
