#if MOSAIC_HAS_PROBUILDER
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ProBuilder
{
    public static class ProBuilderCreateTool
    {
        [MosaicTool("probuilder/create",
                    "Creates a new ProBuilder mesh shape (Cube, Cylinder, Sphere, Stairs, Arch, Door, Pipe, Cone, Torus)",
                    isReadOnly: false, category: "probuilder")]
        public static ToolResult<ProBuilderCreateResult> Create(ProBuilderCreateParams p)
        {
            if (string.IsNullOrEmpty(p.Shape))
                return ToolResult<ProBuilderCreateResult>.Fail(
                    "Shape is required. Valid: Cube, Cylinder, Sphere, Stairs, Arch, Door, Pipe, Cone, Torus",
                    ErrorCodes.INVALID_PARAM);

            if (!System.Enum.TryParse<ShapeType>(p.Shape, true, out var shapeType))
                return ToolResult<ProBuilderCreateResult>.Fail(
                    $"Invalid Shape '{p.Shape}'. Valid: Cube, Cylinder, Sphere, Stairs, Arch, Door, Pipe, Cone, Torus",
                    ErrorCodes.INVALID_PARAM);

            ProBuilderMesh mesh = ShapeGenerator.CreateShape(shapeType);

            if (!string.IsNullOrEmpty(p.Name))
                mesh.gameObject.name = p.Name;

            if (p.Dimensions != null && p.Dimensions.Length == 3)
                mesh.transform.localScale = new Vector3(p.Dimensions[0], p.Dimensions[1], p.Dimensions[2]);

            if (p.Position != null && p.Position.Length == 3)
                mesh.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            mesh.ToMesh();
            mesh.Refresh();

            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Mosaic: ProBuilder Create");

            return ToolResult<ProBuilderCreateResult>.Ok(new ProBuilderCreateResult
            {
                Name = mesh.gameObject.name,
                InstanceId = mesh.gameObject.GetInstanceID(),
                VertexCount = mesh.vertexCount,
                FaceCount = mesh.faceCount
            });
        }
    }

    public sealed class ProBuilderCreateParams
    {
        [Required] public string Shape { get; set; }
        public float[] Dimensions { get; set; }
        public float[] Position { get; set; }
        public string Name { get; set; }
    }

    public sealed class ProBuilderCreateResult
    {
        public string Name { get; set; }
        public int InstanceId { get; set; }
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }
    }
}
#endif
