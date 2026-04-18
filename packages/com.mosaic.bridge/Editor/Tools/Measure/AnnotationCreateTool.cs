using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Measure
{
    public static class AnnotationCreateTool
    {
        [MosaicTool("annotation/create",
                    "Creates an annotation/markup GameObject (text, leader_line, arrow, dimension, callout, pin) for scene markup",
                    isReadOnly: false, category: "annotation", Context = ToolContext.Both)]
        public static ToolResult<AnnotationCreateResult> Execute(AnnotationCreateParams p)
        {
            if (p == null)
                return ToolResult<AnnotationCreateResult>.Fail(
                    "Params are required", ErrorCodes.INVALID_PARAM);

            if (p.Position == null || p.Position.Length != 3)
                return ToolResult<AnnotationCreateResult>.Fail(
                    "Position [x,y,z] is required", ErrorCodes.INVALID_PARAM);

            string type = string.IsNullOrEmpty(p.Type) ? "text" : p.Type.ToLowerInvariant();
            Vector3 position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            Vector3 targetPoint = position;
            bool hasTarget = false;
            if (p.TargetPoint != null && p.TargetPoint.Length == 3)
            {
                targetPoint = new Vector3(p.TargetPoint[0], p.TargetPoint[1], p.TargetPoint[2]);
                hasTarget = true;
            }

            Color textColor = (p.TextColor != null && p.TextColor.Length == 4)
                ? new Color(p.TextColor[0], p.TextColor[1], p.TextColor[2], p.TextColor[3])
                : new Color(1f, 1f, 0f, 1f);

            Color lineColor = (p.LineColor != null && p.LineColor.Length == 4)
                ? new Color(p.LineColor[0], p.LineColor[1], p.LineColor[2], p.LineColor[3])
                : new Color(1f, 1f, 1f, 1f);

            Color? bgColor = null;
            if (p.BackgroundColor != null && p.BackgroundColor.Length == 4)
                bgColor = new Color(p.BackgroundColor[0], p.BackgroundColor[1], p.BackgroundColor[2], p.BackgroundColor[3]);

            int fontSize = p.FontSize > 0 ? p.FontSize : 14;
            string text = p.Text ?? string.Empty;

            string goName = string.IsNullOrEmpty(p.Name)
                ? $"Annotation_{type}_{System.DateTime.Now.Ticks}"
                : p.Name;

            GameObject annotation;

            switch (type)
            {
                case "text":
                    annotation = CreateTextAnnotation(goName, position, text, fontSize, textColor);
                    break;
                case "leader_line":
                    annotation = CreateLeaderLine(goName, position, targetPoint, text, fontSize, textColor, lineColor);
                    break;
                case "arrow":
                    annotation = CreateArrow(goName, position, targetPoint, text, fontSize, textColor, lineColor);
                    break;
                case "dimension":
                    annotation = CreateDimension(goName, position, targetPoint, text, fontSize, textColor, lineColor);
                    break;
                case "callout":
                    annotation = CreateCallout(goName, position, text, fontSize, textColor, bgColor ?? new Color(0f, 0f, 0f, 0.5f));
                    break;
                case "pin":
                    annotation = CreatePin(goName, position, text, fontSize, textColor, lineColor);
                    break;
                default:
                    return ToolResult<AnnotationCreateResult>.Fail(
                        $"Unknown annotation type '{p.Type}'. Use text, leader_line, arrow, dimension, callout, or pin.",
                        ErrorCodes.INVALID_PARAM);
            }

            Undo.RegisterCreatedObjectUndo(annotation, "Mosaic: Create Annotation");

            // Parenting: TargetGameObject takes precedence over Parent.
            if (!string.IsNullOrEmpty(p.TargetGameObject))
            {
                var targetGo = GameObject.Find(p.TargetGameObject);
                if (targetGo == null)
                {
                    Object.DestroyImmediate(annotation);
                    return ToolResult<AnnotationCreateResult>.Fail(
                        $"TargetGameObject '{p.TargetGameObject}' not found", ErrorCodes.NOT_FOUND);
                }
                annotation.transform.SetParent(targetGo.transform, true);
            }
            else if (!string.IsNullOrEmpty(p.Parent))
            {
                var parentGo = GameObject.Find(p.Parent);
                if (parentGo == null)
                {
                    Object.DestroyImmediate(annotation);
                    return ToolResult<AnnotationCreateResult>.Fail(
                        $"Parent '{p.Parent}' not found", ErrorCodes.NOT_FOUND);
                }
                annotation.transform.SetParent(parentGo.transform, true);
            }

            var result = new AnnotationCreateResult
            {
                AnnotationId = annotation.GetInstanceID(),
                GameObjectName = annotation.name,
                Type = type,
                Position = new[] { position.x, position.y, position.z }
            };

            return ToolResult<AnnotationCreateResult>.Ok(result);
        }

        private static Material NewLineMaterial(Color c)
        {
            var mat = new Material(Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default"));
            mat.color = c;
            return mat;
        }

        private static GameObject CreateTextAnnotation(string name, Vector3 position, string text, int fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.1f;
            tm.fontSize = fontSize * 4;
            tm.color = color;
            return go;
        }

        private static GameObject CreateLeaderLine(string name, Vector3 position, Vector3 target,
            string text, int fontSize, Color textColor, Color lineColor)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, position);
            lr.SetPosition(1, target);
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.useWorldSpace = true;
            lr.sharedMaterial = NewLineMaterial(lineColor);
            lr.startColor = lineColor;
            lr.endColor = lineColor;

            AddLabel(go, position, text, fontSize, textColor);
            return go;
        }

        private static GameObject CreateArrow(string name, Vector3 position, Vector3 target,
            string text, int fontSize, Color textColor, Color lineColor)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            // Shaft
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, position);
            lr.SetPosition(1, target);
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.useWorldSpace = true;
            lr.sharedMaterial = NewLineMaterial(lineColor);
            lr.startColor = lineColor;
            lr.endColor = lineColor;

            // Arrowhead cone at target pointing from position -> target
            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "Arrowhead";
            // Cylinder is not a true cone; scale tip narrow. Use small scale.
            head.transform.SetParent(go.transform, false);
            Vector3 dir = target - position;
            float len = dir.magnitude;
            if (len > 0.0001f)
            {
                head.transform.position = target;
                head.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                head.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            }
            // Remove collider to keep annotation non-interactive.
            var col = head.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            AddLabel(go, position, text, fontSize, textColor);
            return go;
        }

        private static GameObject CreateDimension(string name, Vector3 position, Vector3 target,
            string text, int fontSize, Color textColor, Color lineColor)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            // Main dimension line
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, position);
            lr.SetPosition(1, target);
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.useWorldSpace = true;
            lr.sharedMaterial = NewLineMaterial(lineColor);
            lr.startColor = lineColor;
            lr.endColor = lineColor;

            // Perpendicular tick direction (in XZ plane if possible, else arbitrary)
            Vector3 dir = (target - position).normalized;
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.Cross(dir, Vector3.right);
            perp = perp.normalized * 0.1f;

            AddTick(go, "TickStart", position - perp, position + perp, lineColor);
            AddTick(go, "TickEnd", target - perp, target + perp, lineColor);

            // Label with measurement at midpoint
            Vector3 mid = (position + target) * 0.5f;
            string labelText = string.IsNullOrEmpty(text)
                ? $"{Vector3.Distance(position, target):F3} m"
                : text;
            AddLabel(go, mid, labelText, fontSize, textColor);
            return go;
        }

        private static void AddTick(GameObject parent, string name, Vector3 a, Vector3 b, Color color)
        {
            var tick = new GameObject(name);
            tick.transform.SetParent(parent.transform, true);
            var lr = tick.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = 0.015f;
            lr.endWidth = 0.015f;
            lr.useWorldSpace = true;
            lr.sharedMaterial = NewLineMaterial(color);
            lr.startColor = color;
            lr.endColor = color;
        }

        private static GameObject CreateCallout(string name, Vector3 position, string text, int fontSize,
            Color textColor, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            // Background quad (billboarded via Billboard component fallback: set rotation at creation toward camera if available)
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            var col = bg.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(1f, 0.3f, 1f);
            var mr = bg.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/Internal-Colored"));
                mat.color = bgColor;
                mr.sharedMaterial = mat;
            }

            // Billboard toward scene camera if available
            var cam = Camera.current ?? Camera.main;
            if (cam != null)
                go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);

            AddLabel(go, position, text, fontSize, textColor);
            return go;
        }

        private static GameObject CreatePin(string name, Vector3 position, string text, int fontSize,
            Color textColor, Color lineColor)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            // Vertical line from ground (y=0) up to position
            Vector3 ground = new Vector3(position.x, 0f, position.z);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, ground);
            lr.SetPosition(1, position);
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.useWorldSpace = true;
            lr.sharedMaterial = NewLineMaterial(lineColor);
            lr.startColor = lineColor;
            lr.endColor = lineColor;

            AddLabel(go, position, text, fontSize, textColor);
            return go;
        }

        private static void AddLabel(GameObject parent, Vector3 position, string text, int fontSize, Color color)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent.transform, true);
            labelGo.transform.position = position;
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = text ?? string.Empty;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.1f;
            tm.fontSize = fontSize * 4;
            tm.color = color;
        }
    }
}
