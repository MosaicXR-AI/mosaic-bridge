# Tool reliability — findings and fixes

Defects found while driving a live Editor through `/execute` for several hours of automated work.
They share one shape, which is the reason this file exists:

> **A tool that reports success for work it did not do is worse than a tool that fails.**

A caller cannot detect it without independently verifying the world afterwards, and mostly it will
not. Three of the four below are that defect. Each cost real debugging time, and one of them
silently invalidated an experiment whose conclusion was then acted on.

---

## 1 — Unknown parameters were silently ignored  ✅ FIXED

**Symptom.** `POST /execute` with a parameter the tool does not declare returned `success: true`.
The parameter was dropped and the result computed from defaults.

Reproduced: `scene/create-object {name: "Cube", totallyBogusParam: "xyz"}` → `success: true`.

The generated schema already advertised `additionalProperties: false`; the binder did not enforce
it. `JsonConvert.DeserializeObject` ignores unknown members by default.

**Why it matters.** A misspelt or invented parameter produced a successful-looking result that had
quietly ignored the caller's intent. It is how `scene/create-object` was called with a
`primitiveType` it does not accept: the call succeeded, the parameter vanished, and the result was
read as though it had been honoured — which then invalidated a downstream experiment.

**Fix.** `ParameterValidator.Bind` now uses `MissingMemberHandling.Error` and returns
`INVALID_PARAM` naming both the offending parameter and the accepted ones:

```
Unknown parameter 'primitiveType'. This tool accepts: autoApprove, choiceIndex,
name (required), skipStore. Unknown parameters are rejected rather than ignored,
because a silently dropped parameter produces a result that looks correct and is not.
```

Listing the valid names matters: the caller is there precisely because they believed a name that
does not exist, and `"Could not find member 'x'"` says what is wrong without saying what to write.

4 tests added (`ParameterValidatorTests`), including two that the narrowing must not break —
known-parameters-only and missing-optional both still succeed. 1151 tests pass.

---

## 2 — `/execute` answers 404 during a domain reload  ⬜ OPEN

`GET /tools` keeps listing every tool while `POST /execute` returns **404** for those same names
while the domain is reloading. 404 is indistinguishable from "this tool does not exist".

Combined with Unity re-emitting cached compile errors — which survive `console/clear` — this read
as "the assembly failed to compile and its tools are gone" for a long stretch. The assembly was
fine throughout; asking for the loaded `System.Type` proved it. Re-running the identical six calls
minutes later: all succeeded.

**Proposed fix.** **503 with `Retry-After`** while reloading. A reload is transient; a missing tool
is permanent, and the two must not share a status code. Better still, `/tools` and `/execute`
should agree on availability — listing a tool that cannot be executed is the actual lie.

## 3 — `editor/refresh` reports success without rebuilding  ⬜ OPEN

`EditorRefreshTool` returns `{"CompilationFailed": false, "IsCompiling": false}` when no compile
ran at all.

Proven by timestamp: `Library/ScriptAssemblies/<asm>.dll` stayed at 15:37:34 across a `touch` of
the sources, two `editor/refresh` calls, and a `CompilationPipeline.RequestScriptCompilation()`.
Only an Editor restart rebuilt it, at 16:15:58 — half an hour of work against an assembly that
predated the edits.

The verdict is worse than no verdict: `CompilationFailed: false` reads as "compiled and current".

**Proposed fix.** Report whether a compile actually **ran** — an assembly timestamp or a
compilation id — not merely that none is in flight. This likely interacts with Auto Refresh being
disabled in preferences, which the tool should detect and say so, since a refresh that cannot
refresh should not report success.

## 4 — `editor/execute-code` cannot call a two-argument static  ⬜ OPEN

`AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)` → HTTP 500. The single-argument
form and chained property reads work; a flag-enum argument (`A | B`) also fails.

Consequence: the targeted reimport that would have avoided an Editor restart was unavailable.

**Proposed fix.** Support multiple arguments and enum parsing, or state the arity limit in the
description. "Evaluates a single static C# expression via reflection" reads as a language limit
rather than an arity one.

---

## Not a defect, but a naming trap

`scene/create-object` **does not create anything** — it is a router. It returns an `Action`
(`primitive` / `instantiate` / `choose` / `store` / `build`) telling the caller what to do next, and
that is documented in its description. A caller who reads only the tool name will misread
`success: true` as "the object exists"; `gameobject/create` is the one that creates.

Worth considering a rename (`scene/resolve-object`?), or at minimum leading the description with
"Does not create — returns a plan."
