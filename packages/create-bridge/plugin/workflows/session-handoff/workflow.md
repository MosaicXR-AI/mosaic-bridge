# Session Handoff Workflow

Saves a session summary so the next session (with a fresh context window) can pick up
exactly where this one left off — preventing Ahmed's 90-minute repeated-mistake problem.

## When to use
- At the END of any Mosaic Bridge session
- After completing a major milestone (scene setup, shader creation, etc.)
- When context window is running low
- When switching between Claude and Gemini

## Output location
`docs/Sessions/{username}/SESSION_NOTES.md`

The `{username}` defaults to the user's first name or their Git user name.

## Steps

### Step 1 — Gather session facts
Before writing, collect:
1. What scene/project was being worked on?
2. What tools were called? Which succeeded? Which failed?
3. What is the current render pipeline?
4. What shaders/materials were created? What are their paths?
5. What was the last known state of the scene?
6. What errors or warnings were encountered?
7. What remains to be done?

### Step 2 — Write the handoff file
Path: `docs/Sessions/{username}/SESSION_NOTES.md`

Template:
```markdown
# Mosaic Bridge Session Notes
**Date:** {date}
**Scene:** {scene_name} ({scene_path})
**Pipeline:** {pipeline}
**Color Property:** {color_property}

## What Was Done
- {bullet list of completed steps}

## Current State
- {describe exact state — what objects exist, what materials, what shaders}

## Assets Created
| Asset | Path | Type |
|-------|------|------|
| {name} | {path} | material/shader/prefab |

## Errors Encountered
- {list any errors and how they were resolved}

## Remaining Work
- {bullet list of next steps}

## Lessons Learned
- {any hard-won insights the next session should know}
- Pipeline: {pipeline} — use {color_property} for colors
- {any tool quirks encountered}

## Context Recovery Prompt
To resume this session, tell the AI:
"Load session notes from docs/Sessions/{username}/SESSION_NOTES.md and continue where we left off. The scene is {scene_name}, pipeline is {pipeline}."
```

### Step 3 — Confirm write
Report to user:
```
✓ Session handoff saved to docs/Sessions/{username}/SESSION_NOTES.md
  Use this to resume: "Load session notes from docs/Sessions/{username}/SESSION_NOTES.md"
```

## Multi-User Pattern
Each user gets their own folder:
```
docs/Sessions/
├── Ahmed/
│   └── SESSION_NOTES.md
├── Mousa/
│   └── SESSION_NOTES.md
└── {username}/
    └── SESSION_NOTES.md
```
