namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentSetReferenceParams
    {
        public string GameObjectName   { get; set; }
        public string ComponentType    { get; set; }

        /// <summary>
        /// Serialized property path. Supports dot-notation for nested struct members.
        /// Both the user-facing name ("Lens.FieldOfView") and the internal serialized
        /// name ("m_Lens.m_FieldOfView") are accepted — the tool tries both forms.
        /// Examples: "m_Follow", "Target.TrackingTarget", "Lens.NearClipPlane"
        /// </summary>
        public string PropertyPath     { get; set; }

        // ── Object-reference target (original behavior) ──────────────────────
        /// <summary>Asset path or scene GameObject name to assign as an object reference.
        /// Required when setting an ObjectReference property. Leave null when setting a value type.</summary>
        public string TargetObjectPath { get; set; }
        /// <summary>Optional: "Asset" or "GameObject". Defaults to trying asset first.</summary>
        public string TargetType       { get; set; }

        // ── Value-type overrides (new — for non-reference properties) ─────────
        /// <summary>Assign a float value. Used for Float / Range properties and struct float fields (e.g. Lens.FieldOfView).</summary>
        public float?   FloatValue   { get; set; }
        /// <summary>Assign an int value. Used for Integer properties and enum fields (by numeric index).</summary>
        public int?     IntValue     { get; set; }
        /// <summary>Assign a bool value. Used for Boolean properties and toggle fields.</summary>
        public bool?    BoolValue    { get; set; }
        /// <summary>Assign a string value. Also accepted for Enum fields — pass the enum member name (e.g. "Orthographic").</summary>
        public string   StringValue  { get; set; }
        /// <summary>Assign a Color value as [r, g, b, a] floats (0..1 range).</summary>
        public float[]  ColorValue   { get; set; }
        /// <summary>Assign a Vector value as [x, y, z] or [x, y, z, w] floats.</summary>
        public float[]  VectorValue  { get; set; }
    }
}
