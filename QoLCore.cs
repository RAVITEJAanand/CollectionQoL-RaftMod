using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class ScannedItem
{
    public PickupItem Pickup;
    public string UniqueName;
    public string DisplayName;
    public LootKind Kind;
    public Vector3 Position;
    public float Distance;
    public bool Floating;
    public bool Underwater;
    public bool OnGround;

    public bool Valid { get { return Pickup != null && Pickup.gameObject.activeInHierarchy; } }
}

// ============================================================================
//  LOOT SCANNER - one shared scan used by every feature (cheap + consistent)
// ============================================================================
public static class LootScanner
{
    public static readonly List<ScannedItem> Items = new List<ScannedItem>();
    static readonly Dictionary<int, string> nameCache = new Dictionary<int, string>();
    static float nextScan;

    public static void Tick()
    {
        Vector3 pp = QoLContext.PlayerPos;
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + QoLConfig.ScanInterval;
            FullScan(pp);
        }
        else
        {
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                ScannedItem s = Items[i];
                if (!s.Valid) { Items.RemoveAt(i); continue; }
                s.Position = s.Pickup.transform.position;
                s.Distance = Vector3.Distance(pp, s.Position);
                UpdateState(s);
            }
        }
    }

    static void FullScan(Vector3 pp)
    {
        Items.Clear();
        PickupItem[] all = Object.FindObjectsOfType<PickupItem>();
        float max = QoLConfig.MaxScanDistance;
        foreach (PickupItem p in all)
        {
            if (p == null || !p.gameObject.activeInHierarchy) continue;
            Vector3 pos = p.transform.position;
            float d = Vector3.Distance(pp, pos);
            if (d > max) continue;
            if (p.GetComponentInParent<Raft>() != null) continue;           // skip things built/planted on the raft
            if (QoLContext.Player != null && p.transform.IsChildOf(QoLContext.Player.transform)) continue;

            ScannedItem s = new ScannedItem();
            s.Pickup = p;
            s.Position = pos;
            s.Distance = d;
            int id = p.GetInstanceID();
            string uname;
            if (!nameCache.TryGetValue(id, out uname))
            {
                uname = RaftRefs.GetUniqueName(p);
                nameCache[id] = uname;
            }
            s.UniqueName = uname;
            s.DisplayName = uname ?? CleanName(p.gameObject.name);
            s.Kind = RaftRefs.Classify(uname ?? p.gameObject.name);
            UpdateState(s);
            Items.Add(s);
        }
        if (nameCache.Count > 4000) nameCache.Clear();
    }

    static void UpdateState(ScannedItem s)
    {
        float w = QoLConfig.WaterLevelY;
        s.Floating   = Mathf.Abs(s.Position.y - w) <= 1.2f;
        s.Underwater = s.Position.y < w - 1.2f;
        s.OnGround   = s.Position.y > w + 1.2f;
    }

    public static string CleanName(string n)
    {
        return n.Replace("(Clone)", "").Replace("_", " ").Trim();
    }
}

// ============================================================================
//  43  RESOURCE PRIORITY PICKUP (shared by magnet, hook assist, auto grabs)
// ============================================================================
public static class LootPriority
{
    public static int PresetIndex;

    public static string PresetName
    {
        get { return QoLConfig.PriorityPresetNames[PresetIndex % QoLConfig.PriorityPresetNames.Length]; }
    }

    public static void Tick()
    {
        if (!QoLConfig.PriorityPickup || QoLContext.MenuOpen) return;
        if (QoLConfig.Chord(QoLConfig.PriorityCycleKey))
        {
            PresetIndex = (PresetIndex + 1) % QoLConfig.PriorityPresets.Length;
            F44_SoundAlerts.Toast("Priority: " + PresetName);
            F44_SoundAlerts.Beep(F44_SoundAlerts.Tone.Click);
        }
    }

    public static int Weight(LootKind k)
    {
        LootKind[] order = QoLConfig.PriorityPresets[PresetIndex % QoLConfig.PriorityPresets.Length];
        for (int i = 0; i < order.Length; i++) if (order[i] == k) return order.Length - i;
        return 0;
    }

    // Higher = better. Priority dominates, distance breaks ties.
    public static float Score(ScannedItem s)
    {
        if (!QoLConfig.PriorityPickup) return -s.Distance;
        return Weight(s.Kind) * 1000f - s.Distance;
    }

