using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Core.SerializedValues;

namespace Mosaic.Bridge.Tests.Unit.Core.SerializedValues
{
    // O4 §3.2: component/set_property used to reject enums and LayerMask outright, had no array
    // case, and neither setter supported Gradient/AnimationCurve or atomic struct assignment. This
    // exercises the shared codec against a real MonoBehaviour's SerializedProperty, the same shape
    // every one of those field kinds actually has on a component.
    [TestFixture]
    public class SerializedPropertyCodecTests
    {
        private GameObject _go;
        private CodecProbe _probe;
        private SerializedObject _so;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CodecProbe");
            _probe = _go.AddComponent<CodecProbe>();
            _so = new SerializedObject(_probe);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private SerializedProperty Prop(string name) => _so.FindProperty(name);

        // ── Enum ─────────────────────────────────────────────────────────────

        [Test]
        public void TrySet_EnumByName_CaseInsensitive_Succeeds()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Mode"), JToken.FromObject("beta"), null, out var error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual((int)CodecProbeMode.Beta, Prop("Mode").enumValueIndex);
        }

        [Test]
        public void TrySet_EnumByIndex_Succeeds()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Mode"), JToken.FromObject(2), null, out var error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(2, Prop("Mode").enumValueIndex);
        }

        [Test]
        public void TrySet_EnumUnknownName_FailsWithValidNamesListed()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Mode"), JToken.FromObject("NotAMember"), null, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains("Alpha", error);
        }

        [Test]
        public void Get_Enum_ReturnsMemberName()
        {
            Prop("Mode").enumValueIndex = (int)CodecProbeMode.Gamma;
            _so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("Gamma", SerializedPropertyCodec.Get(Prop("Mode")));
        }

        // ── LayerMask ────────────────────────────────────────────────────────

        [Test]
        public void TrySet_LayerMaskAsIntBitmask_Succeeds()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Mask"), JToken.FromObject(5), null, out var error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(5, Prop("Mask").intValue);
        }

        [Test]
        public void TrySet_LayerMaskAsIndexArray_CombinesBits()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Mask"), new JArray(0, 2), null, out var error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual((1 << 0) | (1 << 2), Prop("Mask").intValue);
        }

        [Test]
        public void TrySet_LayerMaskUnknownName_Fails()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Mask"), new JArray("DefinitelyNotARealLayerName"), null, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains("Unknown layer", error);
        }

        // ── Array ────────────────────────────────────────────────────────────

        [Test]
        public void TrySet_FloatArray_ResizesAndAssignsEachElement()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Values"), new JArray(1.0, 2.0, 3.0), null, out var error);

            Assert.IsTrue(ok, error);
            var prop = Prop("Values");
            Assert.AreEqual(3, prop.arraySize);
            Assert.AreEqual(2.0f, prop.GetArrayElementAtIndex(1).floatValue);
        }

        [Test]
        public void TrySet_ArrayShrinks_WhenGivenFewerElements()
        {
            SerializedPropertyCodec.TrySet(Prop("Values"), new JArray(1.0, 2.0, 3.0, 4.0), null, out _);

            var ok = SerializedPropertyCodec.TrySet(Prop("Values"), new JArray(9.0), null, out var error);

            Assert.IsTrue(ok, error);
            Assert.AreEqual(1, Prop("Values").arraySize);
        }

        [Test]
        public void TrySet_ArrayElementTypeMismatch_FailsWithIndexQualifiedError()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Values"), new JArray("not-a-float"), null, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains("Values[0]", error);
        }

        // ── Struct ───────────────────────────────────────────────────────────

        [Test]
        public void TrySet_StructField_AssignsNamedSubFieldsAtomically()
        {
            // Same convention as ComponentSetReferenceTool.FindPropertyFuzzy: the caller uses the
            // Inspector-facing name ("Left"), which the fuzzy match turns into the real serialized
            // field ("m_Left") by prefixing — it does not further case-fold, so "left" would not match.
            var ok = SerializedPropertyCodec.TrySet(Prop("Offset"),
                JObject.FromObject(new { Left = 1, Right = 2, Top = 3, Bottom = 4 }), null, out var error);

            Assert.IsTrue(ok, error);
            var prop = Prop("Offset");
            Assert.AreEqual(1, prop.FindPropertyRelative("m_Left").intValue);
            Assert.AreEqual(4, prop.FindPropertyRelative("m_Bottom").intValue);
        }

        [Test]
        public void TrySet_StructFieldUnknownSubField_FailsWithFieldQualifiedError()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Offset"),
                JObject.FromObject(new { notARealField = 1 }), null, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains("notARealField", error);
        }

        [Test]
        public void Get_StructField_ReturnsNestedObjectWithRealValues()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Offset"),
                JObject.FromObject(new { Left = 5, Right = 6, Top = 7, Bottom = 8 }), null, out var error);
            Assert.IsTrue(ok, error);
            _so.ApplyModifiedPropertiesWithoutUndo();

            var result = SerializedPropertyCodec.Get(Prop("Offset")) as JObject;

            Assert.IsNotNull(result);
            Assert.AreEqual(5, result["m_Left"]?.Value<int>());
        }

        // ── AnimationCurve ───────────────────────────────────────────────────

        [Test]
        public void TrySet_AnimationCurve_RoundTripsKeyframes()
        {
            var keys = new JArray(
                JObject.FromObject(new { time = 0.0, value = 0.0 }),
                JObject.FromObject(new { time = 1.0, value = 1.0, inTangent = 0.5, outTangent = 0.5 }));

            var ok = SerializedPropertyCodec.TrySet(Prop("Curve"), keys, null, out var error);

            Assert.IsTrue(ok, error);
            var curve = Prop("Curve").animationCurveValue;
            Assert.AreEqual(2, curve.length);
            Assert.AreEqual(1.0f, curve.keys[1].value);
            Assert.AreEqual(0.5f, curve.keys[1].inTangent);
        }

        [Test]
        public void TrySet_AnimationCurveEmptyArray_Fails()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Curve"), new JArray(), null, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains("non-empty", error);
        }

        // ── Gradient ─────────────────────────────────────────────────────────

        [Test]
        public void TrySet_Gradient_RoundTripsColorAndAlphaKeys()
        {
            var value = JObject.FromObject(new
            {
                colorKeys = new[]
                {
                    new { color = new[] { 1.0, 0.0, 0.0, 1.0 }, time = 0.0 },
                    new { color = new[] { 0.0, 0.0, 1.0, 1.0 }, time = 1.0 },
                },
                alphaKeys = new[] { new { alpha = 1.0, time = 0.0 }, new { alpha = 0.0, time = 1.0 } },
            });

            var ok = SerializedPropertyCodec.TrySet(Prop("Fade"), value, null, out var error);

            Assert.IsTrue(ok, error);
            var gradient = Prop("Fade").gradientValue;
            Assert.AreEqual(2, gradient.colorKeys.Length);
            Assert.AreEqual(2, gradient.alphaKeys.Length);
            Assert.AreEqual(1f, gradient.colorKeys[0].color.r, 0.001f);
        }

        [Test]
        public void TrySet_GradientSingleColorKey_FailsRatherThanSilentlyDuplicating()
        {
            // Confirmed against a real Gradient: Unity pads a single color key to two rather than
            // keeping the gradient flat at one, so this must fail loudly instead of reporting
            // success for a gradient that isn't what was actually asked for.
            var value = JObject.FromObject(new
            {
                colorKeys = new[] { new { color = new[] { 1.0, 0.0, 0.0, 1.0 }, time = 0.0 } },
                alphaKeys = new[] { new { alpha = 1.0, time = 0.0 }, new { alpha = 0.0, time = 1.0 } },
            });

            var ok = SerializedPropertyCodec.TrySet(Prop("Fade"), value, null, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains("at least 2", error);
        }

        [Test]
        public void TrySet_GradientMissingColorKeys_Fails()
        {
            var ok = SerializedPropertyCodec.TrySet(Prop("Fade"), new JObject(), null, out var error);

            Assert.IsFalse(ok);
            StringAssert.Contains("colorKeys", error);
        }

        // ── Unsupported ──────────────────────────────────────────────────────

        [Test]
        public void TrySet_NullProperty_FailsRatherThanThrows()
        {
            var ok = SerializedPropertyCodec.TrySet(null, JToken.FromObject(1), null, out var error);

            Assert.IsFalse(ok);
            Assert.IsNotEmpty(error);
        }
    }

    internal enum CodecProbeMode { Alpha, Beta, Gamma }

    /// <summary>Every SerializedPropertyType the codec adds support for, on a real component.</summary>
    internal class CodecProbe : MonoBehaviour
    {
        public CodecProbeMode Mode;
        public LayerMask Mask;
        public float[] Values;
        public RectOffset Offset = new RectOffset();
        public AnimationCurve Curve = new AnimationCurve();
        public Gradient Fade = new Gradient();
    }
}
