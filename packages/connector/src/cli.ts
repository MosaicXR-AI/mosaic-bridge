/** Command surface for the installed app.
 *
 *  The machine gets the connector once; Unity projects are added one at a time,
 *  because the bridge package lives in each project's Packages/manifest.json. That
 *  is the shape of Unity, so it is the shape of this tool: `setup` once, `add`
 *  per project, `run` whenever you are working.
 */
import fs from "node:fs";
import path from "node:path";
import os from "node:os";
import readline from "node:readline/promises";
import { randomUUID } from "node:crypto";
import { execFileSync } from "node:child_process";
import { waitForEditor } from "./discovery.js";

export interface AppConfig {
  url: string;
  token: string;
  projects: string[];
  /** Generated once, at setup, and sent with every connection. It is how the service
   *  knows which machines a code is used from, and refuses one too many. */
  machineId?: string;
}

export interface MachineIdentity {
  id: string;
  host: string;
  osUser: string;
  platform: string;
  connector: string;
}

/** This machine, as the service will know it. The id is created the first time it is
 *  needed and kept in connector.json, so it survives Unity reinstalls and connector
 *  re-downloads but not a fresh setup on a different computer. */
/** The machine id lives in its own file, not in connector.json.
 *
 *  It was kept in the config, and written only when a config was being saved. A
 *  connector started with --url and --token and no saved config — which is how it runs
 *  under a service account or an automation tool — therefore minted a new id on every
 *  launch, and one laptop counted as three machines against a limit of two. The id is a
 *  fact about the machine, so it is stored like one. */
export function machineIdPath(): string {
  return path.join(configDir(), "machine-id");
}

export function machineIdentity(cfg?: AppConfig | null): MachineIdentity {
  let id = cfg?.machineId;
  if (!id) {
    try {
      id = fs.readFileSync(machineIdPath(), "utf-8").trim() || undefined;
    } catch {
      /* first run on this machine */
    }
  }
  if (!id) {
    id = randomUUID().replace(/-/g, "");
    try {
      fs.mkdirSync(configDir(), { recursive: true });
      fs.writeFileSync(machineIdPath(), id + "\n", { mode: 0o600 });
    } catch {
      /* unsaved: a new id next time, which counts as a new machine — better than silence */
    }
  }
  if (cfg && cfg.machineId !== id) cfg.machineId = id;
  let osUser = "";
  try {
    osUser = os.userInfo().username;
  } catch {
    osUser = process.env.USER || process.env.USERNAME || "";
  }
  return { id, host: os.hostname(), osUser, platform: process.platform, connector: CONNECTOR_VERSION };
}

/** Where Pro looks for its licence: beside this connector's config. */
export function entitlementPath(): string {
  return path.join(configDir(), "entitlement.json");
}

/** Fetch a fresh entitlement for this code and machine, and write it where Pro reads it.
 *
 *  Pro's licence is a statement signed by the service — who, which features, which Unity
 *  ID, which machine, until when — not a counter in the Editor. It is refreshed on every
 *  setup, add and connection, so while the code is valid it never runs out, and once the
 *  code is revoked it stops within a week. Failure here is reported, never fatal: the
 *  Editor still connects; only Pro's tools will say why they are refused. */
export async function refreshEntitlement(url: string, token: string, machine: MachineIdentity): Promise<{ ok: boolean; message: string }> {
  const base = url.replace(/^ws/, "http").replace(/\/tunnel\/?$/, "");
  let res: Response;
  try {
    res = await fetch(`${base}/entitlement`, { headers: { Authorization: `Bearer ${token}`, ...machineHeaders(machine) } });
  } catch (e) {
    return { ok: false, message: `could not reach the service for a Pro licence (${(e as Error).message}); the last one saved still applies until it expires` };
  }
  if (res.status === 401 && res.headers.get("x-mosaic-reason") === "expired") return { ok: false, message: "this access code has expired, so no fresh Pro licence was issued; the last one saved applies until its own date. Ask whoever issued the code to extend it — nothing to do here" };
  if (res.status === 401) return { ok: false, message: "the service did not accept this access code, so no Pro licence was issued" };
  if (res.status === 404) return { ok: false, message: "this service does not issue Pro licences (older version); Pro tools stay as they were" };
  if (!res.ok) return { ok: false, message: `the service answered ${res.status} when asked for a Pro licence` };
  const body = await res.text();
  let expires = "";
  try {
    const j = JSON.parse(body) as { payload: string };
    expires = String((JSON.parse(Buffer.from(j.payload, "base64").toString("utf-8")) as { expiresAt?: string }).expiresAt || "");
  } catch {
    return { ok: false, message: "the service sent a Pro licence this connector could not read" };
  }
  try {
    fs.mkdirSync(configDir(), { recursive: true });
    fs.writeFileSync(entitlementPath(), body + "\n", { mode: 0o600 });
  } catch (e) {
    return { ok: false, message: `could not save the Pro licence: ${(e as Error).message}` };
  }
  return { ok: true, message: `Pro licence saved${expires ? `, valid until ${expires.slice(0, 10)}` : ""} (refreshed on every connection)` };
}

