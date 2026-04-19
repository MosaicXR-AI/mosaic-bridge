import fs from 'node:fs';
import path from 'node:path';
import { atomicWriteJson, readJson } from './utils.js';

/**
 * Checks that a path is a Unity project: has Assets/ and ProjectSettings/,
 * and (optionally) reads the Unity version from ProjectVersion.txt.
 */
export function validateUnityProject(projectPath) {
  const abs = path.resolve(projectPath);

  if (!fs.existsSync(abs)) {
    return { valid: false, reason: `Directory does not exist: ${abs}` };
  }
  if (!fs.statSync(abs).isDirectory()) {
    return { valid: false, reason: `Not a directory: ${abs}` };
  }

  const assetsDir = path.join(abs, 'Assets');
  const settingsDir = path.join(abs, 'ProjectSettings');
  const packagesDir = path.join(abs, 'Packages');

  if (!fs.existsSync(assetsDir)) {
    return {
      valid: false,
      reason: `Not a Unity project — missing Assets/ directory at: ${abs}. Open Unity Hub, create a new project, then rerun this installer with --project-path pointing at the new project.`,
    };
  }
  if (!fs.existsSync(settingsDir)) {
    return {
      valid: false,
      reason: `Not a Unity project — missing ProjectSettings/ directory at: ${abs}.`,
    };
  }
  if (!fs.existsSync(packagesDir)) {
    // Unity creates this on first open; we can create it too if needed.
    fs.mkdirSync(packagesDir, { recursive: true });
  }

  const unityVersion = readUnityVersion(settingsDir);
  const projectName = path.basename(abs);

  return {
    valid: true,
    projectPath: abs,
    projectName,
    unityVersion,
  };
}

function readUnityVersion(settingsDir) {
  try {
    const file = path.join(settingsDir, 'ProjectVersion.txt');
    if (!fs.existsSync(file)) return null;
    const text = fs.readFileSync(file, 'utf8');
    const match = text.match(/^m_EditorVersion:\s*(\S+)/m);
    return match ? match[1] : null;
  } catch {
    return null;
  }
}

/**
 * Adds (or updates) a package entry in the project's Packages/manifest.json.
 * Returns { added: boolean, manifestPath }. `added: false` means the entry was
 * already present (we left it alone).
 *
 * If `enableTestables` is true, ensures the packageName is also in the
 * `testables` array (development-only; users usually don't need this).
 */
export function injectBridgePackage(projectPath, opts) {
  const { packageName, gitUrl, enableTestables = false } = opts;
  const manifestPath = path.join(projectPath, 'Packages', 'manifest.json');

  let manifest = {};
  if (fs.existsSync(manifestPath)) {
    manifest = readJson(manifestPath) || {};
  }

  if (!manifest.dependencies || typeof manifest.dependencies !== 'object') {
    manifest.dependencies = {};
  }

  const alreadyPresent = Object.prototype.hasOwnProperty.call(
    manifest.dependencies,
    packageName
  );

  if (!alreadyPresent) {
    manifest.dependencies[packageName] = gitUrl;
  }

  if (enableTestables) {
    const testables = Array.isArray(manifest.testables) ? manifest.testables : [];
    if (!testables.includes(packageName)) {
      testables.push(packageName);
    }
    manifest.testables = testables;
  }

  atomicWriteJson(manifestPath, manifest);

  return {
    added: !alreadyPresent,
    manifestPath,
    resolvedUrl: manifest.dependencies[packageName],
  };
}
