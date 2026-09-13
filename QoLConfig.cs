using UnityEngine;

// ============================================================================
//  COLLECTION QoL  -  CONFIG
//  These are DEFAULTS. Players change values in:
//  Raft/BepInEx/config/com.antigravity.collectionqol.cfg  (created on first launch)
//  or live in-game with BepInEx ConfigurationManager (F1).
// ============================================================================
public enum LootKind { Other, Plank, Plastic, Leaf, Scrap, Barrel, Food, Stone, Sand, Clay, Seaweed }

public static class QoLConfig
{
    // ---------------- MASTER TOGGLES (31-45) ----------------
    public static bool AutoEmptyNets        = true;  // 31
    public static bool NetRangeIncrease     = true;  // 32
    public static bool HookSpeedBoost       = true;  // 33
    public static bool HookAccuracyBoost    = true;  // 34
    public static bool MagneticCollector    = true;  // 35
    public static bool HandPickupIsland     = true;  // 36
    public static bool SmartDebrisTracking  = true;  // 37
    public static bool ResourceHighlight    = true;  // 38
    public static bool ItemDetectorScan     = true;  // 39
    public static bool LootGlow             = true;  // 40
    public static bool BarrelTargetAssist   = true;  // 41
    public static bool UnderwaterLootAssist = true;  // 42
    public static bool PriorityPickup       = true;  // 43
    public static bool SoundAlerts          = true;  // 44
    public static bool FloatingLootTracker  = true;  // 45

    // ---------------- HOTKEYS ----------------
    // The four mods in this family share one keyboard, and F1-F10 are fully spoken for:
    //   F1  Farmer's Companion menu      F6  Sailor's Companion HUD
    //   F2  Inventory Master menu        F7  Sailor's Companion magnet
    //   F3  Collection QoL menu          F8  Sailor's Companion teleport to raft
    //   F4  Sailor's Companion sails     F9  Sailor's Companion summon raft
    //   F5  Sailor's Companion menu      F10 Sailor's Companion scanner pulse
    // Plain F5/F6/F8 therefore fired two mods at once. Rather than scatter this mod's
    // actions across whatever keys happen to be free, all four take one shared modifier,
    // so the F-key mnemonics stay and the collisions go. Set HotkeyModifier to None in
    // the config (or in the Controls screen) to go back to bare F-keys when running
    // Collection QoL on its own.
    public static KeyCode HotkeyModifier    = KeyCode.LeftAlt;
    public static KeyCode MagnetKey         = KeyCode.F5;   // Alt + F5
    public static KeyCode ScanKey           = KeyCode.F6;   // Alt + F6
    public static KeyCode PriorityCycleKey  = KeyCode.F7;   // Alt + F7
    public static KeyCode HudToggleKey      = KeyCode.F8;   // Alt + F8  hide/show all overlays
    public static KeyCode DumpKey           = KeyCode.F8;   // Ctrl + F8 R&D dump (own modifier)
    // The menu key takes no modifier: it is the one key a player presses before they know
    // anything about this mod, and F3 is not claimed by any sibling at its default settings.
    public static KeyCode KeyMenu           = KeyCode.F3;   // open/close the Collection QoL settings menu

    #region [START] MODIFIER HELPERS
    // True when the shared modifier is held (or when the player has set it to None).
    public static bool ModifierHeld
    {
        get { return HotkeyModifier == KeyCode.None || Input.GetKey(HotkeyModifier); }
    }

    // One place that decides whether an action hotkey fired, so every feature spells the
    // chord the same way and the Controls screen can label it from the same source.
    public static bool Chord(KeyCode key)
    {
        return ModifierHeld && Input.GetKeyDown(key);
    }

    // "Alt + F5" / "F5" - used by the settings UI and the startup log line.
    public static string ChordLabel(KeyCode key)
    {
        if (HotkeyModifier == KeyCode.None) return key.ToString();
        string mod = HotkeyModifier.ToString();
        if (mod.StartsWith("Left")) mod = mod.Substring(4);
        else if (mod.StartsWith("Right")) mod = mod.Substring(5);
        return mod + " + " + key;
    }
    #endregion [END] MODIFIER HELPERS

