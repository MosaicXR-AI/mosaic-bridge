using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Jobs
{
    /// <summary>
    /// One kind of long-running Editor operation registered with JobRegistry — package
    /// install/remove today, run-block and generate_asset next (see the O4 job registry plan).
    /// Implementations must never trust their own in-memory state surviving a domain reload:
    /// Probe is called after one has genuinely happened, with no live object to ask, and must
    /// still produce a real answer from durable evidence (a file on disk, PackageInfo, a
    /// manifest) or explicitly say it cannot.
    /// </summary>
    public interface IJobKind
    {
        /// <summary>Starts the underlying operation and returns immediately. Mutate record.Message
        /// with a human-readable "what's happening" line; do not set record.Status here — the
        /// caller sets Running once Start returns without throwing.</summary>
        void Start(JobRecord record, JObject parameters);

        /// <summary>Re-derive record.Status/Message/ResultJson in place from whatever evidence is
        /// available right now — a live object if one survived, durable evidence if not. Must
        /// leave Status as Running (not throw) if the operation is legitimately still in flight.</summary>
        void Probe(JobRecord record);

        /// <summary>Best-effort cancel. Returns true only if cancellation was actually accepted —
        /// most Unity async operations (package add/remove) offer no cancel at all.</summary>
        bool Cancel(JobRecord record);
    }
}
