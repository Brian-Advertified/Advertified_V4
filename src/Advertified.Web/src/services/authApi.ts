import type { LoginInput, RegistrationInput, RegistrationResult, SessionUser } from '../types/domain';
import { readStoredSession, writeStoredSession } from './sessionStore';
import { apiRequest } from './apiClient';

type RegisterResponse = {
  userId: string;
  email: string;
  emailVerificationRequired: boolean;
  accountStatus: string;
};

type LoginResponse = {
  userId: string;
  fullName: string;
  email: string;
  phone: string;
  role: string;
  accountStatus: string;
  emailVerified: boolean;
  requiresPasswordSetup: boolean;
  identityComplete: boolean;
  sessionToken: string;
};

type SetPasswordResponse = LoginResponse;

type MeResponse = {
  userId: string;
  fullName: string;
  email: string;
  phone: string;
  role: string;
  accountStatus: string;
  emailVerified: boolean;
  requiresPasswordSetup: boolean;
  identityComplete: boolean;
  phoneVerified: boolean;
  businessName?: string;
  registrationNumber?: string;
  city?: string;
  province?: string;
};

function emptyToUndefined(value?: string) {
  if (!value) {
    return undefined;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

function normalizeRole(role: string): SessionUser['role'] {
  if (role === 'agent') {
    return 'agent';
  }

  if (role === 'creative_director') {
    return 'creative_director';
  }

  if (role === 'admin') {
    return 'admin';
  }

  return 'client';
}

function mapSessionUser(response: LoginResponse | MeResponse, sessionToken?: string): SessionUser {
  return {
    id: response.userId,
    fullName: response.fullName,
    email: response.email,
    phone: 'phone' in response ? response.phone ?? undefined : undefined,
    role: normalizeRole(response.role),
    emailVerified: response.emailVerified,
    requiresPasswordSetup: response.requiresPasswordSetup,
    identityComplete: response.identityComplete,
    sessionToken,
    businessName: 'businessName' in response ? response.businessName ?? undefined : undefined,
    registrationNumber: 'registrationNumber' in response ? response.registrationNumber ?? undefined : undefined,
    city: 'city' in response ? response.city ?? undefined : undefined,
    province: 'province' in response ? response.province ?? undefined : undefined,
  };
}

async function getMe(_userId?: string) {
  const response = await apiRequest<MeResponse>('/me', {});
  return mapSessionUser(response);
}

async function register(input: RegistrationInput) {
  const response = await apiRequest<RegisterResponse>('/auth/register', {
    method: 'POST',
    body: JSON.stringify({
      fullName: input.fullName,
      email: input.email,
      phone: input.phone,
      isSouthAfricanCitizen: input.isSouthAfricanCitizen,
      password: input.password,
      confirmPassword: input.confirmPassword,
      businessName: input.businessName,
      businessType: input.businessType,
      registrationNumber: input.registrationNumber,
      vatNumber: emptyToUndefined(input.vatNumber),
      industry: input.industry,
      annualRevenueBand: input.annualRevenueBand,
      tradingAsName: emptyToUndefined(input.tradingAsName),
      streetAddress: input.streetAddress,
      city: input.city,
      province: input.province,
      saIdNumber: emptyToUndefined(input.saIdNumber),
      passportNumber: emptyToUndefined(input.passportNumber),
      passportCountryIso2: emptyToUndefined(input.passportCountryIso2),
      passportIssueDate: emptyToUndefined(input.passportIssueDate),
      passportValidUntil: emptyToUndefined(input.passportValidUntil),
      nextPath: emptyToUndefined(input.nextPath),
    }),
  });

  return {
    userId: response.userId,
    email: response.email,
    emailVerificationRequired: response.emailVerificationRequired,
    accountStatus: response.accountStatus,
  } satisfies RegistrationResult;
}

async function login(input: LoginInput) {
  const response = await apiRequest<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(input),
  });

  writeStoredSession(mapSessionUser(response, response.sessionToken));
  return getMe(response.userId)
    .then((user) => ({ ...user, sessionToken: response.sessionToken }))
    .catch(() => mapSessionUser(response, response.sessionToken));
}

async function verifyEmail(token: string) {
  const response = await apiRequest<LoginResponse>('/auth/verify-email', {
    method: 'POST',
    body: JSON.stringify({ token }),
  });

  writeStoredSession(mapSessionUser(response, response.sessionToken));
  return getMe(response.userId)
    .then((user) => ({
      ...user,
      sessionToken: response.sessionToken,
    }))
    .catch(() => mapSessionUser(response, response.sessionToken));
}

async function setPassword(input: { password: string; confirmPassword: string }) {
  const response = await apiRequest<SetPasswordResponse>('/auth/set-password', {
    method: 'POST',
    body: JSON.stringify(input),
  });

  writeStoredSession(mapSessionUser(response, response.sessionToken));
  return getMe(response.userId)
    .then((user) => ({ ...user, sessionToken: response.sessionToken, requiresPasswordSetup: false }))
    .catch(() => ({ ...mapSessionUser(response, response.sessionToken), requiresPasswordSetup: false }));
}

async function resendVerification(email?: string, nextPath?: string) {
  const session = readStoredSession();
  return apiRequest<{ message: string; email: string }>('/auth/resend-verification', {
    method: 'POST',
    body: JSON.stringify({
      email: email ?? session?.email ?? '',
      nextPath: emptyToUndefined(nextPath),
    }),
  });
}

export const authApi = {
  register,
  login,
  verifyEmail,
  setPassword,
  getMe,
  resendVerification,
};
