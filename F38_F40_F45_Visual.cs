using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// ============================================================================
//  38  RESOURCE HIGHLIGHT (SHORT RANGE)
//  Corner brackets + name + distance on loot within a few metres.
// ============================================================================
public static class F38_ResourceHighlight
{
    public static void OnGUI()
    {
        if (!QoLConfig.ResourceHighlight) return;
        foreach (ScannedItem s in LootScanner.Items)
        {
            if (!s.Valid || s.Distance > QoLConfig.HighlightRange) continue;
            if (s.Underwater && F42_UnderwaterLootAssist.Underwater) continue;   // 42 draws these
            Vector2 g;
            if (!QDraw.WorldToGUI(s.Position, out g)) continue;
            float size = Mathf.Clamp(500f / Mathf.Max(s.Distance, 1f), 18f, 64f);
            Color c = QoLConfig.KindColor(s.Kind);
            c.a = Mathf.Clamp01(1.3f - s.Distance / QoLConfig.HighlightRange);
            QDraw.Corners(g, size, c, 2f);
            QDraw.TextCentered(g + new Vector2(0, size / 2f + 9f), s.DisplayName + "  " + s.Distance.ToString("F0") + "m", c, 12);
        }
    }
}

// ============================================================================
//  40  LOOT GLOW EFFECT
//  Attaches a small pulsing point Light to the nearest N valuable items.
//  Lights are child objects we own - removed on unload.
// ============================================================================
public static class F40_LootGlow
{
    static readonly Dictionary<PickupItem, Light> lights = new Dictionary<PickupItem, Light>();
    static float next;

    public static void Tick()
    {
        if (!QoLConfig.LootGlow) { Cleanup(); return; }

        if (Time.time >= next)
        {
            next = Time.time + 0.5f;
            Refresh();
        }

        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
        foreach (KeyValuePair<PickupItem, Light> kv in lights)
            if (kv.Value != null) kv.Value.intensity = QoLConfig.GlowIntensity * pulse * (kv.Key != null && RaftRefs.Classify(kv.Key.gameObject.name) == LootKind.Barrel ? 1.5f : 1f);
    }

    static void Refresh()
    {
        List<ScannedItem> wanted = LootPriority.Sorted(s => s.Distance <= QoLConfig.GlowRange && s.Kind != LootKind.Other);
        HashSet<PickupItem> keep = new HashSet<PickupItem>();
        for (int i = 0; i < wanted.Count && i < QoLConfig.GlowMaxLights; i++) keep.Add(wanted[i].Pickup);

        List<PickupItem> remove = new List<PickupItem>();
        foreach (KeyValuePair<PickupItem, Light> kv in lights)
            if (kv.Key == null || !kv.Key.gameObject.activeInHierarchy || !keep.Contains(kv.Key)) remove.Add(kv.Key);
        foreach (PickupItem p in remove)
        {
            Light l = lights[p];
            if (l != null) Object.Destroy(l.gameObject);
            lights.Remove(p);
        }

        for (int i = 0; i < wanted.Count && i < QoLConfig.GlowMaxLights; i++)
        {
            ScannedItem s = wanted[i];
            if (lights.ContainsKey(s.Pickup)) continue;
            GameObject go = new GameObject("QoL_Glow");
            go.transform.SetParent(s.Pickup.transform, false);
            go.transform.localPosition = Vector3.up * 0.4f;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = s.Kind == LootKind.Barrel ? 4f : 2.5f;
            l.color = QoLConfig.KindColor(s.Kind);
            l.shadows = LightShadows.None;
            l.intensity = QoLConfig.GlowIntensity;
            lights[s.Pickup] = l;
        }
    }

    public static void Cleanup()
    {
        foreach (KeyValuePair<PickupItem, Light> kv in lights)
            if (kv.Value != null) Object.Destroy(kv.Value.gameObject);
        lights.Clear();
    }
}

// ============================================================================
//  45  FLOATING LOOT TRACKER
//  Rotating mini-radar (top-left) + screen-edge arrows for tracked kinds
//  (barrels by default) that are off-screen or behind you.
// ============================================================================
public static class F45_FloatingLootTracker
{
    public static void OnGUI()
    {
        if (!QoLConfig.FloatingLootTracker || QoLContext.Cam == null) return;
        DrawRadar();
        DrawEdgeArrows();
    }

    static void DrawRadar()
    {
        float size = QoLConfig.RadarSize, x = 12f, y = 12f;
        Vector2 c = new Vector2(x + size / 2f, y + size / 2f);
        QDraw.Panel(new Rect(x, y, size, size));
        QDraw.Circle(c, size / 2f - 4f, new Color(1, 1, 1, 0.15f), 1f, 32);
        QDraw.Rect(new Rect(c.x - 3, c.y - 3, 6, 6), Color.white);
        QDraw.Line(c, c + new Vector2(0, -12), Color.white, 2f);                 // facing indicator

        float yaw = QoLContext.Cam.transform.eulerAngles.y * Mathf.Deg2Rad;
        float cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw);
        float scale = (size / 2f - 6f) / QoLConfig.RadarRange;

        foreach (ScannedItem s in LootScanner.Items)
        {
            if (!s.Valid || s.Distance > QoLConfig.RadarRange) continue;
            Vector3 rel = s.Position - QoLContext.PlayerPos;
            float rx = rel.x * cos - rel.z * sin;
            float ry = rel.x * sin + rel.z * cos;
            Vector2 p = new Vector2(c.x + rx * scale, c.y - ry * scale);
            float dot = s.Kind == LootKind.Barrel ? 6f : 4f;
            Color col = QoLConfig.KindColor(s.Kind);
            if (s.Underwater) col.a = 0.5f;
            QDraw.Rect(new Rect(p.x - dot / 2f, p.y - dot / 2f, dot, dot), col);
        }
        QDraw.Text(x + 6, y + size - 18, QoLConfig.RadarRange + "m", new Color(1, 1, 1, 0.5f), 10);
    }

    static void DrawEdgeArrows()
    {
        Camera cam = QoLContext.Cam;
        float margin = 40f;
        foreach (ScannedItem s in LootScanner.Items)
        {
            if (!s.Valid || System.Array.IndexOf(QoLConfig.TrackKinds, s.Kind) < 0) continue;
            Vector3 sp = cam.WorldToScreenPoint(s.Position);
            bool behind = sp.z < 0f;
            bool onScreen = !behind && sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height;
            if (onScreen) continue;

            Vector2 centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 dir = new Vector2(sp.x, Screen.height - sp.y) - centre;
            if (behind) dir = -dir;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
            dir.Normalize();

            float kx = (Screen.width / 2f - margin) / Mathf.Max(Mathf.Abs(dir.x), 0.0001f);
            float ky = (Screen.height / 2f - margin) / Mathf.Max(Mathf.Abs(dir.y), 0.0001f);
            Vector2 edge = centre + dir * Mathf.Min(kx, ky);

            Color col = QoLConfig.KindColor(s.Kind);
            QDraw.Line(edge - dir * 18f, edge, col, 4f);
            QDraw.Rect(new Rect(edge.x - 5, edge.y - 5, 10, 10), col);
            QDraw.TextCentered(edge - dir * 32f, Mathf.RoundToInt(s.Distance) + "m", col, 12);
        }
    }
}
