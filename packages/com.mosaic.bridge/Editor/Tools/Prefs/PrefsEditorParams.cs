using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Prefs
{
    public sealed class PrefsEditorParams
    {
        /// <summary>The EditorPrefs key.</summary>
        [Required] public string Key { get; set; }
        /// <summary>Value to set (used with set action).</summary>
        public string Value { get; set; }
        /// <summary>Action to perform: get, set, or delete.</summary>
        [Required] public string Action { get; set; }
    }
}
