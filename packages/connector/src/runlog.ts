/** Where a running `mosaic-connector run` records what it is doing, so a person (or a
 *  support request) has ONE current, discoverable place to look — instead of whichever
 *  terminal or wrapper-script redirect happened to still be scrolled back.
 *
 *  C-3: two connectors were run against the same access code (operator error), the
 *  newcomer evicted the incumbent server-side, and the incumbent's own
 *  "another connector took over this token; exiting" message was found only in a log
 *  FILE that was not the file the live process was writing to — the live process had no
 *  file output of its own at all, only whatever stdout redirect its wrapper used, and
 *  that file was stale by hours. `RunLogger` gives every `run` process the same file
 *  (`connector.log`, beside connector.json), so "where is this process logging" always
 *  has one answer, and `RunState` gives `mosaic-connector status` something live to
 *  read instead of only ever replaying the last stdout line (C-2).
 */
import fs from "node:fs";
import path from "node:path";
import { configDir } from "./cli.js";

/** 2MB is generous for connect/RPC-line text and small enough to open in an editor
 *  without thinking twice. Rotated, not truncated, so the previous session is not
 *  silently lost the moment a long-running connector finally fills it. */
export const MAX_LOG_BYTES = 2 * 1024 * 1024;

/** Past this many milliseconds without an update, a "connected" state file is treated as
 *  stale rather than trusted — comfortably more than one missed heartbeat (see
 *  HEARTBEAT_INTERVAL_MS in index.ts), so a single slow tick does not flip this. */
export const STALE_STATE_MS = 90_000;

export function logFilePath(dir: string = configDir()): string {
  return path.join(dir, "connector.log");
}

export function runStatePath(dir: string = configDir()): string {
  return path.join(dir, "run-state.json");
}

export type ConnectorState = "connecting" | "connected" | "waiting_for_editor" | "disconnected" | "exited";

export interface RunState {
  pid: number;
  startedAt: string;
  updatedAt: string;
  state: ConnectorState;
  unityVersion?: string;
  port?: number;
  detail?: string;
}

export function writeRunState(s: RunState, dir: string = configDir()): void {
  try {
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(runStatePath(dir), JSON.stringify(s, null, 2));
  } catch {
    /* best-effort: a missing state file only degrades `status`, never the connection */
  }
}

export function readRunState(dir: string = configDir()): RunState | null {
  try {
    return JSON.parse(fs.readFileSync(runStatePath(dir), "utf-8")) as RunState;
  } catch {
    return null;
  }
}

/** Best-effort liveness check for a recorded pid. A pid that no longer exists, or one
 *  this user has no permission to signal, both come back false — either way there is
 *  nothing to warn about. `kill` is injectable so this is testable without depending on
 *  which real pids happen to be free on the machine running the test. */
export function pidAlive(pid: number, kill: (pid: number, signal: number) => void = process.kill.bind(process)): boolean {
  try {
    kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

/** True when `state` describes another live process this `run` is about to collide with.
 *
 *  C-3(a): starting a second connector against the same token was silent. The service
 *  evicts the loser, so this check cannot be a hard refusal without risking blocking a
 *  legitimate restart (a process manager relaunching after a crash, for instance, may
 *  leave a stale state file behind pointing at a pid that is gone) — it exists to make
 *  the collision visible before it happens, not to prevent it. Flagged in the code that
 *  calls this: whether to also require confirmation is a product decision, not something
 *  this fix should decide unilaterally. */
export function collidesWithRunningInstance(
  state: RunState | null,
  selfPid: number,
  isAlive: (pid: number) => boolean = pidAlive
): boolean {
  if (!state) return false;
  if (state.pid === selfPid) return false;
  if (state.state === "exited") return false;
  return isAlive(state.pid);
}

/** Rotates the log once before a session starts, so `connector.log` is always the
 *  current run and `connector.log.1` is whatever came before it. */
function rotateIfLarge(file: string): void {
  try {
    if (fs.statSync(file).size > MAX_LOG_BYTES) {
      fs.renameSync(file, file + ".1");
    }
  } catch {
    /* no existing file: nothing to rotate */
  }
}

export class RunLogger {
  private readonly file: string;
  private writable = false;

  // Appends synchronously rather than through a persistent fs.WriteStream: a stream's
  // open() is asynchronous, which meant `existsSync(logger.path)` could observe "not
  // there yet" immediately after construction, and a session ending (close()) raced
  // against its own last writes actually reaching disk. A connector logs a handful of
  // lines a minute at most — durability and simplicity both favour a synchronous append
  // per line over a stream's throughput, which nothing here needs.
  constructor(dir: string = configDir()) {
    this.file = logFilePath(dir);
    try {
      fs.mkdirSync(dir, { recursive: true });
      rotateIfLarge(this.file);
      fs.appendFileSync(this.file, `\n=== session start pid=${process.pid} at ${new Date().toISOString()} ===\n`);
      this.writable = true;
    } catch {
      // File logging is a convenience, not the connection itself: stdout still carries
      // everything below, so a filesystem error here must never take the connector down.
      this.writable = false;
    }
  }

  get path(): string {
    return this.file;
  }

  /** Writes to stdout, as every caller did before, and — best-effort — the same line to
   *  the log file with a timestamp. C-2 was a connector whose last VISIBLE line was
   *  "ready" long after the state underneath had changed; this does not decide what is
   *  worth logging (callers already only call it for lines meant for a person), it just
   *  makes sure a disconnect is exactly as loud, and as durably recorded, as the connect
   *  it followed. */
  write(line: string): void {
    process.stdout.write(line);
    if (!this.writable) return;
    try {
      fs.appendFileSync(this.file, `[${new Date().toISOString()}] ${line}`);
    } catch {
      /* best-effort */
    }
  }

  /** Nothing to flush — every write() already landed on disk synchronously. Kept as a
   *  method so callers (and tests) do not need to know that. */
  close(): void {
    /* no-op */
  }
}

/** Renders `run-state.json` as prose for `mosaic-connector status`, so status reports
 *  what is actually happening right now rather than only the static config it always
 *  reported before. A separate `status` invocation is a different OS process from any
 *  running `run` — they share no memory — so this is the only channel between them. */
export function formatLiveStatus(dir: string = configDir(), now: number = Date.now(), isAlive: (pid: number) => boolean = pidAlive): string {
  const s = readRunState(dir);
  if (!s) return "connector: not running (no session has recorded state here yet)";
  if (!isAlive(s.pid)) {
    return `connector: not running (pid ${s.pid} from its last session is gone; last state was "${s.state}")`;
  }
  const age = now - Date.parse(s.updatedAt);
  const ageStr = Number.isFinite(age) ? `${Math.round(age / 1000)}s ago` : "unknown";
  const stale = Number.isFinite(age) && age > STALE_STATE_MS;
  const where = s.unityVersion || s.port ? ` (Unity ${s.unityVersion ?? "?"} on port ${s.port ?? "?"})` : "";
  const headline = `connector: pid ${s.pid}, state "${s.state}"${where}, updated ${ageStr}`;
  if (stale) {
    return (
      headline +
      `\n  WARNING: that is longer than expected for a healthy connection (${Math.round(STALE_STATE_MS / 1000)}s) — ` +
      "the process is running but may be stuck silently disconnected; check its log:\n" +
      `  ${logFilePath(dir)}`
    );
  }
  return headline + (s.detail ? `\n  ${s.detail}` : "");
}
