# Shader Guide Workflow

Step-by-step workflow for building a ShaderGraph using Mosaic Bridge tools.
Encodes the 6 hard-won serialization rules from Ahmed's Water and Voronoi shader sessions.

## Pre-conditions
- project/preflight has been run (pipeline known)
- URP pipeline confirmed (most ShaderGraph features require URP)

## Steps

### Step 1 — Create the graph
```json
{
  "tool": "shadergraph/create",
  "params": {
    "Name": "MyShader",
    "Path": "Assets/Shaders/MyShader.shadergraph",
    "ShaderType": "Lit"
  }
}
```
Returns: Path, Guid

### Step 2 — Plan nodes
Before adding nodes, sketch the graph on paper or in text:
```
UV → Voronoi → Lerp → BaseColor
Time → Lerp(T)
Color(dark) → Lerp(A)
Color(light) → Lerp(B)
```

### Step 3 — Add nodes (one per call)
```json
{
  "tool": "shadergraph/add-node",
  "params": {
    "GraphPath": "Assets/Shaders/MyShader.shadergraph",
    "NodeType": "voronoi",
    "Position": [100, 200]
  }
}
```
**Store the returned NodeId for wiring.**

Supported aliases: add, subtract, multiply, divide, lerp, clamp, saturate, smoothstep,
split, combine, swizzle, fresnel, float, vector2, vector3, vector4, color, uv, time,
position, normal, viewdir, sampletexture2d, samplecubemap,
voronoi, simplenoise, gradientnoise, customfunction, normalblend

### Step 4 — Wire nodes (one connection per call)
```json
{
  "tool": "shadergraph/connect",
  "params": {
    "GraphPath": "Assets/Shaders/MyShader.shadergraph",
    "FromNodeId": "{voronoi-node-id}",
    "FromSlotId": 3,
    "ToNodeId": "{lerp-node-id}",
    "ToSlotId": 2
  }
}
```

## Serialization Rules (memorize these — they cost 90+ minutes to learn)

| Rule | What | Why |
|------|------|-----|
| UUID GUIDs | m_ObjectId must be `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` | Unity 14.x+ validates format |
| VoronoiNode SGVersion | m_SGVersion must be 1, not 0 | Different serialization format |
| Voronoi ExtraFields | m_HashType=0 must be present | Required for correct output |
| Texture StageCapability | Texture/Sampler slots must be `m_StageCapability: 2` | Fragment-only |
| TimeNode slots | All 5 output slots (0-4) must be declared | Missing slots cause errors |
| CustomFunction | m_SourceType=1 for inline HLSL body | SGVersion=1 required |

**All rules are now enforced automatically by the tools — no manual workarounds needed.**

## Common Mistakes

1. **Don't reuse NodeIds** — each add-node call returns a fresh UUID
2. **Check slot IDs before connecting** — they're in the shadergraph/add-node result
3. **ShaderType=Lit** gives you metallic/smoothness/emission; **Unlit** is simpler
4. **Don't use editor/execute-code for multi-line** — use script/create instead
