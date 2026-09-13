#!/usr/bin/env node
/**
 * mosaic-connector — links this machine's Unity Editor to a Mosaic Cloud service.
 *
 * The connection is made outward only: this process dials the service, so nothing
 * needs to be opened on the customer's network, and the Editor is never exposed to
 * anything except calls arriving on this authenticated socket.
 *
 *   mosaic-connector --url wss://cloud.example.com/tunnel --token <token>
 *
 * Discovery of the local bridge (port and signing secret) uses the same
 * bridge-discovery.json the MCP server reads, so a running Editor is found the same
 * way it always is.
 */
import WebSocket from "ws";
import { setup, readConfig, writeConfig, addProject, statusReport, usage, servicePackages, AccessCodeRejected, MachineLimitReached, machineIdentity, refreshEntitlement, CONNECTOR_VERSION } from "./cli.js";
import { findDiscovery, bridgeAlive, discoveryChanged, type Discovery } from "./discovery.js";
import { computeBackoffMs, nextAttempt, STABLE_MS } from "./backoff.js";
import { RunLogger, writeRunState, readRunState, collidesWithRunningInstance, formatLiveStatus, type RunState } from "./runlog.js";
import { createHash, createHmac, randomUUID } from "node:crypto";

/** How often the connector pings the tunnel while connected, and — same interval — how
 *  often it re-checks the discovery file for a bridge that restarted underneath it.
 *
 *  C-1's reported 1006 loop is what an abnormal close with no close frame usually means
 *  happened on the OTHER end already — a dead intermediary, or a proxy that timed the
 *  connection out — invisibly, for however long nothing here was watching. A ping the
 *  far end must answer converts that into a clean, promptly-detected close instead of a
 *  socket that looks open until some unrelated write eventually fails. A-1 is the same
 *  gap on the bridge side: nothing polled for a domain reload finishing, so nothing
 *  said so until the next RPC happened to arrive. */
const HEARTBEAT_INTERVAL_MS = 20_000;

interface Args {
  url: string;
  token: string;
  discoveryFile?: string;
  verbose: boolean;
}

function parseArgs(argv: string[]): Args {
  const get = (flag: string): string | undefined => {
    const i = argv.indexOf(flag);
    return i >= 0 ? argv[i + 1] : undefined;
  };
  const stored = readConfig();
  const url = get("--url") || process.env.MOSAIC_CLOUD_URL || stored?.url || "";
  const token = get("--token") || process.env.MOSAIC_CLOUD_TOKEN || stored?.token || "";
  if (!url || !token) {
    process.stderr.write("not configured. Run: mosaic-connector setup\n");
    process.exit(2);
  }
  return { url, token, discoveryFile: get("--discovery-file"), verbose: argv.includes("--verbose") };
}

