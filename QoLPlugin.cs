using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

// ============================================================================
//  COLLECTION QoL  -  BepInEx 5 PLUGIN ENTRY
//  Features 31-45. Binds config, applies Harmony patches and spawns the
//  QoLRunner that ticks every feature (see QoLRunner.cs for why the per-frame
//  hooks can't live on this component).
// ============================================================================
[BepInPlugin(QoLPlugin.GUID, QoLPlugin.NAME, QoLPlugin.VERSION)]
public class QoLPlugin : BaseUnityPlugin
{
    public const string GUID    = "com.antigravity.collectionqol";  // matches the sibling mods; never change after release
    public const string NAME    = "Collection QoL";
    public const string VERSION = "1.0.3";

    public static ManualLogSource Log;

    void Awake()
    {
        Log = Logger;
        QoLConfigBinder.Bind(Config);

        // Cursor/camera-freeze patch (Patches/CursorPatch.cs): without it, Collection QoL's own
        // menu is unusable with the mouse when run standalone (nothing else stops Raft from
        // re-locking the cursor and spinning the camera while the menu is open).
        try
        {
            new Harmony(GUID).PatchAll();
        }
        catch (Exception e)
        {
            Log.LogError("Failed to apply Harmony patches: " + e);
        }

        // Raft 1.1.01 destroys BepInEx's manager GameObject (where this component lives) right after
        // the main menu loads, so Start/Update/OnGUI here never ran. The runner gets its own object,
        // flagged HideAndDontSave like the sibling mods' managers so it survives that cleanup.
        try
        {
            var runnerGO = new GameObject("CollectionQoL_Runner");
            runnerGO.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runnerGO);
            runnerGO.AddComponent<QoLRunner>();
        }
        catch (Exception e)
        {
            Log.LogError("Failed to start Collection QoL runner: " + e);
        }

        Log.LogInfo(NAME + " " + VERSION + " loaded. "
                  + QoLConfig.ChordLabel(QoLConfig.MagnetKey) + " Magnet | "
                  + QoLConfig.ChordLabel(QoLConfig.ScanKey) + " Scan | "
                  + QoLConfig.ChordLabel(QoLConfig.PriorityCycleKey) + " Priority | "
                  + QoLConfig.ChordLabel(QoLConfig.HudToggleKey) + " HUD | "
                  + QoLConfig.KeyMenu + " Menu | Ctrl + " + QoLConfig.DumpKey + " R&D dump");
    }

    public static void CleanupWorld()
    {
        F32_NetRangeIncrease.Restore();
        F33_HookSpeedBoost.Restore();
        F40_LootGlow.Cleanup();
        LootScanner.Items.Clear();
        QoLContext.Player = null;
    }
}
