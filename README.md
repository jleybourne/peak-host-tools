# PEAK host tools

Two small BepInEx 5 mods for [PEAK](https://store.steampowered.com/app/3527290/PEAK/). They are independent: install either or both. Only the host needs them.

## PEAK More Players (`PeakMorePlayers`)

Raises the lobby cap past 4 (default 8, configurable up to 30 in `BepInEx/config/au.leybourne.peakmoreplayers.cfg`).
It patches `NetworkingUtilities.MAX_PLAYERS` and `NetworkingUtilities.HostRoomOptions`.

Large lobbies are not fully handled yet: end screen, kiosk, campfire marshmallows and backpacks, and voice are untouched.

## PEAK Host Menu (`PeakHostMenu`)

In-game window, toggled with F3 (rebindable in `BepInEx/config/au.leybourne.peakhostmenu.cfg`).

- **Lobby:** apply a player cap to the current room, lock or unlock joins.
- **Me:** god mode, infinite stamina, auto-clear afflictions, speed and jump multipliers, heal, revive, die.
- **Players:** go to, bring, revive, heal, make host, kick, plus bring/revive/heal everyone.
- **Items:** filterable list of every item, spawn one in front of a chosen player.

Revive, heal, warp and item spawning for other players use network calls inferred from the game's method names and are not yet tested in-game.

## Building

1. Install BepInEx 5.4.23.3 into your PEAK folder.
2. `dotnet build -c Release` in a mod folder. Pass `-p:GameDir="C:\path\to\PEAK\"` if PEAK is not at `D:\Games\Steam\steamapps\common\PEAK\`.
3. The build copies the DLL into `BepInEx/plugins/<mod>/`.

Written against the game's own assemblies. PEAK Unlimited (GPL-3.0) was read for reference, but no code was copied.
