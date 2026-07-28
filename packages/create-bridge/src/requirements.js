/**
 * Single source of truth for the Unity/Node requirements documented in README.md.
 *
 * The installer used to read a project's Unity version purely to display it, so it
 * would happily "succeed" against an editor the bridge cannot compile on — the user
 * only found out when Unity threw a wall of CS0619 errors. These checks move that
 * discovery to setup time, where the message can actually say what to do.
 *
 * Support levels come from the verification matrix in
 * packages/com.mosaic.bridge/CHANGELOG.md.
 */

export const UNITY_MINIMUM = '6000.3';
export const NODE_MINIMUM_MAJOR = 18;

const STREAM_RANK = { a: 0, b: 1, f: 2, p: 3 };

/**
 * Parses a Unity version string such as `6000.5.5f1`, `6000.6.0b5`, or `2022.3.10f1`.
 * Returns null when the shape is unrecognised — callers treat that as "unknown"
 * rather than assuming the worst.
 */
export function parseUnityVersion(raw) {
  if (typeof raw !== 'string') return null;
  const m = raw.trim().match(/^(\d+)\.(\d+)\.(\d+)(?:([abfp])(\d+))?/);
  if (!m) return null;
  return {
    raw: raw.trim(),
    major: Number(m[1]),
    minor: Number(m[2]),
    patch: Number(m[3]),
    stream: m[4] || 'f',
    streamNumber: m[5] ? Number(m[5]) : 0,
  };
}

/** Orders two parsed versions, including prerelease streams (a < b < f < p). */
export function compareUnityVersions(a, b) {
  if (a.major !== b.major) return a.major - b.major;
  if (a.minor !== b.minor) return a.minor - b.minor;
  if (a.patch !== b.patch) return a.patch - b.patch;
  const ra = STREAM_RANK[a.stream] ?? 2;
  const rb = STREAM_RANK[b.stream] ?? 2;
  if (ra !== rb) return ra - rb;
  return a.streamNumber - b.streamNumber;
}

/**
 * Classifies a Unity version against what this package actually supports.
 * Returns { level: 'ok' | 'warn' | 'error' | 'unknown', message }.
 *
 * `error` means the bridge is known not to compile or not to work; the installer
 * refuses unless explicitly overridden.
 */
export function checkUnityVersion(raw) {
  const v = parseUnityVersion(raw);

  if (!v) {
    return {
      level: 'unknown',
      version: raw || null,
      message:
        `Could not determine the project's Unity version` +
        (raw ? ` (read "${raw}")` : '') +
        `. Mosaic Bridge requires Unity ${UNITY_MINIMUM} or newer.`,
    };
  }

  // Below 6000.3 there is no UnityEngine.EntityId and no Resources.EntityIdToObject,
  // both of which the bridge depends on unconditionally.
  if (compareUnityVersions(v, parseUnityVersion('6000.3.0f1')) < 0) {
    return {
      level: 'error',
      version: v.raw,
      message:
        `Unity ${v.raw} is not supported — Mosaic Bridge requires ${UNITY_MINIMUM} or newer. ` +
        `Earlier editors have no UnityEngine.EntityId, which the bridge relies on. ` +
        `Upgrade the project in Unity Hub, or pick a different project.`,
    };
  }

  // The EntityId bit layout changed during the 6.6 beta cycle: the high bits stopped
  // being session-constant, so a 32-bit InstanceId can no longer identify an object.
  if (v.major === 6000 && v.minor === 6 && STREAM_RANK[v.stream] >= STREAM_RANK.b) {
    return {
      level: 'error',
      version: v.raw,
      message:
        `Unity ${v.raw} is not supported yet. The EntityId bit layout changed during the ` +
        `6.6 beta cycle, so object lookups by InstanceId fail. Use Unity 6000.5 (or 6000.3) ` +
        `until the bridge widens its id format.`,
    };
  }

  if (v.major === 6000 && v.minor === 6) {
    return {
      level: 'warn',
      version: v.raw,
      message:
        `Unity ${v.raw} is a prerelease. Verified on 6000.6.0a2; later 6.6 betas are not ` +
        `supported. If you hit InstanceId lookup failures, drop back to Unity 6000.5.`,
    };
  }

  // 6.4 already deprecates GetInstanceID, and the bridge's guards switch at 6.5.
  if (v.major === 6000 && v.minor === 4) {
    return {
      level: 'warn',
      version: v.raw,
      message:
        `Unity ${v.raw} works, but will log deprecation warnings in the Console. ` +
        `Unity 6000.5 is the recommended version.`,
    };
  }

  if (v.major === 6000 && (v.minor === 3 || v.minor === 5)) {
    return { level: 'ok', version: v.raw, message: `Unity ${v.raw} is supported.` };
  }

  // Newer than anything verified (e.g. 6000.7+).
  return {
    level: 'warn',
    version: v.raw,
    message:
      `Unity ${v.raw} has not been verified with this release. ` +
      `Verified editors: 6000.3, 6000.5, 6000.6.0a2.`,
  };
}

/** Checks the running Node against the engines requirement. */
export function checkNodeVersion(raw = process.version) {
  const m = String(raw).match(/^v?(\d+)/);
  if (!m) {
    return { level: 'unknown', version: raw, message: `Could not parse Node version "${raw}".` };
  }
  const major = Number(m[1]);
  if (major < NODE_MINIMUM_MAJOR) {
    return {
      level: 'error',
      version: raw,
      message:
        `Node ${raw} is too old — the MCP server requires Node ${NODE_MINIMUM_MAJOR}+. ` +
        `Install a newer Node from https://nodejs.org and rerun.`,
    };
  }
  return { level: 'ok', version: raw, message: `Node ${raw} is supported.` };
}