export function machineHeaders(m: MachineIdentity): Record<string, string> {
  return {
    "X-Mosaic-Machine": m.id,
    "X-Mosaic-Host": m.host,
    "X-Mosaic-User": m.osUser,
    "X-Mosaic-Platform": m.platform,
    "X-Mosaic-Connector": m.connector,
  };
}

/** Printed by `version` and at the top of `help`. An acceptance round spent a page
 *  reporting connector behaviour as unfixed because the machine was running a build from
 *  before the fix, and nothing on it could say which build that was. */
export const CONNECTOR_VERSION = "0.11.1";

const BRIDGE_PKG = "com.mosaic.bridge";
/** Where the Bridge comes from when the service cannot be asked. Every install failure in
 *  eight acceptance rounds on Windows was on this one package, because a git clone writes
 *  2,709 files into a temp folder and then renames it — and a real-time scanner still
 *  holding a handle inside makes that rename fail with EPERM. The registry never failed
 *  once. So the service now publishes the Bridge too, and this URL is the fallback only. */
const BRIDGE_SRC = "https://github.com/MosaicXR-AI/mosaic-bridge.git?path=/packages/com.mosaic.bridge";

export function configDir(): string {
  const home = os.homedir();
  if (process.platform === "win32") return path.join(process.env.APPDATA || path.join(home, "AppData", "Roaming"), "Mosaic");
  if (process.platform === "darwin") return path.join(home, "Library", "Application Support", "Mosaic");
  return path.join(process.env.XDG_CONFIG_HOME || path.join(home, ".config"), "mosaic");
}

export function configPath(): string {
  return path.join(configDir(), "connector.json");
}

export function readConfig(): AppConfig | null {
  try {
    return JSON.parse(fs.readFileSync(configPath(), "utf-8")) as AppConfig;
  } catch {
    return null;
  }
}

export function writeConfig(cfg: AppConfig): void {
  fs.mkdirSync(configDir(), { recursive: true });
  fs.writeFileSync(configPath(), JSON.stringify(cfg, null, 2), { mode: 0o600 });
}

export interface ServicePackages {
  registry: string;
  scopes: string[];
  packages: { name: string; version: string }[];
}

/** Ask the service which packages this access code includes.
 *
 *  Pro is not on public GitHub and never will be: the service serves it, gated by the
 *  same access code as everything else. Before this existed there was no route by
 *  which a customer could install Pro at all. */
/** Thrown when the service answers 401: the code is wrong, and nothing built on it can
 *  work. Setup used to treat this exactly like "service unreachable" — it printed "none
 *  available (Bridge only)", saved the bad code, and exited 0. The customer found out
 *  inside Unity, from a registry error that named neither Mosaic nor the code. */
export class AccessCodeRejected extends Error {
  constructor() {
    super("The service did not accept that access code.");
  }
}

/** Thrown when the service answers 403: the code is real but already on its limit of
 *  machines. The message is the service's own, and names the machines. */
export class MachineLimitReached extends Error {}

