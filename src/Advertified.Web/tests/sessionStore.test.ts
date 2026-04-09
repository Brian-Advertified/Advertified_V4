import { beforeEach, describe, expect, it } from 'vitest';
import type { SessionUser } from '../src/types/domain';
import { readStoredSession, writeStoredSession } from '../src/services/sessionStore';

const STORAGE_KEY = 'advertified-session-user';

function buildSession(): SessionUser {
  return {
    id: 'user-1',
    fullName: 'Test User',
    email: 'test@example.com',
    role: 'client',
    emailVerified: true,
    identityComplete: false,
    requiresPasswordSetup: false,
  };
}

describe('sessionStore', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  it('writes active sessions to sessionStorage', () => {
    const session = buildSession();

    writeStoredSession(session);

    expect(window.sessionStorage.getItem(STORAGE_KEY)).toContain('test@example.com');
    expect(readStoredSession()).toEqual(session);
  });

  it('returns null for a missing session', () => {
    expect(readStoredSession()).toBeNull();
  });

  it('removes invalid stored session payloads', () => {
    window.sessionStorage.setItem(STORAGE_KEY, '{not-json');

    expect(readStoredSession()).toBeNull();
    expect(window.sessionStorage.getItem(STORAGE_KEY)).toBeNull();
  });

});
