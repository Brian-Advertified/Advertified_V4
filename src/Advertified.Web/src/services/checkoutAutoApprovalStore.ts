type AutoApprovalPayload = {
  campaignId?: string;
  recommendationId?: string;
  proposalPath?: string;
};

function buildStorageKey(orderId: string) {
  return `advertified:auto-approve:${orderId}`;
}

function getStorage() {
  if (typeof window === 'undefined') {
    return null;
  }

  return window.sessionStorage;
}

export function readCheckoutAutoApproval(orderId: string): AutoApprovalPayload | null {
  const storage = getStorage();
  if (!storage || !orderId) {
    return null;
  }

  const raw = storage.getItem(buildStorageKey(orderId));
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as AutoApprovalPayload;
  } catch {
    storage.removeItem(buildStorageKey(orderId));
    return null;
  }
}

export function writeCheckoutAutoApproval(orderId: string, payload: AutoApprovalPayload | null) {
  const storage = getStorage();
  if (!storage || !orderId) {
    return;
  }

  if (payload) {
    storage.setItem(buildStorageKey(orderId), JSON.stringify(payload));
    return;
  }

  storage.removeItem(buildStorageKey(orderId));
}

export function clearCheckoutAutoApproval(orderId: string) {
  writeCheckoutAutoApproval(orderId, null);
}
