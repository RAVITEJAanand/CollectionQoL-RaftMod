using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

// ============================================================================
//  CONFIG BINDER - creates BepInEx/config/<GUID>.cfg and copies its values into
//  QoLConfig. Changes made live (ConfigurationManager F1) apply immediately.
// ============================================================================
public static class QoLConfigBinder
{
    static ConfigFile cfg;
    static readonly List<Action> appliers = new List<Action>();

    // ---------------- section names ----------------
    // Public so the Canvas settings UI (UI/CanvasCollectionQoLUI.cs) can look up the exact same
    // ConfigEntry<T> objects created below via GetBool/GetFloat, instead of duplicating literals.
    public const string SecGeneral = "00 General";
    public const string SecHotkeys = "01 Hotkeys";
    public const string SecAdapter = "02 Game Adapter (R&D)";
    public const string Sec31 = "31 Auto Empty Nets";
    public const string Sec32 = "32 Net Range Increase";
    public const string Sec33 = "33 Hook Speed Boost";
    public const string Sec34 = "34 Hook Accuracy Boost";
    public const string Sec35 = "35 Magnetic Collector";
    public const string Sec36 = "36 Hand Pickup Island";
    public const string Sec37 = "37 Smart Debris Tracking";
    public const string Sec38 = "38 Resource Highlight";
    public const string Sec39 = "39 Item Detector Scan";
    public const string Sec40 = "40 Loot Glow";
    public const string Sec41 = "41 Barrel Target Assist";
    public const string Sec42 = "42 Underwater Loot Assist";
    public const string Sec43 = "43 Resource Priority";
    public const string Sec44 = "44 Sound Alerts";
    public const string Sec45 = "45 Floating Loot Tracker";

    // ---------------- ConfigEntry<T> lookup for the Canvas settings UI ----------------
    // The UI reads/writes ConfigEntry<T>.Value directly (never QoLConfig's static fields) so a
    // change made in-game persists to the .cfg file and re-fires cfg.SettingChanged -> ApplyAll()
    // exactly like editing the file by hand or via BepInEx ConfigurationManager - nothing about
    // that pipeline needed to be duplicated here, only these entries needed to be reachable.
    public static readonly Dictionary<string, ConfigEntry<bool>> BoolEntries = new Dictionary<string, ConfigEntry<bool>>();
    public static readonly Dictionary<string, ConfigEntry<float>> FloatEntries = new Dictionary<string, ConfigEntry<float>>();

    public static ConfigEntry<bool> GetBool(string section, string key)
    {
        ConfigEntry<bool> e;
        BoolEntries.TryGetValue(section + "::" + key, out e);
        return e;
    }

    public static ConfigEntry<float> GetFloat(string section, string key)
    {
        ConfigEntry<float> e;
        FloatEntries.TryGetValue(section + "::" + key, out e);
        return e;
    }

