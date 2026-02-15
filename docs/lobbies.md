# Lobbies

EOSLobbyManager provides a high-level API for EOS lobby operations. It auto-creates as a singleton child of EOSManager -- you never need to add it to your scene manually.

## Creating a Lobby

```csharp
var lobbyManager = EOSLobbyManager.Instance;

// Create with defaults (4 players, public, voice enabled, host migration on)
var (result, lobby) = await lobbyManager.CreateLobbyAsync();
if (result == Result.Success)
{
    Debug.Log($"Lobby created! Join code: {lobby.JoinCode}");
}
```

### Create Options

```csharp
var options = new LobbyCreateOptions
{
    MaxPlayers = 8,
    IsPublic = true,
    EnableVoice = true,
    AllowHostMigration = true,
    BucketId = "v1",
    LobbyName = "My Game Room",
    GameMode = "deathmatch",
    Map = "arena_01",
    Region = "us-east"
};

var (result, lobby) = await lobbyManager.CreateLobbyAsync(options);
string joinCode = lobby.JoinCode; // e.g., "482901"
```

## Joining a Lobby

### By Join Code

```csharp
var (result, lobby) = await lobbyManager.JoinLobbyByCodeAsync("482901");
if (result == Result.Success)
{
    Debug.Log($"Joined lobby owned by {lobby.OwnerPuid}");
}
```

### By Lobby ID

```csharp
var (result, lobby) = await lobbyManager.JoinLobbyByIdAsync(lobbyId);
```

## Searching for Lobbies

### Basic Search

```csharp
var (result, lobbies) = await lobbyManager.SearchLobbiesAsync();
foreach (var lobby in lobbies)
{
    Debug.Log($"Found: {lobby.LobbyName} ({lobby.MemberCount}/{lobby.MaxMembers})");
}
```

### Filtered Search

```csharp
var options = new LobbySearchOptions()
    .WithGameMode("deathmatch")
    .WithRegion("us-east")
    .OnlyWithAvailableSlots()
    .ExcludePassworded()
    .ExcludeGamesInProgress()
    .WithMaxResults(20);

var (result, lobbies) = await lobbyManager.SearchLobbiesAsync(options);
```

### Quick Match

Find and join the first available lobby automatically:

```csharp
var (result, lobby) = await lobbyManager.QuickMatchAsync();
```

With filters (game mode, scene, custom attributes):

```csharp
var (result, lobby) = await lobbyManager.QuickMatchAsync(
    new LobbySearchOptions()
        .WithGameMode("deathmatch")
        .WithAttribute("SCENE", SceneManager.GetActiveScene().name)
        .WithAttribute("QUEUE", "ranked"));
```

### Quick Match or Host

Find an available lobby, or create one if none exist. This is the recommended way to implement "Play Now" buttons.

Basic (no filters):

```csharp
var (result, lobby, didHost) = await lobbyManager.QuickMatchOrHostAsync();
Debug.Log(didHost ? "Hosting new lobby" : "Joined existing lobby");
```

With `LobbyOptions` — one object configures both the search filters AND fallback host settings:

```csharp
var options = new LobbyOptions()
    .WithGameMode("deathmatch")
    .WithAttribute("SCENE", SceneManager.GetActiveScene().name)
    .WithAttribute("QUEUE", "ranked")
    .WithMaxPlayers(4)
    .ExcludePassworded()
    .ExcludeGamesInProgress();

var (result, lobby, didHost) = await lobbyManager.QuickMatchOrHostAsync(options);
// If no matching lobby found, hosts a new one with the same settings
```

With separate search + host options:

```csharp
var searchOptions = new LobbySearchOptions()
    .WithGameMode("deathmatch")
    .WithRegion("us-east");

var hostOptions = new LobbyCreateOptions
{
    GameMode = "deathmatch",
    Region = "us-east",
    MaxPlayers = 8,
    EnableVoice = true
};

var (result, lobby, didHost) = await lobbyManager.QuickMatchOrHostAsync(searchOptions, hostOptions);
```

### Skill-Based Matchmaking

```csharp
var options = LobbySearchOptions.ForSkillRange(playerSkill: 1500, range: 200);
var (result, lobbies) = await lobbyManager.SearchLobbiesAsync(options);
```

### Search by Member

Find all public lobbies containing a specific player:

```csharp
var (result, lobbies) = await lobbyManager.SearchByMemberAsync(friendPuid);
```

## Leaving a Lobby

```csharp
await lobbyManager.LeaveLobbyAsync();
```

For cleanup during application quit (where async will not complete in time):

```csharp
lobbyManager.LeaveLobbySync();
```

## Lobby Attributes

Lobby attributes are public key-value pairs visible to all members and searchable by other players.

### Setting Lobby Attributes (Owner Only)

```csharp
await lobbyManager.SetLobbyAttributeAsync(lobby.LobbyId, "MAP", "arena_02");
await lobbyManager.SetLobbyAttributeAsync(lobby.LobbyId, "IN_PROGRESS", "true");
```

### Standard Attribute Keys

The `LobbyAttributes` class defines standard keys:

