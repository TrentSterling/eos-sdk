using NUnit.Framework;
using UnityEngine;
using EOSNative;

namespace EOSNative.Tests.Editor
{
    public class EOSPlayerRegistryTests
    {
        private GameObject _go;
        private EOSPlayerRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestRegistry");
            _registry = _go.AddComponent<EOSPlayerRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void RegisterPlayer_StoresName()
        {
            _registry.RegisterPlayer("test-puid-001", "TestPlayer");
            var name = _registry.GetPlayerName("test-puid-001");
            Assert.AreEqual("TestPlayer", name);
        }

        [Test]
        public void GetPlayerName_UnknownPuid_ReturnsNull()
        {
            var name = _registry.GetPlayerName("nonexistent-puid");
            Assert.IsTrue(string.IsNullOrEmpty(name));
        }

        [Test]
        public void RegisterPlayer_OverwritesExisting()
        {
            _registry.RegisterPlayer("puid-1", "OldName");
            _registry.RegisterPlayer("puid-1", "NewName");
            Assert.AreEqual("NewName", _registry.GetPlayerName("puid-1"));
        }

        [Test]
        public void GetOrGenerateName_UnknownPuid_ReturnsNonEmpty()
        {
            // GetOrGenerateName should always return something usable
            var name = _registry.GetOrGenerateName("some-random-puid");
            Assert.IsFalse(string.IsNullOrEmpty(name));
        }

        [Test]
        public void GenerateNameFromPuid_IsDeterministic()
        {
            string puid = "test-puid-12345";
            string name1 = EOSPlayerRegistry.GenerateNameFromPuid(puid);
            string name2 = EOSPlayerRegistry.GenerateNameFromPuid(puid);
            Assert.AreEqual(name1, name2, "Same PUID should always produce same name");
        }

        [Test]
        public void GenerateNameFromPuid_DifferentPuids_DifferentNames()
        {
            string name1 = EOSPlayerRegistry.GenerateNameFromPuid("puid-aaa");
            string name2 = EOSPlayerRegistry.GenerateNameFromPuid("puid-bbb");
            Assert.AreNotEqual(name1, name2, "Different PUIDs should produce different names");
        }

        [Test]
        public void HasPlayer_ReturnsTrueAfterRegister()
        {
            _registry.RegisterPlayer("puid-check", "CheckName");
            Assert.IsTrue(_registry.HasPlayer("puid-check"));
        }

        [Test]
        public void HasPlayer_ReturnsFalseForUnknown()
        {
            Assert.IsFalse(_registry.HasPlayer("unknown-puid"));
        }

        [Test]
        public void CachedPlayerCount_IncrementsOnRegister()
        {
            int before = _registry.CachedPlayerCount;
            _registry.RegisterPlayer("puid-count-test", "CountTest");
            Assert.AreEqual(before + 1, _registry.CachedPlayerCount);
        }
    }
}
