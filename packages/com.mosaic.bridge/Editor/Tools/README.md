# Mosaic.Bridge.Tools

Tool implementations organized by namespace. All tools live in the single
`Mosaic.Bridge.Tools` assembly; folders group tools by Unity subsystem.

## Tier 1 Tool Categories (core subset)

| Folder | Namespace | Tools |
|---|---|---|
| `GameObjects/` | `Mosaic.Bridge.Tools.GameObjects` | create, delete, duplicate, info, set-active, set-transform, reparent, find |
| `Components/` | `Mosaic.Bridge.Tools.Components` | add, remove, list, get-properties, set-property, set-reference |
| `Scenes/` | `Mosaic.Bridge.Tools.Scenes` | new, open, save, info, hierarchy |
| `Assets/` | `Mosaic.Bridge.Tools.Assets` | list, import, delete, create-prefab, instantiate-prefab, info |
| `Search/` | `Mosaic.Bridge.Tools.Search` | by-name, by-component, by-tag, by-layer, missing-references |
| `Scripts/` | `Mosaic.Bridge.Tools.Scripts` | create, read, update (with human-in-the-loop approval) |

Additional tool categories cover Physics, Audio, UI, Lighting, Animations,
Terrains, Timeline, InputSystem, Particles, ShaderGraphs, Measure, DataViz,
Spatial, AI, ProcGen, Simulation, AdvancedMesh, AdvancedRendering,
AdvancedNavigation, Compute, and integrations for Cinemachine, ProBuilder,
Addressables, TextMeshPro, URP, HDRP, Splines, and VisualScripting.

## Tool Implementation Pattern

Every tool method follows this pattern:

```csharp
[MosaicTool("gameobject/create", "Create a new GameObject in the active scene")]
public static ToolResult<GameObjectInfo> Create(CreateGameObjectParams args)
{
    // 1. Validate inputs (framework already validated [Required] attributes)
    if (string.IsNullOrEmpty(args.Name))
        return ToolResult<GameObjectInfo>.Fail("Name is required", ErrorCodes.INVALID_PARAM);

    // 2. Begin Undo group
    var undoGroup = Undo.GetCurrentGroup();
    Undo.SetCurrentGroupName("Mosaic: Create GameObject");

    // 3. Do the work on main thread (already guaranteed by dispatcher)
    var go = new GameObject(args.Name);
    Undo.RegisterCreatedObjectUndo(go, "Create GameObject");

    // 4. Apply optional parameters
    if (args.Position.HasValue)
        go.transform.position = args.Position.Value;

    // 5. Return success with data and undo group ID
    return ToolResult<GameObjectInfo>.Ok(
        data: GameObjectInfo.From(go),
        undoGroup: undoGroup.ToString()
    );
}
```

## Mandatory Rules

1. Tool methods are static
2. Tool methods take a single typed parameter class
3. Tool methods return `ToolResult<T>` — never throw for user errors
4. State-changing tools register Undo operations
5. Use `ErrorCodes` constants for all error codes
6. Never call `Debug.Log` directly — use `IMosaicLogger`
7. Never call `EditorApplication.delayCall` from inside a tool method (the dispatcher already guarantees main thread)
8. Use namespaces for categorization; all tools share one asmdef
