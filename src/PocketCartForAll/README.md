# PocketCartForAll

Companion for **[PocketCartPlus](https://thunderstore.io/c/repo/p/darmuh/PocketCartPlus/)** by darmuh. It shares PocketCartPlus's **"Pocket C.A.R.T. Upgrade - Keep Items"** with the whole lobby. Once anyone has consumed the upgrade (or always, if the host picks that), **every player** can pocket the POCKET C.A.R.T. and keep the items inside it.

This mod does not include, copy or change any PocketCartPlus files. It adjusts PocketCartPlus's checks at runtime and only works with PocketCartPlus installed.

## What it does

- Normally only the player who consumed the Keep Items upgrade box gets its effect. With this mod, everyone in the lobby gets it, at the **highest upgrade level anyone currently connected owns**.
- It works across levels and for late joiners, because the level is recalculated every time PocketCartPlus checks it. It uses the upgrade data the host already syncs.
- Nothing is written to your save. PocketCartPlus's own per-player flag is restored right after each check.

## Requirements

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)
- [darmuh-PocketCartPlus](https://thunderstore.io/c/repo/p/darmuh/PocketCartPlus/) 0.6.1 or newer (PocketCartPlus itself must be installed by **all** players).

## Who needs it (host-controlled)

- **The host must have it.** The host's `Enabled` and `Mode` settings apply to the whole lobby. The host publishes them through the Photon room, and guests running the mod follow them.
- **If the host does not run it, nothing changes.** Guests who have it do nothing on their own, and Keep Items stays per-player, exactly like plain PocketCartPlus.
- **Recommended: everyone installs it.** Guests who don't have it are covered by **HostAssist**: the host sends them the shared level with PocketCartPlus's own `ReceiveItemsUpgrade` message when they spawn in each level and right after someone consumes the upgrade.

## Config (`BepInEx/config/MrGlim.PocketCartForAll.cfg`)

| Section / Key | Default | Description |
| --- | --- | --- |
| General / `Enabled` | `true` | Master switch. In multiplayer only the host's value matters. |
| General / `Mode` | `AfterAnyoneConsumed` | `AfterAnyoneConsumed`: shared once at least one connected player has consumed the upgrade box, so buying it still matters. `Always`: everyone has at least level 1 without buying anything. The host's value is used. |
| Multiplayer / `HostAssist` | `true` | Host only. Pushes the shared level to guests with PocketCartPlus's own message, so guests without this mod are covered too. |
| Debug / `DebugLogging` | `false` | Extra log output. |

PocketCartPlus's own settings still apply. Its "Upgrade Levels" limit (how many carts can hold items) uses the lobby's highest level.

## Compatibility

- Built for R.E.P.O. (September 2026 build) and PocketCartPlus 0.6.1. It hooks PocketCartPlus by class and method name, so a PocketCartPlus update that renames those methods may disable parts of this mod. If that happens, a warning is written to the BepInEx log.
- Works with PocketCartPlus's "Unlock without Upgrade" option.
- PocketCartPlus's "Shared Unlock" option aims at the same goal. You can use this mod instead of that option.

## Known issues / notes

- "Lobby" means players who are connected right now. In `AfterAnyoneConsumed` mode, the shared ability goes away if every player who bought the upgrade leaves.
- A guest who joins mid-level without this mod gets the ability when HostAssist sends it, a few seconds after they spawn.

## AI disclosure

This mod was made with the help of generative AI. The code, this README and the icon were produced with an AI coding agent (xAI Grok), directed by the author. The DLL also declares this in its `AI_Assisted_Creation` / `AI_Model_Vendor` assembly metadata, following Thunderstore's AI guidelines. The package is listed in the **AI Generated** category.

## Credits

- **darmuh** for [PocketCartPlus](https://thunderstore.io/c/repo/p/darmuh/PocketCartPlus/). All of the Keep Items functionality is theirs; this mod only shares it.
- semiwork for R.E.P.O.
- Made by MrGlim.

## Changelog

See CHANGELOG.md.
