using System.Collections.Generic;
using NUnit.Framework;
using EOSNative.Lobbies;

namespace EOSNative.Tests.Editor
{
    public class LobbyDataTests
    {
        #region IsGhost

        [Test]
        public void IsGhost_ZeroMembers_ReturnsTrue()
        {
            var lobby = new LobbyData
            {
                LobbyId = "test-lobby",
                OwnerPuid = "some-owner",
                MemberCount = 0,
                MaxMembers = 4
            };
            Assert.IsTrue(lobby.IsGhost);
        }

        [Test]
        public void IsGhost_NullOwner_ReturnsTrue()
        {
            var lobby = new LobbyData
            {
                LobbyId = "test-lobby",
                OwnerPuid = null,
                MemberCount = 2,
                MaxMembers = 4
            };
            Assert.IsTrue(lobby.IsGhost);
        }

        [Test]
        public void IsGhost_EmptyOwner_ReturnsTrue()
        {
            var lobby = new LobbyData
            {
                LobbyId = "test-lobby",
                OwnerPuid = "",
                MemberCount = 2,
                MaxMembers = 4
            };
            Assert.IsTrue(lobby.IsGhost);
        }

        [Test]
        public void IsGhost_ValidLobby_ReturnsFalse()
        {
            var lobby = new LobbyData
            {
                LobbyId = "test-lobby",
                OwnerPuid = "valid-owner-puid",
                MemberCount = 2,
                MaxMembers = 4
            };
            Assert.IsFalse(lobby.IsGhost);
        }

        [Test]
        public void IsGhost_ZeroMembersAndNullOwner_ReturnsTrue()
        {
            var lobby = new LobbyData
            {
                LobbyId = "test-lobby",
                OwnerPuid = null,
                MemberCount = 0,
                MaxMembers = 4
            };
            Assert.IsTrue(lobby.IsGhost);
        }

        #endregion

        #region IsValid

        [Test]
        public void IsValid_ValidLobby_ReturnsTrue()
        {
            var lobby = new LobbyData
            {
                LobbyId = "test-lobby-id",
                OwnerPuid = "valid-owner"
            };
            Assert.IsTrue(lobby.IsValid);
        }

        [Test]
        public void IsValid_NullLobbyId_ReturnsFalse()
        {
            var lobby = new LobbyData
            {
                LobbyId = null,
                OwnerPuid = "valid-owner"
            };
            Assert.IsFalse(lobby.IsValid);
        }

        [Test]
        public void IsValid_EmptyOwnerPuid_ReturnsFalse()
        {
            var lobby = new LobbyData
            {
                LobbyId = "test-lobby-id",
                OwnerPuid = ""
            };
            Assert.IsFalse(lobby.IsValid);
        }

        #endregion

        #region CanJoin

        [Test]
        public void CanJoin_AvailableSlots_ReturnsTrue()
        {
            var lobby = new LobbyData { AvailableSlots = 2 };
            Assert.IsTrue(lobby.CanJoin);
        }

        [Test]
        public void CanJoin_NoSlots_ReturnsFalse()
        {
            var lobby = new LobbyData { AvailableSlots = 0 };
            Assert.IsFalse(lobby.CanJoin);
        }

        #endregion

        #region Typed Attribute Accessors

        [Test]
        public void GameMode_ReturnsAttributeValue()
        {
            var lobby = new LobbyData
            {
                Attributes = new Dictionary<string, string>
                {
                    { LobbyAttributes.GAME_MODE, "deathmatch" }
                }
            };
            Assert.AreEqual("deathmatch", lobby.GameMode);
        }

        [Test]
        public void IsPasswordProtected_WithPassword_ReturnsTrue()
        {
            var lobby = new LobbyData
            {
                Attributes = new Dictionary<string, string>
                {
                    { LobbyAttributes.PASSWORD, "some-hash" }
                }
            };
            Assert.IsTrue(lobby.IsPasswordProtected);
        }

        [Test]
        public void IsPasswordProtected_NoPassword_ReturnsFalse()
        {
            var lobby = new LobbyData
            {
                Attributes = new Dictionary<string, string>()
            };
            Assert.IsFalse(lobby.IsPasswordProtected);
        }

        [Test]
        public void IsInProgress_True()
        {
            var lobby = new LobbyData
            {
                Attributes = new Dictionary<string, string>
                {
                    { LobbyAttributes.IN_PROGRESS, "true" }
                }
            };
            Assert.IsTrue(lobby.IsInProgress);
        }

        [Test]
        public void IsInProgress_False()
        {
            var lobby = new LobbyData
            {
                Attributes = new Dictionary<string, string>
                {
                    { LobbyAttributes.IN_PROGRESS, "false" }
                }
            };
            Assert.IsFalse(lobby.IsInProgress);
        }

        [Test]
        public void GetAttribute_NullAttributes_ReturnsNull()
        {
            var lobby = new LobbyData { Attributes = null };
            Assert.IsNull(lobby.GetAttribute("ANYTHING"));
        }

        #endregion

        #region Default Struct

        [Test]
        public void Default_IsGhost()
        {
            var lobby = default(LobbyData);
            Assert.IsTrue(lobby.IsGhost, "Default LobbyData should be ghost (0 members, null owner)");
        }

        [Test]
        public void Default_IsNotValid()
        {
            var lobby = default(LobbyData);
            Assert.IsFalse(lobby.IsValid);
        }

        [Test]
        public void Default_CannotJoin()
        {
            var lobby = default(LobbyData);
            Assert.IsFalse(lobby.CanJoin);
        }

        #endregion
    }
}
