using NUnit.Framework;
using EOSNative.Lobbies;

namespace EOSNative.Tests.Editor
{
    public class EOSLobbyChatManagerTests
    {
        [Test]
        public void GenerateNameFromPuid_IsDeterministic()
        {
            string puid = "test-puid-12345";
            string name1 = EOSLobbyChatManager.GenerateNameFromPuid(puid);
            string name2 = EOSLobbyChatManager.GenerateNameFromPuid(puid);
            Assert.AreEqual(name1, name2, "Same PUID should always produce same name");
        }

        [Test]
        public void GenerateNameFromPuid_DifferentPuids_DifferentNames()
        {
            string name1 = EOSLobbyChatManager.GenerateNameFromPuid("puid-aaa");
            string name2 = EOSLobbyChatManager.GenerateNameFromPuid("puid-bbb");
            Assert.AreNotEqual(name1, name2, "Different PUIDs should produce different names");
        }

        [Test]
        public void GenerateNameFromPuid_NullPuid_ReturnsFallback()
        {
            string name = EOSLobbyChatManager.GenerateNameFromPuid(null);
            Assert.IsTrue(name.StartsWith("Player"), "Null PUID should return Player+number fallback");
        }

        [Test]
        public void GenerateNameFromPuid_EmptyPuid_ReturnsFallback()
        {
            string name = EOSLobbyChatManager.GenerateNameFromPuid("");
            Assert.IsTrue(name.StartsWith("Player"), "Empty PUID should return Player+number fallback");
        }

        [Test]
        public void ChatMessage_ToString_FormatsCorrectly()
        {
            var msg = new ChatMessage
            {
                SenderPuid = "puid",
                SenderName = "TestUser",
                Message = "Hello world",
                Timestamp = 1700000000000, // some fixed timestamp
                IsSystem = false
            };
            string str = msg.ToString();
            Assert.IsTrue(str.Contains("TestUser"), "Should contain sender name");
            Assert.IsTrue(str.Contains("Hello world"), "Should contain message");
        }

        [Test]
        public void ChatMessage_SystemMessage_FormatsWithAsterisk()
        {
            var msg = new ChatMessage
            {
                SenderName = "System",
                Message = "Player joined",
                Timestamp = 1700000000000,
                IsSystem = true
            };
            string str = msg.ToString();
            Assert.IsTrue(str.Contains("*"), "System messages should contain asterisk");
            Assert.IsTrue(str.Contains("Player joined"), "Should contain message");
        }
    }
}
