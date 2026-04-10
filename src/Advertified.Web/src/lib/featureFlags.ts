const inventoryFallbackOverride = (import.meta.env.VITE_ENABLE_AGENT_INVENTORY_FALLBACK as string | undefined)?.trim().toLowerCase();
const publicAiStudioOverride = (import.meta.env.VITE_ENABLE_PUBLIC_AI_STUDIO as string | undefined)?.trim().toLowerCase();

export const agentInventoryFallbackEnabled =
  import.meta.env.DEV &&
  inventoryFallbackOverride !== 'false';

export const publicAiStudioEnabled =
  import.meta.env.DEV
  && publicAiStudioOverride !== 'false';
