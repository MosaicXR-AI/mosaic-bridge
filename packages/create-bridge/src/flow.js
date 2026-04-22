import * as p from '@clack/prompts';
import pc from 'picocolors';
import path from 'node:path';
import fs from 'node:fs';
import { validateUnityProject, injectBridgePackage } from './unity.js';
import { getClientRegistry } from './clients/index.js';

const BRIDGE_PACKAGE_NAME = 'com.mosaic.bridge';
const BRIDGE_GIT_URL =
  'https://github.com/MosaicXR-AI/mosaic-bridge.git?path=/packages/com.mosaic.bridge';

const CLAUDE_MD_CONTENT = `# Unity Project — AI Assistant Instructions

This project uses **Mosaic Bridge MCP** to drive the Unity Editor via tool calls.

---

## Scene Building — Interview Protocol

When I ask you to "build a scene", "create an environment", "make a desert/forest/city", or describe any vague place/mood:

**STOP. Do not call any Mosaic tools. Run the Scene Interview first.**

Ask all four Tier 1 questions in a single message:

1. **Scene type?** — game level / playable · cinematic · archviz · prototype
2. **Geographic or thematic reference?** — be specific: "Wadi Rum Jordan", "Pacific Northwest forest", "dystopian 2080 Tokyo". Generic = generic output.
3. **Scale?** — < 100m · 100m–1km · 1–10km · 10km+
4. **Player perspective?** — first person · third person · drone / flight · top-down · no player (cinematic only)

If needed, ask follow-up questions:
- Visual style (realistic / stylized / lowpoly)
- Time of day + weather ("golden hour, light haze")
- Per-region visual characteristics (list the biomes)
- Focal points / landmarks
- Platform target (desktop / mobile / VR)
- Build phased (confirm each phase) or one-shot?

After interview, generate a **ScenePlan summary** and wait for my confirmation before executing any tools.

---

## Spatial Coherence Contract

**Every placed object Y must = terrain.SampleHeight(x, z) + small_offset.**

Never use Y=0 as a placement coordinate unless the scene is a flat indoor space.

Use \`terrain/sample-height\` before every \`gameobject/create\` or \`prefab/instantiate\` call.
Use \`scene/plan-composition\` to get pre-resolved Y coordinates for all landmarks at once.
Use \`gameobject/snap-to-ground\` to fix already-placed objects after terrain sculpting.

---

## Execution Pipeline Order

Always build in this order (skipping creates visual artifacts):

1. **Terrain** — create, sculpt major features, secondary detail
2. **Water** — if applicable; sets the shoreline Y reference
3. **Terrain textures** — layer setup + splatmap painting
4. **Sky + Lighting** — directional light, skybox, ambient
5. **Large structures** — buildings, rock formations (use terrain/sample-height for Y)
6. **Vegetation** — trees (terrain system) then grass then small details
7. **Post-processing** — fog, bloom, color grade (last pass)
8. **Camera / player controller** — calibrated to final scene scale

---

## Tool Usage Rules

- **Render pipeline:** Always call \`settings/get-render\` before material or shader work. Use \`currentRenderPipeline\` field.
- **Terrain trees:** Prefab root must have \`MeshRenderer\`, \`LODGroup\`, or \`BillboardRenderer\`. Nested-child visuals are ignored.
- **Material keywords:** Use \`keyword\` ValueType on \`material/set-property\` for \`_EMISSION\`, \`_NORMALMAP\`, \`_ALPHATEST_ON\`.
- **ShaderGraph nodes:** Use \`shadergraph/add-node\` + \`shadergraph/connect\` — do not fall back to raw HLSL .shader files.
- **Nested component properties:** \`component/set_reference\` now supports dot-notation paths (e.g. "Lens.FieldOfView") and m_ prefix fallback.
- **HDRI skybox:** Use \`texture/set-import-settings\` with \`TextureShape=Cube\` to convert equirectangular HDRI to cubemap.

---

## When in Doubt

Ask a clarifying question rather than guessing. A 2-minute interview prevents a 20-minute rebuild.
`;

