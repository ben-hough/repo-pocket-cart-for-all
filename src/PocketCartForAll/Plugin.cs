using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace MrGlim.PocketCartForAll
{
    public enum GrantMode
    {
        /// <summary>Everyone gets the Keep Items ability once at least one player in the lobby has consumed the upgrade.</summary>
        AfterAnyoneConsumed,
        /// <summary>Everyone always has the Keep Items ability (level 1 minimum), even if nobody bought it.</summary>
        Always
    }

    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(PocketCartPlusGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "MrGlim.PocketCartForAll";
        public const string Name = "PocketCartForAll";
        public const string Version = "1.0.0";
        public const string PocketCartPlusGuid = "com.github.darmuh.PocketCartPlus";

        /// <summary>StatsManager save/sync key PocketCartPlus uses for the Keep Items upgrade (steamID -> level).</summary>
        internal const string UpgradeKey = "playerUpgradePocketcartKeepItems";

        /// <summary>Photon room property the host publishes: int[] { enabled (0/1), (int)GrantMode }. Guests follow it.</summary>
        internal const string RoomKey = "MrGlim.PCFA.cfg";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<GrantMode> Mode;
        internal static ConfigEntry<bool> HostAssist;
        internal static ConfigEntry<bool> DebugLogging;

        // PocketCartPlus internals (resolved by reflection so we never ship/modify its code)
        private static FieldInfo _localItemsUpgrade;   // static bool UpgradeManager.LocalItemsUpgrade

        // Gate bookkeeping
        private static int _gateDepth;
        private static bool _savedLocalFlag;
        private static float _cacheTime = -10f;
        private static int _cachedLevel;
        private static bool _warnedNoHost;
        private float _publishTimer;
        private int _publishedEnabled = -1, _publishedMode = -1;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. When true, every player is treated as owning PocketCartPlus's 'Keep Items' Pocket C.A.R.T. upgrade.\n" +
                "In multiplayer the HOST's Enabled/Mode are used for everyone; a guest's own values only matter when they host or play solo.");
            Mode = Config.Bind("General", "Mode", GrantMode.AfterAnyoneConsumed,
                "AfterAnyoneConsumed = everyone gets the ability (at the highest level anyone in the lobby owns) once at least one player in the lobby has consumed the upgrade box.\n" +
                "Always = everyone always has it (level 1 minimum), no purchase needed.");
            HostAssist = Config.Bind("Multiplayer", "HostAssist", true,
                "Host only. When you are the host, also push the shared level to other players using PocketCartPlus's own 'ReceiveItemsUpgrade' RPC " +
                "(on each level spawn and right after someone consumes the upgrade). This makes it work for guests who have PocketCartPlus but NOT this companion. Nothing is written to the save.");
            DebugLogging = Config.Bind("Debug", "DebugLogging", false, "Log extra details.");

            var upgradeManager = AccessTools.TypeByName("PocketCartPlus.UpgradeManager");
            var equipPatch = AccessTools.TypeByName("PocketCartPlus.EquipPatch");
            var cartMessagePatch = AccessTools.TypeByName("PocketCartPlus.CartMessagePatch");
            var updateVisualsPatch = AccessTools.TypeByName("PocketCartPlus.UpdateVisualsPatch");
            var upgradeItems = AccessTools.TypeByName("PocketCartPlus.PocketCartUpgradeItems");
            if (upgradeManager == null || equipPatch == null)
            {
                Log.LogError("PocketCartPlus types not found - is darmuh-PocketCartPlus installed and loaded? Companion disabled.");
                return;
            }
            _localItemsUpgrade = AccessTools.Field(upgradeManager, "LocalItemsUpgrade");

            var harmony = new Harmony(Guid);
            var self = typeof(Plugin);
            var gatePrefix = new HarmonyMethod(AccessTools.Method(self, nameof(GatePrefix))) { priority = Priority.First };
            var gateFinalizer = new HarmonyMethod(AccessTools.Method(self, nameof(GateFinalizer)));

            // The three places PocketCartPlus checks UpgradeManager.LocalItemsUpgrade (all run on the local client):
            //  EquipPatch.Postfix        -> actually storing items when the cart is pocketed
            //  CartMessagePatch.Postfix  -> the "hold ALT to deposit" hint
            //  UpdateVisualsPatch.Postfix-> re-showing stored items when the cart is taken out again
            PatchGate(harmony, equipPatch, gatePrefix, gateFinalizer);
            PatchGate(harmony, cartMessagePatch, gatePrefix, gateFinalizer);
            PatchGate(harmony, updateVisualsPatch, gatePrefix, gateFinalizer);

            var levelGetter = AccessTools.PropertyGetter(upgradeManager, "CartItemsUpgradeLevel");
            if (levelGetter != null)
                harmony.Patch(levelGetter, prefix: new HarmonyMethod(AccessTools.Method(self, nameof(LevelPrefix))));
            else
                Log.LogWarning("UpgradeManager.CartItemsUpgradeLevel not found; 'Upgrade Levels' limit will use PocketCartPlus's own value.");

            var upgrade = upgradeItems != null ? AccessTools.Method(upgradeItems, "Upgrade") : null;
            if (upgrade != null)
                harmony.Patch(upgrade, postfix: new HarmonyMethod(AccessTools.Method(self, nameof(UpgradePostfix))));

            var spawn = AccessTools.Method(typeof(PlayerAvatar), "SpawnRPC");
            if (spawn != null)
                harmony.Patch(spawn, postfix: new HarmonyMethod(AccessTools.Method(self, nameof(SpawnPostfix))));

            Log.LogInfo($"{Name} {Version} loaded (Mode={Mode.Value}, HostAssist={HostAssist.Value}).");
        }

        private static void PatchGate(Harmony harmony, Type patchClass, HarmonyMethod prefix, HarmonyMethod finalizer)
        {
            var m = patchClass != null ? AccessTools.Method(patchClass, "Postfix") : null;
            if (m == null)
            {
                Log.LogWarning($"Could not find {patchClass?.FullName ?? "<missing type>"}.Postfix - that part of the gate is not overridden.");
                return;
            }
            harmony.Patch(m, prefix: prefix, finalizer: finalizer);
            Debug($"Patched gate {patchClass.FullName}.Postfix");
        }

        internal static void Debug(string msg)
        {
            if (DebugLogging != null && DebugLogging.Value) Log.LogInfo(msg);
        }

        // ------------------------------------------------------------------ host gating

        private void Update()
        {
            // The host publishes its Enabled/Mode through the Photon room so every guest running the
            // companion uses the host's rules. Guests never enable anything on their own.
            _publishTimer -= Time.unscaledDeltaTime;
            if (_publishTimer > 0f) return;
            _publishTimer = 2f;
            try
            {
                if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) { _publishedEnabled = _publishedMode = -1; return; }
                int en = Enabled.Value ? 1 : 0, mode = (int)Mode.Value;
                var room = PhotonNetwork.CurrentRoom;
                bool present = room.CustomProperties != null && room.CustomProperties.ContainsKey(RoomKey);
                if (present && en == _publishedEnabled && mode == _publishedMode) return;
                room.SetCustomProperties(new PhotonHashtable { { RoomKey, new int[] { en, mode } } });
                _publishedEnabled = en; _publishedMode = mode;
                _cacheTime = -10f;
                Debug($"Published host settings Enabled={en} Mode={(GrantMode)mode}");
            }
            catch (Exception e) { Debug("Publish failed: " + e.Message); }
        }

        /// <summary>
        /// Settings in force for this client. Solo / host: own config. Guest: whatever the host published;
        /// if the host doesn't run the companion, nothing is granted.
        /// </summary>
        internal static bool ActiveSettings(out GrantMode mode)
        {
            mode = Mode != null ? Mode.Value : GrantMode.AfterAnyoneConsumed;
            if (SemiFunc.IsMultiplayer() && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            {
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                if (props != null && props.TryGetValue(RoomKey, out object o) && o is int[] a && a.Length >= 2)
                {
                    _warnedNoHost = false;
                    mode = Enum.IsDefined(typeof(GrantMode), a[1]) ? (GrantMode)a[1] : GrantMode.AfterAnyoneConsumed;
                    return a[0] != 0;
                }
                if (!_warnedNoHost)
                {
                    _warnedNoHost = true;
                    Log.LogWarning("Host is not running PocketCartForAll - Keep Items stays per-player (vanilla PocketCartPlus behaviour) in this lobby.");
                }
                return false;
            }
            return Enabled != null && Enabled.Value;
        }

        // ------------------------------------------------------------------ level math

        private static Dictionary<string, int> UpgradeDict()
        {
            var sm = StatsManager.instance;
            if (sm == null || sm.dictionaryOfDictionaries == null) return null;
            sm.dictionaryOfDictionaries.TryGetValue(UpgradeKey, out var d);
            return d;
        }

        /// <summary>Highest Keep Items level owned by anyone currently in the lobby (the host syncs this dictionary to every client each scene change).</summary>
        internal static int LobbyMaxLevel()
        {
            var dict = UpgradeDict();
            int max = 0;
            if (dict == null) return 0;
            var players = GameDirector.instance != null ? GameDirector.instance.PlayerList : null;
            if (players == null) return 0;
            foreach (var p in players)
            {
                if (p == null || string.IsNullOrEmpty(p.steamID)) continue;
                if (dict.TryGetValue(p.steamID, out int v) && v > max) max = v;
            }
            return max;
        }

        /// <summary>Level every player should be treated as having; 0 = companion not granting anything.</summary>
        internal static int EffectiveLevel()
        {
            float now = Time.unscaledTime;
            if (now - _cacheTime < 0.25f && now >= _cacheTime) return _cachedLevel;
            int lvl = 0;
            try
            {
                if (ActiveSettings(out GrantMode mode))
                {
                    lvl = LobbyMaxLevel();
                    if (mode == GrantMode.Always && lvl < 1) lvl = 1;
                }
            }
            catch (Exception e) { Debug("EffectiveLevel failed: " + e.Message); lvl = 0; }
            _cachedLevel = lvl;
            _cacheTime = now;
            return lvl;
        }

        private static int OwnLevel()
        {
            var dict = UpgradeDict();
            var me = PlayerAvatar.instance;
            if (dict == null || me == null || me.steamID == null) return 0;
            return dict.TryGetValue(me.steamID, out int v) ? v : 0;
        }

        // ------------------------------------------------------------------ gate patches

        private static void GatePrefix(out bool __state)
        {
            __state = false;
            if (_localItemsUpgrade == null) return;
            if (EffectiveLevel() < 1) return;
            if (_gateDepth == 0) _savedLocalFlag = (bool)_localItemsUpgrade.GetValue(null);
            _gateDepth++;
            __state = true;
            _localItemsUpgrade.SetValue(null, true);
        }

        private static Exception GateFinalizer(Exception __exception, bool __state)
        {
            if (__state)
            {
                _gateDepth--;
                if (_gateDepth <= 0)
                {
                    _gateDepth = 0;
                    // Restore PocketCartPlus's own per-player flag so we never permanently change its state.
                    _localItemsUpgrade.SetValue(null, _savedLocalFlag);
                }
            }
            return __exception;
        }

        /// <summary>Inside a gate, report max(own level, lobby level) without touching the save dictionary.</summary>
        private static bool LevelPrefix(ref int __result)
        {
            if (_gateDepth <= 0) return true;
            int eff = EffectiveLevel();
            if (eff < 1) return true;
            __result = Math.Max(OwnLevel(), eff);
            return false;
        }

        // ------------------------------------------------------------------ consumption / host assist

        private static void UpgradePostfix(object __instance)
        {
            try
            {
                var toggle = Traverse.Create(__instance).Field("itemToggle").GetValue<ItemToggle>();
                if (toggle == null) return;
                var avatar = SemiFunc.PlayerAvatarGetFromPhotonID(toggle.playerTogglePhotonID);
                if (avatar == null || string.IsNullOrEmpty(avatar.steamID)) return;

                if (!SemiFunc.IsMasterClientOrSingleplayer())
                {
                    // Guests: mirror the host's bookkeeping in our local copy so the lobby level is right
                    // immediately (the host's authoritative dictionary overwrites it at the next scene sync anyway).
                    var dict = UpgradeDict();
                    if (dict != null)
                    {
                        dict.TryGetValue(avatar.steamID, out int cur);
                        dict[avatar.steamID] = cur + 1;
                    }
                }
                _cacheTime = -10f;
                Debug($"{avatar.playerName} consumed Keep Items upgrade; lobby level now {EffectiveLevel()}");

                if (IsHostInMultiplayer() && HostAssist.Value && Instance != null)
                    Instance.StartCoroutine(Instance.BroadcastLater(1f, null));
            }
            catch (Exception e) { Log.LogWarning("UpgradePostfix: " + e); }
        }

        private static void SpawnPostfix(PlayerAvatar __instance)
        {
            try
            {
                if (Enabled == null || !Enabled.Value || !HostAssist.Value || Instance == null) return;
                if (!IsHostInMultiplayer() || __instance == null || __instance.isLocal) return;
                if (__instance.photonView == null || __instance.photonView.Owner == null) return;
                Instance.StartCoroutine(Instance.BroadcastLater(3f, __instance));
            }
            catch (Exception e) { Log.LogWarning("SpawnPostfix: " + e); }
        }

        private static bool IsHostInMultiplayer() => SemiFunc.IsMultiplayer() && PhotonNetwork.IsMasterClient;

        private IEnumerator BroadcastLater(float delay, PlayerAvatar target)
        {
            yield return new WaitForSeconds(delay);
            if (!IsHostInMultiplayer()) yield break;
            _cacheTime = -10f;
            int level = EffectiveLevel();
            if (level < 1) yield break;
            var rm = RunManager.instance;
            if (rm == null || rm.runManagerPUN == null) yield break;
            var pv = rm.runManagerPUN.GetComponent<PhotonView>();
            var gn = AccessTools.TypeByName("PocketCartPlus.GlobalNetworking");
            if (pv == null || gn == null || rm.runManagerPUN.GetComponent(gn) == null) yield break;
            try
            {
                if (target != null)
                {
                    if (target == null || target.photonView == null || target.photonView.Owner == null) yield break;
                    pv.RPC("ReceiveItemsUpgrade", target.photonView.Owner, level);
                    Debug($"HostAssist: sent Keep Items level {level} to {target.playerName}");
                }
                else
                {
                    pv.RPC("ReceiveItemsUpgrade", RpcTarget.Others, level);
                    Debug($"HostAssist: sent Keep Items level {level} to all guests");
                }
            }
            catch (Exception e) { Log.LogWarning("HostAssist RPC failed: " + e.Message); }
        }
    }
}
