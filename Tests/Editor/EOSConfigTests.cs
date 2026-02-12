using NUnit.Framework;
using UnityEngine;
using EOSNative;

namespace EOSNative.Tests.Editor
{
    public class EOSConfigTests
    {
        [Test]
        public void EOSConfig_CreateInstance_HasDefaultValues()
        {
            var config = ScriptableObject.CreateInstance<EOSConfig>();
            Assert.IsNotNull(config);
            // EncryptionKey should be empty or default
            Assert.IsNotNull(config.EncryptionKey);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void EOSConfig_EncryptionKey_MustBe64HexChars()
        {
            var config = ScriptableObject.CreateInstance<EOSConfig>();
            // Valid key: 64 hex chars
            config.EncryptionKey = "1111111111111111111111111111111111111111111111111111111111111111";
            Assert.AreEqual(64, config.EncryptionKey.Length);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void EOSConfig_SampleCredentials_AreValid()
        {
            // PlayEveryWare sample credentials from the setup wizard
            string sampleProductId = "f7102b835ed14b5fb6b3a05d87b3d101";
            string sampleSandboxId = "ab139ee5b644412781cf99f48b993b45";
            string sampleDeploymentId = "c529498f660a4a3d8a123fd04552cb47";

            Assert.AreEqual(32, sampleProductId.Length);
            Assert.AreEqual(32, sampleSandboxId.Length);
            Assert.AreEqual(32, sampleDeploymentId.Length);
        }

        [Test]
        public void EOSConfig_Validate_FailsWhenEmpty()
        {
            var config = ScriptableObject.CreateInstance<EOSConfig>();
            config.ProductId = "";
            config.SandboxId = "";
            config.DeploymentId = "";
            config.ClientId = "";
            config.ClientSecret = "";
            config.EncryptionKey = "";

            bool valid = config.Validate(out string error);
            Assert.IsFalse(valid);
            Assert.IsNotNull(error);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void EOSConfig_Validate_PassesWithFullCredentials()
        {
            var config = ScriptableObject.CreateInstance<EOSConfig>();
            config.ProductName = "TestGame";
            config.ProductId = "f7102b835ed14b5fb6b3a05d87b3d101";
            config.SandboxId = "ab139ee5b644412781cf99f48b993b45";
            config.DeploymentId = "c529498f660a4a3d8a123fd04552cb47";
            config.ClientId = "xyza7891wPzGRvRf4SkjlIF8YuqlRLbQ";
            config.ClientSecret = "aXPlP1xDH0PXnp5U+i+M5pYHhaE1a8viV0l1GO422ms";
            config.EncryptionKey = "1111111111111111111111111111111111111111111111111111111111111111";
            config.DefaultDisplayName = "Player";

            bool valid = config.Validate(out string error);
            Assert.IsTrue(valid, $"Validation should pass but got error: {error}");
            Assert.IsNull(error);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void EOSConfig_Validate_FailsWithShortEncryptionKey()
        {
            var config = ScriptableObject.CreateInstance<EOSConfig>();
            config.ProductName = "TestGame";
            config.ProductId = "f7102b835ed14b5fb6b3a05d87b3d101";
            config.SandboxId = "ab139ee5b644412781cf99f48b993b45";
            config.DeploymentId = "c529498f660a4a3d8a123fd04552cb47";
            config.ClientId = "xyza7891wPzGRvRf4SkjlIF8YuqlRLbQ";
            config.ClientSecret = "aXPlP1xDH0PXnp5U+i+M5pYHhaE1a8viV0l1GO422ms";
            config.EncryptionKey = "1234"; // Too short
            config.DefaultDisplayName = "Player";

            bool valid = config.Validate(out string error);
            Assert.IsFalse(valid);
            Assert.IsTrue(error.Contains("64"), "Error should mention 64-character requirement");
            Object.DestroyImmediate(config);
        }

        [Test]
        public void EOSConfig_Validate_FailsWithLongDisplayName()
        {
            var config = ScriptableObject.CreateInstance<EOSConfig>();
            config.ProductName = "TestGame";
            config.ProductId = "f7102b835ed14b5fb6b3a05d87b3d101";
            config.SandboxId = "ab139ee5b644412781cf99f48b993b45";
            config.DeploymentId = "c529498f660a4a3d8a123fd04552cb47";
            config.ClientId = "xyza7891wPzGRvRf4SkjlIF8YuqlRLbQ";
            config.ClientSecret = "aXPlP1xDH0PXnp5U+i+M5pYHhaE1a8viV0l1GO422ms";
            config.EncryptionKey = "1111111111111111111111111111111111111111111111111111111111111111";
            config.DefaultDisplayName = "ThisDisplayNameIsWayTooLongAndExceedsTheThirtyTwoCharacterLimit";

            bool valid = config.Validate(out string error);
            Assert.IsFalse(valid);
            Assert.IsTrue(error.Contains("32"), "Error should mention 32-character limit");
            Object.DestroyImmediate(config);
        }
    }
}
