namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphConnectParams
    {
        /// <summary>Asset path to the .shadergraph file.</summary>
        public string GraphPath { get; set; }

        /// <summary>GUID of the node that produces the output (returned by shadergraph/add-node as NodeId).</summary>
        public string OutputNodeId { get; set; }

        /// <summary>Slot ID on the output node to read from. See Slots[*] returned by shadergraph/add-node.</summary>
        public int OutputSlotId { get; set; }

        /// <summary>GUID of the node that receives the input.</summary>
        public string InputNodeId { get; set; }

        /// <summary>Slot ID on the input node to write to.</summary>
        public int InputSlotId { get; set; }
    }
}