| Key | Description |
|---|---|
| `JOIN_CODE` | Numeric join code (auto-set on create) |
| `LOBBY_NAME` | Human-readable lobby name |
| `GAME_MODE` | Game mode (e.g., "deathmatch", "coop") |
| `MAP` | Current map name |
| `REGION` | Server region |
| `VERSION` | Game version string |
| `PASSWORD` | Password hash (presence = password-protected) |
| `SKILL_LEVEL` | Skill/MMR for matchmaking |
| `IN_PROGRESS` | Whether game is in progress ("true"/"false") |
| `HOST_PLATFORM` | Host's platform ("WIN", "MAC", "AND", etc.) |

### Reading Attributes from LobbyData

```csharp
var lobby = lobbyManager.CurrentLobby;
string gameMode = lobby.GameMode;       // Typed accessor
string map = lobby.Map;                 // Typed accessor
string custom = lobby.GetAttribute("MY_KEY"); // Custom attribute
bool hasPassword = lobby.IsPasswordProtected; // Computed property
```

## Member Attributes

Member attributes are per-player key-value pairs on the local player.

```csharp
await lobbyManager.SetMemberAttributeAsync("READY", "true");
await lobbyManager.SetMemberAttributeAsync("TEAM", "blue");
```

## Text Chat

Chat messages are sent via member attributes, which means they survive host migration (unlike P2P messages).

```csharp
// Send a chat message
await lobbyManager.SendChatMessageAsync("Hello everyone!");
```

Listen for chat messages:

```csharp
lobbyManager.OnMemberAttributeUpdated += (puid, key, value) =>
{
    if (key == "CHAT")
    {
        // value format: "timestamp:message"
        int colonIndex = value.IndexOf(':');
        string message = value.Substring(colonIndex + 1);
        Debug.Log($"[{puid}]: {message}");
    }
};
```

## Events

```csharp
lobbyManager.OnLobbyJoined += (LobbyData lobby) => {
    Debug.Log($"Joined lobby {lobby.JoinCode}");
};

lobbyManager.OnLobbyLeft += () => {
    Debug.Log("Left lobby");
};

lobbyManager.OnMemberJoined += (LobbyMemberData member) => {
    Debug.Log($"Player joined: {member.Puid}");
};

lobbyManager.OnMemberLeft += (string puid) => {
    Debug.Log($"Player left: {puid}");
};

lobbyManager.OnOwnerChanged += (string newOwnerPuid) => {
    Debug.Log($"New host: {newOwnerPuid}");
};

lobbyManager.OnLobbyUpdated += (LobbyData lobby) => {
    Debug.Log($"Lobby updated: {lobby.MemberCount} members");
};
```

## Kicking Members

The lobby owner can remove players:

```csharp
if (lobbyManager.IsOwner)
{
    await lobbyManager.KickMemberAsync(targetPuid);
}
```

## Properties

| Property | Type | Description |
|---|---|---|
| `CurrentLobby` | `LobbyData` | The lobby you are currently in |
| `IsInLobby` | `bool` | Whether you are in a lobby |
| `IsOwner` | `bool` | Whether you are the lobby owner (host) |
| `JoinCodeLength` | `int` | Length of generated join codes (4-8, default 6) |

## Password-Protected Lobbies

```csharp
// Create a password-protected lobby
var options = new LobbyCreateOptions
{
    Password = "secret123"
};
var (result, lobby) = await lobbyManager.CreateLobbyAsync(options);
```

Passwords are stored as SHA256 hashes in lobby attributes -- the actual password is never sent over the network.

## Unified LobbyOptions

`LobbyOptions` is a single class that configures both create and search operations. Fields that don't apply to a given operation are ignored. This is the easiest way to set up matchmaking:

```csharp
// One options object, used for everything
var options = new LobbyOptions()
    .WithGameMode("coop")
    .WithMap("forest")
    .WithMaxPlayers(4)
    .WithAttribute("QUEUE", "casual")
    .WithVoice()
    .ExcludePassworded()
    .ExcludeGamesInProgress();

// Search uses GameMode, Map, QUEUE as filters
var (searchResult, lobbies) = await lobbyManager.SearchLobbiesAsync(options.ToSearchOptions());

// Create uses GameMode, Map, MaxPlayers, Voice, QUEUE as lobby settings
var (createResult, lobby) = await lobbyManager.CreateLobbyAsync(options.ToCreateOptions());

// Or just use QuickMatchOrHost — it does both automatically
var (result, lobby2, didHost) = await lobbyManager.QuickMatchOrHostAsync(options);
```

Factory presets:

```csharp
var quickMatch = LobbyOptions.QuickMatch();
var ranked = LobbyOptions.ForSkillRange(playerSkill: 1500, range: 200);
var coop = LobbyOptions.ForGameMode("coop");
```

## Crossplay Filtering

```csharp
// Desktop-only lobby
var options = new LobbyCreateOptions
{
    AllowCrossplay = false
};

// Search for same-platform lobbies
var searchOptions = new LobbySearchOptions().SamePlatformOnly();
```
