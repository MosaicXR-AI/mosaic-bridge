using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Contracts.Compat
{
    /// <summary>
    /// Single place where the bridge touches Unity's object-identity API.
    ///
    /// Unity 6.5 (6000.5) deprecated the 32-bit instance ID in favour of the 64-bit
    /// <c>UnityEngine.EntityId</c>, and made the old entry points obsolete-as-error
    /// (CS0619): <c>Object.GetInstanceID()</c>, <c>Resources.InstanceIDToObject()</c>
    /// and <c>SerializedProperty.objectReferenceInstanceIDValue</c>. The replacements
    /// (<c>GetEntityId()</c>, <c>EntityId.ToULong()</c>, <c>objectReferenceEntityIdValue</c>)
    /// do not all exist on 6000.0–6000.4, so every call site goes through the helpers
    /// here instead of calling the engine directly.
    ///
    /// The MCP wire format keeps <c>InstanceId</c> as a 32-bit <c>int</c>. On 6.5+ that
    /// is the low 32 bits of the <c>EntityId</c> — the exact value Unity's own
    /// <c>GetInstanceID()</c> shim returns — so ids stay stable across versions.
    ///
    /// Unity offers no public API to rebuild an <c>EntityId</c> from a 32-bit id — the
    /// implicit operator is a warning on 6.5 and a hard error on 6.6, and
    /// <c>EntityId.Parse</c> / <c>EntityId.From(int)</c> are both <c>internal</c>. So
    /// <see cref="Resolve"/> recombines instead: see <see cref="HighBits"/>.
    /// </summary>
    public static class UnityIds
    {
#if UNITY_6000_5_OR_NEWER
        private static ulong _highBits;
        private static bool  _highBitsKnown;

        /// <summary>
        /// The upper 32 bits shared by every <c>EntityId</c> in the session (Unity stores
        /// a per-version constant there and the legacy instance ID in the low 32 bits).
        /// Read once off a throwaway object, since Unity exposes no accessor for it.
        ///
        /// Verified on 6000.5.5f1 and 6000.6.0a2: 602 objects spanning scene GameObjects,
        /// Components and assets all shared one high word, and rebuilding from the low 32
        /// bits round-tripped every one of them. <c>UnityIdsTests</c> pins that invariant,
        /// so it fails loudly on the first Unity version that changes the layout — at
        /// which point the id has to widen to 64 bits on the wire.
        /// </summary>
        private static ulong HighBits
        {
            get
            {
                if (!_highBitsKnown)
                {
                    // A ScriptableObject rather than a GameObject, so the scene is untouched.
                    var probe = ScriptableObject.CreateInstance<ScriptableObject>();
                    _highBits = EntityId.ToULong(probe.GetEntityId()) & 0xFFFFFFFF00000000UL;
                    Object.DestroyImmediate(probe);
                    _highBitsKnown = true;
                }
                return _highBits;
            }
        }

        /// <summary>Rebuilds the <c>EntityId</c> for a 32-bit id from this session.</summary>
        internal static EntityId ToEntityId(int id) => EntityId.FromULong(HighBits | (uint)id);
#endif

        /// <summary>
        /// Stable 32-bit id for <paramref name="obj"/>. Mirrors the semantics of the old
        /// <c>Object.GetInstanceID()</c> exactly, including throwing on a null reference
        /// and still returning an id for a destroyed object.
        /// </summary>
        public static int Of(Object obj)
        {
#if UNITY_6000_5_OR_NEWER
            // Low 32 bits of the EntityId are the legacy instance ID. This is precisely
            // what Unity's own (obsolete) GetInstanceID() does internally.
            return (int)(EntityId.ToULong(obj.GetEntityId()) & 0xFFFFFFFFUL);
#else
            return obj.GetInstanceID();
#endif
        }

        /// <summary>
        /// Resolves an id produced by <see cref="Of"/> back to its object, or null if the
        /// object is gone. Finds loaded scene objects only — see <see cref="ResolveAny"/>.
        /// </summary>
        public static Object Resolve(int id)
        {
            if (id == 0) return null;
#if UNITY_6000_5_OR_NEWER
            return Resources.EntityIdToObject(ToEntityId(id));
#elif UNITY_6000_3_OR_NEWER
            return Resources.EntityIdToObject(id);
#else
            return Resources.InstanceIDToObject(id);
#endif
        }

        /// <summary>
        /// Resolves an id to any object the editor knows about, including assets and
        /// unloaded objects. Broader than <see cref="Resolve"/>.
        /// </summary>
        public static Object ResolveAny(int id)
        {
            if (id == 0) return null;
#if UNITY_6000_5_OR_NEWER
            return EditorUtility.EntityIdToObject(ToEntityId(id));
#elif UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(id);
#else
            return EditorUtility.InstanceIDToObject(id);
#endif
        }

        /// <summary>
        /// All active loaded objects of type <typeparamref name="T"/>, unsorted.
        /// Replaces <c>FindObjectsByType&lt;T&gt;(FindObjectsSortMode.None)</c>, whose
        /// sort-mode parameter is deprecated on 6.5+. Inactive objects are excluded, the
        /// same as before.
        /// </summary>
        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_6000_5_OR_NEWER
            return Object.FindObjectsByType<T>();
#else
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#endif
        }

        /// <summary>
        /// The id of a <c>SerializedProperty</c>'s object reference, non-zero when the
        /// reference points at something even if that target is missing. Replaces
        /// <c>SerializedProperty.objectReferenceInstanceIDValue</c>.
        /// </summary>
        public static int ObjectReferenceId(SerializedProperty prop)
        {
#if UNITY_6000_5_OR_NEWER
            return (int)(EntityId.ToULong(prop.objectReferenceEntityIdValue) & 0xFFFFFFFFUL);
#else
            return prop.objectReferenceInstanceIDValue;
#endif
        }
    }
}