    public static void Bind(ConfigFile file)
    {
        cfg = file;

        const string G = SecGeneral;
        Bool(G, "HideHudWhenCursorVisible", QoLConfig.HideHudWhenCursorVisible, "Hide overlays and ignore hotkeys while the mouse cursor is visible (menus, inventory).", v => QoLConfig.HideHudWhenCursorVisible = v);
        Float(G, "WaterLevelY", QoLConfig.WaterLevelY, -50f, 50f, "Sea surface height. Verify with the R&D dump (LeftCtrl+F8).", v => QoLConfig.WaterLevelY = v);
        Float(G, "ScanInterval", QoLConfig.ScanInterval, 0.1f, 5f, "Seconds between full loot scans. Higher = better FPS.", v => QoLConfig.ScanInterval = v);
        Float(G, "MaxScanDistance", QoLConfig.MaxScanDistance, 10f, 300f, "Loot further than this is ignored by every feature.", v => QoLConfig.MaxScanDistance = v);

        const string K = SecHotkeys;
        Key(K, "HotkeyModifier", QoLConfig.HotkeyModifier, "Held with Magnet / Scan / PriorityCycle / HudToggle so they do not collide with Sailor's Companion's bare F5 / F6 / F8. Set to None for plain F-keys.", v => QoLConfig.HotkeyModifier = v);
        Key(K, "Magnet", QoLConfig.MagnetKey, "35 Magnetic Collector. Pressed with HotkeyModifier.", v => QoLConfig.MagnetKey = v);
        Key(K, "Scan", QoLConfig.ScanKey, "39 Item Detector Scan. Pressed with HotkeyModifier.", v => QoLConfig.ScanKey = v);
        Key(K, "PriorityCycle", QoLConfig.PriorityCycleKey, "43 cycle priority presets. Pressed with HotkeyModifier.", v => QoLConfig.PriorityCycleKey = v);
        Key(K, "HudToggle", QoLConfig.HudToggleKey, "Show / hide all overlays. Pressed with HotkeyModifier.", v => QoLConfig.HudToggleKey = v);
        Key(K, "Dump", QoLConfig.DumpKey, "Hold LeftCtrl + this key to write the R&D dump to the log.", v => QoLConfig.DumpKey = v);
        Key(K, "Menu", QoLConfig.KeyMenu, "Open / close the Collection QoL settings menu.", v => QoLConfig.KeyMenu = v);

        const string A = SecAdapter;
        Str(A, "NetTypeName", QoLConfig.NetTypeName, "Class name of the collection net.", v => QoLConfig.NetTypeName = v);
        Str(A, "NetCollectorField", QoLConfig.NetCollectorField, "Field on the net holding the collector object.", v => QoLConfig.NetCollectorField = v);
        Str(A, "NetItemsField", QoLConfig.NetItemsField, "Collector field with caught items (list or int).", v => QoLConfig.NetItemsField = v);
        Str(A, "NetCapacityField", QoLConfig.NetCapacityField, "Collector int field with max capacity.", v => QoLConfig.NetCapacityField = v);
        Csv(A, "NetEmptyMethods", QoLConfig.NetEmptyMethods, "Collector methods tried in order to empty a net.", v => QoLConfig.NetEmptyMethods = v);
        Str(A, "HookTypeName", QoLConfig.HookTypeName, "Class name of the hook script.", v => QoLConfig.HookTypeName = v);
        Csv(A, "HookTipFields", QoLConfig.HookTipFields, "Hook fields holding the thrown part.", v => QoLConfig.HookTipFields = v);
        Csv(A, "HookMultiplyFields", QoLConfig.HookMultiplyFields, "Exact hook float fields to multiply (speeds).", v => QoLConfig.HookMultiplyFields = v);
        Csv(A, "HookDivideFields", QoLConfig.HookDivideFields, "Exact hook float fields to divide (times).", v => QoLConfig.HookDivideFields = v);
        Bool(A, "HookAutoDetectFields", QoLConfig.HookAutoDetectFields, "Also auto-detect *speed* and *gather/reel/pull*time* fields.", v => QoLConfig.HookAutoDetectFields = v);
        Str(A, "PickupItemInstanceField", QoLConfig.PickupItemInstanceField, "PickupItem field holding the item instance.", v => QoLConfig.PickupItemInstanceField = v);
        Str(A, "ItemInstanceBaseField", QoLConfig.ItemInstanceBaseField, "Item instance field holding the base item.", v => QoLConfig.ItemInstanceBaseField = v);
        Str(A, "ItemInstanceAmountField", QoLConfig.ItemInstanceAmountField, "Item instance int member with the amount.", v => QoLConfig.ItemInstanceAmountField = v);
        Str(A, "PickupOwnerType", QoLConfig.PickupOwnerType, "Class that owns the game's pickup method (multiplayer-safe collecting).", v => QoLConfig.PickupOwnerType = v);
        Str(A, "PickupMethod", QoLConfig.PickupMethod, "Game method taking PickupItem or PickupItem_Networked.", v => QoLConfig.PickupMethod = v);
        Bool(A, "AllowFallbackPickup", QoLConfig.AllowFallbackPickup, "SINGLEPLAYER ONLY: give item + hide object when no game method is set.", v => QoLConfig.AllowFallbackPickup = v);
        Str(A, "PlayerIsLocalMember", QoLConfig.PlayerIsLocalMember, "Bool member on Network_Player that is true for your own player.", v => QoLConfig.PlayerIsLocalMember = v);
        Csv(A, "DumpTypes", QoLConfig.DumpTypes, "Types listed by the R&D dump.", v => QoLConfig.DumpTypes = v);

        const string S31 = Sec31;
        Bool(S31, "Enabled", SiblingMods.OverlapDefault(), "Empty nearby nets into your inventory. Also in Sailor's Companion (Auto-Empty Collection Nets) - run only one.", v => QoLConfig.AutoEmptyNets = v);
        Float(S31, "Interval", QoLConfig.AutoEmptyInterval, 1f, 60f, "Seconds between checks.", v => QoLConfig.AutoEmptyInterval = v);
        Float(S31, "Range", QoLConfig.AutoEmptyRange, 5f, 150f, "Only nets within this distance.", v => QoLConfig.AutoEmptyRange = v);

        const string S32 = Sec32;
        Bool(S32, "Enabled", QoLConfig.NetRangeIncrease, "Bigger net catch area.", v => QoLConfig.NetRangeIncrease = v);
        Float(S32, "Multiplier", QoLConfig.NetRangeMultiplier, 1f, 3f, "Trigger collider scale.", v => QoLConfig.NetRangeMultiplier = v);

        const string S33 = Sec33;
        Bool(S33, "Enabled", SiblingMods.OverlapDefault(), "Faster hook reeling / gathering. Also in Sailor's Companion (Hook Reel Speed) - the two multipliers compound, run only one.", v => QoLConfig.HookSpeedBoost = v);
        Float(S33, "Multiplier", QoLConfig.HookSpeedMultiplier, 1f, 4f, "Speed multiplier.", v => QoLConfig.HookSpeedMultiplier = v);

        const string S34 = Sec34;
        Bool(S34, "Enabled", QoLConfig.HookAccuracyBoost, "Pull near-miss items onto the hook.", v => QoLConfig.HookAccuracyBoost = v);
        Float(S34, "AssistRadius", QoLConfig.HookAssistRadius, 0.5f, 8f, "Metres around the hook tip.", v => QoLConfig.HookAssistRadius = v);
        Float(S34, "AssistPull", QoLConfig.HookAssistPull, 0.5f, 20f, "Pull speed (m/s).", v => QoLConfig.HookAssistPull = v);

        const string S35 = Sec35;
        Bool(S35, "Enabled", SiblingMods.OverlapDefault(), "Timed loot magnet on hotkey. Also in Sailor's Companion (Magnetic Collector) - run only one.", v => QoLConfig.MagneticCollector = v);
        Float(S35, "Duration", QoLConfig.MagnetDuration, 1f, 60f, "Seconds active.", v => QoLConfig.MagnetDuration = v);
        Float(S35, "Cooldown", QoLConfig.MagnetCooldown, 0f, 600f, "Seconds after it ends.", v => QoLConfig.MagnetCooldown = v);
        Float(S35, "Radius", QoLConfig.MagnetRadius, 2f, 60f, "Pull radius.", v => QoLConfig.MagnetRadius = v);
        Float(S35, "PullSpeed", QoLConfig.MagnetPullSpeed, 0.5f, 30f, "Pull speed (m/s).", v => QoLConfig.MagnetPullSpeed = v);
        Float(S35, "CollectDistance", QoLConfig.MagnetCollectDistance, 0.5f, 6f, "Collect when this close.", v => QoLConfig.MagnetCollectDistance = v);
        Int(S35, "MaxItems", QoLConfig.MagnetMaxItems, 1, 50, "Max items handled per frame.", v => QoLConfig.MagnetMaxItems = v);
        Bool(S35, "AutoCollect", QoLConfig.MagnetAutoCollect, "Collect items that arrive.", v => QoLConfig.MagnetAutoCollect = v);
        Bool(S35, "PullsBarrels", QoLConfig.MagnetPullsBarrels, "Also pull barrels (never auto-opened).", v => QoLConfig.MagnetPullsBarrels = v);

        const string S36 = Sec36;
        Bool(S36, "Enabled", SiblingMods.OverlapDefault(), "Auto pickup small items while on land. Also in Sailor's Companion (Island Hand Pickup) - two pickup loops fight over the same item, run only one.", v => QoLConfig.HandPickupIsland = v);
        Float(S36, "Radius", QoLConfig.IslandPickupRadius, 0.5f, 8f, "Pickup radius.", v => QoLConfig.IslandPickupRadius = v);
        Float(S36, "Interval", QoLConfig.IslandPickupInterval, 0.05f, 2f, "Seconds between pickups.", v => QoLConfig.IslandPickupInterval = v);
        Kinds(S36, "AllowedKinds", QoLConfig.IslandAllowed, "Kinds picked up.", v => QoLConfig.IslandAllowed = v);

        const string S37 = Sec37;
        Bool(S37, "Enabled", QoLConfig.SmartDebrisTracking, "Debris panel with net / hook predictions.", v => QoLConfig.SmartDebrisTracking = v);
        Float(S37, "Range", QoLConfig.DebrisRange, 10f, 200f, "Tracking range.", v => QoLConfig.DebrisRange = v);
        Float(S37, "NetCourseMargin", QoLConfig.NetCourseMargin, 0f, 10f, "Extra metres beside the raft nets still catch.", v => QoLConfig.NetCourseMargin = v);
        Float(S37, "HookCourseMargin", QoLConfig.HookCourseMargin, 1f, 40f, "Within this = worth hooking.", v => QoLConfig.HookCourseMargin = v);

        const string S38 = Sec38;
        Bool(S38, "Enabled", QoLConfig.ResourceHighlight, "Brackets + labels on close loot.", v => QoLConfig.ResourceHighlight = v);
        Float(S38, "Range", QoLConfig.HighlightRange, 2f, 50f, "Highlight range.", v => QoLConfig.HighlightRange = v);

        const string S39 = Sec39;
        Bool(S39, "Enabled", SiblingMods.OverlapDefault(), "Scan pulse on hotkey. Also in Sailor's Companion (Island Pulse Scan) - run only one.", v => QoLConfig.ItemDetectorScan = v);
        Float(S39, "Radius", QoLConfig.ScanRadius, 10f, 200f, "Scan radius.", v => QoLConfig.ScanRadius = v);
        Float(S39, "Duration", QoLConfig.ScanDuration, 2f, 30f, "Seconds results stay visible.", v => QoLConfig.ScanDuration = v);
        Float(S39, "Cooldown", QoLConfig.ScanCooldown, 0f, 300f, "Seconds between scans.", v => QoLConfig.ScanCooldown = v);

        const string S40 = Sec40;
        Bool(S40, "Enabled", QoLConfig.LootGlow, "Pulsing lights on valuable loot.", v => QoLConfig.LootGlow = v);
        Float(S40, "Range", QoLConfig.GlowRange, 2f, 80f, "Glow range.", v => QoLConfig.GlowRange = v);
        Int(S40, "MaxLights", QoLConfig.GlowMaxLights, 0, 32, "Max lights at once (performance).", v => QoLConfig.GlowMaxLights = v);
        Float(S40, "Intensity", QoLConfig.GlowIntensity, 0.1f, 10f, "Light intensity.", v => QoLConfig.GlowIntensity = v);

        const string S41 = Sec41;
        Bool(S41, "Enabled", QoLConfig.BarrelTargetAssist, "Barrel aim marker + bigger hook capture.", v => QoLConfig.BarrelTargetAssist = v);
        Float(S41, "AimRange", QoLConfig.BarrelAimRange, 5f, 80f, "Max barrel distance.", v => QoLConfig.BarrelAimRange = v);
        Float(S41, "AimAngle", QoLConfig.BarrelAimAngle, 5f, 90f, "Degrees from view centre.", v => QoLConfig.BarrelAimAngle = v);
        Float(S41, "AssistRadius", QoLConfig.BarrelAssistRadius, 0.5f, 12f, "Capture radius around hook tip.", v => QoLConfig.BarrelAssistRadius = v);

        const string S42 = Sec42;
        Bool(S42, "Enabled", QoLConfig.UnderwaterLootAssist, "Dive HUD + underwater markers.", v => QoLConfig.UnderwaterLootAssist = v);
        Float(S42, "Range", QoLConfig.UnderwaterRange, 5f, 80f, "Marker range.", v => QoLConfig.UnderwaterRange = v);
        Bool(S42, "AutoGrab", QoLConfig.UnderwaterAutoGrab, "Grab very close underwater loot.", v => QoLConfig.UnderwaterAutoGrab = v);
        Float(S42, "GrabRadius", QoLConfig.UnderwaterGrabRadius, 0.5f, 6f, "Auto-grab radius.", v => QoLConfig.UnderwaterGrabRadius = v);

        const string S43 = Sec43;
        Bool(S43, "Enabled", QoLConfig.PriorityPickup, "Use priority order (off = nearest first).", v => QoLConfig.PriorityPickup = v);
        Csv(S43, "PresetNames", QoLConfig.PriorityPresetNames, "Names of the 3 presets.", v => { if (v.Length > 0) QoLConfig.PriorityPresetNames = v; });
        Kinds(S43, "Preset1Order", QoLConfig.PriorityPresets[0], "First = most wanted.", v => QoLConfig.PriorityPresets[0] = v);
        Kinds(S43, "Preset2Order", QoLConfig.PriorityPresets[1], "First = most wanted.", v => QoLConfig.PriorityPresets[1] = v);
        Kinds(S43, "Preset3Order", QoLConfig.PriorityPresets[2], "First = most wanted.", v => QoLConfig.PriorityPresets[2] = v);

        const string S44 = Sec44;
        Bool(S44, "Enabled", QoLConfig.SoundAlerts, "Alert tones.", v => QoLConfig.SoundAlerts = v);
        Float(S44, "Volume", QoLConfig.AlertVolume, 0f, 1f, "Alert volume.", v => QoLConfig.AlertVolume = v);
        Float(S44, "AlertRange", QoLConfig.AlertRange, 5f, 150f, "Announce loot inside this range.", v => QoLConfig.AlertRange = v);
        Kinds(S44, "AlertKinds", QoLConfig.AlertKinds, "Kinds that trigger an alert.", v => QoLConfig.AlertKinds = v);

        const string S45 = Sec45;
        Bool(S45, "Enabled", QoLConfig.FloatingLootTracker, "Radar + edge arrows.", v => QoLConfig.FloatingLootTracker = v);
        Float(S45, "RadarRange", QoLConfig.RadarRange, 10f, 200f, "Radar range.", v => QoLConfig.RadarRange = v);
        Float(S45, "RadarSize", QoLConfig.RadarSize, 80f, 400f, "Radar size in pixels.", v => QoLConfig.RadarSize = v);
        Kinds(S45, "TrackKinds", QoLConfig.TrackKinds, "Kinds that get edge arrows.", v => QoLConfig.TrackKinds = v);

        ApplyAll();
        cfg.SettingChanged += OnSettingChanged;
    }

