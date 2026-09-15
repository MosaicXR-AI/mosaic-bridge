using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Models
{
    public sealed class ModelInfoParams
    {
        /// <summary>Asset path to the model (e.g. "Assets/Characters/Hero.fbx").</summary>
        [Required] public string AssetPath { get; set; }
    }
}