/** Subcommands run and exit; anything else falls through to the connection loop. */
async function main(argv: string[]): Promise<void> {
  const cmd = argv[0];
  if (cmd === "setup") {
    const get = (f: string) => (argv.indexOf(f) >= 0 ? argv[argv.indexOf(f) + 1] : undefined);
    // --project makes the whole of setup answerable from a command line, which is what
    // an automation tool or an IT rollout needs; without it, setup could only ever be
    // completed by a person typing at a prompt.
    await setup({ url: get("--url"), token: get("--token") }, get("--project"));
    return;
  }
  if (cmd === "add") {
    const target = argv[1];
    if (!target) {
      process.stderr.write("usage: mosaic-connector add <path to Unity project>\n");
      process.exit(2);
    }
    // A project added later must get the same packages as one added during setup,
    // so ask the service again rather than adding Bridge alone.
    const stored = readConfig();
    let svc = null;
    try {
      svc = stored ? await servicePackages(stored.url, stored.token, machineIdentity(stored)) : null;
    } catch (e) {
      if (e instanceof MachineLimitReached) {
        process.stderr.write(e.message + "\nThe project was not changed.\n");
        process.exit(2);
      }
      if (e instanceof AccessCodeRejected) {
        process.stderr.write(
          "The service did not accept the saved access code, so the project was not changed.\n" +
            "Run: mosaic-connector setup   with the correct code, then add the project again.\n"
        );
        process.exit(2);
      }
      throw e;
    }
    // Record the project before touching it. A crash between the manifest write and
    // the config write leaves the project and the connector disagreeing about
    // reality, which is exactly what happened: `add` reported success, then died,
    // and `status` showed nothing.
    const cfg = readConfig();
    if (cfg && !cfg.projects.includes(target)) {
      cfg.projects.push(target);
      writeConfig(cfg);
    }
    const r = addProject(target, svc);
    process.stdout.write(r.message + "\n");
    if (stored) {
      const ent = await refreshEntitlement(stored.url, stored.token, machineIdentity(stored));
      process.stdout.write((ent.ok ? "" : "NOTE: ") + ent.message + "\n");
    }
    if (r.added) {
      process.stdout.write("Open the project in Unity once so the packages import.\n");
    }
    return;
  }
  if (cmd === "version" || cmd === "--version" || cmd === "-v") {
    process.stdout.write(`mosaic-connector ${CONNECTOR_VERSION}\n`);
    return;
  }
  if (cmd === "status") {
    // C-2: static config used to be the whole answer. It is still the first half; the
    // second half is whatever a currently-running `run` process last recorded about
    // itself, read from disk because a separate `status` invocation shares no memory
    // with it.
    process.stdout.write(statusReport() + "\n\n" + formatLiveStatus() + "\n");
    return;
  }
  if (cmd === "help" || cmd === "--help" || cmd === "-h") {
    process.stdout.write(usage() + "\n");
    return;
  }
  // No configuration yet and no command: walk the person through setup rather than
  // printing a usage error they cannot act on.
  if (!cmd && !readConfig()) {
    await setup();
    return;
  }

  // C-3(a): a second connector against the same token evicts the first, silently. The
  // service is the actual authority here (it is the one that enforces the eviction), so
  // this cannot be a hard refusal without risking blocking a legitimate restart (a
  // process manager relaunching after a crash can easily leave a stale state file
  // pointing at a pid that is already gone). What it CAN do, safely, is say what is
  // about to happen before it happens, so "another connector took over this token" is
  // not the first anyone hears of it. Whether a future version should instead prompt for
  // confirmation (and, if so, how to do that under a process supervisor with no
  // attached terminal) is a product decision left open here.
  const priorState = readRunState();
  if (collidesWithRunningInstance(priorState, process.pid)) {
    process.stdout.write(
      `NOTE: another mosaic-connector process (pid ${priorState!.pid}) appears to already be running on ` +
        `this machine (state: "${priorState!.state}", last updated ${priorState!.updatedAt}).\n` +
        "Starting this one will take over its connection to the service; the other process will then exit.\n"
    );
  }

  const logger = new RunLogger();
  const args = parseArgs(argv.filter((a) => a !== "run"));

  const state: RunState = { pid: process.pid, startedAt: new Date().toISOString(), updatedAt: new Date().toISOString(), state: "connecting" };
  const updateState = (patch: Partial<RunState>) => {
    Object.assign(state, patch, { updatedAt: new Date().toISOString() });
    writeRunState(state);
  };
  updateState({});

  // A process manager sending SIGTERM, or a person pressing Ctrl+C, both leave "connected"
  // behind in run-state.json forever unless something marks the exit — which would make
  // a LATER `status` (or the collision check above, for the next connector started here)
  // report a live connection that no longer exists.
  const markExited = () => {
    updateState({ state: "exited" });
    logger.close();
    process.exit(0);
  };
  process.on("SIGINT", markExited);
  process.on("SIGTERM", markExited);

  connect(args, 0, logger, updateState);
}

/** The bridge authenticates every request with an HMAC over a canonical string.
 *
 *  This mirrors packages/mcp-server/src/hmac.ts exactly, including the v1
 *  length-prefixed framing: a signature that merely "looks right" is rejected, and
 *  the local MCP server is the reference implementation for what right means. */
function buildCanonical(nonce: string, timestamp: string, method: string, path: string, bodySha256: string): string {
  const len = (x: string) => Buffer.byteLength(x, "utf8");
  return [
    "v1",
    `${len(nonce)}:${nonce}`,
    `${len(timestamp)}:${timestamp}`,
    `${len(method)}:${method}`,
    `${len(path)}:${path}`,
    `${len(bodySha256)}:${bodySha256}`,
  ].join("\n");
}

function signRequest(secretBase64: string, method: string, path: string, body: Buffer) {
  const nonce = randomUUID().replace(/-/g, "");
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const bodySha256 = createHash("sha256").update(body).digest("hex");
  const canonical = buildCanonical(nonce, timestamp, method.toUpperCase(), path, bodySha256);
  const signature = createHmac("sha256", Buffer.from(secretBase64, "base64"))
    .update(Buffer.from(canonical, "utf8"))
    .digest("hex");
  return { nonce, timestamp, signature };
}