/**
 * The main orchestrated flow — prompts the user, then runs steps.
 * Throws an error with { cancelled: true } if the user Ctrl-C'd.
 */
export async function runInteractive(opts) {
  showIntro();

  // 1. Resolve Unity project path (prompt if not provided).
  const projectPath = await resolveProjectPath(opts);

  // 2. Validate the Unity project.
  const projectInfo = await validateAndReport(projectPath);

  // 3. Decide which clients to configure.
  const clientKeys = await resolveClients(opts);

  // 4. Compute the MCP server name to use.
  const serverName =
    opts.serverNameOverride || deriveServerName(projectInfo.projectName);

  // 5. Confirmation step (unless --yes).
  if (!opts.nonInteractive) {
    await confirmPlan({
      projectPath: projectInfo.projectPath,
      projectName: projectInfo.projectName,
      unityVersion: projectInfo.unityVersion,
      clientKeys,
      skipUnity: opts.skipUnity,
      skipClients: opts.skipClients,
      serverName,
    });
  }

  // 6. Execute — Unity install + each client config.
  const results = [];

  if (!opts.skipUnity) {
    const unityStep = p.spinner();
    unityStep.start(`Adding ${BRIDGE_PACKAGE_NAME} to Unity manifest.json`);
    try {
      const unityResult = injectBridgePackage(projectInfo.projectPath, {
        packageName: BRIDGE_PACKAGE_NAME,
        gitUrl: BRIDGE_GIT_URL,
        enableTestables: false,
      });
      unityStep.stop(
        unityResult.added
          ? pc.green(`✓ Added ${BRIDGE_PACKAGE_NAME} to manifest.json`)
          : pc.dim(`↩ ${BRIDGE_PACKAGE_NAME} already present — skipped`)
      );
      results.push({ kind: 'unity', ok: true, detail: unityResult });
    } catch (err) {
      unityStep.stop(pc.red(`✗ Unity install failed: ${err.message}`));
      results.push({ kind: 'unity', ok: false, error: err });
    }

    // Write CLAUDE.md with scene-building instructions into the Unity project root
    // (skipped if --skip-claude or if file already exists and --force is not set)
    if (!opts.skipClaude) {
      const claudeMdPath = path.join(projectInfo.projectPath, 'CLAUDE.md');
      const alreadyExists = fs.existsSync(claudeMdPath);
      if (!alreadyExists || opts.force) {
        try {
          fs.writeFileSync(claudeMdPath, CLAUDE_MD_CONTENT, 'utf8');
          p.log.success(
            alreadyExists
              ? pc.green(`✓ Updated CLAUDE.md (scene-building instructions) — --force`)
              : pc.green(`✓ Wrote CLAUDE.md (scene-building instructions) to project root`)
          );
          results.push({ kind: 'claude-md', ok: true });
        } catch (err) {
          p.log.warn(pc.yellow(`⚠ Could not write CLAUDE.md: ${err.message}`));
          results.push({ kind: 'claude-md', ok: false, error: err });
        }
      } else {
        p.log.info(pc.dim('↩ CLAUDE.md already exists — skipped (use --force to overwrite)'));
        results.push({ kind: 'claude-md', ok: true, detail: { skipped: true } });
      }
    }
  } else {
    p.log.info(pc.dim('Skipped Unity package install (--skip-unity)'));
  }

  if (!opts.skipClients && clientKeys.length > 0) {
    const registry = getClientRegistry();
    for (const key of clientKeys) {
      const client = registry[key];
      const step = p.spinner();
      step.start(`Configuring ${client.label}`);
      try {
        const r = await client.configure({
          projectPath: projectInfo.projectPath,
          projectName: projectInfo.projectName,
          serverName,
          force: !!opts.force,
        });
        step.stop(
          pc.green(`✓ ${client.label}: ${r.summary}`)
        );
        results.push({ kind: 'client', key, ok: true, detail: r });
      } catch (err) {
        step.stop(pc.red(`✗ ${client.label}: ${err.message}`));
        results.push({ kind: 'client', key, ok: false, error: err });
      }
    }
  } else if (opts.skipClients) {
    p.log.info(pc.dim('Skipped MCP client configs (--skip-clients)'));
  }

  // 7. Outro with next steps.
  showOutro({ projectInfo, clientKeys, results, skipClients: !!opts.skipClients });
}

