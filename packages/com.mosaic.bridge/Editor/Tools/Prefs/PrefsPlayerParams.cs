using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Prefs
{
    public sealed class PrefsPlayerParams
    {
        /// <summary>The PlayerPrefs key. Required for get/set/delete.</summary>
        public string Key { get; set; }
        /// <summary>Value to set (used with set action).</summary>
        public string Value { get; set; }
        /// <summary>Action to perform: get, set, delete, or delete-all.</summary>
        [Required] public string Action { get; set; }
    }
}
