using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  31  AUTO EMPTY NETS
//  Every few seconds, nets near the player are emptied into the inventory by
//  calling the net's OWN collect method (same code path as pressing E).
// ============================================================================
public static class F31_AutoEmptyNets
{
    static float next;

    public static void Tick()
    {
        if (!QoLConfig.AutoEmptyNets || Time.time < next) return;
        next = Time.time + Mathf.Max(1f, QoLConfig.AutoEmptyInterval);

        int emptied = 0;
        foreach (Component net in RaftRefs.GetNets())
        {
            if (net == null) continue;
            if (Vector3.Distance(net.transform.position, QoLContext.PlayerPos) > QoLConfig.AutoEmptyRange) continue;
            if (RaftRefs.GetNetCount(net) == 0) continue;   // -1 (unknown) still tries
            if (RaftRefs.TryEmptyNet(net, QoLContext.Player)) emptied++;
            else break;                                     // method missing: stop, warning already logged
        }
        if (emptied > 0) F44_SoundAlerts.Beep(F44_SoundAlerts.Tone.Collect);
    }
}

// ============================================================================
//  32  NET RANGE INCREASE
//  Nets catch debris with a TRIGGER collider. We scale only trigger colliders
//  and remember the original size so unloading the mod restores everything.
// ============================================================================
public static class F32_NetRangeIncrease
{
    static readonly Dictionary<BoxCollider, Vector3> original = new Dictionary<BoxCollider, Vector3>();
    static float next;
    static float appliedMultiplier = 1f;

    public static void Tick()
    {
        if (Time.time < next) return;
        next = Time.time + 3f;

        if (!QoLConfig.NetRangeIncrease) { Restore(); return; }

        float mult = Mathf.Clamp(QoLConfig.NetRangeMultiplier, 1f, 3f);
        if (!Mathf.Approximately(mult, appliedMultiplier)) { Restore(); appliedMultiplier = mult; }

        foreach (Component net in RaftRefs.GetNets())
        {
            if (net == null) continue;
            foreach (BoxCollider bc in net.GetComponentsInChildren<BoxCollider>(true))
            {
                if (bc == null || !bc.isTrigger || original.ContainsKey(bc)) continue;
                original[bc] = bc.size;
                bc.size = bc.size * mult;
            }
        }
    }

    public static void Restore()
    {
        foreach (KeyValuePair<BoxCollider, Vector3> kv in original)
            if (kv.Key != null) kv.Key.size = kv.Value;
        original.Clear();
        appliedMultiplier = 1f;
    }
}
