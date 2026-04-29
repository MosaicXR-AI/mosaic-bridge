# Preflight Workflow

Run at the start of every Mosaic Bridge session to avoid render-pipeline and missing-package failures.

## Steps

### Step 1 — Call project/preflight
```json
{ "tool": "project/preflight" }
```

### Step 2 — Parse and report
From the result, extract and report:
- **RenderPipeline** — "URP", "HDRP", or "BuiltIn"
- **ColorProperty** — `_BaseColor` (URP/HDRP) or `_Color` (BuiltIn)
- **ActiveSceneName** — what scene we're working in
- **ConsoleErrorCount** — if > 0, list RecentErrors before proceeding
- **InstalledPackages** — check for critical packages

### Step 3 — Check for critical packages

| Goal | Required Package |
|------|-----------------|
| URP shaders | com.unity.render-pipelines.universal |
| ShaderGraph | com.unity.shadergraph |
| ProBuilder | com.unity.probuilder |
| Cinemachine | com.unity.cinemachine |
| TextMeshPro | com.unity.textmeshpro |

If a required package is missing, warn the user before attempting to use related tools.

### Step 4 — Set session variables
Store these for the session:
- `{pipeline}` = RenderPipeline value
- `{colorProp}` = ColorProperty value
- `{sceneName}` = ActiveSceneName

### Step 5 — Report to user
```
✓ Preflight complete
  Pipeline:      URP
  Color prop:    _BaseColor
  Active scene:  MyScene
  Console errors: 0
```

## Decision Tree

```
RenderPipeline = URP?
  → Use "Universal Render Pipeline/Lit" as default shader
  → Use _BaseColor for colors
  → UseUrpParticlesMaterial=true for particles

RenderPipeline = BuiltIn?
  → Use "Standard" as default shader
  → Use _Color for colors
  → Particle shaders: "Particles/Standard Unlit"

ConsoleErrorCount > 0?
  → Show RecentErrors
  → Ask user if they want to fix errors before proceeding
  → Some errors (compile errors) will prevent tools from working
```
