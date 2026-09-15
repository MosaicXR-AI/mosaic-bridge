namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingSetPropertiesParams
    {
        public int InstanceId { get; set; }       // 0 means not specified
        public string Name { get; set; }          // find light by GameObject name
        public float[] Color { get; set; }        // [r,g,b] or [r,g,b,a] 0-1 range
        public float? Intensity { get; set; }
        public float? Range { get; set; }
        public float? SpotAngle { get; set; }
        public string Shadows { get; set; }       // None, Hard, Soft
        public float? ColorTemperature { get; set; }
        public float? BounceIntensity { get; set; }

        /// <summary>"Realtime", "Mixed", or "Baked". Editor-only. Null to leave unchanged.</summary>
        public string LightmapBakeType { get; set; }
        /// <summary>Asset path of a Texture to project as a cookie. Empty string clears it. Null to leave unchanged.</summary>
        public string CookiePath { get; set; }
        public float[] CookieSize { get; set; } // [width, height] — Light.cookieSize2D
        /// <summary>Layer names (comma-separated) this light affects. Null to leave unchanged.</summary>
        public string CullingMask { get; set; }
        public float? ShadowBias { get; set; }
        /// <summary>[width, height] for Area lights (rectangular/disc/tube/pyramid, per light type).</summary>
        public float[] AreaSize { get; set; }
    }
}