    static void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        ApplyAll();
        F32_NetRangeIncrease.Restore();   // re-applied with new values on their next tick
        F33_HookSpeedBoost.Restore();
    }

    static void ApplyAll()
    {
        foreach (Action a in appliers)
        {
            try { a(); }
            catch (Exception ex) { RaftRefs.WarnOnce("cfg_" + ex.Message, "Config apply error: " + ex.Message); }
        }
    }

    // ---------------- helpers ----------------
    static void Bool(string s, string k, bool def, string desc, Action<bool> set)
    {
        ConfigEntry<bool> e = cfg.Bind(s, k, def, desc);
        BoolEntries[s + "::" + k] = e;
        appliers.Add(() => set(e.Value));
    }

    static void Str(string s, string k, string def, string desc, Action<string> set)
    {
        ConfigEntry<string> e = cfg.Bind(s, k, def ?? "", desc);
        appliers.Add(() => set(e.Value.Trim()));
    }

    static void Key(string s, string k, KeyCode def, string desc, Action<KeyCode> set)
    {
        ConfigEntry<KeyCode> e = cfg.Bind(s, k, def, desc);
        appliers.Add(() => set(e.Value));
    }

    static void Float(string s, string k, float def, float min, float max, string desc, Action<float> set)
    {
        ConfigEntry<float> e = cfg.Bind(s, k, def, new ConfigDescription(desc, new AcceptableValueRange<float>(min, max)));
        FloatEntries[s + "::" + k] = e;
        appliers.Add(() => set(e.Value));
    }

    static void Int(string s, string k, int def, int min, int max, string desc, Action<int> set)
    {
        ConfigEntry<int> e = cfg.Bind(s, k, def, new ConfigDescription(desc, new AcceptableValueRange<int>(min, max)));
        appliers.Add(() => set(e.Value));
    }

    static void Csv(string s, string k, string[] def, string desc, Action<string[]> set)
    {
        ConfigEntry<string> e = cfg.Bind(s, k, string.Join(",", def), desc + " Comma separated.");
        appliers.Add(() => set(SplitCsv(e.Value)));
    }

    static void Kinds(string s, string k, LootKind[] def, string desc, Action<LootKind[]> set)
    {
        string[] names = new string[def.Length];
        for (int i = 0; i < def.Length; i++) names[i] = def[i].ToString();
        LootKind[] fallback = (LootKind[])def.Clone();
        ConfigEntry<string> e = cfg.Bind(s, k, string.Join(",", names),
            desc + " Comma separated. Kinds: " + string.Join(", ", Enum.GetNames(typeof(LootKind))));
        appliers.Add(() => set(ParseKinds(e.Value, fallback)));
    }

    static string[] SplitCsv(string v)
    {
        List<string> list = new List<string>();
        if (!string.IsNullOrEmpty(v))
            foreach (string part in v.Split(','))
            {
                string t = part.Trim();
                if (t.Length > 0) list.Add(t);
            }
        return list.ToArray();
    }

    static LootKind[] ParseKinds(string v, LootKind[] fallback)
    {
        List<LootKind> list = new List<LootKind>();
        foreach (string p in SplitCsv(v))
        {
            try { list.Add((LootKind)Enum.Parse(typeof(LootKind), p, true)); }
            catch { RaftRefs.WarnOnce("kind_" + p, "Unknown loot kind in config: " + p); }
        }
        return list.Count > 0 ? list.ToArray() : fallback;
    }
}
