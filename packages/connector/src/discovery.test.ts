/**
 * A-1 — the connector doesn't re-pair after a Unity domain-reload restarts the bridge.
 *
 * `discoveryChanged` is the pure comparator the connect loop polls with to notice a
 * bridge that restarted underneath it (same or different port, a rotated secret, or —
 * what a domain reload actually produces — the same port and secret but a new
 * process_id / started_unix_seconds). These tests cover that comparison directly,
 * independent of the filesystem or the poll loop that calls it.
 */
import { describe, it, expect } from "vitest";
import { discoveryChanged, type Discovery } from "./discovery.js";

const base: Discovery = {
  port: 8282,
  secret_base64: "c2VjcmV0",
  unity_version: "6000.3.23f1",
  unity_project_path: "/Users/test/Projects/MyGame",
  process_id: 1111,
  started_unix_seconds: 1_700_000_000,
};

describe("discoveryChanged", () => {
  it("is 'changed' the first time, with no prior discovery", () => {
    expect(discoveryChanged(null, base)).toBe(true);
  });

  it("is unchanged when every field matches", () => {
    expect(discoveryChanged(base, { ...base })).toBe(false);
  });

  it("detects a domain reload: same pid, new started_unix_seconds (what DomainReloadHandler actually produces)", () => {
    const afterReload: Discovery = { ...base, started_unix_seconds: base.started_unix_seconds! + 45 };
    expect(discoveryChanged(base, afterReload)).toBe(true);
  });

  it("detects a full Editor restart: new pid and new started_unix_seconds", () => {
    const restarted: Discovery = { ...base, process_id: 2222, started_unix_seconds: base.started_unix_seconds! + 300 };
    expect(discoveryChanged(base, restarted)).toBe(true);
  });

  it("detects a different port", () => {
    expect(discoveryChanged(base, { ...base, port: 8383 })).toBe(true);
  });

  it("detects a rotated secret even if pid/port/started_at are all identical", () => {
    expect(discoveryChanged(base, { ...base, secret_base64: "bmV3c2VjcmV0" })).toBe(true);
  });

  it("does not false-positive on an unrelated field changing (unity_version bump alone, everything else identical)", () => {
    // Not expected in practice (a version change implies a restart too), but the
    // comparator should reason about identity fields, not every field in the struct.
    expect(discoveryChanged(base, { ...base, unity_version: "6000.3.24f1" })).toBe(false);
  });

  it("treats missing process_id/started_unix_seconds on either side as 'cannot tell', not 'changed' — older bridge builds may not write them", () => {
    const noProcessInfo: Discovery = { port: 8282, secret_base64: "c2VjcmV0" };
    expect(discoveryChanged(noProcessInfo, { ...noProcessInfo })).toBe(false);
  });
});
