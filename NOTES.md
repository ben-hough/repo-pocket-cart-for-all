# R.E.P.O. mods by MrGlim: build notes (2026-09-25)

Built on the Grok box from REPO_Data/Managed copied off DESKTOP-8MHDAIU. BepInEx.Core 5.4.21 + BepInEx.AssemblyPublicizer
(NuGet), target netstandard2.1. Neither mod has been run in-game yet. Not uploaded to Thunderstore.

Rebuild: `dotnet build -c Release` in src/<Mod>. Directory.Build.props looks for `../refs/Managed`, otherwise it uses
`C:\Program Files (x86)\Steam\steamapps\common\REPO\REPO_Data\Managed`. Override with `-p:ManagedPath=...`.

## Naming (confirmed from resources.assets)
- `Item Cart Small`: display name "POCKET C.A.R.T.", the vanilla little cart (PhysGrabCart.isSmallCart = true)
- `Item Cart Medium`: display name "C.A.R.T.", the normal cart
- darmuh-PocketCartPlus's "Keep Items" upgrade works on that same vanilla POCKET C.A.R.T. (it hooks every
  PhysGrabCart with isSmallCart).

## PocketCartForAll
- Target: darmuh-PocketCartPlus 0.6.1 (GUID com.github.darmuh.PocketCartPlus), https://thunderstore.io/c/repo/p/darmuh/PocketCartPlus/
- Other candidates: BandbreitenBanditen-PocketCartPlusFixed 0.5.6 (community fix of the same code, same GUID),
  sunwu-PocketCart 1.0.4 (a different item-stash cart mod with no upgrade box), and
  Omniscye-Pocket_Dimension_Cart 1.6.8 (turns carts into pocket dimensions).
- How the gate works: EquipPatch.Postfix (on ItemEquippable.RequestEquip) runs on the player who pockets the cart. It checks
  `UpgradeManager.LocalItemsUpgrade` (a static bool set only for the player who consumed the box, or via the ReceiveItemsUpgrade
  RPC) or "Unlock without Upgrade". With "Upgrade Levels" on it also checks `CartItemsUpgradeLevel` (your own steamID entry in
  the StatsManager dict `playerUpgradePocketcartKeepItems`) > CartsStoringItems. The host keeps and saves that dict.
  Vanilla PunManager.SyncAllDictionaries sends it to all clients on every scene switch.

## WideLittleCart: measured prefab data (root-local metres)
| | little cart (Item Cart Small) | normal cart (Item Cart Medium) |
|---|---|---|
| floor collider `Inside` (W x D) | 0.774 x 0.786 | 1.232 x 1.939 |
| side-wall centres (x) | -0.321 / +0.367 (thickness 0.085) | -0.545 / +0.549 (thickness 0.135) |
| front/back wall centres (z) | +0.360 / -0.361 | +0.895 / -0.885 |
| `In Cart` overlap box (W x D x H) | 0.716 x 0.746 x 0.559 | 1.011 x 1.678 x 0.613 |
| mesh AABB (W x H x D) | 0.766 x 0.722 x 0.888 ("Portable Cart") | 1.270 x 0.841 x 2.078 ("Cart Base") |
| cart grab point z | -0.423 | -0.989 |
| push distance (CartSteer) | 1.5-2.0 m | 2.0-2.5 m |

Scale factors: width 1.232/0.774 = x1.592, depth x2.000, height x1.000.
New little cart floor: 1.232 x 1.572 m (the normal cart is 1.232 x 1.939). In-Cart box: 1.140 x 1.492 x 0.559.
Push distance +0.423 m (grab point moves from z -0.423 to -0.846).

## 2026-09-25: Thunderstore prep (see E:\Codex-Mods\shared\notes\thunderstore-lessons.md)
- Both mods are now host-gated. PocketCartForAll: the host publishes room property `MrGlim.PCFA.cfg` = int[]{enabled, mode}; guests follow it and do nothing if it's absent. WideLittleCart: removed the `FollowHostSize` option, so guests always use the host's `MrGlim.WLC.scale` and stay vanilla without it.
- Both csproj files declare `AI_Assisted_Creation` / `AI_Model_Vendor` = "xAI (Grok)" AssemblyMetadata. READMEs have an AI disclosure section.
- New 256x256 icons (make_icons2.py). tcli-built zips (thunderstore.toml in shared\ts-packages\<Name>-1.0.0\). DLL at the zip root, matching the earlier accepted packages.
- NOT uploaded. Test in game first (solo, host, guest, and guest with a host who doesn't have the mod).
