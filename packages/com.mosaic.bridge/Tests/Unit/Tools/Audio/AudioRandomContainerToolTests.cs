using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Audio;

namespace Mosaic.Bridge.Tests.Unit.Tools.Audio
{
    // O4 §4.4: audio/create-random-container — footstep/impact variation via Unity 6's
    // AudioRandomContainer, whose entire authoring surface turned out to be internal (confirmed
    // via a live reflection probe; the doc's "public API, internal ctor" undersold the real gap).
    [TestFixture]
    [Category("Audio")]
    public class AudioRandomContainerToolTests
    {
        private const string Clip1Path = "Assets/MosaicRandomContainerClip1.wav";
        private const string Clip2Path = "Assets/MosaicRandomContainerClip2.wav";
        private const string ContainerPath = "Assets/MosaicRandomContainerTest.asset";
        private GameObject _testGo;

        [SetUp]
        public void SetUp()
        {
            AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = Clip1Path, Waveform = "tone", DurationSeconds = 0.1f, SampleRate = 8000,
            });
            AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = Clip2Path, Waveform = "click", DurationSeconds = 0.05f, SampleRate = 8000,
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null) Object.DestroyImmediate(_testGo);
            if (AssetDatabase.AssetPathExists(ContainerPath)) AssetDatabase.DeleteAsset(ContainerPath);
            if (AssetDatabase.AssetPathExists(Clip1Path)) AssetDatabase.DeleteAsset(Clip1Path);
            if (AssetDatabase.AssetPathExists(Clip2Path)) AssetDatabase.DeleteAsset(Clip2Path);
        }

        [Test]
        public void Create_WithTwoClips_ProducesLoadableResource()
        {
            var result = AudioCreateRandomContainerTool.Execute(new AudioCreateRandomContainerParams
            {
                AssetPath = ContainerPath, ClipPaths = new[] { Clip1Path, Clip2Path },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Data.ElementCount);

            var resource = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioResource>(ContainerPath);
            Assert.IsNotNull(resource);
        }

        [Test]
        public void Create_NoClips_ReturnsInvalidParam()
        {
            var result = AudioCreateRandomContainerTool.Execute(new AudioCreateRandomContainerParams
            {
                AssetPath = ContainerPath, ClipPaths = new string[0],
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_MissingClip_ReturnsError()
        {
            var result = AudioCreateRandomContainerTool.Execute(new AudioCreateRandomContainerParams
            {
                AssetPath = ContainerPath, ClipPaths = new[] { "Assets/DoesNotExist.wav" },
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Create_AlreadyExists_ReturnsConflict()
        {
            AudioCreateRandomContainerTool.Execute(new AudioCreateRandomContainerParams
            {
                AssetPath = ContainerPath, ClipPaths = new[] { Clip1Path },
            });

            var result = AudioCreateRandomContainerTool.Execute(new AudioCreateRandomContainerParams
            {
                AssetPath = ContainerPath, ClipPaths = new[] { Clip1Path },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("CONFLICT", result.ErrorCode);
        }

        [Test]
        public void SetSource_ResourcePath_AssignsToAudioSource()
        {
            AudioCreateRandomContainerTool.Execute(new AudioCreateRandomContainerParams
            {
                AssetPath = ContainerPath, ClipPaths = new[] { Clip1Path, Clip2Path },
            });

            _testGo = new GameObject("AudioTest_Resource");
            _testGo.AddComponent<AudioSource>();

            var result = AudioSetSourceTool.Execute(new AudioSetSourceParams
            {
                Name = "AudioTest_Resource", ResourcePath = ContainerPath,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.ResourceName);
            Assert.IsNotNull(_testGo.GetComponent<AudioSource>().resource);
        }
    }
}
