# Scene Plan Workflow

Generates a layered build plan for a Unity scene before executing any tool calls.
Prevents the "start building, then realize the pipeline is wrong" failure mode.

## Steps

### Step 1 — Gather requirements
Ask the user:
1. What is the scene name/theme? (e.g., "rainy night city", "forest with particles")
2. What gameplay elements are needed? (physics, audio, UI, particles, etc.)
3. Are there existing assets to reuse or is everything new?

### Step 2 — Run preflight
```json
{ "tool": "project/preflight" }
```
Record: pipeline, color property, installed packages.

### Step 3 — Generate build plan
Present a numbered layered plan:

```
SCENE BUILD PLAN: {scene_name}
Pipeline: {pipeline} | Color prop: {color_property}

Layer 1: Geometry
  [ ] Create base terrain/floor (scene/create-object or probuilder/create)
  [ ] Create main structures (scene/create-object x N)
  [ ] Name all objects descriptively

Layer 2: Materials
  [ ] Create materials (material/create-batch: {N} materials)
  [ ] Assign shaders ({pipeline} pipeline: {default_shader})
  [ ] Set colors (material/set-property, Property={color_property})
  [ ] Assign textures if available

Layer 3: Lighting
  [ ] Set directional light (sun/moon)
  [ ] Add local point lights if needed
  [ ] Configure ambient

Layer 4: Effects
  [ ] Particles: {particle_presets_needed}
  [ ] ShaderGraph materials: {shader_count}

Layer 5: Physics
  [ ] Add colliders to all solid objects
  [ ] Add Rigidbody to dynamic objects (AddRigidbody=true)

Layer 6: Audio (if needed)
  [ ] Set up AudioSources with spatial settings
  [ ] Verify AudioListener present (check NoAudioListenerWarning)

Layer 7: UI (if needed)
  [ ] Create Canvas (RenderMode: {screen_space|world_space})
  [ ] Add HUD elements

Checkpoints:
  [ ] scene/save after Layer 2 complete
  [ ] scene/save after Layer 4 complete
  [ ] scene/save at end
```

### Step 4 — Get approval
Present the plan and ask: "Does this plan look right? Any changes before I start?"

### Step 5 — Execute layer by layer
Execute one layer at a time. After each layer:
1. Report what was created
2. Note any errors
3. Do a scene/save if it's a major milestone
4. Ask if user wants to adjust before continuing

### Step 6 — Session handoff
When done (or at end of context), write session handoff.
Follow @${CLAUDE_PLUGIN_ROOT}/workflows/session-handoff/workflow.md
