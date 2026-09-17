using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using CollectionQoL.UI;

namespace CollectionQoL.Patches
{
    #region [START] PATCH: CURSOR UNLOCKING & CAMERA FREEZE
    // ============================================================================
    // [START] PATCH: CURSOR UNLOCKING & CAMERA FREEZE
    // Description: Ensures mouse cursor is freed and camera is frozen when any mod UI is open.
    //
    // The three sibling mods (Sailor's Companion, Farmer's Companion, Inventory Master) each
    // ship this exact patch already, and each one already checks Collection QoL's own window
    // state via reflection - Collection QoL was always designed assuming one of them would be
    // installed and would supply this protection for free. Running Collection QoL by itself,
    // with none of the siblings present, meant nothing ever froze MouseLook or force-freed the
    // cursor while its own menu was open: Raft's own game code kept re-locking the cursor and
    // spinning the camera every frame, so the menu rendered but was effectively unusable with
    // the mouse. This is Collection QoL's own copy of that same patch, so it works standalone.
    // ============================================================================
    public static class CursorPatchHelper
    {
        private static PropertyInfo _scWindowProp;
        private static PropertyInfo _fcWindowProp;
        private static PropertyInfo _imWindowProp;
        private static PropertyInfo _modsMgrProp;
        private static bool _typesResolved = false;

        public static bool ShouldForceCursorFree()
        {
            // 1. Collection QoL's own menu
            if (CanvasCollectionQoLUI.IsWindowOpen) return true;

            // 2. Peer mods (Sailor's Companion, Farmer's Companion, Inventory Master)
            if (!_typesResolved) ResolvePeerTypes();

            if (_scWindowProp != null)
            {
                try { if ((bool)_scWindowProp.GetValue(null)) return true; } catch { }
            }
            if (_fcWindowProp != null)
            {
                try { if ((bool)_fcWindowProp.GetValue(null)) return true; } catch { }
            }
            if (_imWindowProp != null)
            {
                try { if ((bool)_imWindowProp.GetValue(null)) return true; } catch { }
            }
            if (_modsMgrProp != null)
            {
                try { if ((bool)_modsMgrProp.GetValue(null)) return true; } catch { }
            }

            return false;
        }

        private static void ResolvePeerTypes()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (_scWindowProp == null)
                    {
                        var scType = asm.GetType("SailorsCompanion.UI.CanvasModUI");
                        if (scType != null)
                            _scWindowProp = scType.GetProperty("IsWindowOpen", BindingFlags.Public | BindingFlags.Static);
                    }
                    if (_fcWindowProp == null)
                    {
                        var fcType = asm.GetType("FarmersCompanion.UI.CanvasFarmersCompanionUI");
                        if (fcType != null)
                            _fcWindowProp = fcType.GetProperty("IsWindowOpen", BindingFlags.Public | BindingFlags.Static);
                    }
                    if (_imWindowProp == null)
                    {
                        var imType = asm.GetType("InventoryMaster.UI.CanvasInventoryMasterUI");
                        if (imType != null)
                            _imWindowProp = imType.GetProperty("IsWindowOpen", BindingFlags.Public | BindingFlags.Static);
                    }
                    if (_modsMgrProp == null)
                    {
                        // Note: this one exposes "IsOpen", not "IsWindowOpen".
                        var mmType = asm.GetType("SailorsCompanion.UI.CanvasInstalledModsUI");
                        if (mmType != null)
                            _modsMgrProp = mmType.GetProperty("IsOpen", BindingFlags.Public | BindingFlags.Static);
                    }
                }
            }
            catch { }
            finally
            {
                // Resolve only once: this runs on every MouseLook.Update() frame, so retrying the
                // AppDomain assembly scan forever (e.g. when a peer mod is simply not installed) would
                // be a persistent per-frame reflection cost.
                _typesResolved = true;
            }
        }
    }

    [HarmonyPatch(typeof(MouseLook), "Update")]
    public static class MouseLookUpdatePatch
    {
        public static bool Prefix()
        {
            if (CursorPatchHelper.ShouldForceCursorFree())
            {
                return false; // Freeze camera rotation completely while any mod UI is open!
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Helper), "SetCursorVisibleAndLockState")]
    public static class HelperSetCursorVisibleAndLockStatePatch
    {
        public static void Prefix(ref bool state, ref CursorLockMode mode)
        {
            if (CursorPatchHelper.ShouldForceCursorFree())
            {
                state = true;
                mode = CursorLockMode.None;
            }
        }
    }

    [HarmonyPatch(typeof(Helper), "SetCursorLockState")]
    public static class HelperSetCursorLockStatePatch
    {
        public static void Prefix(ref CursorLockMode mode)
        {
            if (CursorPatchHelper.ShouldForceCursorFree())
            {
                mode = CursorLockMode.None;
            }
        }
    }

    [HarmonyPatch(typeof(Helper), "SetCursorVisible")]
    public static class HelperSetCursorVisiblePatch
    {
        public static void Prefix(ref bool state)
        {
            if (CursorPatchHelper.ShouldForceCursorFree())
            {
                state = true;
            }
        }
    }
    // ============================================================================
    // [END] PATCH: CURSOR UNLOCKING & CAMERA FREEZE
    // ============================================================================
    #endregion
}
