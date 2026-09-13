/**
 * C-2 — `connector ready` describes a past event, not live state.
 * C-3 — silent token takeover, and a stale log file.
 *
 * These cover the two pieces that back both findings:
 *   - RunLogger: every `run` process writes to ONE discoverable, current file, so
 *     there is never a question of "which file is the live process actually writing".
 *   - run-state.json + formatLiveStatus: `status` reads what a running process last
 *     recorded about itself, instead of only ever having static config to report.
 *   - collidesWithRunningInstance: the pre-start check for C-3(a).
 */
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  RunLogger,
  writeRunState,
  readRunState,
  pidAlive,
  collidesWithRunningInstance,
  formatLiveStatus,
  logFilePath,
  runStatePath,
  MAX_LOG_BYTES,
  STALE_STATE_MS,
  type RunState,
} from "./runlog.js";

let dir: string;

beforeEach(() => {
  dir = fs.mkdtempSync(path.join(os.tmpdir(), "connector-runlog-test-"));
});

afterEach(() => {
  fs.rmSync(dir, { recursive: true, force: true });
});

describe("RunLogger — C-3(b): one discoverable, current log file", () => {
  it("creates connector.log at the expected, fixed path", () => {
    const logger = new RunLogger(dir);
    expect(logger.path).toBe(logFilePath(dir));
    expect(fs.existsSync(logger.path)).toBe(true);
    logger.close();
  });

  it("still writes to stdout (unchanged behaviour) as well as the file", () => {
    const logger = new RunLogger(dir);
    let captured = "";
    const original = process.stdout.write.bind(process.stdout);
    process.stdout.write = ((chunk: any) => {
      captured += chunk;
      return true;
    }) as any;
    try {
      logger.write("connector ready (Unity 6000.3.23f1 on port 8282)\n");
    } finally {
      process.stdout.write = original;
    }
    expect(captured).toContain("connector ready");
    logger.close();
  });

  it("appends a timestamped line to the file matching what stdout received", async () => {
    const logger = new RunLogger(dir);
    logger.write("connector ready (Unity 6000.3.23f1 on port 8282)\n");
    logger.close();
    await new Promise((r) => setTimeout(r, 20)); // let the write stream flush
    const contents = fs.readFileSync(logFilePath(dir), "utf-8");
    expect(contents).toContain("connector ready (Unity 6000.3.23f1 on port 8282)");
    // A disconnect must be exactly as durable/loud as the connect that preceded it —
    // this is the direct fix for "connector ready stayed the last line while the
    // socket had already dropped".
    expect(contents).toMatch(/\[\d{4}-\d{2}-\d{2}T.*\] connector ready/);
  });

  it("marks each session with a start banner, so staleness is obvious on inspection", async () => {
    const logger = new RunLogger(dir);
    logger.close();
    await new Promise((r) => setTimeout(r, 20));
    const contents = fs.readFileSync(logFilePath(dir), "utf-8");
    expect(contents).toMatch(/=== session start pid=\d+ at .* ===/);
  });

  it("rotates rather than growing without bound once the log is large", async () => {
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(logFilePath(dir), "x".repeat(MAX_LOG_BYTES + 1));
    const logger = new RunLogger(dir);
    logger.close();
    await new Promise((r) => setTimeout(r, 20));
    expect(fs.existsSync(logFilePath(dir) + ".1")).toBe(true);
    expect(fs.statSync(logFilePath(dir)).size).toBeLessThan(MAX_LOG_BYTES);
  });
});

describe("run-state — C-2: status reads live state, not a replayed log line", () => {
  const stateAt = (patch: Partial<RunState>): RunState => ({
    pid: process.pid,
    startedAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    state: "connected",
    ...patch,
  });

  it("round-trips through disk", () => {
    const s = stateAt({ unityVersion: "6000.3.23f1", port: 8282, detail: "connector ready" });
    writeRunState(s, dir);
    expect(readRunState(dir)).toEqual(s);
  });

  it("returns null when nothing has been written yet", () => {
    expect(readRunState(dir)).toBeNull();
  });

  it("formatLiveStatus says 'not running' when no state file exists", () => {
    expect(formatLiveStatus(dir)).toMatch(/not running/);
  });

  it("formatLiveStatus reports a healthy, recently-updated connection plainly", () => {
    writeRunState(stateAt({ unityVersion: "6000.3.23f1", port: 8282 }), dir);
    const out = formatLiveStatus(dir, Date.now(), () => true);
    expect(out).toContain("connected");
    expect(out).toContain("8282");
    expect(out).not.toMatch(/WARNING/);
  });

  it("formatLiveStatus warns when the state is old enough to suggest a silent hang — this is the C-2 fix: 'ready' can no longer be the last word", () => {
    const staleUpdatedAt = new Date(Date.now() - (STALE_STATE_MS + 5000)).toISOString();
    writeRunState(stateAt({ updatedAt: staleUpdatedAt, detail: "connector ready" }), dir);
    const out = formatLiveStatus(dir, Date.now(), () => true);
    expect(out).toMatch(/WARNING/);
    expect(out).toMatch(/silently disconnected/);
  });

  it("formatLiveStatus reports 'not running' when the recorded pid is gone, regardless of what the file says", () => {
    writeRunState(stateAt({ detail: "connector ready" }), dir);
    const out = formatLiveStatus(dir, Date.now(), () => false);
    expect(out).toMatch(/not running/);
  });
});

describe("pidAlive", () => {
  it("is true for a pid the injected kill() does not throw for", () => {
    expect(pidAlive(123, () => undefined)).toBe(true);
  });

  it("is false for a pid the injected kill() throws for (ESRCH-style)", () => {
    expect(
      pidAlive(123, () => {
        throw new Error("ESRCH");
      })
    ).toBe(false);
  });
});

describe("collidesWithRunningInstance — C-3(a): pre-start collision warning", () => {
  it("does not collide with itself", () => {
    const s: RunState = { pid: 42, startedAt: "", updatedAt: "", state: "connected" };
    expect(collidesWithRunningInstance(s, 42, () => true)).toBe(false);
  });

  it("does not collide when no prior state exists", () => {
    expect(collidesWithRunningInstance(null, 42, () => true)).toBe(false);
  });

  it("does not collide with a state that already recorded its own exit", () => {
    const s: RunState = { pid: 99, startedAt: "", updatedAt: "", state: "exited" };
    expect(collidesWithRunningInstance(s, 42, () => true)).toBe(false);
  });

  it("does not collide when the recorded pid is no longer alive (stale state file)", () => {
    const s: RunState = { pid: 99, startedAt: "", updatedAt: "", state: "connected" };
    expect(collidesWithRunningInstance(s, 42, () => false)).toBe(false);
  });

  it("collides when another pid recorded a non-exited state and is still alive — the C-3 scenario: two connectors, same token", () => {
    const s: RunState = { pid: 99, startedAt: "", updatedAt: "", state: "connected" };
    expect(collidesWithRunningInstance(s, 42, () => true)).toBe(true);
  });
});

describe("path helpers", () => {
  it("logFilePath and runStatePath live beside each other in the given directory", () => {
    expect(path.dirname(logFilePath(dir))).toBe(dir);
    expect(path.dirname(runStatePath(dir))).toBe(dir);
  });
});