export async function servicePackages(url: string, token: string, machine?: MachineIdentity): Promise<ServicePackages | null> {
  const base = url.replace(/^ws/, "http").replace(/\/tunnel\/?$/, "");
  let res: Response;
  try {
    res = await fetch(`${base}/registry`, {
      headers: { Authorization: `Bearer ${token}`, ...(machine ? machineHeaders(machine) : {}) },
    });
  } catch {
    return null; // unreachable: a different problem, reported differently
  }
  if (res.status === 401) throw new AccessCodeRejected();
  if (res.status === 403) {
    let message = "This access code is already in use on its maximum number of machines.";
    try {
      message = ((await res.json()) as { message?: string }).message || message;
    } catch {
      /* keep the default */
    }
    throw new MachineLimitReached(message);
  }
  if (!res.ok) return null;
  return (await res.json()) as ServicePackages;
}

/** Unity reads registry access tokens from ~/.upmconfig.toml, not from the project,
 *  so this is written once per machine rather than into anything a team shares. */
export function writeUpmConfig(registry: string, token: string): string {
  const file = path.join(os.homedir(), ".upmconfig.toml");
  const origin = registry.replace(/\/$/, "");
  const block = `[npmAuth."${origin}"]\ntoken = "${token}"\nalwaysAuth = true\n`;
  let existing = "";
  try {
    existing = fs.readFileSync(file, "utf-8");
  } catch {
    /* first time */
  }
  // Replace our own block if it is already there; leave every other registry alone.
  const escaped = origin.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const re = new RegExp(`\\[npmAuth\\."${escaped}"\\][^[]*`, "g");
  const next = (existing.replace(re, "").trimEnd() + "\n\n" + block).trimStart();
  fs.writeFileSync(file, next, { mode: 0o600 });
  return file;
}

/** Adds the Mosaic packages to a Unity project, leaving every other dependency and the
 *  file's formatting alone. Idempotent: running it twice changes nothing.
 *
 *  Bridge comes from its public git URL; Pro comes from the service's registry, added
 *  as a scoped registry so Unity fetches it as the machine's licensed user. */
export function addProject(projectPath: string, svc?: ServicePackages | null): { added: boolean; message: string } {
  const manifest = path.join(projectPath, "Packages", "manifest.json");
  if (!fs.existsSync(manifest)) {
    return { added: false, message: `not a Unity project (no Packages/manifest.json): ${projectPath}` };
  }
  const raw = fs.readFileSync(manifest, "utf-8");
  const m = JSON.parse(raw.replace(/^﻿/, ""));
  m.dependencies = m.dependencies || {};
  const before = JSON.stringify(m);

  // The service lists the Bridge itself now; git is only for when it cannot be asked.
  const bridgeFromService = Boolean(svc && svc.packages.some((p) => p.name === BRIDGE_PKG));
  if (!m.dependencies[BRIDGE_PKG] && !bridgeFromService) m.dependencies[BRIDGE_PKG] = BRIDGE_SRC;

  const extra: string[] = [];
  if (svc && svc.packages.length) {
    m.scopedRegistries = Array.isArray(m.scopedRegistries) ? m.scopedRegistries : [];
    const entry = { name: "Mosaic", url: svc.registry, scopes: svc.scopes };
    const idx = m.scopedRegistries.findIndex((r: any) => r && r.name === "Mosaic");
    if (idx >= 0) m.scopedRegistries[idx] = entry;
    else m.scopedRegistries.push(entry);
    for (const p of svc.packages) {
      const pinned = m.dependencies[p.name];
      if (!pinned) {
        m.dependencies[p.name] = p.version;
        extra.push(p.name.replace(/^com\.mosaic\./, ""));
      } else if (pinned !== p.version) {
        // An existing pin was left untouched, so there was no way to upgrade a project
        // short of hand-editing manifest.json — and a project pinned to a version the
        // service had moved past kept running only on Unity's package cache. Running
        // `add` again is the obvious thing to try, so it is what performs the upgrade.
        m.dependencies[p.name] = p.version;
        // A git URL as the old pin is the Bridge moving off git; say "git" rather than
        // printing the whole URL into a one-line summary.
        const was = String(pinned).startsWith("http") ? "git" : String(pinned);
        extra.push(`${p.name.replace(/^com\.mosaic\./, "")} ${was} -> ${p.version}`);
      }
    }
  }

  if (JSON.stringify(m) === before) {
    return { added: false, message: `already set up: ${path.basename(projectPath)}` };
  }
  fs.writeFileSync(manifest, JSON.stringify(m, null, 2) + "\n");
  // "added Bridge + bridge git -> 1.0.0-beta.13" named the Bridge twice once it came
  // from the service; the unconditional prefix was from when git was its only source.
  const what = (bridgeFromService ? extra : ["Bridge (git)", ...extra]).join(" + ");
  return {
    added: true,
    message:
      `added ${what} to ${path.basename(projectPath)}` +
      (svc ? "" : " (Pro packages unavailable: the service did not answer)"),
  };
}

