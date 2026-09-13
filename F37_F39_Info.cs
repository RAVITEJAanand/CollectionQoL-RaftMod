using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// ============================================================================
//  37  SMART DEBRIS TRACKING
//  Measures raft velocity, predicts which floating debris will pass through
//  the raft footprint (nets will catch it) vs. needs hooking vs. will miss.
// ============================================================================
public static class F37_SmartDebrisTracking
{
    class Row { public int Total, OnCourse, Hookable; public float BestEta = -1f; }

    static Raft raft;
    static Vector3 lastRaftPos;
    static Vector3 raftVel;
    static float halfWidth = 4f;
    static float nextBounds, nextRows;
    static readonly Dictionary<LootKind, Row> rows = new Dictionary<LootKind, Row>();

    public static void Tick()
    {
        if (!QoLConfig.SmartDebrisTracking) return;
        if (raft == null) { raft = Object.FindObjectOfType<Raft>(); if (raft != null) lastRaftPos = raft.transform.position; return; }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 pos = raft.transform.position;
        Vector3 v = (pos - lastRaftPos) / dt; v.y = 0f;
        if (v.magnitude < 50f) raftVel = Vector3.Lerp(raftVel, v, 0.05f);   // ignore teleports / anchor snaps
        lastRaftPos = pos;

        if (Time.time >= nextBounds) { nextBounds = Time.time + 10f; RecalcBounds(); }
        if (Time.time >= nextRows)   { nextRows = Time.time + 0.5f; RecalcRows(pos); }
    }

    static void RecalcBounds()
    {
        bool has = false;
        Bounds b = new Bounds(raft.transform.position, Vector3.zero);
        foreach (Collider c in raft.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
        }
        if (has) halfWidth = Mathf.Max(2f, Mathf.Min(b.extents.x, b.extents.z));
    }

    static void RecalcRows(Vector3 raftPos)
    {
        rows.Clear();
        float speed = raftVel.magnitude;
        Vector3 dir = speed > 0.05f ? raftVel / speed : Vector3.zero;

        foreach (ScannedItem s in LootScanner.Items)
        {
            if (!s.Valid || !s.Floating || s.Distance > QoLConfig.DebrisRange) continue;
            Row r;
            if (!rows.TryGetValue(s.Kind, out r)) { r = new Row(); rows[s.Kind] = r; }
            r.Total++;
            if (dir == Vector3.zero) continue;

            Vector3 rel = s.Position - raftPos; rel.y = 0f;
            float along = Vector3.Dot(rel, dir);
            if (along <= 0f) continue;                                // already behind the raft
            float lateral = (rel - dir * along).magnitude;
            float eta = along / speed;

            if (lateral <= halfWidth + QoLConfig.NetCourseMargin)
            {
                r.OnCourse++;
                if (r.BestEta < 0f || eta < r.BestEta) r.BestEta = eta;
            }
            else if (lateral <= halfWidth + QoLConfig.HookCourseMargin) r.Hookable++;
        }
    }

    public static void OnGUI()
    {
        if (!QoLConfig.SmartDebrisTracking || raft == null) return;
        float w = 250f, x = Screen.width - w - 12f, y = 12f;
        int lines = Mathf.Max(1, rows.Count);
        QDraw.Panel(new Rect(x, y, w, 30 + lines * 19));
        QDraw.Text(x + 8, y + 5, "DEBRIS   raft " + raftVel.magnitude.ToString("F1") + " m/s", Color.white, 13);

        float ly = y + 26;
        if (rows.Count == 0) { QDraw.Text(x + 8, ly, "nothing in range", new Color(1, 1, 1, 0.6f), 12); return; }
        foreach (KeyValuePair<LootKind, Row> kv in rows)
        {
            Row r = kv.Value;
            string txt = kv.Key + "  x" + r.Total;
            if (r.OnCourse > 0) txt += "   net " + r.OnCourse + " (" + Mathf.RoundToInt(r.BestEta) + "s)";
            if (r.Hookable > 0) txt += "   hook " + r.Hookable;
            QDraw.Text(x + 8, ly, txt, QoLConfig.KindColor(kv.Key), 12);
            ly += 19;
        }
    }
}

// ============================================================================
//  39  ITEM DETECTOR SCAN MODE
//  Press ScanKey: an expanding pulse, then a sorted list + world labels of
//  everything within ScanRadius (floating, underwater, on land) for a few seconds.
// ============================================================================
public static class F39_ItemDetectorScan
{
    static float scanStart = -99f;
    static float cooldownUntil = -1f;
    static readonly List<ScannedItem> results = new List<ScannedItem>();

    static bool Showing { get { return Time.time - scanStart < QoLConfig.ScanDuration; } }

    public static void Tick()
    {
        if (!QoLConfig.ItemDetectorScan || QoLContext.MenuOpen) return;
        if (!QoLConfig.Chord(QoLConfig.ScanKey)) return;

        if (Time.time < cooldownUntil)
        {
            F44_SoundAlerts.Toast("Scanner recharging: " + Mathf.CeilToInt(cooldownUntil - Time.time) + "s");
            return;
        }
        scanStart = Time.time;
        cooldownUntil = Time.time + QoLConfig.ScanCooldown;
        results.Clear();
        results.AddRange(LootPriority.Sorted(s => s.Distance <= QoLConfig.ScanRadius));
        F44_SoundAlerts.Beep(F44_SoundAlerts.Tone.Scan);
    }

    public static void OnGUI()
    {
        if (!QoLConfig.ItemDetectorScan || !Showing) return;
        float t = Time.time - scanStart;
        Vector2 centre = new Vector2(Screen.width / 2f, Screen.height / 2f);

        if (t < 1.2f)   // pulse animation
        {
            float k = t / 1.2f;
            QDraw.Circle(centre, Mathf.Lerp(20f, Screen.height * 0.6f, k), new Color(0.3f, 1f, 0.8f, 1f - k), 3f, 40);
        }

        float alpha = Mathf.Clamp01((QoLConfig.ScanDuration - t) / 1.5f);
        float x = 12f, y = Screen.height * 0.35f, w = 270f;
        int shown = Mathf.Min(results.Count, 12);
        QDraw.Panel(new Rect(x, y, w, 28 + Mathf.Max(1, shown) * 18));
        QDraw.Text(x + 8, y + 5, "SCAN  " + results.Count + " items / " + QoLConfig.ScanRadius + "m", new Color(0.3f, 1f, 0.8f, alpha), 13);

        float ly = y + 26;
        int n = 0;
        foreach (ScannedItem s in results)
        {
            if (!s.Valid) continue;
            Color c = QoLConfig.KindColor(s.Kind); c.a = alpha;
            if (n < 12)
            {
                string where = s.Underwater ? "under" : s.OnGround ? "land" : "sea";
                QDraw.Text(x + 8, ly, s.DisplayName + "  " + Mathf.RoundToInt(s.Distance) + "m  " + QDraw.BearingText(s.Position) + "  [" + where + "]", c, 12);
                ly += 18;
            }
            n++;

            Vector2 g;
            if (QDraw.WorldToGUI(s.Position, out g))
            {
                QDraw.Rect(new Rect(g.x - 3, g.y - 3, 6, 6), c);
                if (s.Distance < 35f) QDraw.TextCentered(g + new Vector2(0, 14), s.DisplayName, c, 10);
            }
        }
    }
}
