using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainHolesTool
    {
        [MosaicTool("terrain/holes",
                    "rect/circle: punches a hole (RectX/Y/Width/Height, or CenterX/Y+Radius, in " +
                    "holesmap pixel coordinates) — cave/mine entrances. array: sets an explicit " +
                    "[HeightCells*Width] bool array (true=surface, false=hole) at ArrayX/Y. clear: " +
                    "fills the entire holesmap solid. get: reads the current holes.",
                    isReadOnly: false)]
        public static ToolResult<TerrainHolesResult> Execute(TerrainHolesParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainHolesResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var data = terrain.terrainData;
            int res = data.holesResolution;

            switch (p.Action?.ToLowerInvariant())
            {
                case "rect":   return Rect(terrain, data, res, p);
                case "circle": return Circle(terrain, data, res, p);
                case "array":  return ArrayHoles(terrain, data, res, p);
                case "clear":  return Clear(terrain, data, res, p);
                case "get":    return Get(terrain, data, res, p);
                default:
                    return ToolResult<TerrainHolesResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: rect, circle, array, clear, get", ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TerrainHolesResult> Rect(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHolesParams p)
        {
            if (p.RectWidth <= 0 || p.RectHeight <= 0)
                return ToolResult<TerrainHolesResult>.Fail("RectWidth and RectHeight must be > 0", ErrorCodes.INVALID_PARAM);
            if (p.RectX < 0 || p.RectY < 0 || p.RectX + p.RectWidth > res || p.RectY + p.RectHeight > res)
                return ToolResult<TerrainHolesResult>.Fail(
                    $"Rect ({p.RectX},{p.RectY})+({p.RectWidth}x{p.RectHeight}) exceeds holesResolution {res}", ErrorCodes.INVALID_PARAM);

            var holes = new bool[p.RectHeight, p.RectWidth]; // default false = hole
            data.SetHoles(p.RectX, p.RectY, holes);
            terrain.Flush();

            return Ok(terrain, res, "rect", $"Punched a {p.RectWidth}x{p.RectHeight} hole at ({p.RectX},{p.RectY})");
        }

        private static ToolResult<TerrainHolesResult> Circle(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHolesParams p)
        {
            if (p.Radius <= 0)
                return ToolResult<TerrainHolesResult>.Fail("Radius must be > 0", ErrorCodes.INVALID_PARAM);

            int xMin = Mathf.Max(0, p.CenterX - p.Radius);
            int yMin = Mathf.Max(0, p.CenterY - p.Radius);
            int xMax = Mathf.Min(res - 1, p.CenterX + p.Radius);
            int yMax = Mathf.Min(res - 1, p.CenterY + p.Radius);
            int w = xMax - xMin + 1;
            int h = yMax - yMin + 1;
            if (w <= 0 || h <= 0)
                return ToolResult<TerrainHolesResult>.Fail("Circle is entirely outside the holesmap", ErrorCodes.INVALID_PARAM);

            var holes = data.GetHoles(xMin, yMin, w, h);
            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    var dist = Vector2.Distance(new Vector2(xMin + dx, yMin + dy), new Vector2(p.CenterX, p.CenterY));
                    if (dist <= p.Radius) holes[dy, dx] = false;
                }
            }
            data.SetHoles(xMin, yMin, holes);
            terrain.Flush();

            return Ok(terrain, res, "circle", $"Punched a circular hole of radius {p.Radius} at ({p.CenterX},{p.CenterY})");
        }

        private static ToolResult<TerrainHolesResult> ArrayHoles(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHolesParams p)
        {
            if (p.Holes == null || p.Holes.Length == 0)
                return ToolResult<TerrainHolesResult>.Fail("Holes is required for action 'array'", ErrorCodes.INVALID_PARAM);
            if (p.Width <= 0 || p.HeightCells <= 0)
                return ToolResult<TerrainHolesResult>.Fail("Width and HeightCells must be > 0 for action 'array'", ErrorCodes.INVALID_PARAM);
            if (p.Holes.Length != p.Width * p.HeightCells)
                return ToolResult<TerrainHolesResult>.Fail(
                    $"Holes length {p.Holes.Length} does not match Width*HeightCells ({p.Width * p.HeightCells})", ErrorCodes.INVALID_PARAM);
            if (p.ArrayX < 0 || p.ArrayY < 0 || p.ArrayX + p.Width > res || p.ArrayY + p.HeightCells > res)
                return ToolResult<TerrainHolesResult>.Fail(
                    $"Array region ({p.ArrayX},{p.ArrayY})+({p.Width}x{p.HeightCells}) exceeds holesResolution {res}", ErrorCodes.INVALID_PARAM);

            var holes = new bool[p.HeightCells, p.Width];
            for (int y = 0; y < p.HeightCells; y++)
                for (int x = 0; x < p.Width; x++)
                    holes[y, x] = p.Holes[y * p.Width + x];

            data.SetHoles(p.ArrayX, p.ArrayY, holes);
            terrain.Flush();

            return Ok(terrain, res, "array", $"Applied {p.Width}x{p.HeightCells} holes array at ({p.ArrayX},{p.ArrayY})");
        }

        private static ToolResult<TerrainHolesResult> Clear(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHolesParams p)
        {
            var holes = new bool[res, res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    holes[y, x] = true;
            data.SetHoles(0, 0, holes);
            terrain.Flush();

            return Ok(terrain, res, "clear", "Cleared all holes (entire terrain solid)");
        }

        private static ToolResult<TerrainHolesResult> Get(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHolesParams p)
        {
            int x = p.Width > 0 ? p.ArrayX : 0;
            int y = p.Width > 0 ? p.ArrayY : 0;
            int w = p.Width > 0 ? p.Width : res;
            int h = p.HeightCells > 0 ? p.HeightCells : res;

            if (x < 0 || y < 0 || x + w > res || y + h > res)
                return ToolResult<TerrainHolesResult>.Fail(
                    $"Region ({x},{y})+({w}x{h}) exceeds holesResolution {res}", ErrorCodes.INVALID_PARAM);

            var holes2D = data.GetHoles(x, y, w, h);
            var flat = new bool[w * h];
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    flat[dy * w + dx] = holes2D[dy, dx];

            var result = Ok(terrain, res, "get", $"Read {w}x{h} holes at ({x},{y})");
            result.Data.Holes = flat;
            return result;
        }

        private static ToolResult<TerrainHolesResult> Ok(UnityEngine.Terrain terrain, int res, string action, string message) =>
            ToolResult<TerrainHolesResult>.Ok(new TerrainHolesResult
            {
                Action = action, InstanceId = UnityIds.Of(terrain.gameObject), Name = terrain.gameObject.name,
                HolesResolution = res, Message = message,
            });
    }
}
