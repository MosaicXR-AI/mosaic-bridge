using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public static class NavFOVVisualizeTool
    {
        [MosaicTool("nav/fov-visualize",
                    "Creates a field-of-view visualization mesh using raycasts from a specified origin",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<NavFOVVisualizeResult> Execute(NavFOVVisualizeParams p)
        {
            var viewAngle  = p.ViewAngle ?? 90f;
            var viewRadius = p.ViewRadius ?? 10f;
            var rayCount   = p.RayCount ?? 50;
            var maskName   = string.IsNullOrEmpty(p.ObstacleMask) ? "Default" : p.ObstacleMask;

            if (viewAngle <= 0 || viewAngle > 360)
                return ToolResult<NavFOVVisualizeResult>.Fail(
                    "ViewAngle must be between 0 and 360 degrees", ErrorCodes.OUT_OF_RANGE);

            if (rayCount < 3)
                return ToolResult<NavFOVVisualizeResult>.Fail(
                    "RayCount must be at least 3", ErrorCodes.OUT_OF_RANGE);

            // Find origin GameObject
            GameObject origin = null;
            if (p.OriginInstanceId.HasValue && p.OriginInstanceId.Value != 0)
            {
                origin = UnityEngine.Resources.EntityIdToObject(p.OriginInstanceId.Value) as GameObject;
            }
            if (origin == null && !string.IsNullOrEmpty(p.OriginName))
            {
                origin = GameObject.Find(p.OriginName);
            }
            if (origin == null)
                return ToolResult<NavFOVVisualizeResult>.Fail(
                    "Could not find origin GameObject. Provide a valid OriginInstanceId or OriginName.",
                    ErrorCodes.NOT_FOUND);

            int layerMask = LayerMask.GetMask(maskName);

            var transform = origin.transform;
            var position  = transform.position;
            var forward   = transform.forward;

            // Cast rays in a fan
            float halfAngle = viewAngle / 2f;
            float stepAngle = viewAngle / rayCount;

            var hitDistances = new List<float>();
            int hitCount = 0;

            for (int i = 0; i <= rayCount; i++)
            {
                float angle = -halfAngle + stepAngle * i;
                Vector3 dir = Quaternion.Euler(0, angle, 0) * forward;

                RaycastHit hit;
                if (UnityEngine.Physics.Raycast(position, dir, out hit, viewRadius, layerMask))
                {
                    hitDistances.Add(hit.distance);
                    hitCount++;
                }
                else
                {
                    hitDistances.Add(viewRadius);
                }
            }

            int meshVertCount = 0;
            int fovGoId = origin.GetInstanceID();

            if (p.CreateMesh)
            {
                // Build mesh from fan triangles
                var vertices  = new List<Vector3>();
                var triangles = new List<int>();

                // Center vertex (in local space of origin)
                vertices.Add(Vector3.zero);

                for (int i = 0; i <= rayCount; i++)
                {
                    float angle = -halfAngle + stepAngle * i;
                    Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                    vertices.Add(dir * hitDistances[i]);
                }

                for (int i = 0; i < rayCount; i++)
                {
                    triangles.Add(0);
                    triangles.Add(i + 1);
                    triangles.Add(i + 2);
                }

                var mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                meshVertCount = vertices.Count;

                // Create child GO with mesh
                var fovGo = new GameObject("FOV_Visualization");
                fovGo.transform.SetParent(origin.transform);
                fovGo.transform.localPosition = Vector3.zero;
                fovGo.transform.localRotation = Quaternion.identity;

                var mf = fovGo.AddComponent<MeshFilter>();
                mf.mesh = mesh;

                var mr = fovGo.AddComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(1f, 1f, 0f, 0.3f);
                mr.material = mat;

                Undo.RegisterCreatedObjectUndo(fovGo, "FOV Visualization");
                fovGoId = fovGo.GetInstanceID();
            }

            return ToolResult<NavFOVVisualizeResult>.Ok(new NavFOVVisualizeResult
            {
                GameObjectInstanceId = fovGoId,
                ViewAngle            = viewAngle,
                ViewRadius           = viewRadius,
                HitCount             = hitCount,
                MeshVertexCount      = meshVertCount
            });
        }
    }
}