// ────────────────────────────────────────────────────────────────────────────
// Intro / outro
// ────────────────────────────────────────────────────────────────────────────

function showIntro() {
  console.log();
  p.intro(pc.bgCyan(pc.black(' Mosaic Bridge ')) + pc.cyan(' · Unity MCP setup'));
  p.log.message(
    pc.dim('This will install com.mosaic.bridge in your Unity project and')
  );
  p.log.message(
    pc.dim('configure the MCP clients you choose so they can drive Unity.')
  );
}

function showOutro({ projectInfo, clientKeys, results, skipClients }) {
  const unityOk = results.find((r) => r.kind === 'unity')?.ok !== false;
  const clientsOk =
    skipClients || results.filter((r) => r.kind === 'client').every((r) => r.ok);

  if (unityOk && clientsOk) {
    p.note(
      [
        `${pc.bold('Next steps:')}`,
        '',
        `  ${pc.dim('1.')} Open the Unity project in Unity Hub:`,
        `     ${pc.cyan(projectInfo.projectPath)}`,
        `  ${pc.dim('2.')} Wait for Unity to compile (first time may take a few minutes)`,
        `  ${pc.dim('3.')} ${
          skipClients
            ? 'Configure your MCP client manually (see --help)'
            : 'Restart your MCP client(s): ' +
              clientKeys.map((k) => pc.cyan(k)).join(', ')
        }`,
        `  ${pc.dim('4.')} Start prompting — ask the client to create GameObjects, scenes, etc.`,
        '',
        `  Docs: ${pc.cyan('https://github.com/MosaicXR-AI/mosaic-bridge')}`,
      ].join('\n'),
      pc.green('✓ Setup complete')
    );
  } else {
    const failed = results.filter((r) => !r.ok);
    p.note(
      [
        `${pc.bold('Completed with ' + failed.length + ' error(s).')}`,
        '',
        ...failed.map((f) => `  ${pc.red('✗')} ${f.kind}${f.key ? ':' + f.key : ''}: ${f.error?.message || 'unknown'}`),
        '',
        `  File issues at: ${pc.cyan('https://github.com/MosaicXR-AI/mosaic-bridge/issues')}`,
      ].join('\n'),
      pc.yellow('⚠ Setup partial')
    );
  }
  p.outro(pc.dim('Happy prompting.'));
}

// ────────────────────────────────────────────────────────────────────────────
// Prompt helpers
// ────────────────────────────────────────────────────────────────────────────

async function resolveProjectPath(opts) {
  if (opts.projectPath) {
    return path.resolve(opts.projectPath);
  }
  if (opts.nonInteractive) {
    throw new Error('--yes requires --project-path');
  }
  const cwd = process.cwd();
  const initialValue = looksLikeUnityProject(cwd) ? cwd : '';
  const value = await p.text({
    message: 'Absolute path to your Unity project root',
    placeholder: initialValue || '/path/to/your/UnityProject',
    initialValue,
    validate(v) {
      if (!v || v.trim().length === 0) return 'Path is required';
      const abs = path.resolve(v);
      if (!fs.existsSync(abs)) return `Directory not found: ${abs}`;
      if (!fs.statSync(abs).isDirectory()) return `Not a directory: ${abs}`;
    },
  });
  assertNotCancelled(value);
  return path.resolve(value);
}

