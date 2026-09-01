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

export interface AppConfig {
  url: string;
  token: string;
  projects: string[];
}

const BRIDGE_PKG = "com.mosaic.bridge";
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

/** Adds the bridge package to a Unity project, leaving every other dependency and the
 *  file's formatting alone. Idempotent: running it twice changes nothing. */
export function addProject(projectPath: string): { added: boolean; message: string } {
  const manifest = path.join(projectPath, "Packages", "manifest.json");
  if (!fs.existsSync(manifest)) {
    return { added: false, message: `not a Unity project (no Packages/manifest.json): ${projectPath}` };
  }
  const raw = fs.readFileSync(manifest, "utf-8");
  const m = JSON.parse(raw.replace(/^﻿/, ""));
  m.dependencies = m.dependencies || {};
  if (m.dependencies[BRIDGE_PKG]) {
    return { added: false, message: `already added to ${path.basename(projectPath)}` };
  }
  m.dependencies[BRIDGE_PKG] = BRIDGE_SRC;
  fs.writeFileSync(manifest, JSON.stringify(m, null, 2) + "\n");
  return { added: true, message: `added the Mosaic Bridge package to ${path.basename(projectPath)}` };
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
export async function setup(preset: Partial<AppConfig> = {}): Promise<AppConfig> {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  const existing = readConfig();
  try {
    const url =
      preset.url ||
      (await rl.question(`Service address${existing?.url ? ` [${existing.url}]` : ""}: `)) ||
      existing?.url ||
      "";
    const token =
      preset.token ||
      (await rl.question(`Access code${existing?.token ? " [keep existing]" : ""}: `)) ||
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

    // Unity fetches the bridge package over git. Without git on PATH the failure
    // surfaces much later, as a Unity package-resolution error that mentions neither
    // git nor Mosaic, so it is worth saying here.
    try {
      const { execSync } = await import("node:child_process");
      execSync("git --version", { stdio: "ignore" });
    } catch {
      process.stdout.write(
        "\nNOTE: git was not found on PATH. Unity needs it to fetch the Mosaic Bridge\n" +
          "package, so install Git and re-open Unity if the package fails to import.\n"
      );
    }

    const cfg: AppConfig = { url, token, projects: existing?.projects ?? [] };

    // Unity Hub only knows projects that have been opened through it, so a project
    // cloned from git or copied from another machine is invisible here. Typing a path
    // has to be a first-class option, not a documented workaround.
    const found = knownUnityProjects().filter((p) => !cfg.projects.includes(p));
    if (found.length) {
      process.stdout.write("\nUnity projects Unity Hub knows about:\n");
      found.forEach((p, i) => process.stdout.write(`  ${i + 1}. ${p}\n`));
    } else {
      process.stdout.write("\nUnity Hub has no projects registered on this machine.\n");
    }
    process.stdout.write(
      found.length
        ? "\nEnter numbers to add, or type a full path to a project Hub does not list.\n"
        : "\nType the full path to your Unity project.\n"
    );
    const answer = (await rl.question("Add which? (numbers, a path, or Enter to skip): ")).trim();
    if (answer) {
      const tokens = answer.startsWith("/") || /^[A-Za-z]:/.test(answer) ? [answer] : answer.split(/\s+/);
      for (const t of tokens) {
        const target = /^\d+$/.test(t) ? found[Number(t) - 1] : t.replace(/^["']|["']$/g, "");
        if (!target) {
          process.stdout.write(`  ${t}: no such entry\n`);
          continue;
        }
        const r = addProject(target);
        process.stdout.write(`  ${r.message}\n`);
        if (fs.existsSync(path.join(target, "Packages", "manifest.json")) && !cfg.projects.includes(target)) {
          cfg.projects.push(target);
        }
      }
    }

    writeConfig(cfg);
    process.stdout.write(`\nSaved to ${configPath()}\n`);
    if (cfg.projects.length) {
      process.stdout.write("Open the project in Unity once so the package imports, then run: mosaic-connector run\n");
    }
    return cfg;
  } finally {
    rl.close();
  }
}

export function statusReport(): string {
  const cfg = readConfig();
  if (!cfg) return `not configured yet. Run: mosaic-connector setup`;
  const lines = [
    `service:  ${cfg.url}`,
    `code:     ${cfg.token.slice(0, 6)}… (stored in ${configPath()})`,
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
    "  setup                 first run: service address, access code, pick projects",
    "  add <project path>    add the Mosaic Bridge package to one more Unity project",
    "  status                what is configured on this machine",
    "  run                   connect and stay connected (leave the window open)",
    "",
    "With no command, `run` is assumed when configured, and `setup` when not.",
  ].join("\n");
}
