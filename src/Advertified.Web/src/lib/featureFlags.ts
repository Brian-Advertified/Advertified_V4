const inventoryFallbackOverride = (import.meta.env.VITE_ENABLE_AGENT_INVENTORY_FALLBACK as string | undefined)?.trim().toLowerCase();
const publicAiStudioOverride = (import.meta.env.VITE_ENABLE_PUBLIC_AI_STUDIO as string | undefined)?.trim().toLowerCase();

export const agentInventoryFallbackEnabled =
  import.meta.env.DEV &&
  inventoryFallbackOverride !== 'false';

function isDevRuntimeHost() {
  if (typeof window === 'undefined') {
    return false;
  }

  const hostname = window.location.hostname.toLowerCase();
  return hostname === 'localhost'
    || hostname === '127.0.0.1'
    || hostname.startsWith('dev.');
}

export const publicAiStudioEnabled =
  publicAiStudioOverride === 'true' &&
  (import.meta.env.DEV || isDevRuntimeHost());