async function bridgeRequest(
  d: Discovery,
  method: "GET" | "POST",
  path: string,
  body: unknown,
  timeoutMs: number
): Promise<unknown> {
  const bodyBuffer = body ? Buffer.from(JSON.stringify(body), "utf8") : Buffer.alloc(0);
  const { nonce, timestamp, signature } = signRequest(d.secret_base64, method, path, bodyBuffer);
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(`http://127.0.0.1:${d.port}${path}`, {
      method,
      headers: {
        "Content-Type": "application/json",
        "X-Mosaic-Nonce": nonce,
        "X-Mosaic-Timestamp": timestamp,
        "X-Mosaic-Signature": signature,
      },
      body: bodyBuffer.length > 0 ? bodyBuffer : undefined,
      signal: controller.signal,
    });
    const text = await res.text();
    let parsed: unknown = text;
    try {
      parsed = JSON.parse(text);
    } catch {
      /* the bridge answered with plain text; pass it through */
    }
    if (!res.ok) throw new Error(`bridge ${res.status}: ${typeof parsed === "string" ? parsed : JSON.stringify(parsed)}`);
    return parsed;
  } finally {
    clearTimeout(timer);
  }
}

/** Routes the cloud understands: a bridge tool name executes, and two service routes
 *  let the cloud ask what this Editor is and what it can do. */