async function validateAndReport(projectPath) {
  const info = validateUnityProject(projectPath);
  if (!info.valid) {
    throw new Error(info.reason);
  }
  const versionLabel = info.unityVersion || pc.dim('(unknown)');
  p.log.success(
    `Unity project detected: ${pc.cyan(info.projectName)} ${pc.dim('(' + versionLabel + ')')}`
  );
  return info;
}

async function resolveClients(opts) {
  if (opts.skipClients) return [];
  if (opts.clients && opts.clients.length > 0) return opts.clients;
  if (opts.nonInteractive) {
    // Sensible default for --yes: all five clients.
    return ['claude-code', 'claude-desktop', 'cursor', 'gemini', 'codex'];
  }
  const selection = await p.multiselect({
    message: 'Which MCP clients should I configure?',
    initialValues: ['claude-code'],
    options: [
      {
        value: 'claude-code',
        label: 'Claude Code',
        hint: 'project-local .mcp.json — auto-loads when you open this folder in Claude Code',
      },
      {
        value: 'claude-desktop',
        label: 'Claude Desktop',
        hint: 'user-global (~/Library/Application Support/Claude/claude_desktop_config.json on macOS)',
      },
      {
        value: 'cursor',
        label: 'Cursor',
        hint: 'user-global (~/.cursor/mcp.json)',
      },
      {
        value: 'gemini',
        label: 'Gemini CLI',
        hint: 'user-global (~/.gemini/settings.json) — hyphen-only server name required',
      },
      {
        value: 'codex',
        label: 'OpenAI Codex',
        hint: 'user-global (~/.codex/config.toml) — TOML format',
      },
    ],
    required: false,
  });
  assertNotCancelled(selection);
  return selection;
}

async function confirmPlan({
  projectPath,
  projectName,
  unityVersion,
  clientKeys,
  skipUnity,
  skipClients,
  serverName,
}) {
  const lines = [
    `  Project: ${pc.cyan(projectName)} ${pc.dim('(' + (unityVersion || 'unknown Unity version') + ')')}`,
    `  Path:    ${pc.dim(projectPath)}`,
    '',
    `  ${skipUnity ? pc.dim('⏭  Skip Unity install') : pc.green('✓') + ' Add ' + pc.cyan('com.mosaic.bridge') + ' to Packages/manifest.json'}`,
    `  ${
      skipClients
        ? pc.dim('⏭  Skip MCP client configs')
        : pc.green('✓') +
          ' Configure clients as ' +
          pc.cyan(serverName) +
          ': ' +
          (clientKeys.length === 0 ? pc.dim('none') : clientKeys.map((k) => pc.cyan(k)).join(', '))
    }`,
  ];
  p.note(lines.join('\n'), 'Plan');

  const ok = await p.confirm({
    message: 'Proceed?',
    initialValue: true,
  });
  assertNotCancelled(ok);
  if (ok === false) {
    throw Object.assign(new Error('Cancelled by user'), { cancelled: true });
  }
}

// ────────────────────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────────────────────

function looksLikeUnityProject(dir) {
  try {
    return (
      fs.existsSync(path.join(dir, 'Assets')) &&
      fs.existsSync(path.join(dir, 'ProjectSettings'))
    );
  } catch {
    return false;
  }
}

/**
 * Gemini CLI rejects server names that contain underscores (tool FQN parsing).
 * All four clients accept hyphens, so we normalize to hyphens everywhere.
 * Also drops any characters that aren't [a-z0-9-] and trims dashes.
 */
function deriveServerName(projectName) {
  const safe = (projectName || 'unity-project')
    .toLowerCase()
    .replace(/[^a-z0-9-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40);
  return `mosaic-bridge-${safe || 'unity'}`;
}

function assertNotCancelled(v) {
  if (p.isCancel(v)) {
    p.cancel('Cancelled.');
    throw Object.assign(new Error('Cancelled by user'), { cancelled: true });
  }
}
