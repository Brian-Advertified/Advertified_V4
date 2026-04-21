const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? 'http://localhost:5050';

type ApiErrorShape = {
  message?: string;
  Message?: string;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

function looksLikeHtmlDocument(value: string) {
  const trimmed = value.trim().toLowerCase();
  return trimmed.startsWith('<!doctype html') || trimmed.startsWith('<html');
}

function parseApiPayload(text: string, contentType: string | null): ApiErrorShape | null {
  if (!text.trim()) {
    return null;
  }

  const shouldTryJson = contentType?.toLowerCase().includes('json') ?? false;
  if (!shouldTryJson && looksLikeHtmlDocument(text)) {
    return null;
  }

  try {
    return JSON.parse(text) as ApiErrorShape;
  } catch {
    return null;
  }
}

function humanizeApiMessage(message: string) {
  const normalized = message.trim();

  if (!normalized) {
    return 'Something went wrong. Please try again.';
  }

  if (/selected budget must be between/i.test(normalized)) {
    return normalized.replace('Selected budget must be between', 'Choose an amount between');
  }

  if (/email or password is incorrect/i.test(normalized)) {
    return 'Invalid log in, please check your email and password.';
  }

  if (/activate your account from the email we sent before signing in/i.test(normalized)) {
    return 'Your account has not been activated yet. Please check your email for the activation link.';
  }

  if (/vodapay initiate request failed/i.test(normalized) || /did not return a checkout url/i.test(normalized)) {
    return 'We could not start the VodaPay checkout just now. Please try again in a moment.';
  }

  if (/could not resolve package order id/i.test(normalized)) {
    return 'We could not match this payment update to your order.';
  }

  if (/package band not found/i.test(normalized)) {
    return 'That package is no longer available. Please choose a package again.';
  }

  if (/package order not found/i.test(normalized)) {
    return 'We could not find that order anymore. Please refresh and try again.';
  }

  if (/campaign brief not found/i.test(normalized)) {
    return 'Fill in your campaign brief first, then try again.';
  }

  if (/campaign not found/i.test(normalized)) {
    return 'We could not find that campaign. Please refresh and try again.';
  }

  if (/a user with this email address already exists/i.test(normalized)) {
    return 'An account with this email already exists. Try signing in or use a different email address.';
  }

  if (/request failed with status 400/i.test(normalized)) {
    return 'Something in the request needs attention. Please check the form and try again.';
  }

  if (/request failed with status 401|unauthorized/i.test(normalized)) {
    return 'Your session expired due to no activity. Please sign in again.';
  }

  if (/request failed with status 403/i.test(normalized)) {
    return 'You do not have access to do that yet.';
  }

  if (/request failed with status 404/i.test(normalized)) {
    return 'We could not find what you were looking for.';
  }

  if (/request failed with status 5\d\d/i.test(normalized)) {
    return 'Our side had a problem processing that request. Please try again shortly.';
  }

  return normalized;
}

export async function parseApiError(response: Response) {
  const contentType = response.headers.get('content-type');
  const text = await response.text();
  const payload = parseApiPayload(text, contentType);

  const validationErrors = payload?.errors
    ? Object.values(payload.errors)
        .flat()
        .filter(Boolean)
    : [];
  const message =
    validationErrors[0] ??
    payload?.message ??
    payload?.Message ??
    payload?.detail ??
    payload?.title ??
    (looksLikeHtmlDocument(text)
      ? 'The app received an HTML page instead of an API response. Check the API base URL or sign in again.'
      : null) ??
    text.trim() ??
    `Request failed with status ${response.status}.`;

  throw new Error(humanizeApiMessage(message));
}

export async function readJsonResponse<T>(response: Response): Promise<T> {
  const contentType = response.headers.get('content-type');
  const text = await response.text();

  if (!text.trim()) {
    return undefined as T;
  }

  if (looksLikeHtmlDocument(text)) {
    throw new Error('The app received an HTML page instead of API JSON. Check the API base URL or sign in again.');
  }

  try {
    return JSON.parse(text) as T;
  } catch {
    const contentTypeLabel = contentType ? ` (${contentType})` : '';
    throw new Error(`The app received an invalid API response${contentTypeLabel}.`);
  }
}

export function toAbsoluteApiUrl(path?: string | null) {
  if (!path) {
    return undefined;
  }

  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  return `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`;
}

function resolveDownloadFileName(response: Response, fallbackFileName?: string) {
  const contentDisposition = response.headers.get('content-disposition');
  const match = contentDisposition?.match(/filename\*?=(?:UTF-8''|")?([^\";]+)/i);
  const decoded = match?.[1] ? decodeURIComponent(match[1].replace(/\"/g, '')) : null;
  return decoded ?? fallbackFileName ?? 'document.pdf';
}

export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);

  if (!headers.has('Content-Type') && options.body) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
    credentials: 'include',
  });

  if (!response.ok) {
    await parseApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return readJsonResponse<T>(response);
}

export async function downloadProtectedFile(path: string, fallbackFileName?: string) {
  const response = await fetch(toAbsoluteApiUrl(path) ?? path, {
    credentials: 'include',
  });

  if (!response.ok) {
    await parseApiError(response);
  }

  const blob = await response.blob();
  const objectUrl = window.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = resolveDownloadFileName(response, fallbackFileName);
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.URL.revokeObjectURL(objectUrl);
}

export async function downloadPublicFile(path: string, fallbackFileName?: string) {
  const response = await fetch(toAbsoluteApiUrl(path) ?? path, {
    credentials: 'include',
  });

  if (!response.ok) {
    await parseApiError(response);
  }

  const blob = await response.blob();
  const objectUrl = window.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = resolveDownloadFileName(response, fallbackFileName);
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.URL.revokeObjectURL(objectUrl);
}

export { API_BASE_URL };
