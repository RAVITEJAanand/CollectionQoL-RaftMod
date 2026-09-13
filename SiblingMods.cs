using System;
using System.IO;
using BepInEx;

// ============================================================================
//  SIBLING MOD DETECTION
//  Five Collection QoL features also exist in Sailor's Companion (hook speed,
//  magnetic collector, island hand pickup, item detector scan, auto-empty
//  nets). With both mods installed each of those runs twice: the two hook
//  multipliers compound, and the two pickup loops fight over the same
//  PickupItem in the same frame.
//
//  The fix is a DEFAULT, not a runtime override. When Sailor's Companion is
//  present, the overlapping features are bound with a default of OFF, so a
//  fresh install starts sane. A value the player has already saved in the .cfg
//  is never touched - BepInEx only uses a default when the key is absent - so
//  turning one back on sticks, and uninstalling Sailor's later does not
//  silently flip anything either way.
// ============================================================================
#region [START] SIBLING MOD DETECTION
public static class SiblingMods
{
    private static bool _resolved;
    private static bool _sailors;

    #region [START] SAILORS COMPANION INSTALLED
    // True when Sailor's Companion is present in this BepInEx install.
    public static bool SailorsCompanionInstalled
    {
        get
        {
            if (!_resolved) Resolve();
            return _sailors;
        }
    }
    #endregion [END] SAILORS COMPANION INSTALLED

    #region [START] OVERLAP DEFAULT
    // Default for a feature that duplicates one of Sailor's Companion's: off when
    // the sibling is installed, on when Collection QoL is running standalone.
    public static bool OverlapDefault()
    {
        return !SailorsCompanionInstalled;
    }
    #endregion [END] OVERLAP DEFAULT

    #region [START] RESOLVE
    // Looks on disk rather than at Chainloader.PluginInfos or the loaded assemblies:
    // BepInEx fills PluginInfos as each plugin is instantiated, so whichever of the two
    // mods loads first would see the other as absent. The plugin folder is already
    // fully populated before any Awake() runs, which makes the file check the only
    // load-order-independent answer.
    private static void Resolve()
    {
        _resolved = true;
        try
        {
            string root = Paths.PluginPath;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
            string[] hits = Directory.GetFiles(root, "SailorsCompanion.dll", SearchOption.AllDirectories);
            _sailors = hits != null && hits.Length > 0;
            if (_sailors && QoLPlugin.Log != null)
            {
                QoLPlugin.Log.LogInfo("Sailor's Companion detected - features it already provides "
                                    + "(hook speed, magnet, island pickup, detector scan, auto-empty nets) "
                                    + "default to OFF on first run. Saved settings are left alone.");
            }
        }
        catch (Exception e)
        {
            // Detection is an optimisation, never a hard dependency: on any IO failure fall
            // back to standalone behaviour so every feature still works.
            if (QoLPlugin.Log != null) QoLPlugin.Log.LogWarning("Sibling mod detection failed: " + e.Message);
            _sailors = false;
        }
    }
    #endregion [END] RESOLVE
}
#endregion [END] SIBLING MOD DETECTION
