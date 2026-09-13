using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// ============================================================================
//  33  HOOK SPEED BOOST
//  Scales float fields on the Hook script (speeds x mult, gather times / mult).
//  Originals are saved and restored on toggle-off / unload.
// ============================================================================
public static class F33_HookSpeedBoost
{
    class Saved { public Component Target; public FieldInfo Field; public float Original; }

    static readonly List<Saved> saved = new List<Saved>();
    static readonly HashSet<int> boosted = new HashSet<int>();
    static float next;

    public static void Tick()
    {
        if (Time.time < next) return;
        next = Time.time + 1f;

        if (!QoLConfig.HookSpeedBoost) { Restore(); return; }

        foreach (Component hook in RaftRefs.GetPlayerHooks(QoLContext.Player, false))
        {
            if (hook == null || boosted.Contains(hook.GetInstanceID())) continue;
            boosted.Add(hook.GetInstanceID());
            int changed = Apply(hook);
            if (changed == 0)
                RaftRefs.WarnOnce("hookSpeed0", "Hook Speed: no matching float fields. Run the dump and fill HookMultiplyFields / HookDivideFields.");
        }
    }

    static int Apply(Component hook)
    {
        float mult = Mathf.Clamp(QoLConfig.HookSpeedMultiplier, 1f, 4f);
        int changed = 0;
        for (System.Type t = hook.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            foreach (FieldInfo f in t.GetFields(RaftRefs.ALL | BindingFlags.DeclaredOnly))
            {
                if (f.IsStatic || f.IsLiteral || f.IsInitOnly || f.FieldType != typeof(float)) continue;
                int mode = Mode(f.Name);
                if (mode == 0) continue;
                float v = (float)f.GetValue(hook);
                if (v <= 0f) continue;
                saved.Add(new Saved { Target = hook, Field = f, Original = v });
                f.SetValue(hook, mode > 0 ? v * mult : v / mult);
                RaftRefs.Log("Hook Speed: " + f.Name + " " + v + " -> " + f.GetValue(hook));
                changed++;
            }
        }
        return changed;
    }

    // +1 multiply, -1 divide, 0 skip
    static int Mode(string name)
    {
        if (System.Array.IndexOf(QoLConfig.HookMultiplyFields, name) >= 0) return 1;
        if (System.Array.IndexOf(QoLConfig.HookDivideFields, name) >= 0) return -1;
        if (!QoLConfig.HookAutoDetectFields) return 0;

        string n = name.ToLowerInvariant();
        if (n.Contains("timer") || n.StartsWith("current") || n.StartsWith("last") || n.Contains("throw")) return 0;
        if (n.Contains("speed")) return 1;
        if ((n.Contains("gather") || n.Contains("reel") || n.Contains("pull")) && (n.Contains("time") || n.Contains("duration"))) return -1;
        return 0;
    }

    public static void Restore()
    {
        foreach (Saved s in saved)
            if (s.Target != null) s.Field.SetValue(s.Target, s.Original);
        saved.Clear();
        boosted.Clear();
    }
}

// ============================================================================
//  34  HOOK ACCURACY BOOST  +  41  AUTO BARREL TARGET ASSIST
//  When the hook is out in the water, the best nearby item (by priority) is
//  gently pulled onto the hook tip. Barrels get a bigger radius (41), and
//  while aiming, the best barrel in front of you gets an on-screen target.
// ============================================================================
public static class F34_F41_HookAssist
{
    static ScannedItem aimBarrel;
    static bool hookOut;

    public static void Tick()
    {
        aimBarrel = null;
        hookOut = false;
        if (!QoLConfig.HookAccuracyBoost && !QoLConfig.BarrelTargetAssist) return;

        Component[] hooks = RaftRefs.GetPlayerHooks(QoLContext.Player, true);
        if (hooks.Length == 0) return;                    // not holding a hook

        Transform tip = RaftRefs.GetHookTip(hooks[0]);
        if (tip != null)
        {
            Vector3 tp = tip.position;
            hookOut = Vector3.Distance(tp, QoLContext.PlayerPos) > 3f && tp.y < QoLConfig.WaterLevelY + 1.5f;
        }

        if (hookOut)
        {
            Vector3 tp = tip.position;
            ScannedItem target = LootPriority.Best(s =>
                s.Floating && Vector3.Distance(s.Position, tp) <= RadiusFor(s));
            if (target != null)
            {
                Vector3 goal = new Vector3(tp.x, target.Position.y, tp.z);   // stay on the water surface
                RaftRefs.MoveItemToward(target.Pickup, goal, QoLConfig.HookAssistPull);
            }
        }
        else if (QoLConfig.BarrelTargetAssist)
        {
            aimBarrel = LootPriority.Best(s =>
                s.Kind == LootKind.Barrel && s.Floating &&
                s.Distance <= QoLConfig.BarrelAimRange &&
                Mathf.Abs(QDraw.Bearing(s.Position)) <= QoLConfig.BarrelAimAngle);
        }
    }

    static float RadiusFor(ScannedItem s)
    {
        if (s.Kind == LootKind.Barrel && QoLConfig.BarrelTargetAssist) return QoLConfig.BarrelAssistRadius;
        return QoLConfig.HookAccuracyBoost ? QoLConfig.HookAssistRadius : 0f;
    }

    public static void OnGUI()
    {
        if (aimBarrel == null || !aimBarrel.Valid) return;
        Vector2 g;
        if (!QDraw.WorldToGUI(aimBarrel.Position, out g)) return;

        Color c = QoLConfig.KindColor(LootKind.Barrel);
        float pulse = 34f + Mathf.Sin(Time.time * 6f) * 4f;
        QDraw.Corners(g, pulse, c, 3f);
        Vector2 centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
        QDraw.Line(centre, g, new Color(c.r, c.g, c.b, 0.35f), 2f);
        // simple arc hint: further barrels need a higher aim
        string hint = aimBarrel.Distance > 18f ? "aim high" : aimBarrel.Distance > 10f ? "aim slightly up" : "aim direct";
        QDraw.TextCentered(g + new Vector2(0, 30), "BARREL " + Mathf.RoundToInt(aimBarrel.Distance) + "m  (" + hint + ")", c, 14);
    }
}
