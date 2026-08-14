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

## 2 — `/execute` answers 404 during a domain reload  ✅ FIXED (status unchanged by design)

`GET /tools` keeps listing every tool while `POST /execute` returns **404** for those same names
while the domain is reloading. 404 is indistinguishable from "this tool does not exist".

Combined with Unity re-emitting cached compile errors — which survive `console/clear` — this read
as "the assembly failed to compile and its tools are gone" for a long stretch. The assembly was
fine throughout; asking for the loaded `System.Type` proved it. Re-running the identical six calls
minutes later: all succeeded.

**What was done.** The response body now carries `reloading` and `retryable`, and says so in
words: *"…the Editor is compiling or importing — during a domain reload the registry is briefly
empty. Retry in a couple of seconds before concluding the tool does not exist."*

**The status stays 404, deliberately.** 503 is the more correct answer, and switching to it broke
`ToolRegistryTests.Execute_UnknownTool_Returns404` — a test asserting a public contract. That is a
breaking API change and belongs in a deliberate version bump, not smuggled in with a diagnostic
fix. The body carries the same information at no cost.

A first attempt also treated *an empty registry* as proof of reloading. The same test caught it
within one run: a freshly constructed registry is empty too, so every unknown tool began answering
503. Erring toward 404 is the safer default anyway — a wrong 404 is clear and actionable, while a
wrong 503 invites a client to retry forever against a tool that will never exist.

**Source fixed too.** `/tools` now returns `count` and `reloading` beside the list. During a reload
that list is briefly a promise the executor cannot keep — every name in it answers "unknown tool"
for a few seconds — and listing them without saying so was the actual lie behind the confusion.
`tools` is unchanged in shape, so existing clients are untouched; a caller that reads `reloading`
knows a failure right now is transient, and `count` shows the registry emptying without diffing
the list.

## 3 — `editor/refresh` reports success without rebuilding  ✅ FIXED

`EditorRefreshTool` returns `{"CompilationFailed": false, "IsCompiling": false}` when no compile
ran at all.

Proven by timestamp: `Library/ScriptAssemblies/<asm>.dll` stayed at 15:37:34 across a `touch` of
the sources, two `editor/refresh` calls, and a `CompilationPipeline.RequestScriptCompilation()`.
Only an Editor restart rebuilt it, at 16:15:58 — half an hour of work against an assembly that
predated the edits.

The verdict is worse than no verdict: `CompilationFailed: false` reads as "compiled and current".

**Fix.** The tool now samples the newest `Library/ScriptAssemblies` write time either side of the
refresh and reports `AssembliesRebuilt`, `AssembliesUpdatedUtc`, `AutoRefreshEnabled` and
`Pending`, with a plain-language `Message`. The case that cost the half hour — nothing rebuilt,
nothing compiling, Auto Refresh disabled, so nothing is *going* to rebuild — now fails with
`STALE_ASSEMBLIES` and says to enable Auto Refresh or restart, because a refresh that cannot
refresh must not answer like one that did.

`Refresh()` only QUEUES compilation, so `isCompiling` sampled immediately after is usually false —
the honest-looking answer arrived before the thing it described. `Pending` now carries that.

## 4 — `editor/execute-code` could not take a qualified or combined enum  ✅ FIXED

`AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)` → HTTP 500. The single-argument
form and chained property reads work; a flag-enum argument (`A | B`) also fails.

Consequence: the targeted reimport that would have avoided an Editor restart was unavailable.

**It was never an arity limit** — multiple arguments already worked. The failure was the argument
FORM: `ConvertArgument` passed the raw text to `Enum.Parse`, which accepts neither a qualified
member (`UnityEditor.ImportAssetOptions.ForceUpdate`) nor a `|` combination. Callers write enums
the way they appear in C#, so both forms are what they will send.

**Fix.** Qualified prefixes are stripped and `|` is translated to the comma list `Enum.Parse`
expects. 5 tests, including the exact call that failed.

---

## Not a defect, but a naming trap

`scene/create-object` **does not create anything** — it is a router. It returns an `Action`
(`primitive` / `instantiate` / `choose` / `store` / `build`) telling the caller what to do next, and
that is documented in its description. A caller who reads only the tool name will misread
`success: true` as "the object exists"; `gameobject/create` is the one that creates.

Worth considering a rename (`scene/resolve-object`?), or at minimum leading the description with
"Does not create — returns a plan."
