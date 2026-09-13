using UnityEngine;

// ============================================================================
//  35  MAGNETIC COLLECTOR (TIMED)
//  Press MagnetKey: for N seconds, floating loot is pulled toward you, highest
//  priority first. Close items are collected. Then a cooldown starts.
// ============================================================================
public static class F35_MagneticCollector
{
    static float activeUntil = -1f;
    static float cooldownUntil = -1f;

    public static bool Active { get { return Time.time < activeUntil; } }

    public static void Tick()
    {
        if (!QoLConfig.MagneticCollector) return;

        if (!QoLContext.MenuOpen && QoLConfig.Chord(QoLConfig.MagnetKey))
        {
            if (Time.time >= cooldownUntil)
            {
                activeUntil = Time.time + QoLConfig.MagnetDuration;
                cooldownUntil = activeUntil + QoLConfig.MagnetCooldown;
                F44_SoundAlerts.Beep(F44_SoundAlerts.Tone.Start);
            }
            else
            {
                F44_SoundAlerts.Toast("Magnet cooling down: " + Mathf.CeilToInt(cooldownUntil - Time.time) + "s");
            }
        }
        if (!Active) return;

        Vector3 me = QoLContext.PlayerPos;
        int handled = 0;
        foreach (ScannedItem s in LootPriority.Sorted(x =>
                     x.Floating && x.Distance <= QoLConfig.MagnetRadius &&
                     (x.Kind != LootKind.Barrel || QoLConfig.MagnetPullsBarrels)))
        {
            if (handled++ >= QoLConfig.MagnetMaxItems) break;
            Vector3 flat = new Vector3(me.x, s.Position.y, me.z);
            float flatDist = Vector3.Distance(flat, s.Position);
            if (flatDist <= QoLConfig.MagnetCollectDistance)
            {
                if (QoLConfig.MagnetAutoCollect) RaftRefs.CollectPickup(s.Pickup);
            }
            else
            {
                RaftRefs.MoveItemToward(s.Pickup, flat, QoLConfig.MagnetPullSpeed);
            }
        }
    }

    public static void OnGUI()
    {
        if (!QoLConfig.MagneticCollector) return;
        float w = 220f, x = (Screen.width - w) / 2f, y = 40f;
        if (Active)
        {
            float left = activeUntil - Time.time;
            QDraw.Panel(new Rect(x, y, w, 24));
            QDraw.Rect(new Rect(x + 2, y + 2, (w - 4) * (left / QoLConfig.MagnetDuration), 20), new Color(0.3f, 0.7f, 1f, 0.7f));
            QDraw.TextCentered(new Vector2(Screen.width / 2f, y + 12), "MAGNET  " + left.ToString("F1") + "s", Color.white, 13);
        }
        else if (Time.time < cooldownUntil)
        {
            QDraw.TextCentered(new Vector2(Screen.width / 2f, y + 12), "Magnet " + Mathf.CeilToInt(cooldownUntil - Time.time) + "s", new Color(1, 1, 1, 0.5f), 12);
        }
    }
}

// ============================================================================
//  36  HAND PICKUP (ISLAND ONLY)
//  Only while standing on land (not raft, not water): allowed small pickups
//  within a short radius are collected one at a time.
// ============================================================================
public static class F36_HandPickupIsland
{
    static float next;
    public static bool OnIsland;

    public static void Tick()
    {
        if (!QoLConfig.HandPickupIsland || Time.time < next) return;
        next = Time.time + QoLConfig.IslandPickupInterval;

        OnIsland = RaftRefs.IsOnLand(QoLContext.Player);
        if (!OnIsland) return;

        ScannedItem best = LootPriority.Best(s =>
            !s.Underwater &&
            s.Distance <= QoLConfig.IslandPickupRadius &&
            System.Array.IndexOf(QoLConfig.IslandAllowed, s.Kind) >= 0);

        if (best != null && RaftRefs.CollectPickup(best.Pickup))
            F44_SoundAlerts.Beep(F44_SoundAlerts.Tone.Click);
    }
}

// ============================================================================
//  42  UNDERWATER LOOT ASSIST
//  While your camera is underwater: markers on underwater loot, a line to the
//  best target, dive timer + depth, and optional auto-grab when very close.
// ============================================================================
public static class F42_UnderwaterLootAssist
{
    static float diveStart = -1f;
    static ScannedItem best;

    public static bool Underwater
    {
        get { return QoLContext.Cam != null && QoLContext.Cam.transform.position.y < QoLConfig.WaterLevelY - 0.2f; }
    }

    public static void Tick()
    {
        best = null;
        if (!QoLConfig.UnderwaterLootAssist) return;
        if (!Underwater) { diveStart = -1f; return; }
        if (diveStart < 0f) diveStart = Time.time;

        best = LootPriority.Best(s => s.Underwater && s.Distance <= QoLConfig.UnderwaterRange);
        if (best != null && QoLConfig.UnderwaterAutoGrab && best.Distance <= QoLConfig.UnderwaterGrabRadius)
        {
            if (RaftRefs.CollectPickup(best.Pickup)) F44_SoundAlerts.Beep(F44_SoundAlerts.Tone.Click);
        }
    }

    public static void OnGUI()
    {
        if (!QoLConfig.UnderwaterLootAssist || diveStart < 0f || QoLContext.Cam == null) return;

        float depth = QoLConfig.WaterLevelY - QoLContext.Cam.transform.position.y;
        QDraw.Panel(new Rect(Screen.width / 2f - 90, Screen.height - 150, 180, 42));
        QDraw.TextCentered(new Vector2(Screen.width / 2f, Screen.height - 138), "Dive " + (Time.time - diveStart).ToString("F0") + "s", Color.white, 14);
        QDraw.TextCentered(new Vector2(Screen.width / 2f, Screen.height - 120), "Depth " + depth.ToString("F1") + "m", new Color(0.6f, 0.9f, 1f), 12);

        foreach (ScannedItem s in LootScanner.Items)
        {
            if (!s.Valid || !s.Underwater || s.Distance > QoLConfig.UnderwaterRange) continue;
            Vector2 g;
            if (!QDraw.WorldToGUI(s.Position, out g)) continue;
            Color c = QoLConfig.KindColor(s.Kind);
            bool isBest = s == best;
            QDraw.Corners(g, isBest ? 40f : 24f, c, isBest ? 3f : 2f);
            QDraw.TextCentered(g + new Vector2(0, isBest ? 28 : 20), s.DisplayName + " " + Mathf.RoundToInt(s.Distance) + "m", c, isBest ? 14 : 11);
            if (isBest) QDraw.Line(new Vector2(Screen.width / 2f, Screen.height / 2f), g, new Color(c.r, c.g, c.b, 0.4f), 2f);
        }
    }
}