    public static List<ScannedItem> Sorted(System.Predicate<ScannedItem> filter)
    {
        List<ScannedItem> list = new List<ScannedItem>();
        foreach (ScannedItem s in LootScanner.Items) if (s.Valid && filter(s)) list.Add(s);
        list.Sort((a, b) => Score(b).CompareTo(Score(a)));
        return list;
    }

    public static ScannedItem Best(System.Predicate<ScannedItem> filter)
    {
        ScannedItem best = null;
        float bestScore = float.MinValue;
        foreach (ScannedItem s in LootScanner.Items)
        {
            if (!s.Valid || !filter(s)) continue;
            float sc = Score(s);
            if (sc > bestScore) { bestScore = sc; best = s; }
        }
        return best;
    }
}

// ============================================================================
//  GUI DRAW HELPERS (no asset bundles needed)
// ============================================================================
public static class QDraw
{
    static Texture2D white;
    static GUIStyle label;

    public static Texture2D White
    {
        get
        {
            if (white == null)
            {
                white = new Texture2D(1, 1);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            return white;
        }
    }

    static GUIStyle Style(int size)
    {
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label);
            label.richText = false;
            label.wordWrap = false;
        }
        label.fontSize = size;
        return label;
    }

    public static void Rect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, White);
        GUI.color = old;
    }

    public static void Text(float x, float y, string text, Color c, int size)
    {
        GUIStyle st = Style(size);
        Vector2 sz = st.CalcSize(new GUIContent(text));
        Color old = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.8f * c.a);
        GUI.Label(new Rect(x + 1, y + 1, sz.x, sz.y), text, st);
        GUI.color = c;
        GUI.Label(new Rect(x, y, sz.x, sz.y), text, st);
        GUI.color = old;
    }

    public static void TextCentered(Vector2 center, string text, Color c, int size)
    {
        Vector2 sz = Style(size).CalcSize(new GUIContent(text));
        Text(center.x - sz.x / 2f, center.y - sz.y / 2f, text, c, size);
    }

    public static void Panel(Rect r)
    {
        Rect(r, new Color(0f, 0f, 0f, 0.45f));
    }

    public static bool WorldToGUI(Vector3 world, out Vector2 gui)
    {
        gui = Vector2.zero;
        Camera cam = QoLContext.Cam;
        if (cam == null) return false;
        Vector3 s = cam.WorldToScreenPoint(world);
        gui = new Vector2(s.x, Screen.height - s.y);
        return s.z > 0f;
    }

    public static void Corners(Vector2 c, float size, Color col, float t)
    {
        float h = size / 2f, l = size / 3f;
        Rect(new Rect(c.x - h, c.y - h, l, t), col); Rect(new Rect(c.x - h, c.y - h, t, l), col);
        Rect(new Rect(c.x + h - l, c.y - h, l, t), col); Rect(new Rect(c.x + h - t, c.y - h, t, l), col);
        Rect(new Rect(c.x - h, c.y + h - t, l, t), col); Rect(new Rect(c.x - h, c.y + h - l, t, l), col);
        Rect(new Rect(c.x + h - l, c.y + h - t, l, t), col); Rect(new Rect(c.x + h - t, c.y + h - l, t, l), col);
    }

    public static void Line(Vector2 a, Vector2 b, Color c, float width)
    {
        Matrix4x4 m = GUI.matrix;
        float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        float len = Vector2.Distance(a, b);
        GUIUtility.RotateAroundPivot(angle, a);
        Rect(new Rect(a.x, a.y - width / 2f, len, width), c);
        GUI.matrix = m;
    }

    public static void Circle(Vector2 c, float radius, Color col, float width, int segments)
    {
        Vector2 prev = c + new Vector2(radius, 0);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector2 p = c + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            Line(prev, p, col, width);
            prev = p;
        }
    }

    // Degrees left(-) / right(+) of camera forward, flattened.
    public static float Bearing(Vector3 world)
    {
        Camera cam = QoLContext.Cam;
        if (cam == null) return 0f;
        Vector3 f = cam.transform.forward; f.y = 0f;
        Vector3 d = world - cam.transform.position; d.y = 0f;
        return Vector3.SignedAngle(f, d, Vector3.up);
    }

    public static string BearingText(Vector3 world)
    {
        float b = Bearing(world);
        if (Mathf.Abs(b) < 12f) return "ahead";
        if (Mathf.Abs(b) > 150f) return "behind";
        return (b < 0 ? "L " : "R ") + Mathf.RoundToInt(Mathf.Abs(b)) + " deg";
    }
}
