using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

// Shared per-frame state, filled by the main mod before features run.
public static class QoLContext
{
    public static Network_Player Player;
    public static Vector3 PlayerPos;
    public static Camera Cam;
    public static bool MenuOpen;
    public static bool HudVisible = true;
}

// ============================================================================
//  ADAPTER LAYER - every "game internal" access goes through here.
// ============================================================================
public static class RaftRefs
{
    public const BindingFlags ALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static readonly HashSet<string> warned = new HashSet<string>();
    static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
    static readonly Dictionary<string, MemberInfo> memberCache = new Dictionary<string, MemberInfo>();

    public static void Log(string msg)
    {
        if (QoLPlugin.Log != null) QoLPlugin.Log.LogInfo(msg);
        else Debug.Log("[CollectionQoL] " + msg);
    }

    public static void WarnOnce(string key, string msg)
    {
        if (!warned.Add(key)) return;
        if (QoLPlugin.Log != null) QoLPlugin.Log.LogWarning(msg);
        else Debug.LogWarning("[CollectionQoL] " + msg);
    }

    public static bool InGame()
    {
        return GetLocalPlayer() != null;
    }

    // ---------------- LOCAL PLAYER (replaces RML's RAPI.GetLocalPlayer) ----------------
    static Network_Player cachedPlayer;
    static float nextPlayerLookup;

    public static Network_Player GetLocalPlayer()
    {
        if (cachedPlayer != null && cachedPlayer.gameObject.activeInHierarchy) return cachedPlayer;
        cachedPlayer = null;
        if (Time.unscaledTime < nextPlayerLookup) return null;
        nextPlayerLookup = Time.unscaledTime + 1f;

        string route;
        cachedPlayer = FindLocalPlayer(out route);
        if (cachedPlayer != null) Log("Local player found (" + route + ").");
        return cachedPlayer;
    }

    static Network_Player FindLocalPlayer(out string route)
    {
        route = "";
        // 1) Raft's singleton pattern: ComponentManager<Network_Player>.Value
        try
        {
            Type cm = FindType("ComponentManager`1");
            if (cm != null)
            {
                Type g = cm.MakeGenericType(typeof(Network_Player));
                Network_Player p = GetStaticMember(g, "Value") as Network_Player;
                if (p != null && IsLocal(p) != false) { route = "ComponentManager"; return p; }
            }
        }
        catch { }

        Network_Player[] all = Object.FindObjectsOfType<Network_Player>();
        // 2) a bool like IsLocalPlayer on the player
        foreach (Network_Player p in all)
            if (IsLocal(p) == true) { route = QoLConfig.PlayerIsLocalMember; return p; }
        // 3) the player that owns the main camera
        Camera main = Camera.main;
        if (main != null)
            foreach (Network_Player p in all)
                if (main.transform.IsChildOf(p.transform)) { route = "main camera"; return p; }
        // 4) only one player exists (singleplayer)
        if (all.Length == 1) { route = "single player object"; return all[0]; }
        return null;
    }

    static bool? IsLocal(Network_Player p)
    {
        object v = GetMember(p, QoLConfig.PlayerIsLocalMember);
        if (v is bool) return (bool)v;
        return null;
    }

    public static object GetStaticMember(Type type, string name)
    {
        const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        FieldInfo f = type.GetField(name, S);
        if (f != null) return f.GetValue(null);
        PropertyInfo p = type.GetProperty(name, S);
        if (p != null && p.CanRead) return p.GetValue(null, null);
        return null;
    }

    public static Camera GetCamera(Network_Player player)
    {
        Camera c = Camera.main;
        if (c == null && player != null) c = player.GetComponentInChildren<Camera>();
        return c;
    }

