using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

// ============================================================================
//  44  COLLECTION SOUND ALERTS
//  Plays the game's own FMOD sound events (no audio files). Alerts for:
//  valuable loot entering range, nets becoming full, magnet/scan/collect
//  feedback. Also owns the small on-screen "toast" message used by other features.
//  Raft plays all its audio through FMOD with Unity's built-in audio disabled,
//  so Unity AudioClips/AudioSources stay silent there (every generated tone
//  logged "AudioClip.SetData failed; AudioClip contains no data").
// ============================================================================
public static class F44_SoundAlerts
{
    public enum Tone { Click, Collect, Start, Scan, Rare, NetFull }

    struct Sound
    {
        public string Path;
        public float Gain;
        public Sound(string path, float peakDb) { Path = path; Gain = Mathf.Pow(10f, -peakDb / 20f); }
    }

    // Paths from Raft's MasterBank.strings.bank; all are one-shots (none loop). The second value is
    // the event's measured peak level: the game mixes its sounds with a lot of headroom, so each one
    // is lifted to a full-scale peak. Volume then means what it did for the old generated tones
    // (1.0 = full scale; the default 0.35 lands between the game's own menu click and fail sound).
    static readonly Dictionary<Tone, Sound> sounds = new Dictionary<Tone, Sound>
    {
        { Tone.Click,   new Sound("event:/ui/click",                -12.7f) },
        { Tone.Collect, new Sound("event:/ui/pickup",               -23.4f) },
        { Tone.Start,   new Sound("event:/raft/radar/radar_on",     -23.9f) },
        { Tone.Scan,    new Sound("event:/raft/radar/radar_bip",    -26.4f) },
        { Tone.Rare,    new Sound("event:/exploring/shop/new_tier", -12.1f) },
        { Tone.NetFull, new Sound("event:/ui/fail",                  -7.2f) },
    };
    static readonly Sound Fallback = new Sound("event:/ui/click", -12.7f);   // if a game update renames one

    static readonly Dictionary<Tone, EventDescription> events = new Dictionary<Tone, EventDescription>();
    static readonly Dictionary<Tone, float> gains = new Dictionary<Tone, float>();
    static readonly HashSet<Tone> warned = new HashSet<Tone>();
    static readonly Dictionary<Tone, float> lastPlayed = new Dictionary<Tone, float>();
    static readonly HashSet<int> announced = new HashSet<int>();
    static readonly HashSet<int> fullNets = new HashSet<int>();
    static float nextNetCheck;

    static string toastText;
    static float toastUntil;

    public static void Init()
    {
        // Events are looked up on first use instead: the game may still be loading its
        // FMOD banks when the plugin starts.
        events.Clear();
        warned.Clear();
    }

    static bool TryGetEvent(Tone t, out EventDescription desc)
    {
        if (events.TryGetValue(t, out desc) && desc.isValid()) return true;
        if (!RuntimeManager.IsInitialized) return false;   // never spin up FMOD ourselves

        FMOD.Studio.System studio = RuntimeManager.StudioSystem;
        Sound s = sounds[t];
        if (studio.getEvent(s.Path, out desc) != FMOD.RESULT.OK)
        {
            if (warned.Add(t)) QoLPlugin.Log.LogWarning("Sound alert " + t + ": FMOD event " + s.Path + " not found, using " + Fallback.Path + ".");
            s = Fallback;
            if (studio.getEvent(s.Path, out desc) != FMOD.RESULT.OK) return false;
        }
        events[t] = desc;
        gains[t] = s.Gain;
        return true;
    }

    public static void Beep(Tone t)
    {
        if (!QoLConfig.SoundAlerts) return;
        float last;
        if (lastPlayed.TryGetValue(t, out last) && Time.time - last < 0.25f) return;
        lastPlayed[t] = Time.time;

        EventDescription desc;
        EventInstance inst;
        if (!TryGetEvent(t, out desc) || desc.createInstance(out inst) != FMOD.RESULT.OK) return;
        // Some of these events are 3D: placing them on the camera (the listener) plays them centred.
        Camera cam = QoLContext.Cam != null ? QoLContext.Cam : Camera.main;
        inst.set3DAttributes(RuntimeUtils.To3DAttributes(cam != null ? cam.transform.position : Vector3.zero));
        inst.setVolume(QoLConfig.AlertVolume * gains[t]);
        inst.start();
        inst.release();   // FMOD frees the instance once it finishes playing
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
        // The event descriptions belong to FMOD's loaded banks; nothing to release here.
        events.Clear();
        announced.Clear();
        fullNets.Clear();
    }
}
