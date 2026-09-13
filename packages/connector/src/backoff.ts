/** Reconnect backoff math, kept separate from the socket so it can be tested without a
 *  network.
 *
 *  A customer reproduction (connector v0.9.1) showed a paired connector dropping in a
 *  1006 loop — "connector ready", "connection closed (1006); reconnecting in 1s",
 *  "connector ready", ... every cycle waiting the same 1s. `connect()` already computed
 *  an exponential wait from an `attempt` counter, but reset that counter to 0 inside its
 *  own `ws.on("open")` handler — and every cycle in the loop DID open successfully
 *  (pairing is what "connector ready" means) before dying moments later. So the counter
 *  was reset every single time, and the backoff never grew: reconnecting once a second
 *  is not "no backoff" by accident, it is what "reset attempt on open" produces against a
 *  connection that pairs but does not hold.
 */

/** Longest wait between reconnects, before jitter. */
export const MAX_BACKOFF_MS = 30_000;

/** How long a connection has to stay open before the NEXT drop is treated as a fresh,
 *  one-off event rather than a continuation of a run of failures. Chosen to be well
 *  clear of a single heartbeat cycle (see HEARTBEAT_INTERVAL_MS in runlog.ts / index.ts)
 *  so one missed ping cannot masquerade as "stable". This threshold is a judgement call,
 *  not a measured constant — 30s comfortably separates "just paired and immediately
 *  died" from "was fine for a while, then dropped", which is the distinction C-1 needed,
 *  without being so long that a person restarting Unity a few times in a row loses their
 *  backoff protection entirely. */
export const STABLE_MS = 30_000;

/** Exponential backoff, doubling per attempt up to MAX_BACKOFF_MS, with ±20% jitter so
 *  many connectors reconnecting at the same moment do not all retry in lockstep — a
 *  thundering herd against a service that may itself still be recovering from whatever
 *  caused the drop. `rand` is injectable so the jitter is deterministic in tests. */
export function computeBackoffMs(attempt: number, rand: () => number = Math.random): number {
  const base = Math.min(MAX_BACKOFF_MS, 1000 * 2 ** Math.max(0, Math.min(attempt, 5)));
  const jitterFactor = 0.8 + rand() * 0.4; // [0.8, 1.2)
  return Math.round(base * jitterFactor);
}

/** How many consecutive un-stable attempts the NEXT reconnect should count as.
 *
 *  `wasStable` is true when the connection that just closed had been open at least
 *  STABLE_MS. A stable connection resets the counter to 0: whatever caused this drop is
 *  presumably new, so the next retry should not inherit a long backoff earned by
 *  failures that happened minutes or hours ago. A connection that never became stable —
 *  paired, then dropped again quickly, as in C-1 — keeps growing the backoff instead of
 *  resetting on the mere fact that it opened. */
export function nextAttempt(current: number, wasStable: boolean): number {
  return wasStable ? 0 : current + 1;
}
