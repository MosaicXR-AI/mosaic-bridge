#if MOSAIC_HAS_SPLINES
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Splines
{
    public sealed class SplineAddKnotParams
    {
        /// <summary>Name of the GameObject with a SplineContainer component.</summary>
        [Required] public string GameObjectName { get; set; }

        /// <summary>Action to perform: "add", "edit", or "remove".</summary>
        [Required] public string Action { get; set; }

        /// <summary>
        /// Knot index. Required for "edit" and "remove".
        /// For "add", specifies insert position (appends to end if omitted or -1).
        /// </summary>
        public int Index { get; set; } = -1;

        /// <summary>Knot data. Required for "add" and "edit".</summary>
        public SplineKnotData KnotData { get; set; }
    }
}
#endif