    // ---------------- WORLD / SCANNER ----------------
    public static float WaterLevelY          = 0f;    // VERIFY with the dump (see report, Step 0)
    public static float ScanInterval         = 0.5f;  // seconds between full scene scans
    public static float MaxScanDistance      = 90f;
    public static bool  HideHudWhenCursorVisible = true;

    // ================= GAME ADAPTER NAMES (VERIFY IN dnSpy / DUMP) =================
    // These are the ONLY game-internal names the mod depends on. If Raft updates and a
    // feature logs "not found", fix the name here - no other code changes needed.
    public static string NetTypeName          = "ItemNet";
    public static string NetCollectorField    = "itemCollector";
    public static string NetItemsField        = "collectedItems";
    public static string NetCapacityField     = "maxNumberOfItems";
    // Verified against the live Assembly-CSharp.dll: ItemCollector's real method is
    // AddCollectedItemsToPlayer(Network_Player) - none of the original guessed names
    // ("CollectItems"/"PickupAllItems"/"Collect") exist, so Feature 31 never fired.
    public static string[] NetEmptyMethods    = { "AddCollectedItemsToPlayer", "CollectItems", "PickupAllItems", "Collect" };

    public static string HookTypeName         = "Hook";
    // "throwable.throwableObject" verified against the live Assembly-CSharp.dll as the
    // real flying-hook Transform. Plain "throwable" resolves to the SAME GameObject as
    // the Hook script itself (rejected as a no-op), so without the dotted path below,
    // GetHookTip() always returned null and Features 34/41 never activated.
    public static string[] HookTipFields      = { "throwable.throwableObject", "hookHead", "hookObject", "hookTransform" };
    public static string[] HookMultiplyFields = { };   // exact float field names to MULTIPLY (speeds)
    public static string[] HookDivideFields   = { };   // exact float field names to DIVIDE (times)
    public static bool   HookAutoDetectFields = true;  // also auto-pick fields named *speed* / *gather*time*

    public static string PickupItemInstanceField = "itemInstance";
    public static string ItemInstanceBaseField   = "baseItem";
    public static string ItemInstanceAmountField = "Amount";

    // Game's own pickup method (best, multiplayer-safe). Verified against the live
    // Assembly-CSharp.dll: Network_Player.PickupScript is a "Pickup" component whose
    // public PickupItem(PickupItem, bool forcePickup, bool triggerHandAnimation) method
    // is exactly what vanilla interact-key pickup calls - reflection finds it on the
    // player via GetComponentInChildren(owner) since PickupOwnerType is non-static.
    public static string PickupOwnerType      = "Pickup";
    public static string PickupMethod         = "PickupItem";
    public static bool   AllowFallbackPickup  = true;  // SINGLEPLAYER ONLY: give item + hide object

    public static string PlayerIsLocalMember  = "IsLocalPlayer"; // bool member on Network_Player (local player detection)

    public static string[] DumpTypes = { "ItemNet", "ItemCollector", "Hook", "PickupItem",
                                         "PickupItem_Networked", "Network_Player", "PlayerInventory", "Raft" };

    // ---------------- 31 AUTO EMPTY NETS ----------------
    public static float AutoEmptyInterval = 5f;
    public static float AutoEmptyRange    = 40f;

    // ---------------- 32 NET RANGE ----------------
    public static float NetRangeMultiplier = 1.6f;   // clamped 1..3

    // ---------------- 33 HOOK SPEED ----------------
    public static float HookSpeedMultiplier = 1.75f; // clamped 1..4

    // ---------------- 34 HOOK ACCURACY ----------------
    public static float HookAssistRadius = 2.5f;
    public static float HookAssistPull   = 4f;

    // ---------------- 35 MAGNET ----------------
    public static float MagnetDuration        = 8f;
    public static float MagnetCooldown        = 30f;
    public static float MagnetRadius          = 18f;
    public static float MagnetPullSpeed       = 5f;
    public static float MagnetCollectDistance = 2.2f;
    public static int   MagnetMaxItems        = 12;
    public static bool  MagnetAutoCollect     = true;
    public static bool  MagnetPullsBarrels    = true;