/** Unity Hub keeps the list of known projects; offering them beats asking a person to
 *  type a path. Hub's format has changed across versions, so read defensively. */
export function knownUnityProjects(): string[] {
  const home = os.homedir();
  const candidates = [
    path.join(home, "Library", "Application Support", "UnityHub", "projects-v1.json"),
    path.join(process.env.APPDATA || "", "UnityHub", "projects-v1.json"),
    path.join(home, ".config", "UnityHub", "projects-v1.json"),
  ].filter(Boolean);
  for (const file of candidates) {
    try {
      const data = JSON.parse(fs.readFileSync(file, "utf-8"));
      const entries = data.data ?? data.projects ?? data;
      const paths: string[] = [];
      for (const key of Object.keys(entries)) {
        const e = entries[key];
        const p = typeof e === "string" ? e : e?.path || e?.projectPath || key;
        if (typeof p === "string" && fs.existsSync(path.join(p, "Packages", "manifest.json"))) paths.push(p);
      }
      if (paths.length) return [...new Set(paths)];
    } catch {
      /* try the next location */
    }
  }
  return [];
}

/** First run: ask for the two things only the customer knows, verify them against the
 *  service, and offer the Unity projects we can already see. */
export async function setup(preset: Partial<AppConfig> = {}, project?: string): Promise<AppConfig> {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  const existing = readConfig();
  // Whether there is anyone to answer. Setup crashed with a Node stack trace —
  // ERR_USE_AFTER_CLOSE, readline was closed — the moment it asked a question with no
  // terminal attached, which is exactly how it runs under an automation tool or from a
  // script. A prompt that cannot be answered is not an error; it is a question that
  // should not be asked.
  const interactive = Boolean(process.stdin.isTTY);
  const ask = async (q: string): Promise<string> => {
    if (!interactive) return "";
    return await rl.question(q);
  };
  try {
    const url =
      preset.url ||
      (await ask(`Service address${existing?.url ? ` [${existing.url}]` : ""}: `)) ||
      existing?.url ||
      "";
    const token =
      preset.token ||
      (await ask(`Access code${existing?.token ? " [keep existing]" : ""}: `)) ||
      existing?.token ||
      "";
    if (!url || !token) throw new Error("both the service address and the access code are required");

    const health = url.replace(/^ws/, "http").replace(/\/tunnel\/?$/, "") + "/health";
    process.stdout.write("checking the service... ");
    try {
      const res = await fetch(health);
      process.stdout.write(res.ok ? "reachable\n" : `answered ${res.status}\n`);
    } catch (e) {
      process.stdout.write(`could not reach it (${(e as Error).message})\n`);
    }

    const cfg: AppConfig = { url, token, projects: existing?.projects ?? [], machineId: existing?.machineId };
    {
      const ent = await refreshEntitlement(url, token, machineIdentity(cfg));
      process.stdout.write((ent.ok ? "" : "NOTE: ") + ent.message + "\n");
    }

    // What this access code includes, and what Unity needs in order to fetch it.
    // Done once here rather than per project.
    process.stdout.write("checking which packages your code includes... ");
    let svc: ServicePackages | null;
    // Identify this machine before saving anything: an id created here and saved with
    // the config is the one every later connection presents.
    const machine = machineIdentity(cfg);
    try {
      svc = await servicePackages(url, token, machine);
    } catch (e) {
      if (e instanceof MachineLimitReached) {
        process.stdout.write("refused\n\n" + e.message + "\nNothing was saved.\n");
        process.exitCode = 2;
        return { url, token: "", projects: existing?.projects ?? [] };
      }
      if (e instanceof AccessCodeRejected) {
        process.stdout.write("refused\n\n");
        process.stdout.write(
          "The service did not accept that access code. Nothing was saved.\n" +
            "Check the code you were given (it is case-sensitive, with no spaces) and run\n" +
            "setup again. If it keeps being refused, ask whoever issued it.\n"
        );
        process.exitCode = 2;
        return { url, token: "", projects: existing?.projects ?? [] };
      }
      throw e;
    }
    if (svc && svc.packages.length) {
      process.stdout.write(svc.packages.map((p) => p.name.replace(/^com\.mosaic\./, "")).join(", ") + "\n");
      const written = writeUpmConfig(svc.registry, token);
      process.stdout.write(`Unity registry access written to ${written}\n`);
    } else {
      process.stdout.write("none available (Bridge only)\n");
    }

    // Unity Hub only knows projects that have been opened through it, so a project
    // cloned from git or copied from another machine is invisible here. Typing a path
    // has to be a first-class option, not a documented workaround.
    const found = knownUnityProjects().filter((p) => !cfg.projects.includes(p));
    // Only when a question is actually being asked. Told to "type the full path to your
    // Unity project" by a run that had already been given one, and with no prompt to
    // type it at, a reader reasonably concludes the run went wrong.
    const asking = !project && interactive;
    if (asking) {
      if (found.length) {
        process.stdout.write("\nUnity projects Unity Hub knows about:\n");
        found.forEach((p, i) => process.stdout.write(`  ${i + 1}. ${p}\n`));
      } else {
        process.stdout.write("\nUnity Hub has no projects registered on this machine.\n");
      }
      process.stdout.write(
        found.length
          ? `\nEnter numbers to add (for example 1, or 1,2, or 1-${found.length}), or type a full path\n` +
            "to a project Hub does not list.\n"
          : "\nType the full path to your Unity project.\n"
      );
    }
    const answer = (project || (await ask("Add which? (numbers, a path, or Enter to skip): "))).trim();
    if (answer) {
      // "numbers" invites 1,2 as readily as 1 2, and a comma used to be reinterpreted
      // as a file path: the person was told their Unity project was not a Unity
      // project, and setup then reported success having added nothing.
      const looksLikePath = answer.startsWith("/") || answer.startsWith("~") || /^[A-Za-z]:[\\/]/.test(answer);
      const numeric = /^[\d\s,\-]+$/.test(answer);
      let tokens: string[];
      if (looksLikePath) {
        tokens = [answer];
      } else if (numeric) {
        tokens = [];
        for (const part of answer.split(/[\s,]+/).filter(Boolean)) {
          const range = /^(\d+)-(\d+)$/.exec(part);
          if (range) {
            const [a, b] = [Number(range[1]), Number(range[2])];
            for (let i = Math.min(a, b); i <= Math.max(a, b); i++) tokens.push(String(i));
          } else {
            tokens.push(part);
          }
        }
      } else {
        tokens = [answer];
      }
      for (const t of tokens) {
        const target = /^\d+$/.test(t) ? found[Number(t) - 1] : t.replace(/^["']|["']$/g, "");
        if (!target) {
          process.stdout.write(`  ${t}: there is no project ${t} in the list above\n`);
          continue;
        }
        const r = addProject(target, svc);
        process.stdout.write(`  ${r.message}\n`);
        if (fs.existsSync(path.join(target, "Packages", "manifest.json")) && !cfg.projects.includes(target)) {
          cfg.projects.push(target);
        }
      }
    }

    writeConfig(cfg);
    process.stdout.write(`\nSaved to ${configPath()}\n`);

    // Do the client configuration rather than printing a command to copy.
    const claude = configureClaudeCode(url, token);
    process.stdout.write((claude.ok ? "" : "\n") + claude.message + "\n");

    if (!cfg.projects.length) {
      // Saying "saved" after adding nothing is how a person ends up with a configured
      // machine and no configured project, and no way to tell.
      process.stdout.write(
        "\nNo Unity project was set up. Nothing in the Editor will work until one is.\n" +
          "Run: mosaic-connector add <path to your Unity project>\n"
      );
    }

    if (cfg.projects.length && !interactive) {
      // With nobody at the keyboard there is nobody to open Unity either, so waiting six
      // minutes for an Editor that cannot appear would just look like a hang.
      process.stdout.write(
        "\nOpen the project in Unity once so the packages import, then run: mosaic-connector run\n"
      );
    } else if (cfg.projects.length) {
      // Then prove it. Ending on three unverified instructions leaves a person with
      // no way to tell success from silence; waiting costs a minute and answers it.
      const answer = (await ask("\nOpen the project in Unity now and I will wait for it? [Y/n] ")).trim().toLowerCase();
      if (answer !== "n" && answer !== "no") {
        process.stdout.write("Waiting for the Unity Editor. Open the project; the packages import on first open.\n");
        let lastReport = 0;
        const editor = await waitForEditor(6 * 60_000, (secs) => {
          if (secs - lastReport >= 30) {
            lastReport = secs;
            process.stdout.write(`  still waiting (${secs}s). Unity is probably still importing.\n`);
          }
        });
        if (editor) {
          process.stdout.write(`\nconnected - Unity ${editor.unity_version ?? "?"} on port ${editor.port}\n`);
          process.stdout.write("Everything is set up. Run: mosaic-connector run\n");
        } else {
          process.stdout.write(
            "\nThe Editor did not appear within six minutes. That usually means the project\n" +
              "is still importing, or it does not have the Mosaic packages yet. When it is\n" +
              "open, run: mosaic-connector run\n"
          );
        }
      } else {
        process.stdout.write("Open the project in Unity once so the packages import, then run: mosaic-connector run\n");
      }
    }
    return cfg;
  } finally {
    rl.close();
  }
}

/** Configure Claude Code for the person, instead of handing them a command.
 *
 *  Running `claude mcp add` by hand is the single most common way a first run goes
 *  wrong: without --scope user the server exists only in whichever folder the command
 *  happened to run in, and the tools vanish the moment they open their project. The
 *  connector already holds the address and the code, so there is nothing to type. */
export function configureClaudeCode(url: string, token: string): { ok: boolean; message: string } {
  const base = url.replace(/^ws/, "http").replace(/\/tunnel\/?$/, "");
  try {
    execFileSync("claude", [
      "mcp", "add",
      "--scope", "user",
      "--transport", "http",
      "mosaic", `${base}/mcp`,
      "--header", `Authorization: Bearer ${token}`,
    ], { stdio: "pipe" });
    return { ok: true, message: "Claude Code configured for this machine (user scope)." };
  } catch (err: any) {
    const out = String(err?.stderr || err?.stdout || err?.message || "");
    if (/already exists/i.test(out)) {
      return { ok: true, message: "Claude Code already had a Mosaic server configured." };
    }
    if (/ENOENT|not found/i.test(out)) {
      return {
        ok: false,
        message:
          "Claude Code is not installed on this machine. Install it from claude.com/claude-code, " +
          "then run: mosaic-connector setup again, or add the server yourself from the install page.",
      };
    }
    return { ok: false, message: `Could not configure Claude Code automatically: ${out.split("\n")[0]}` };
  }
}

export function statusReport(): string {
  const m = machineIdentity(readConfig());
  const cfg = readConfig();
  if (!cfg) return `not configured yet. Run: mosaic-connector setup`;
  const lines = [
    `service:  ${cfg.url}`,
    `code:     ${cfg.token.slice(0, 6)}… (stored in ${configPath()})`,
    `machine:  ${m.host} (${m.id.slice(0, 12)}…) — quote this if a machine needs releasing from your code`,
    `projects: ${cfg.projects.length ? "" : "none added yet"}`,
  ];
  for (const p of cfg.projects) {
    const has = fs.existsSync(path.join(p, "Packages", "manifest.json"))
      ? fs.readFileSync(path.join(p, "Packages", "manifest.json"), "utf-8").includes(BRIDGE_PKG)
      : false;
    lines.push(`  ${has ? "ok " : "!! "} ${p}${has ? "" : "  (bridge package missing)"}`);
  }
  return lines.join("\n");
}

export function usage(): string {
  return [
    "mosaic-connector — connects this machine's Unity Editor to Mosaic",
    "",
    `mosaic-connector ${CONNECTOR_VERSION}`,
    "",
    "  setup                 first run: service address, access code, pick projects",
    "    --url U --token T --project P   answer it all up front, no prompts",
    "  add <project path>    add the Mosaic Bridge package to one more Unity project",
    "  status                what is configured on this machine",
    "  run                   connect and stay connected (leave the window open)",
    "",
    "  version               print the connector build",
    "With no command, `run` is assumed when configured, and `setup` when not.",
  ].join("\n");
}
