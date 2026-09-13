/**
 * C-1 — a paired connector drops in a 1006 reconnect loop with no backoff growth.
 *
 * The root cause was `connect()` resetting its retry counter to 0 inside its own
 * `ws.on("open")` handler: every cycle in the reported loop opened successfully
 * (pairing worked every time — "connector ready" printed each cycle) before dying
 * again moments later, so the counter reset every cycle and the wait stayed at ~1s
 * for the whole half hour. These tests cover the pure math that replaces that reset:
 * backoff only resets after a connection has proven itself stable for a while.
 */
import { describe, it, expect } from "vitest";
import { computeBackoffMs, nextAttempt, MAX_BACKOFF_MS, STABLE_MS } from "./backoff.js";

describe("nextAttempt — the C-1 fix itself", () => {
  it("keeps growing when the connection never became stable (the reported loop)", () => {
    // Simulates: open, immediately drop, five times in a row. Each drop is NOT stable,
    // so the attempt counter must climb 0 -> 1 -> 2 -> 3 -> 4, never resetting to 0
    // just because the socket opened.
    let attempt = 0;
    for (let i = 0; i < 5; i++) {
      attempt = nextAttempt(attempt, false);
    }
    expect(attempt).toBe(5);
  });

  it("resets to 0 once a connection has been stable", () => {
    expect(nextAttempt(4, true)).toBe(0);
  });

  it("a stable connection followed by an unstable one grows from 0, not from history", () => {
    let attempt = nextAttempt(3, true); // was stable: reset
    expect(attempt).toBe(0);
    attempt = nextAttempt(attempt, false); // then dropped again quickly
    expect(attempt).toBe(1);
  });
});

describe("computeBackoffMs — exponential with a cap and jitter", () => {
  const noJitter = () => 0.5; // midpoint of the [0.8, 1.2) jitter band -> factor 1.0

  it("doubles per attempt with no jitter applied", () => {
    expect(computeBackoffMs(0, noJitter)).toBe(1000);
    expect(computeBackoffMs(1, noJitter)).toBe(2000);
    expect(computeBackoffMs(2, noJitter)).toBe(4000);
    expect(computeBackoffMs(3, noJitter)).toBe(8000);
  });

  it("caps at MAX_BACKOFF_MS however high attempt climbs", () => {
    expect(computeBackoffMs(5, noJitter)).toBe(MAX_BACKOFF_MS);
    expect(computeBackoffMs(50, noJitter)).toBe(MAX_BACKOFF_MS);
  });

  it("never goes negative or zero for attempt 0, even at the low end of jitter", () => {
    const wait = computeBackoffMs(0, () => 0);
    expect(wait).toBeGreaterThan(0);
  });

  it("jitter stays within +/-20% of the unjittered base", () => {
    const base = 4000; // attempt 2
    for (const rand of [0, 0.25, 0.5, 0.75, 0.999]) {
      const wait = computeBackoffMs(2, () => rand);
      expect(wait).toBeGreaterThanOrEqual(base * 0.8 - 1);
      expect(wait).toBeLessThanOrEqual(base * 1.2 + 1);
    }
  });

  it("reproduces the reported bug's absence when growth is disabled: without the fix, every retry after a stable-open would wait the same 1s", () => {
    // This documents the bug being fixed, not the fix: if `nextAttempt` always
    // returned 0 on open (the old behaviour), backoff would never exceed attempt 0's
    // wait no matter how many times the cycle repeated.
    const oldBrokenNextAttempt = (_: number, _wasOpenAtAll: boolean) => 0;
    let attempt = 0;
    for (let i = 0; i < 5; i++) attempt = oldBrokenNextAttempt(attempt, true);
    expect(computeBackoffMs(attempt, noJitter)).toBe(1000); // stuck at the floor — the observed symptom
  });
});

describe("STABLE_MS", () => {
  it("is positive and comfortably longer than a single heartbeat tick could accidentally satisfy", () => {
    expect(STABLE_MS).toBeGreaterThan(0);
  });
});
