import { beforeEach, describe, expect, it } from 'vitest';
import {
  clearCheckoutAutoApproval,
  readCheckoutAutoApproval,
  writeCheckoutAutoApproval,
} from '../src/services/checkoutAutoApprovalStore';

describe('checkoutAutoApprovalStore', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  it('writes and reads checkout auto-approval state from sessionStorage', () => {
    writeCheckoutAutoApproval('order-1', {
      campaignId: 'campaign-1',
      recommendationId: 'recommendation-1',
      proposalPath: '/proposal/abc',
    });

    expect(readCheckoutAutoApproval('order-1')).toEqual({
      campaignId: 'campaign-1',
      recommendationId: 'recommendation-1',
      proposalPath: '/proposal/abc',
    });
  });

  it('clears stored checkout auto-approval state', () => {
    writeCheckoutAutoApproval('order-1', {
      campaignId: 'campaign-1',
    });

    clearCheckoutAutoApproval('order-1');

    expect(readCheckoutAutoApproval('order-1')).toBeNull();
  });

  it('removes invalid checkout auto-approval payloads', () => {
    window.sessionStorage.setItem('advertified:auto-approve:order-1', '{bad-json');

    expect(readCheckoutAutoApproval('order-1')).toBeNull();
    expect(window.sessionStorage.getItem('advertified:auto-approve:order-1')).toBeNull();
  });
});
