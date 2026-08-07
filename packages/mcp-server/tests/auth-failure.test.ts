import { describe, expect, it } from 'vitest';

import { AuthRejectedError, DomainReloadError } from '../src/bridge-client.js';

/**
 * A 401 from the bridge is one of nine distinct reasons, sent in the response body. The client
 * used to discard the body and label every one of them "HMAC secret changed (domain reload)",
 * then retry for 30 seconds.
 *
 * That cost a real session an hour: every education/* call (GIF capture, package export) failed
 * identically while fast tools stayed healthy, and the operator chased Editor focus because the
 * only signal said "domain reload". The bridge had been naming the cause the whole time.
 */
describe('bridge 401 handling', () => {
  it('names the reason the bridge actually reported', () => {
    const e = new AuthRejectedError('nonce_replayed', 'POST', '/execute');
    expect(e.reason).toBe('nonce_replayed');
    expect(e.message).toContain('nonce_replayed');
    expect(e.message).toContain('POST /execute');
  });

  it('explains what a reason means, so the operator is not left guessing', () => {
    expect(new AuthRejectedError('nonce_replayed', 'POST', '/execute').message)
      .toContain('sent twice');
    expect(new AuthRejectedError('body_too_large', 'POST', '/execute').message)
      .toContain('exceeded');
    expect(new AuthRejectedError('clock_skew', 'POST', '/execute').message)
      .toContain('clocks disagree');
  });

  it('is NOT a DomainReloadError, so the retry loop cannot pick it up', () => {
    // Retrying a replayed nonce re-sends the same rejected request: the loop feeds itself.
    const e = new AuthRejectedError('nonce_replayed', 'POST', '/execute');
    expect(e).not.toBeInstanceOf(DomainReloadError);
    expect(e).toBeInstanceOf(Error);
    expect(e.name).toBe('AuthRejectedError');
  });

  it('says plainly that retrying will not help', () => {
    expect(new AuthRejectedError('body_too_large', 'POST', '/execute').message)
      .toContain('retrying will not help');
  });

  it('surfaces an unmapped reason rather than swallowing it', () => {
    // The bridge may add reasons; an unknown token must still reach the operator verbatim.
    const e = new AuthRejectedError('some_future_reason', 'GET', '/tools');
    expect(e.message).toContain('some_future_reason');
    expect(e.reason).toBe('some_future_reason');
  });

  it('keeps DomainReloadError for the case that is genuinely retryable', () => {
    const e = new DomainReloadError('bridge auth rejected (signature_mismatch) — likely a domain reload');
    expect(e).toBeInstanceOf(DomainReloadError);
    expect(e.message).toContain('signature_mismatch');
  });
});
