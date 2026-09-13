using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// ============================================================================
//  44  COLLECTION SOUND ALERTS
//  Procedurally generated tones (no audio files). Alerts for: valuable loot
//  entering range, nets becoming full, magnet/scan/collect feedback.
//  Also owns the small on-screen "toast" message used by other features.
// ============================================================================
public static class F44_SoundAlerts
{
    public enum Tone { Click, Collect, Start, Scan, Rare, NetFull }

    static GameObject host;
    static AudioSource source;
    static readonly Dictionary<Tone, AudioClip> clips = new Dictionary<Tone, AudioClip>();
    static readonly Dictionary<Tone, float> lastPlayed = new Dictionary<Tone, float>();
    static readonly HashSet<int> announced = new HashSet<int>();
    static readonly HashSet<int> fullNets = new HashSet<int>();
    static float nextNetCheck;

    static string toastText;
    static float toastUntil;

    public static void Init()
    {
        if (host != null) return;
        host = new GameObject("CollectionQoL_Audio");
        Object.DontDestroyOnLoad(host);
        source = host.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.playOnAwake = false;

        clips[Tone.Click]   = MakeTone(900f, 0f, 0.05f);
        clips[Tone.Collect] = MakeTone(660f, 990f, 0.12f);
        clips[Tone.Start]   = MakeTone(440f, 880f, 0.25f);
        clips[Tone.Scan]    = MakeTone(300f, 1200f, 0.45f);
        clips[Tone.Rare]    = MakeTone(1046f, 1318f, 0.30f);
        clips[Tone.NetFull] = MakeTone(520f, 390f, 0.35f);
    }

    // Sine sweep from f0 to f1 (f1 = 0 means constant) with a soft fade-out.
    static AudioClip MakeTone(float f0, float f1, float duration)
    {
        const int rate = 44100;
        int n = Mathf.Max(1, (int)(rate * duration));
        float[] data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = (float)i / n;
            float f = f1 > 0f ? Mathf.Lerp(f0, f1, k) : f0;
            phase += 2f * Mathf.PI * f / rate;
            float env = Mathf.Min(1f, i / 200f) * (1f - k);
            data[i] = Mathf.Sin(phase) * env;
        }
        AudioClip clip = AudioClip.Create("qol_tone", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static void Beep(Tone t)
    {
        if (!QoLConfig.SoundAlerts || source == null) return;
        float last;
        if (lastPlayed.TryGetValue(t, out last) && Time.time - last < 0.25f) return;
        lastPlayed[t] = Time.time;
        AudioClip c;
        if (clips.TryGetValue(t, out c) && c != null) source.PlayOneShot(c, QoLConfig.AlertVolume);
    }

    public static void Toast(string text)
    {
        toastText = text;
        toastUntil = Time.time + 2.5f;
    }

    public static void Tick()
    {
        if (!QoLConfig.SoundAlerts) return;

        // valuable loot entering range (announce each item once)
        foreach (ScannedItem s in LootScanner.Items)
        {
            if (!s.Valid || s.Distance > QoLConfig.AlertRange) continue;
            if (System.Array.IndexOf(QoLConfig.AlertKinds, s.Kind) < 0) continue;
            if (!announced.Add(s.Pickup.GetInstanceID())) continue;
            Beep(Tone.Rare);
            Toast(s.DisplayName + " nearby  " + Mathf.RoundToInt(s.Distance) + "m  " + QDraw.BearingText(s.Position));
        }
        if (announced.Count > 2000) announced.Clear();

        // nets full
        if (Time.time >= nextNetCheck)
        {
            nextNetCheck = Time.time + 5f;
            foreach (Component net in RaftRefs.GetNets())
            {
                if (net == null) continue;
                int count = RaftRefs.GetNetCount(net), cap = RaftRefs.GetNetCapacity(net);
                if (count < 0 || cap <= 0) continue;
                int id = net.GetInstanceID();
                if (count >= cap)
                {
                    if (fullNets.Add(id)) { Beep(Tone.NetFull); Toast("A collection net is full"); }
                }
                else fullNets.Remove(id);
            }
        }
    }

    public static void OnGUI()
    {
        if (string.IsNullOrEmpty(toastText) || Time.time > toastUntil) return;
        float a = Mathf.Clamp01(toastUntil - Time.time);
        QDraw.TextCentered(new Vector2(Screen.width / 2f, Screen.height * 0.22f), toastText, new Color(1f, 1f, 0.8f, a), 16);
    }

    public static void Cleanup()
    {
        if (host != null) Object.Destroy(host);
        host = null;
        source = null;
        foreach (AudioClip c in clips.Values) if (c != null) Object.Destroy(c);
        clips.Clear();
        announced.Clear();
        fullNets.Clear();
    }
}
