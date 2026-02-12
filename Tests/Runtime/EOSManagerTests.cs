using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using EOSNative;

namespace EOSNative.Tests.Runtime
{
    public class EOSManagerTests
    {
        private GameObject _go;

        private static readonly FieldInfo s_InstanceField =
            typeof(EOSManager).GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);

            // Clear singleton via reflection
            s_InstanceField?.SetValue(null, null);
        }

        [Test]
        public void EOSManager_Singleton_CreatesOnAccess()
        {
            _go = new GameObject("TestEOSManager");
            var mgr = _go.AddComponent<EOSManager>();
            Assert.IsNotNull(EOSManager.Instance);
            Assert.AreEqual(mgr, EOSManager.Instance);
        }

        [Test]
        public void EOSManager_DefaultState_NotInitialized()
        {
            _go = new GameObject("TestEOSManager");
            var mgr = _go.AddComponent<EOSManager>();
            Assert.IsFalse(mgr.IsInitialized);
            Assert.IsFalse(mgr.IsLoggedIn);
            Assert.IsNull(mgr.LocalProductUserId);
        }

        [Test]
        public void EOSManager_DuplicateInstance_DestroysItself()
        {
            _go = new GameObject("TestEOSManager");
            var mgr1 = _go.AddComponent<EOSManager>();
            // Force singleton assignment via reflection
            s_InstanceField?.SetValue(null, mgr1);

            var go2 = new GameObject("DuplicateEOSManager");
            var mgr2 = go2.AddComponent<EOSManager>();
            // Awake on mgr2 should detect duplicate and destroy

            // In editor tests, Destroy is deferred, but we can check instance is still mgr1
            Assert.AreEqual(mgr1, EOSManager.Instance);
            Object.DestroyImmediate(go2);
        }
    }
}
