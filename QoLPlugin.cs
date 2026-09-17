using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

// ============================================================================
//  COLLECTION QoL  -  BepInEx 5 PLUGIN ENTRY
//  Features 31-45. Each feature runs isolated: if one throws, it is disabled
//  for the session and logged, the rest keep working.
// ============================================================================
[BepInPlugin(QoLPlugin.GUID, QoLPlugin.NAME, QoLPlugin.VERSION)]
public class QoLPlugin : BaseUnityPlugin
{
    public const string GUID    = "com.antigravity.collectionqol";  // matches the sibling mods; never change after release
    public const string NAME    = "Collection QoL";
    public const string VERSION = "1.0.1";

    public static ManualLogSource Log;

    struct Feature
    {
        public string Name;
        public Action Tick;
        public Action Gui;
        public bool Broken;
    }

    Feature[] features;
    bool wasInGame;

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

        features = new Feature[]
        {
            Make("Scanner",            LootScanner.Tick,                   null),
            Make("43 Priority",        LootPriority.Tick,                  null),
            Make("31 AutoEmptyNets",   F31_AutoEmptyNets.Tick,             null),
            Make("32 NetRange",        F32_NetRangeIncrease.Tick,          null),
            Make("33 HookSpeed",       F33_HookSpeedBoost.Tick,            null),
            Make("34/41 HookAssist",   F34_F41_HookAssist.Tick,            F34_F41_HookAssist.OnGUI),
            Make("35 Magnet",          F35_MagneticCollector.Tick,         F35_MagneticCollector.OnGUI),
            Make("36 IslandPickup",    F36_HandPickupIsland.Tick,          null),
            Make("37 DebrisTracking",  F37_SmartDebrisTracking.Tick,       F37_SmartDebrisTracking.OnGUI),
            Make("38 Highlight",       null,                               F38_ResourceHighlight.OnGUI),
            Make("39 Scanner",         F39_ItemDetectorScan.Tick,          F39_ItemDetectorScan.OnGUI),
            Make("40 Glow",            F40_LootGlow.Tick,                  null),
            Make("42 Underwater",      F42_UnderwaterLootAssist.Tick,      F42_UnderwaterLootAssist.OnGUI),
            Make("44 SoundAlerts",     F44_SoundAlerts.Tick,               null),   // toasts drawn in OnGUI below
            Make("45 LootTracker",     null,                               F45_FloatingLootTracker.OnGUI),
        };
        Log.LogInfo(NAME + " " + VERSION + " loaded. "
                  + QoLConfig.ChordLabel(QoLConfig.MagnetKey) + " Magnet | "
                  + QoLConfig.ChordLabel(QoLConfig.ScanKey) + " Scan | "
                  + QoLConfig.ChordLabel(QoLConfig.PriorityCycleKey) + " Priority | "
                  + QoLConfig.ChordLabel(QoLConfig.HudToggleKey) + " HUD | "
                  + QoLConfig.KeyMenu + " Menu | Ctrl + " + QoLConfig.DumpKey + " R&D dump");
    }

    // Confirmed via live in-game test: calling AudioClip.Create + SetData directly from
    // Awake() (which BepInEx runs extremely early, before Unity's own scene/audio system
    // has spun up) failed every time with "AudioClip.SetData failed; AudioClip contains no
    // data" for all 6 tones. Deferring to Start() - which runs one frame later, after every
    // other Awake() and Unity's own subsystem init - fixes it.
    void Start()
    {
        F44_SoundAlerts.Init();

        // The settings UI polls the menu hotkey in its own Update(), but nothing used to create
        // the component - it was only constructed inside Open(). That made the menu key dead
        // until the menu had already been opened some other way (the Mods Manager button), which
        // is a chicken-and-egg: the key could never open it the first time. The sibling mods all
        // attach their UI component at startup for exactly this reason.
        try
        {
            if (CollectionQoL.UI.CanvasCollectionQoLUI.Instance == null)
            {
                var uiGO = new GameObject("CollectionQoL_CanvasUI");
                UnityEngine.Object.DontDestroyOnLoad(uiGO);
                uiGO.AddComponent<CollectionQoL.UI.CanvasCollectionQoLUI>();
            }
        }
        catch (Exception e)
        {
            Log.LogError("Failed to attach Collection QoL settings UI: " + e);
        }
    }

    static Feature Make(string name, Action tick, Action gui)
    {
        Feature f = new Feature();
        f.Name = name; f.Tick = tick; f.Gui = gui;
        return f;
    }

    void Update()
    {
        if (features == null) return;
        Network_Player player = RaftRefs.GetLocalPlayer();
        if (player == null)
        {
            if (wasInGame) { CleanupWorld(); wasInGame = false; }
            return;
        }
        wasInGame = true;

        QoLContext.Player = player;
        QoLContext.PlayerPos = player.transform.position;
        QoLContext.Cam = RaftRefs.GetCamera(player);
        QoLContext.MenuOpen = QoLConfig.HideHudWhenCursorVisible && Cursor.visible;

        if (!QoLContext.MenuOpen)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl);
            if (ctrl && Input.GetKeyDown(QoLConfig.DumpKey))
            {
                RaftRefs.DumpAll();
                F44_SoundAlerts.Toast("R&D dump written to LogOutput.log");
            }
            else if (!ctrl && QoLConfig.Chord(QoLConfig.HudToggleKey))
            {
                QoLContext.HudVisible = !QoLContext.HudVisible;
                F44_SoundAlerts.Toast("QoL HUD " + (QoLContext.HudVisible ? "ON" : "OFF"));
            }
        }

        for (int i = 0; i < features.Length; i++)
        {
            if (features[i].Broken || features[i].Tick == null) continue;
            try { features[i].Tick(); }
            catch (Exception e) { Break(i, "Update", e); }
        }
    }

    void OnGUI()
    {
        if (features == null || !wasInGame || QoLContext.MenuOpen || QoLContext.Player == null) return;
        // Network_Player already exists while the world is still streaming in, so wasInGame alone
        // goes true during the "INITIALIZING ASSETS" screen and every overlay drew on top of it.
        // These are the game's own load-state flags (Sailor's Companion gates its HUD on the same
        // pair, which is why its overlay never bled through): the scene must be finished loading
        // and the save data must be done restoring before anything is safe to draw.
        try
        {
            if (LoadSceneManager.IsLoadingScene || !LoadSceneManager.IsGameSceneLoaded) return;
            if (SaveAndLoad.IsGameLoading) return;
        }
        catch { }

        F44_SoundAlerts.OnGUI();
        if (!QoLContext.HudVisible) return;

        for (int i = 0; i < features.Length; i++)
        {
            if (features[i].Broken || features[i].Gui == null) continue;
            try { features[i].Gui(); }
            catch (Exception e) { Break(i, "OnGUI", e); }
        }
    }

    void Break(int i, string where, Exception e)
    {
        features[i].Broken = true;
        Log.LogError("Feature '" + features[i].Name + "' disabled after error in " + where + ": " + e);
    }

    public static void CleanupWorld()
    {
        F32_NetRangeIncrease.Restore();
        F33_HookSpeedBoost.Restore();
        F40_LootGlow.Cleanup();
        LootScanner.Items.Clear();
        QoLContext.Player = null;
    }

    void OnDestroy()
    {
        CleanupWorld();
        F44_SoundAlerts.Cleanup();
    }
}
