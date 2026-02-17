using NUnit.Framework;
using EOSNative.Lobbies;

namespace EOSNative.Tests.Editor
{
    public class LobbySearchOptionsTests
    {
        #region Defaults

        [Test]
        public void Default_OnlyAvailable_IsTrue()
        {
            var options = new LobbySearchOptions();
            Assert.IsTrue(options.OnlyAvailable, "OnlyAvailable should default to true");
        }

        [Test]
        public void Default_MaxResults_Is10()
        {
            var options = new LobbySearchOptions();
            Assert.AreEqual(10u, options.MaxResults);
        }

        [Test]
        public void Default_ExcludePasswordProtected_IsFalse()
        {
            var options = new LobbySearchOptions();
            Assert.IsFalse(options.ExcludePasswordProtected);
        }

        [Test]
        public void Default_ExcludeInProgress_IsFalse()
        {
            var options = new LobbySearchOptions();
            Assert.IsFalse(options.ExcludeInProgress);
        }

        #endregion

        #region Fluent Builders

        [Test]
        public void OnlyWithAvailableSlots_SetsFlag()
        {
            var options = new LobbySearchOptions().OnlyWithAvailableSlots(true);
            Assert.IsTrue(options.OnlyAvailable);
        }

        [Test]
        public void OnlyWithAvailableSlots_False_ClearsFlag()
        {
            var options = new LobbySearchOptions().OnlyWithAvailableSlots(false);
            Assert.IsFalse(options.OnlyAvailable);
        }

        [Test]
        public void ExcludeFull_SetsOnlyAvailable()
        {
            var options = new LobbySearchOptions { OnlyAvailable = false };
            options.ExcludeFull();
            Assert.IsTrue(options.OnlyAvailable);
        }

        [Test]
        public void WithMaxResults_SetsValue()
        {
            var options = new LobbySearchOptions().WithMaxResults(50);
            Assert.AreEqual(50u, options.MaxResults);
        }

        [Test]
        public void WithGameMode_SetsAttribute()
        {
            var options = new LobbySearchOptions().WithGameMode("deathmatch");
            Assert.IsNotNull(options.Filters);
            Assert.AreEqual("deathmatch", options.Filters[LobbyAttributes.GAME_MODE]);
        }

        [Test]
        public void WithAttribute_AddsToFilters()
        {
            var options = new LobbySearchOptions()
                .WithAttribute("CUSTOM_KEY", "custom_value");
            Assert.IsNotNull(options.Filters);
            Assert.AreEqual("custom_value", options.Filters["CUSTOM_KEY"]);
        }

        [Test]
        public void ChainedBuilders_AllApply()
        {
            var options = new LobbySearchOptions()
                .WithGameMode("coop")
                .WithMaxResults(25)
                .OnlyWithAvailableSlots(true)
                .ExcludeGamesInProgress();

            Assert.AreEqual(25u, options.MaxResults);
            Assert.IsTrue(options.OnlyAvailable);
            Assert.IsTrue(options.ExcludeInProgress);
            Assert.AreEqual("coop", options.Filters[LobbyAttributes.GAME_MODE]);
        }

        #endregion

        #region QuickMatch Factory

        [Test]
        public void QuickMatch_MaxResults_Is50()
        {
            var options = LobbySearchOptions.QuickMatch();
            Assert.AreEqual(50u, options.MaxResults);
        }

        [Test]
        public void QuickMatch_OnlyAvailable_IsTrue()
        {
            var options = LobbySearchOptions.QuickMatch();
            Assert.IsTrue(options.OnlyAvailable);
        }

        #endregion

        #region LobbyOptions Conversion

        [Test]
        public void LobbyOptions_ToSearchOptions_PreservesOnlyAvailable()
        {
            var lobbyOptions = new LobbyOptions { OnlyAvailable = true };
            var searchOptions = lobbyOptions.ToSearchOptions();
            Assert.IsTrue(searchOptions.OnlyAvailable);
        }

        [Test]
        public void LobbyOptions_ToSearchOptions_PreservesMaxResults()
        {
            var lobbyOptions = new LobbyOptions().WithMaxResults(42);
            var searchOptions = lobbyOptions.ToSearchOptions();
            Assert.AreEqual(42u, searchOptions.MaxResults);
        }

        [Test]
        public void LobbyOptions_ExcludeFull_Sets()
        {
            var lobbyOptions = new LobbyOptions();
            lobbyOptions.ExcludeFull();
            Assert.IsTrue(lobbyOptions.OnlyAvailable);
        }

        [Test]
        public void LobbyOptions_IncludeFull_Clears()
        {
            var lobbyOptions = new LobbyOptions();
            lobbyOptions.IncludeFull();
            Assert.IsFalse(lobbyOptions.OnlyAvailable);
        }

        #endregion
    }
}