    // ---------------- REFLECTION ----------------
    public static Type FindType(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        Type t;
        if (typeCache.TryGetValue(name, out t)) return t;
        t = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { t = a.GetType(name, false); } catch { t = null; }
            if (t != null) break;
        }
        typeCache[name] = t;
        return t;
    }

    static MemberInfo FindMember(Type type, string name)
    {
        string key = type.FullName + "::" + name;
        MemberInfo m;
        if (memberCache.TryGetValue(key, out m)) return m;
        m = null;
        for (Type t = type; t != null && m == null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, ALL | BindingFlags.DeclaredOnly);
            if (f != null) { m = f; break; }
            PropertyInfo p = t.GetProperty(name, ALL | BindingFlags.DeclaredOnly);
            if (p != null && p.GetIndexParameters().Length == 0 && p.CanRead) m = p;
        }
        memberCache[key] = m;
        return m;
    }

    public static object GetMember(object obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name)) return null;
        MemberInfo m = FindMember(obj.GetType(), name);
        try
        {
            FieldInfo f = m as FieldInfo;
            if (f != null) return f.GetValue(obj);
            PropertyInfo p = m as PropertyInfo;
            if (p != null) return p.GetValue(obj, null);
        }
        catch { }
        return null;
    }

    public static MethodInfo FindMethod(Type type, string name)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            foreach (MethodInfo mi in t.GetMethods(ALL | BindingFlags.DeclaredOnly))
                if (mi.Name == name) return mi;
        }
        return null;
    }

    // ---------------- PICKUP INFO ----------------
    public static string GetUniqueName(PickupItem p)
    {
        object inst = GetMember(p, QoLConfig.PickupItemInstanceField);
        object baseItem = GetMember(inst, QoLConfig.ItemInstanceBaseField);
        string n = GetMember(baseItem, "UniqueName") as string;
        return string.IsNullOrEmpty(n) ? null : n;
    }

    public static int GetAmount(PickupItem p)
    {
        object inst = GetMember(p, QoLConfig.PickupItemInstanceField);
        object a = GetMember(inst, QoLConfig.ItemInstanceAmountField);
        if (a is int && (int)a > 0) return (int)a;
        return 1;
    }

    public static LootKind Classify(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("barrel") || n.Contains("crate")) return LootKind.Barrel;
        if (n.Contains("plank")) return LootKind.Plank;
        if (n.Contains("plastic")) return LootKind.Plastic;
        if (n.Contains("thatch") || n.Contains("palm") || n.Contains("leaf")) return LootKind.Leaf;
        if (n.Contains("scrap")) return LootKind.Scrap;
        if (n.Contains("seaweed")) return LootKind.Seaweed;
        if (n.Contains("clay")) return LootKind.Clay;
        if (n.Contains("sand")) return LootKind.Sand;
        if (n.Contains("stone") || n.Contains("pebble")) return LootKind.Stone;
        if (n.Contains("beet") || n.Contains("potato") || n.Contains("pineapple") || n.Contains("banana")
            || n.Contains("coconut") || n.Contains("berr") || n.Contains("mango") || n.Contains("watermelon"))
            return LootKind.Food;
        return LootKind.Other;
    }

    // ---------------- ACTIONS ----------------
    public static void MoveItemToward(PickupItem p, Vector3 target, float speed)
    {
        if (p == null) return;
        Vector3 next = Vector3.MoveTowards(p.transform.position, target, speed * Time.deltaTime);
        Rigidbody rb = p.GetComponent<Rigidbody>();
        if (rb != null) rb.MovePosition(next);
        else p.transform.position = next;
    }

    public static bool CollectPickup(PickupItem p)
    {
        if (p == null || !p.gameObject.activeInHierarchy) return false;
        Network_Player player = QoLContext.Player;
        if (player == null) return false;

        if (TryGamePickup(p, player)) return true;

        if (!QoLConfig.AllowFallbackPickup) return false;
        string unique = GetUniqueName(p);
        LootKind kind = Classify(unique ?? p.gameObject.name);
        if (kind == LootKind.Barrel) return false;              // barrels have random loot: never fake it
        if (unique == null) unique = QoLConfig.FallbackUniqueName(kind);
        if (unique == null) return false;
        if (!GiveItem(player, unique, GetAmount(p))) return false;
        p.gameObject.SetActive(false);
        return true;
    }

    static bool TryGamePickup(PickupItem p, Network_Player player)
    {
        if (string.IsNullOrEmpty(QoLConfig.PickupOwnerType) || string.IsNullOrEmpty(QoLConfig.PickupMethod)) return false;
        Type owner = FindType(QoLConfig.PickupOwnerType);
        if (owner == null) { WarnOnce("pickOwner", "PickupOwnerType '" + QoLConfig.PickupOwnerType + "' not found."); return false; }
        MethodInfo m = FindMethod(owner, QoLConfig.PickupMethod);
        if (m == null) { WarnOnce("pickMethod", "PickupMethod '" + QoLConfig.PickupMethod + "' not found."); return false; }

        object target = null;
        if (!m.IsStatic)
        {
            Component c = player.GetComponentInChildren(owner, true);
            if (c == null) c = Object.FindObjectOfType(owner) as Component;
            if (c == null) return false;
            target = c;
        }

        ParameterInfo[] ps = m.GetParameters();
        if (ps.Length == 0) return false;
        object[] args = new object[ps.Length];
        Type pt = ps[0].ParameterType;
        if (pt.IsInstanceOfType(p)) args[0] = p;
        else if (typeof(Component).IsAssignableFrom(pt)) args[0] = p.GetComponent(pt);
        if (args[0] == null) return false;
        for (int i = 1; i < ps.Length; i++)
        {
            if (ps[i].IsOptional) args[i] = ps[i].DefaultValue;
            else if (ps[i].ParameterType == typeof(bool)) args[i] = false;
            else { WarnOnce("pickArgs", "PickupMethod has unsupported extra parameters."); return false; }
        }
        try
        {
            object r = m.Invoke(target, args);
            return !(r is bool) || (bool)r;
        }
        catch (Exception e)
        {
            WarnOnce("pickInvoke", "Game pickup call failed: " + e.InnerException);
            return false;
        }
    }

    public static bool GiveItem(Network_Player player, string uniqueName, int amount)
    {
        try
        {
            object inv = GetMember(player, "Inventory");
            if (inv != null)
            {
                // Route A: Inventory.AddItem(string uniqueName, int amount)
                MethodInfo add = inv.GetType().GetMethod("AddItem", ALL, null, new Type[] { typeof(string), typeof(int) }, null);
                if (add != null) { add.Invoke(inv, new object[] { uniqueName, amount }); return true; }

                // Route B: Inventory.AddItem(Item_Base item, int amount) using ItemManager.GetItemByName
                Type im = FindType("ItemManager");
                MethodInfo get = im != null ? im.GetMethod("GetItemByName", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null) : null;
                object item = get != null ? get.Invoke(null, new object[] { uniqueName }) : null;
                if (item != null)
                {
                    foreach (MethodInfo m in inv.GetType().GetMethods(ALL))
                    {
                        if (m.Name != "AddItem") continue;
                        ParameterInfo[] ps = m.GetParameters();
                        if (ps.Length == 2 && ps[0].ParameterType.IsInstanceOfType(item) && ps[1].ParameterType == typeof(int))
                        {
                            m.Invoke(inv, new object[] { item, amount });
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception e) { WarnOnce("give_" + uniqueName, "GiveItem failed for " + uniqueName + ": " + e.Message); }
        WarnOnce("giveNone", "No working GiveItem route found. Check Network_Player.Inventory / AddItem in dnSpy.");
        return false;
    }

    // ---------------- NETS ----------------
    static Component[] netCache = new Component[0];
    static float netCacheTime = -99f;

    public static Component[] GetNets()
    {
        if (Time.time - netCacheTime < 2f) return netCache;
        netCacheTime = Time.time;
        Type t = FindType(QoLConfig.NetTypeName);
        if (t == null) { WarnOnce("netType", "Net type '" + QoLConfig.NetTypeName + "' not found."); netCache = new Component[0]; return netCache; }
        Object[] found = Object.FindObjectsOfType(t);
        List<Component> list = new List<Component>();
        foreach (Object o in found) { Component c = o as Component; if (c != null) list.Add(c); }
        netCache = list.ToArray();
        return netCache;
    }

    public static object GetCollector(Component net)
    {
        object c = GetMember(net, QoLConfig.NetCollectorField);
        return c ?? net;
    }

    // returns -1 when unknown
    public static int GetNetCount(Component net)
    {
        object v = GetMember(GetCollector(net), QoLConfig.NetItemsField);
        if (v is int) return (int)v;
        ICollection col = v as ICollection;
        return col != null ? col.Count : -1;
    }

    public static int GetNetCapacity(Component net)
    {
        object v = GetMember(GetCollector(net), QoLConfig.NetCapacityField);
        return v is int ? (int)v : -1;
    }

    public static bool TryEmptyNet(Component net, Network_Player player)
    {
        object collector = GetCollector(net);
        foreach (string name in QoLConfig.NetEmptyMethods)
        {
            MethodInfo m = FindMethod(collector.GetType(), name);
            if (m == null) continue;
            ParameterInfo[] ps = m.GetParameters();
            object[] args;
            if (ps.Length == 0) args = new object[0];
            else if (ps.Length == 1 && ps[0].ParameterType.IsInstanceOfType(player)) args = new object[] { player };
            else continue;
            try { m.Invoke(m.IsStatic ? null : collector, args); return true; }
            catch (Exception e) { WarnOnce("netInvoke", "Net empty call '" + name + "' failed: " + e.InnerException); }
        }
        WarnOnce("netEmpty", "No net-empty method found. Run the dump (LeftCtrl+F8) and set NetEmptyMethods.");
        return false;
    }

    // ---------------- HOOK ----------------
    public static Component[] GetPlayerHooks(Network_Player player, bool activeOnly)
    {
        Type t = FindType(QoLConfig.HookTypeName);
        if (t == null || player == null) return new Component[0];
        Component[] all = player.GetComponentsInChildren(t, true);
        if (!activeOnly) return all;
        List<Component> list = new List<Component>();
        foreach (Component c in all) if (c != null && c.gameObject.activeInHierarchy) list.Add(c);
        return list.ToArray();
    }

    public static Transform GetHookTip(Component hook)
    {
        foreach (string name in QoLConfig.HookTipFields)
        {
            // Supports dotted paths (e.g. "throwable.throwableObject"). This matters because
            // Hook's own "throwable" field is a Throwable component living on the SAME
            // GameObject as the Hook script itself (Hook.Awake: throwable = GetComponent
            // <Throwable>()), so a single-level lookup of "throwable" resolves to
            // throwable.transform == hook.transform - correctly rejected by the check below,
            // but leaving no working candidate. The real flying hook position is the nested
            // Throwable.throwableObject Transform field (verified against the live game DLL:
            // Throwable.GetPositon() literally returns throwableObject.position).
            object cur = hook;
            foreach (string part in name.Split('.'))
            {
                cur = GetMember(cur, part);
                if (cur == null) break;
            }
            Transform t = AsTransform(cur);
            if (t != null && t != hook.transform) return t;
        }
        // auto-detect: first Rigidbody field on the hook script
        for (Type ty = hook.GetType(); ty != null && ty != typeof(MonoBehaviour); ty = ty.BaseType)
        {
            foreach (FieldInfo f in ty.GetFields(ALL | BindingFlags.DeclaredOnly))
            {
                if (f.IsStatic || f.FieldType != typeof(Rigidbody)) continue;
                Transform t = AsTransform(f.GetValue(hook));
                if (t != null) return t;
            }
        }
        WarnOnce("hookTip", "Hook tip not found. Run the dump and set HookTipFields.");
        return null;
    }

    static Transform AsTransform(object o)
    {
        if (o is Transform) return (Transform)o;
        Component c = o as Component;
        if (c != null) return c.transform;
        GameObject g = o as GameObject;
        return g != null ? g.transform : null;
    }

    // ---------------- ENVIRONMENT ----------------
    public static bool IsOnRaft(Network_Player player)
    {
        RaycastHit hit;
        return GroundHit(player, out hit) && hit.collider.GetComponentInParent<Raft>() != null;
    }

    public static bool IsOnLand(Network_Player player)
    {
        RaycastHit hit;
        if (player.transform.position.y < QoLConfig.WaterLevelY + 0.3f) return false;
        return GroundHit(player, out hit) && hit.collider.GetComponentInParent<Raft>() == null;
    }

    static bool GroundHit(Network_Player player, out RaycastHit best)
    {
        best = new RaycastHit();
        RaycastHit[] hits = Physics.RaycastAll(player.transform.position + Vector3.up * 0.6f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(player.transform)) continue;
            if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
        }
        return found;
    }

    // ---------------- R&D DUMP (LeftCtrl + F8) ----------------
    public static void DumpAll()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("================ CollectionQoL R&D DUMP ================");
        if (QoLContext.Player != null)
            sb.AppendLine("Player position: " + QoLContext.Player.transform.position + "   Camera Y: " + (QoLContext.Cam != null ? QoLContext.Cam.transform.position.y.ToString("F2") : "?"));
        foreach (string tn in QoLConfig.DumpTypes) DumpType(sb, tn);

        sb.AppendLine("---- Nearest pickups ----");
        int n = 0;
        foreach (ScannedItem s in LootScanner.Items)
        {
            if (n++ >= 25) break;
            sb.AppendLine(string.Format("  {0,-28} unique={1,-14} kind={2,-8} y={3,6:F2} dist={4,5:F1}",
                s.Pickup != null ? s.Pickup.gameObject.name : "null", s.UniqueName ?? "?", s.Kind, s.Position.y, s.Distance));
        }
        Debug.Log(sb.ToString());
    }

    static void DumpType(StringBuilder sb, string typeName)
    {
        Type t = FindType(typeName);
        if (t == null) { sb.AppendLine("## " + typeName + " : NOT FOUND"); return; }
        Object inst = typeof(Object).IsAssignableFrom(t) ? Object.FindObjectOfType(t) : null;
        sb.AppendLine("## " + t.FullName + " : " + (t.BaseType != null ? t.BaseType.Name : "") + (inst != null ? "   (live instance found)" : ""));
        foreach (FieldInfo f in t.GetFields(ALL | BindingFlags.DeclaredOnly))
        {
            string val = "";
            if (inst != null && !f.IsStatic && (f.FieldType.IsPrimitive || f.FieldType == typeof(string) || f.FieldType.IsEnum))
            {
                try { val = " = " + f.GetValue(inst); } catch { }
            }
            sb.AppendLine("   F " + (f.IsPublic ? "public " : "private ") + f.FieldType.Name + " " + f.Name + val);
        }
        foreach (MethodInfo m in t.GetMethods(ALL | BindingFlags.DeclaredOnly))
        {
            if (m.IsSpecialName) continue;
            List<string> ps = new List<string>();
            foreach (ParameterInfo p in m.GetParameters()) ps.Add(p.ParameterType.Name + " " + p.Name);
            sb.AppendLine("   M " + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", ps.ToArray()) + ")");
        }
    }
}