    // ---------------- 36 HAND PICKUP (ISLAND) ----------------
    public static float IslandPickupRadius   = 2.5f;
    public static float IslandPickupInterval = 0.25f;
    public static LootKind[] IslandAllowed   = { LootKind.Stone, LootKind.Sand, LootKind.Clay,
                                                 LootKind.Scrap, LootKind.Food, LootKind.Seaweed };

    // ---------------- 37 SMART DEBRIS ----------------
    public static float DebrisRange       = 70f;
    public static float NetCourseMargin   = 2f;   // extra metres beside the raft that nets still catch
    public static float HookCourseMargin  = 12f;  // within this = worth hooking

    // ---------------- 38 HIGHLIGHT ----------------
    public static float HighlightRange = 12f;

    // ---------------- 39 SCANNER ----------------
    public static float ScanRadius   = 60f;
    public static float ScanDuration = 8f;
    public static float ScanCooldown = 15f;

    // ---------------- 40 GLOW ----------------
    public static float GlowRange     = 20f;
    public static int   GlowMaxLights = 8;
    public static float GlowIntensity = 2.5f;

    // ---------------- 41 BARREL ASSIST ----------------
    public static float BarrelAimRange     = 30f;
    public static float BarrelAimAngle     = 25f;
    public static float BarrelAssistRadius = 5f;

    // ---------------- 42 UNDERWATER ----------------
    public static float UnderwaterRange      = 25f;
    public static bool  UnderwaterAutoGrab   = true;
    public static float UnderwaterGrabRadius = 2f;

    // ---------------- 43 PRIORITY ----------------
    public static string[] PriorityPresetNames = { "Balanced", "Building", "Food" };
    public static LootKind[][] PriorityPresets =
    {
        new LootKind[] { LootKind.Barrel, LootKind.Scrap, LootKind.Plastic, LootKind.Plank, LootKind.Leaf, LootKind.Food, LootKind.Clay, LootKind.Sand, LootKind.Stone, LootKind.Seaweed },
        new LootKind[] { LootKind.Plank, LootKind.Plastic, LootKind.Scrap, LootKind.Leaf, LootKind.Barrel, LootKind.Stone, LootKind.Clay, LootKind.Sand, LootKind.Food, LootKind.Seaweed },
        new LootKind[] { LootKind.Food, LootKind.Barrel, LootKind.Leaf, LootKind.Plank, LootKind.Seaweed, LootKind.Plastic, LootKind.Scrap, LootKind.Stone, LootKind.Clay, LootKind.Sand }
    };

    // ---------------- 44 SOUND ALERTS ----------------
    public static float AlertVolume      = 0.35f;
    public static float AlertRange       = 35f;
    public static LootKind[] AlertKinds  = { LootKind.Barrel, LootKind.Scrap };

    // ---------------- 45 TRACKER / RADAR ----------------
    public static float RadarRange     = 60f;
    public static float RadarSize      = 170f;
    public static LootKind[] TrackKinds = { LootKind.Barrel };

    // ---------------- COLORS ----------------
    public static Color KindColor(LootKind k)
    {
        switch (k)
        {
            case LootKind.Plank:   return new Color(0.85f, 0.60f, 0.30f);
            case LootKind.Plastic: return new Color(0.40f, 0.80f, 1.00f);
            case LootKind.Leaf:    return new Color(0.45f, 0.95f, 0.40f);
            case LootKind.Scrap:   return new Color(0.75f, 0.75f, 0.80f);
            case LootKind.Barrel:  return new Color(1.00f, 0.85f, 0.10f);
            case LootKind.Food:    return new Color(1.00f, 0.45f, 0.45f);
            case LootKind.Seaweed: return new Color(0.20f, 0.70f, 0.50f);
            default:               return new Color(0.95f, 0.95f, 0.95f);
        }
    }

    // Fallback unique item names used only when the game's item name can't be read.
    public static string FallbackUniqueName(LootKind k)
    {
        switch (k)
        {
            case LootKind.Plank:   return "Plank";
            case LootKind.Plastic: return "Plastic";
            case LootKind.Leaf:    return "Thatch";
            case LootKind.Scrap:   return "Scrap";
            case LootKind.Stone:   return "Stone";
            case LootKind.Sand:    return "Sand";
            case LootKind.Clay:    return "Clay";
            case LootKind.Seaweed: return "Seaweed";
            default:               return null; // barrels & unknown: never faked
        }
    }
}