async function callBridge(d: Discovery, route: string, params: unknown, timeoutMs: number): Promise<unknown> {
  if (route === "_health") {
    // Which Editor this is. The service kept reporting one project's "first answered"
    // time after a different project had been opened, because nothing in the health
    // reply said which Editor was answering.
    const h = await bridgeRequest(d, "GET", "/health", undefined, timeoutMs);
    return typeof h === "object" && h
      ? { ...(h as object), project_path: d.unity_project_path ?? null, unity_version: d.unity_version ?? null, port: d.port }
      : h;
  }
  if (route === "_tools") return bridgeRequest(d, "GET", "/tools", undefined, timeoutMs);
  // The Editor registers tools as mosaic_<category>_<action>; the pipeline and its
  // docs speak of routes as <category>/<action>. Accept either spelling rather than
  // making the caller remember which layer it is talking to.
  const tool = route.includes("/") ? "mosaic_" + route.replace(/\//g, "_") : route;
  return bridgeRequest(d, "POST", "/execute", { tool, parameters: params ?? {} }, timeoutMs);
}

function connect(args: Args, attempt: number, logger: RunLogger, updateState: (patch: Partial<RunState>) => void): void {
  // Who is dialling in, not just with which code. The service keeps the list of
  // machines per code and refuses one too many.
  const m = machineIdentity(readConfig());
  const q = new URLSearchParams({
    token: args.token, machine: m.id, host: m.host, osuser: m.osUser, platform: m.platform, connector: m.connector,
  });
  const target = `${args.url}${args.url.includes("?") ? "&" : "?"}${q.toString()}`;
  const ws = new WebSocket(target);
  const openedAt = Date.now();

  // A 403 carries the reason in its body — which machines already hold this code —
  // and reconnecting would only repeat it. Print it, and stop. A 401 marked "expired"
  // is different: the code is right and its date has passed. That is the operator's
  // to extend, so this connector waits and checks every ten minutes; when the date
  // moves it connects on its own and nothing has to be typed here.
  // (With a listener on this event, ws emits no "error", so each branch here must
  // finish the story itself: exit, or schedule the next attempt.)
  ws.on("unexpected-response", (req, res) => {
    if (res.statusCode === 401 && String(res.headers["x-mosaic-reason"] || "") === "expired") {
      let body = "";
      res.on("data", (c: Buffer) => (body += c.toString()));
      res.on("end", () => {
        const wait = 10 * 60_000;
        logger.write((body || "This access code has expired.") + `\nChecking again in ${wait / 60_000} minutes.\n`);
        updateState({ state: "disconnected", detail: "access code expired" });
        req.destroy();
        setTimeout(() => connect(args, 0, logger, updateState), wait);
      });
      return;
    }
    if (res.statusCode !== 403) return;
    let body = "";
    res.on("data", (c: Buffer) => (body += c.toString()));
    res.on("end", () => {
      logger.write((body || "The service refused this machine.") + "\n");
      updateState({ state: "exited", detail: "service refused this machine (403)" });
      logger.close();
      process.exit(2);
    });
  });

  ws.on("open", async () => {
    // C-1: this used to reset `attempt` to 0 right here, unconditionally. Every cycle
    // in the reported 1006 loop DID reach "open" — pairing succeeded every time — so
    // that reset fired every time too, and the exponential backoff below never grew: it
    // is the reason "reconnecting in 1s" repeated for half an hour. `attempt` now only
    // resets once this connection has proven itself open for STABLE_MS (see retry()).

    // A ping the far end must answer, not merely a socket object that has not yet
    // reported an error. `terminate()` forces a close (and so a loud, logged retry) as
    // soon as a pong is missed, instead of leaving a half-open connection undetected.
    let alive = true;
    ws.on("pong", () => {
      alive = true;
    });
    const heartbeat = setInterval(() => {
      if (!alive) {
        ws.terminate();
        return;
      }
      alive = false;
      ws.ping();
      // A live, idle connection with nothing to say still proves it is alive every
      // heartbeat, so `status` never has to guess whether "connected, updated 40
      // minutes ago" means healthy-and-quiet or actually-dead-and-nobody-noticed.
      updateState({});
    }, HEARTBEAT_INTERVAL_MS);
    ws.on("close", () => clearInterval(heartbeat));

    // A fresh Pro licence on every connection, and every six hours while connected: a
    // person who is still allowed never sees it expire, and a revoked code stops Pro
    // within a week whether or not the connector is ever restarted.
    const refresh = async () => {
      const ent = await refreshEntitlement(args.url, args.token, m);
      if (!ent.ok) logger.write("NOTE: " + ent.message + "\n");
      else if (args.verbose) logger.write(ent.message + "\n");
    };
    void refresh();
    const refreshTimer = setInterval(refresh, 6 * 3600_000);
    ws.on("close", () => clearInterval(refreshTimer));
    let d: Discovery | null = null;
    try {
      const found = findDiscovery(args.discoveryFile);
      // Announce a version and a port only after something answers on them. The file
      // outlives the Editor, and an Editor whose bridge failed to compile leaves one
      // behind that describes a bridge which never ran.
      d = (await bridgeAlive(found)) ? found : null;
      if (!d) {
        logger.write(
          "A Unity Editor was found on record, but its Mosaic Bridge is not answering.\n" +
            "  Usually the project is still importing, or the package failed to compile.\n" +
            "  Check the Unity Console for errors, then leave this running.\n"
        );
      }
    } catch {
      /* reported below as a waiting state, not as a failure */
    }

    // A-1: watches the discovery file for a bridge that restarted underneath this
    // connection (a domain reload tears down and restarts the bridge's HTTP server,
    // same or different pid, always a new started_unix_seconds). The message handler
    // below already re-reads the file on every RPC, so a call arriving after a restart
    // mostly self-heals on its own — but if nothing arrives from the cloud during the
    // gap, nothing here ever SAID the bridge came back, and the last printed line stayed
    // "connector ready" for an Editor that had, for a while, actually gone (compounding
    // C-2). This polls independently of any RPC traffic so that reconnection is
    // observed and announced, not merely eventually true.
    let known: Discovery | null = d;
    const watchDiscovery = setInterval(async () => {
      try {
        const found = findDiscovery(args.discoveryFile);
        if (!discoveryChanged(known, found)) return;
        const stillAlive = await bridgeAlive(found);
        known = found;
        if (stillAlive) {
          logger.write(`bridge restarted (Unity ${found.unity_version ?? "?"} on port ${found.port}) — re-paired\n`);
          updateState({ state: "connected", unityVersion: found.unity_version, port: found.port, detail: "re-paired after bridge restart" });
        } else {
          logger.write("the Unity Editor's Mosaic Bridge went away; waiting for it to come back.\n");
          updateState({ state: "waiting_for_editor", detail: "bridge stopped answering" });
        }
      } catch {
        // findDiscovery throws when nothing is on record at all (e.g. the Editor fully
        // closed, not just reloaded); treat that the same as "went away" once, not on
        // every tick.
        if (known !== null) {
          known = null;
          logger.write("the Unity Editor's Mosaic Bridge went away; waiting for it to come back.\n");
          updateState({ state: "waiting_for_editor", detail: "bridge no longer on record" });
        }
      }
    }, HEARTBEAT_INTERVAL_MS);
    ws.on("close", () => clearInterval(watchDiscovery));

    if (d) {
      logger.write(`connector ready (Unity ${d.unity_version ?? "?"} on port ${d.port})\n`);
      updateState({ state: "connected", unityVersion: d.unity_version, port: d.port, detail: "connector ready" });
    } else {
      // Do not say "ready" when there is no Editor: the previous version printed the
      // problem and "connector ready" one line apart, and a person reasonably read
      // the second line and stopped. It also blamed a closed project when the real
      // cause is usually a project without the Mosaic Bridge package.
      logger.write(
        "connected to the service, waiting for a Unity Editor.\n" +
          "  Open a Unity project that has the Mosaic Bridge package installed.\n" +
          "  If it is already open, that project may not have the package: run\n" +
          "  mosaic-connector add <project path>, then reopen it in Unity.\n"
      );
      updateState({ state: "waiting_for_editor" });
      // Keep looking, so the state resolves itself when the Editor appears rather
      // than requiring the person to restart something.
      const poll = setInterval(async () => {
        try {
          const found = findDiscovery(args.discoveryFile);
          if (!(await bridgeAlive(found))) return; // recorded, but not answering yet
          clearInterval(poll);
          known = found;
          logger.write(`connector ready (Unity ${found.unity_version ?? "?"} on port ${found.port})\n`);
          updateState({ state: "connected", unityVersion: found.unity_version, port: found.port, detail: "connector ready" });
        } catch {
          /* still waiting */
        }
      }, 3000);
      ws.on("close", () => clearInterval(poll));
    }
  });

  ws.on("message", async (raw) => {
    let msg: any;
    try {
      msg = JSON.parse(raw.toString());
    } catch {
      return;
    }
    if (msg.type === "hello") {
      if (args.verbose) logger.write(`authenticated as ${msg.user}\n`);
      return;
    }
    if (!msg.id || !msg.route) return;
    if (args.verbose) logger.write(`-> ${msg.route}\n`);
    try {
      const d = findDiscovery(args.discoveryFile);
      const result = await callBridge(d, msg.route, msg.params, 120_000);
      ws.send(JSON.stringify({ id: msg.id, result }));
    } catch (err) {
      // "fetch failed" is what Node says when nothing is listening, and it names
      // neither the cause nor a remedy. The person reading it is an instructor.
      const raw = (err as Error).message || String(err);
      const message = /fetch failed|ECONNREFUSED|ENOTFOUND/i.test(raw)
        ? "The Unity Editor is not answering. Its Mosaic Bridge is not running: the " +
          "project may still be importing, or the Mosaic package may have failed to " +
          "compile. Check the Unity Console for errors."
        : raw;
      ws.send(JSON.stringify({ id: msg.id, error: message }));
    }
  });

  const retry = (why: string, code?: number) => {
    // 401 on the upgrade means the code is wrong; reconnecting every two seconds for
    // ever just hides that behind a scrolling log.
    if (/\b401\b/.test(why)) {
      logger.write(
        "The service rejected this access code. It may have been mistyped or replaced.\n" +
          "Run: mosaic-connector setup   with the correct code.\n"
      );
      updateState({ state: "exited", detail: "access code rejected (401)" });
      logger.close();
      process.exit(2);
    }
    // 4000 means the service accepted a newer connector for this user: another
    // process took the slot. Reconnecting would start a fight neither side wins,
    // so this one steps aside instead.
    if (code === 4000) {
      logger.write("another connector took over this token; exiting\n");
      updateState({ state: "exited", detail: "evicted by another connector (4000)" });
      logger.close();
      process.exit(0);
    }
    // C-1: `wasStable` is true only once this connection survived at least STABLE_MS —
    // "connector ready" printing is not enough on its own, since the 1006 loop reached
    // that every cycle. An un-stable connection keeps growing the backoff instead of
    // being handed a fresh 1s wait just because it briefly opened.
    const wasStable = Date.now() - openedAt >= STABLE_MS;
    const wait = computeBackoffMs(attempt);
    logger.write(`${why}; reconnecting in ${Math.round(wait / 1000)}s\n`);
    updateState({ state: "disconnected", detail: why });
    setTimeout(() => connect(args, nextAttempt(attempt, wasStable), logger, updateState), wait);
  };
  ws.on("close", (code) => retry(`connection closed (${code})`, code));
  ws.on("error", (err) => {
    if (ws.readyState !== WebSocket.OPEN) retry(`connection error: ${err.message}`);
  });
}

main(process.argv.slice(2));
