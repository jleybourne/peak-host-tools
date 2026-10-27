# PEAK host tools @jleybourne

Three small BepInEx 5 mods for [PEAK](https://store.steampowered.com/app/3527290/PEAK/). They are independent: install any of them. Only the host needs them.

## PEAK More Players (`PeakMorePlayers`)

Raises the lobby cap past 4. Press **F2** for a small window with a slider (1 to 30), a Save button, and an "Apply to current room" button for the host. The cap is also stored in `BepInEx/config/au.leybourne.peakmoreplayers.cfg` (default 8).
It patches `NetworkingUtilities.MAX_PLAYERS` and `NetworkingUtilities.HostRoomOptions`.

Large lobbies are not fully handled yet: end screen, kiosk, campfire marshmallows and backpacks, and voice are untouched.

## PEAK Host Menu (`PeakHostMenu`)

In-game window (scales to your resolution, with a UI size slider), toggled with **F3** (rebindable in `BepInEx/config/au.leybourne.peakhostmenu.cfg`). Eight tabs:

- **Lobby:** apply a player cap to the current room, lock or unlock joins, player ESP, coordinates overlay.
- **Me:** god mode, infinite stamina, no afflictions, no fall damage, no carry weight, no ragdoll, unlimited item uses, per-status immunity, fly (F4, WASD + Space/Left Ctrl), speed, jump, climb, vine and rope speed, heal, revive, die. Every switch can have its own hotkey, rebindable in the menu. Settings are remembered.
- **Players:** go to, bring, revive, heal, make host, kick, stamina and affliction bars, give any status, spawn the Scoutmaster, plus bring, revive, heal and status for everyone.
- **Fun:** freeze, make fall or jump, pass out, wake, stop climbing, fling, eruption, coconut on head, banana peel, beehive, dynamite. Pick one player or everyone.
- **Teleport:** type-in coordinates, to where you look, random player, swap two players, three waypoints, biomes, teleport to ping, campfires.
- **World:** time of day, fog, wind, gravity and game speed (this computer only), light or put out campfires, luggage ESP and open nearby or all, freeze, spin, cook or clear loose items, force win, peak timer, call the helicopter.
- **Items:** your three inventory slots (recharge, remove), then a filterable list of every item to spawn at a player, where you look, or into a hand.
- **Mods:** every loaded BepInEx mod with its version and hotkeys, read from each mod's config.

Everything that touches other players uses the game's own network calls. Revive, heal, warp and item spawning on other players are untested with a second player. Fling strength, the helicopter, the luggage open call and the Scoutmaster spawn are the least certain.

Not included because the game has no networked call for them or the information is missing: invisible players, resizing players, spawning luggage by size, and changing the map.

## PEAK Probe (`PeakProbe`)

Read-only diagnostics for developing the other mods. It logs every RPC the game sends and receives to `BepInEx/PeakProbe/rpc.log`, and F6 writes a dump (RPC method list, item list with a check that each `0_Items/<name>` prefab loads, character fields, statuses, campfires) to `BepInEx/PeakProbe/dump-*.txt`. It changes nothing in the game.

## Building

1. Install BepInEx 5.4.23.3 into your PEAK folder.
2. `dotnet build -c Release` in a mod folder. Pass `-p:GameDir="C:\path\to\PEAK\"` if PEAK is not at `D:\Games\Steam\steamapps\common\PEAK\`.
3. The build copies the DLL into `BepInEx/plugins/<mod>/`.

`Shared/Ui.cs` is the UI kit used by both windows. Written against the game's own assemblies. PEAK Unlimited (GPL-3.0) and TheLocalAdmin/PeakMod (MIT) were read for reference, but no code was copied.
