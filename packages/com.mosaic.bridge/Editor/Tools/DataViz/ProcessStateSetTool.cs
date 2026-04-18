using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Sets a "process state" (running, idle, fault, maintenance, offline, ...) on a
    /// GameObject and applies a corresponding visual: tinted color, emissive glow,
    /// TextMesh icon, or combined color+icon. Supports propagation to children and an
    /// optional blink-on-change pulse.
    /// </summary>
    public static class ProcessStateSetTool
    {
        static readonly HashSet<string> ValidDisplayModes = new HashSet<string>
        {
            "color", "emission", "icon", "combined"
        };

        // Stores last-known state per GameObject instance ID for the editor session.
        // Persisted across calls but not across domain reloads — adequate for visualization.
        static readonly Dictionary<int, string> _stateMemory = new Dictionary<int, string>();

        struct StateVisual
        {
            public Color Color;
            public string Icon;
            public string Description;
        }

        static StateVisual GetDefaultVisual(string state)
        {
            switch (state)
            {
                case "running":
                    return new StateVisual { Color = new Color(0f, 1f, 0f, 1f), Icon = "▶", Description = "Running" };
                case "idle":
                    return new StateVisual { Color = new Color(0f, 0.5f, 1f, 1f), Icon = "⏸", Description = "Idle" };
                case "fault":
                    return new StateVisual { Color = new Color(1f, 0f, 0f, 1f), Icon = "⚠", Description = "Fault" };
                case "maintenance":
                    return new StateVisual { Color = new Color(1f, 0.6f, 0f, 1f), Icon = "⚙", Description = "Maintenance" };
                case "offline":
                    return new StateVisual { Color = new Color(0.3f, 0.3f, 0.3f, 1f), Icon = "⏻", Description = "Offline" };
                default:
                    return new StateVisual { Color = Color.white, Icon = "", Description = "Unknown" };
            }
        }

        [MosaicTool("process/state-set",
                    "Sets a process state (running/idle/fault/maintenance/offline/custom) on a GameObject and applies a visual representation: color tint, emissive glow, TextMesh icon, or combined color+icon. Optionally blinks on change and propagates to all child renderers.",
                    isReadOnly: false, category: "process", Context = ToolContext.Both)]
        public static ToolResult<ProcessStateSetResult> Execute(ProcessStateSetParams p)
        {
            if (p == null)
                return ToolResult<ProcessStateSetResult>.Fail(
                    "Params are required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrWhiteSpace(p.TargetGameObject))
                return ToolResult<ProcessStateSetResult>.Fail(
                    "TargetGameObject is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrWhiteSpace(p.State))
                return ToolResult<ProcessStateSetResult>.Fail(
                    "State is required", ErrorCodes.INVALID_PARAM);

            var displayMode = string.IsNullOrEmpty(p.DisplayMode)
                ? "color"
                : p.DisplayMode.ToLowerInvariant();

            if (!ValidDisplayModes.Contains(displayMode))
                return ToolResult<ProcessStateSetResult>.Fail(
                    $"Invalid DisplayMode '{p.DisplayMode}'. Valid: {string.Join(", ", ValidDisplayModes)}",
                    ErrorCodes.INVALID_PARAM);

            var target = GameObject.Find(p.TargetGameObject);
            if (target == null)
                return ToolResult<ProcessStateSetResult>.Fail(
                    $"TargetGameObject '{p.TargetGameObject}' not found", ErrorCodes.NOT_FOUND);

            string state = p.State.ToLowerInvariant();

            // Resolve visual (custom config overrides defaults).
            StateVisual visual = GetDefaultVisual(state);
            if (p.StateConfig != null)
            {
                foreach (var def in p.StateConfig)
                {
                    if (def == null || string.IsNullOrWhiteSpace(def.State)) continue;
                    if (!string.Equals(def.State.ToLowerInvariant(), state, System.StringComparison.Ordinal))
                        continue;

                    if (def.Color != null && def.Color.Length >= 3)
                    {
                        float a = def.Color.Length >= 4 ? def.Color[3] : 1f;
                        visual.Color = new Color(def.Color[0], def.Color[1], def.Color[2], a);
                    }
                    if (!string.IsNullOrEmpty(def.IconText)) visual.Icon = def.IconText;
                    if (!string.IsNullOrEmpty(def.Description)) visual.Description = def.Description;
                    break;
                }
            }

            int instanceId = target.GetInstanceID();
            string previousState = null;
            _stateMemory.TryGetValue(instanceId, out previousState);

            // Gather renderers to affect.
            var renderers = new List<Renderer>();
            if (p.Propagate)
            {
                renderers.AddRange(target.GetComponentsInChildren<Renderer>(true));
            }
            else
            {
                var r = target.GetComponent<Renderer>();
                if (r != null) renderers.Add(r);
            }

            int affected = 0;

            bool wantColor = displayMode == "color" || displayMode == "combined";
            bool wantEmission = displayMode == "emission";
            bool wantIcon = displayMode == "icon" || displayMode == "combined";

            if (wantColor || wantEmission)
            {
                foreach (var r in renderers)
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    Undo.RecordObject(r, "Mosaic: Set Process State");

                    // Instance the material so we don't mutate shared assets.
                    var mat = new Material(r.sharedMaterial);
                    if (wantColor)
                    {
                        mat.color = visual.Color;
                    }
                    if (wantEmission && mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", visual.Color * 1.5f);
                    }
                    r.sharedMaterial = mat;
                    affected++;
                }
            }

            if (wantIcon)
            {
                AttachOrUpdateIcon(target, visual.Icon, visual.Color);
                if (affected == 0) affected = 1;
            }

            if (p.BlinkOnChange && (previousState == null || previousState != state))
            {
                AttachBlinker(target, visual.Color);
            }

            _stateMemory[instanceId] = state;

            // Also persist via PlayerPrefs as a soft hint for runtime / cross-session readers.
            PlayerPrefs.SetString($"State_{instanceId}", state);

            return ToolResult<ProcessStateSetResult>.Ok(new ProcessStateSetResult
            {
                GameObjectName = target.name,
                State = state,
                PreviousState = previousState,
                AffectedCount = affected,
                DisplayMode = displayMode
            });
        }

        // -------------------------------------------------------------
        // Icon child
        // -------------------------------------------------------------
        const string IconChildName = "__ProcessStateIcon";

        static void AttachOrUpdateIcon(GameObject target, string iconText, Color color)
        {
            var existing = target.transform.Find(IconChildName);
            GameObject iconGo;
            if (existing != null)
            {
                iconGo = existing.gameObject;
            }
            else
            {
                iconGo = new GameObject(IconChildName);
                Undo.RegisterCreatedObjectUndo(iconGo, "Mosaic: Create State Icon");
                iconGo.transform.SetParent(target.transform, false);
                iconGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            }

            var tm = iconGo.GetComponent<TextMesh>();
            if (tm == null) tm = iconGo.AddComponent<TextMesh>();
            tm.text = iconText ?? "";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.1f;
            tm.fontSize = 64;
            tm.color = color;
        }

        // -------------------------------------------------------------
        // Blinker
        // -------------------------------------------------------------
        static void AttachBlinker(GameObject target, Color pulseColor)
        {
            // Remove any prior blinker so re-applying restarts the pulse cleanly.
            var prior = target.GetComponent<ProcessStateBlinker>();
            if (prior != null) Object.DestroyImmediate(prior);

            var b = target.AddComponent<ProcessStateBlinker>();
            b.PulseColor = pulseColor;
            b.Duration = 2f;
        }
    }

    /// <summary>
    /// Lightweight runtime helper that briefly pulses (lerps) the target's renderer color
    /// toward a pulse color, then restores. Self-destructs when finished.
    /// </summary>
    [ExecuteAlways]
    public class ProcessStateBlinker : MonoBehaviour
    {
        public Color PulseColor = Color.white;
        public float Duration = 2f;

        Renderer[] _renderers;
        Color[] _originals;
        float _t;

        void OnEnable()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (_renderers == null || _renderers.Length == 0)
            {
                Destroy(this);
                return;
            }
            _originals = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].sharedMaterial != null)
                    _originals[i] = _renderers[i].sharedMaterial.color;
                else
                    _originals[i] = Color.white;
            }
            _t = 0f;
        }

        void Update()
        {
            if (_renderers == null) return;
            _t += Application.isPlaying ? Time.deltaTime : 0.016f;
            float k = Duration > 0f ? Mathf.Clamp01(_t / Duration) : 1f;
            // Triangle wave 0->1->0 over duration.
            float pulse = 1f - Mathf.Abs(k * 2f - 1f);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                r.sharedMaterial.color = Color.Lerp(_originals[i], PulseColor, pulse);
            }
            if (k >= 1f)
            {
                // Restore originals and remove.
                for (int i = 0; i < _renderers.Length; i++)
                {
                    var r = _renderers[i];
                    if (r == null || r.sharedMaterial == null) continue;
                    r.sharedMaterial.color = _originals[i];
                }
                if (Application.isPlaying) Destroy(this);
                else DestroyImmediate(this);
            }
        }
    }
}
