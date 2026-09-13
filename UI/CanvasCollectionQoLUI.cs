using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace CollectionQoL.UI
{
    #region [START] CANVAS COLLECTION QoL SETTINGS UI
    // ============================================================================
    // [START] CANVAS COLLECTION QoL SETTINGS UI
    // Purpose: "Modern Clean" in-game settings canvas for Collection QoL - the same dark,
    //          flat, card-based design system already shared by Farmer's Companion, Inventory
    //          Master, and Sailor's Companion, making Collection QoL the 4th citizen of that
    //          mod family. Exposes the 15 master toggles (features 31-45) plus one well-chosen
    //          tunable per feature. Everything here reads/writes the real ConfigEntry<T>
    //          objects exposed by QoLConfigBinder (GetBool/GetFloat) - never a private copy -
    //          so a change made here persists to the .cfg file exactly like BepInEx
    //          ConfigurationManager would, and QoLConfig's static fields stay in sync via the
    //          existing cfg.SettingChanged -> ApplyAll() pipeline already in QoLConfigBinder.
    //
    //          Collection QoL has no Harmony cursor-patch layer like the other 3 mods -
    //          QoLContext.MenuOpen is derived directly from Cursor.visible (see QoLPlugin.cs
    //          Update()), so Open()/Close() only need to set Cursor.visible/lockState; that
    //          alone makes MenuOpen true/false and correctly suppresses this mod's hotkeys and
    //          OnGUI HUD while the menu is open.
    // ============================================================================
    public class CanvasCollectionQoLUI : MonoBehaviour
    {
        public static CanvasCollectionQoLUI Instance { get; private set; }

        private GameObject _canvasGO;
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private GraphicRaycaster _raycaster;
        private GameObject _rootGO;
        private GameObject _modWindowGO;
        private Font _gameFont;

        public static bool IsWindowOpen => Instance != null && Instance._rootGO != null && Instance._rootGO.activeSelf;

        #region [START] SIBLING MOD NOTE
        // Appended to the description of every feature Sailor's Companion also provides, so the
        // reason a toggle starts off is visible on the row itself rather than only in the .cfg.
        private static string SiblingNote
        {
            get
            {
                return SiblingMods.SailorsCompanionInstalled
                    ? "Sailor's Companion also does this - keep it on in one mod only."
                    : "Sailor's Companion also provides this if you install it.";
            }
        }
        #endregion [END] SIBLING MOD NOTE

        #region [START] MODERN CLEAN PALETTE (shared across the mod family - do not change values)
        private static readonly Color ColBg          = new Color32(0x0F, 0x15, 0x18, 0xFF);
        private static readonly Color ColPanel       = new Color32(0x16, 0x1F, 0x24, 0xFF);
        private static readonly Color ColPanel2      = new Color32(0x1C, 0x27, 0x2D, 0xFF);
        private static readonly Color ColRow         = new Color32(0x1A, 0x24, 0x2A, 0xFF);
        private static readonly Color ColBorder      = new Color32(0x26, 0x33, 0x3B, 0xFF);
        private static readonly Color ColBorderSoft  = new Color32(0x1E, 0x29, 0x30, 0xFF);
        private static readonly Color ColText        = new Color32(0xEA, 0xF3, 0xF1, 0xFF);
        private static readonly Color ColTextMuted   = new Color32(0x8F, 0xA3, 0xA9, 0xFF);
        private static readonly Color ColTextFaint   = new Color32(0x5E, 0x73, 0x79, 0xFF);
        private static readonly Color ColAccent      = new Color32(0x2F, 0xC7, 0xB0, 0xFF);
        private static readonly Color ColAccentStrong= new Color32(0x20, 0xA7, 0x94, 0xFF);
        private static readonly Color ColAccentWash  = new Color(0x2F / 255f, 0xC7 / 255f, 0xB0 / 255f, 0.16f);
        private static readonly Color ColGold        = new Color32(0xE8, 0xB9, 0x4A, 0xFF);
        private static readonly Color ColSuccess     = new Color32(0x5F, 0xBE, 0x7A, 0xFF);
        private static readonly Color ColSuccessWash = new Color(0x5F / 255f, 0xBE / 255f, 0x7A / 255f, 0.16f);
        private static readonly Color ColDanger      = new Color32(0xE0, 0x5A, 0x5A, 0xFF);
        private static readonly Color ColOnAccentTxt = new Color32(0x06, 0x23, 0x1F, 0xFF);
        #endregion

        private const int SCREEN_COUNT = 5;
        // 0=Overview 1=Nets & Hooks 2=Pickup & Magnet 3=Detection & Highlights 4=Priority & Alerts
        private static readonly string[] ScreenLabels = { "Overview", "Nets & Hooks", "Pickup & Magnet", "Detection & Highlights", "Priority & Alerts" };
        private static readonly string[] ScreenMonograms = { "O", "N", "M", "D", "A" };

        private readonly GameObject[] _screens = new GameObject[SCREEN_COUNT];
        private readonly Button[] _railButtons = new Button[SCREEN_COUNT];
        private readonly Image[] _railBg = new Image[SCREEN_COUNT];
        private readonly Text[] _railTexts = new Text[SCREEN_COUNT];
        private readonly Image[] _railIcons = new Image[SCREEN_COUNT];
        private readonly GameObject[] _railBars = new GameObject[SCREEN_COUNT];
        private int _activeScreen = 0;

        #region [START] UNITY LIFECYCLE
        #region [START] AWAKE
        private void Awake()
        {
            Instance = this;
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);
            try
            {
                GetGameFont();
                BuildCanvasUI();
                RaftRefs.Log("CanvasCollectionQoLUI ready. Press " + QoLConfig.KeyMenu + " to open settings.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Collection QoL] CanvasCollectionQoLUI.Awake() FAILED: " + ex);
            }
        }
        #endregion [END] AWAKE

        #region [START] GET GAME FONT
        public Font GetGameFont()
        {
            if (_gameFont != null) return _gameFont;

            try
            {
                _gameFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Semibold", "Segoe UI", "Arial", "Tahoma" }, 24);
            }
            catch { }

            if (_gameFont != null) return _gameFont;

            var texts = Resources.FindObjectsOfTypeAll<Text>();
            foreach (var t in texts)
            {
                if (t != null && t.font != null)
                {
                    _gameFont = t.font;
                    return _gameFont;
                }
            }

            _gameFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _gameFont;
        }
        #endregion [END] GET GAME FONT

        private static float _lastToggleTime = 0f;

        // EXACT name/signature required: a companion "Installed Mods" browser dialog (built in
        // parallel by another agent) reflects into CollectionQoL.UI.CanvasCollectionQoLUI.Toggle().
        #region [START] TOGGLE
        public static void Toggle()
        {
            if (Time.unscaledTime - _lastToggleTime < 0.25f) return;
            _lastToggleTime = Time.unscaledTime;

            if (IsWindowOpen) Close();
            else Open();
        }
        #endregion [END] TOGGLE

        #region [START] OPEN
        public static void Open()
        {
            if (Instance == null)
            {
                var go = new GameObject("CollectionQoL_CanvasUI");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<CanvasCollectionQoLUI>();
            }
            if (Instance._rootGO == null) Instance.BuildCanvasUI();

            Instance.EnsureEventSystem();
            if (Instance._raycaster != null && !Instance._raycaster.enabled) Instance._raycaster.enabled = true;
            Instance._rootGO.SetActive(true);

            // Raft drives input through Unity's New Input System: without switching to the "UI"
            // action map the menu's buttons never reliably receive clicks.
            try
            {
                var cic = CustomInputConfig.Instance;
                if (cic != null)
                {
                    cic.EnableInput();
                    cic.SwitchCurrentActionMap("UI");
                }
            }
            catch { }

            // Raft re-asserts cursor state every frame, so setting Cursor.visible on its own gets
            // overwritten and the pointer vanishes mid-click. Helper.SetCursorVisibleAndLockState
            // is the call the game itself honours. QoLContext.MenuOpen still derives from
            // Cursor.visible (QoLPlugin.cs Update()), so hotkey suppression keeps working.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            try { Helper.SetCursorVisibleAndLockState(true, CursorLockMode.None); }
            catch { }

            // Marks a menu as active so the game stops raycasting world interactions underneath -
            // without it you place blocks / hit things through the open menu.
            try
            {
                if (CanvasHelper.ActiveMenu == MenuType.None)
                {
                    CanvasHelper.ActiveMenu = MenuType.Cheat;
                }
            }
            catch { }

            // Without this the camera keeps turning as the player moves the mouse across the menu.
            try
            {
                var np = ComponentManager<Network_Player>.Value;
                if (np != null && np.PlayerScript != null)
                {
                    np.PlayerScript.SetLockMouseLook(true);
                }
            }
            catch { }

            Instance.RefreshOverview();
            Instance.SelectScreen(Instance._activeScreen);

            // The whole window is built once (while _rootGO is still inactive so it starts
            // hidden) and only re-shown here. Unity's layout system skips computing layout for
            // inactive hierarchies and never recalculates it later just because the object
            // becomes active again, so every nested layout group would otherwise stay at its
            // raw default rect forever. Force one rebuild here, every time the window opens.
            if (Instance._modWindowGO != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(Instance._modWindowGO.GetComponent<RectTransform>());
            }
        }
        #endregion [END] OPEN

        #region [START] CLOSE
        public static void Close()
        {
            if (Instance == null || Instance._rootGO == null) return;
            if (!Instance._rootGO.activeSelf) return;

            Instance._rootGO.SetActive(false);

            // Another Konduri mod's menu may still be open behind this one - re-locking the cursor
            // then would leave that menu unusable. The sibling mods each do the same peer check.
            bool peerModOpen = AnyPeerMenuOpen();

            // Hand control back in the reverse order it was taken, mirroring the sibling mods.
            try
            {
                if (CanvasHelper.ActiveMenu == MenuType.Cheat && !peerModOpen)
                {
                    CanvasHelper.ActiveMenu = MenuType.None;
                }
            }
            catch { }

            try
            {
                var np = ComponentManager<Network_Player>.Value;
                if (np != null && np.PlayerScript != null && !peerModOpen)
                {
                    np.PlayerScript.SetLockMouseLook(false);
                }
            }
            catch { }

            if (peerModOpen)
            {
                // Leave the cursor free for whichever menu is still up.
                return;
            }

            bool inGame = RaftRefs.InGame();

            // Only gameplay has a "Player" action map to return to - switching to it from the main
            // menu leaves the home screen's own buttons unable to receive clicks.
            try
            {
                var cic = CustomInputConfig.Instance;
                if (cic != null)
                {
                    cic.SwitchCurrentActionMap(inGame ? "Player" : "UI");
                }
            }
            catch { }

            if (inGame)
            {
                try { Helper.SetCursorVisibleAndLockState(false, CursorLockMode.Locked); }
                catch
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            else
            {
                try { Helper.SetCursorVisibleAndLockState(true, CursorLockMode.None); }
                catch
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }
        #endregion [END] CLOSE

        // Reflects into the sibling mods' window flags rather than referencing their assemblies,
        // so Collection QoL still builds and runs with any of them absent.
        private static PropertyInfo[] _peerWindowProps;

        #region [START] ANY PEER MENU OPEN
        private static bool AnyPeerMenuOpen()
        {
            try
            {
                if (_peerWindowProps == null)
                {
                    var found = new List<PropertyInfo>();
                    var wanted = new[]
                    {
                        new[] { "SailorsCompanion.UI.CanvasModUI", "IsWindowOpen" },
                        new[] { "SailorsCompanion.UI.CanvasInstalledModsUI", "IsOpen" },
                        new[] { "FarmersCompanion.UI.CanvasFarmersCompanionUI", "IsWindowOpen" },
                        new[] { "InventoryMaster.UI.CanvasInventoryMasterUI", "IsWindowOpen" },
                    };

                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var w in wanted)
                        {
                            var t = asm.GetType(w[0]);
                            if (t == null) continue;
                            var p = t.GetProperty(w[1], BindingFlags.Public | BindingFlags.Static);
                            if (p != null) found.Add(p);
                        }
                    }
                    // Cached after one scan - an absent peer must not cause a repeat AppDomain walk.
                    _peerWindowProps = found.ToArray();
                }

                for (int i = 0; i < _peerWindowProps.Length; i++)
                {
                    try { if ((bool)_peerWindowProps[i].GetValue(null)) return true; }
                    catch { }
                }
            }
            catch { }

            return false;
        }
        #endregion [END] ANY PEER MENU OPEN

        #region [START] ENSURE EVENT SYSTEM
        private void EnsureEventSystem()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null)
            {
                var existing = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
                if (existing != null)
                {
                    UnityEngine.EventSystems.EventSystem.current = existing;
                    es = existing;
                }
                else
                {
                    var esGO = new GameObject("CollectionQoL_EventSystem");
                    esGO.hideFlags = HideFlags.HideAndDontSave;
                    es = esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                    esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    DontDestroyOnLoad(esGO);
                    UnityEngine.EventSystems.EventSystem.current = es;
                }
            }

            if (es != null)
            {
                if (!es.enabled) es.enabled = true;
                if (!es.gameObject.activeInHierarchy) es.gameObject.SetActive(true);
                es.SetSelectedGameObject(null);
            }
        }
        #endregion [END] ENSURE EVENT SYSTEM

        #region [START] UPDATE
        private void Update()
        {
            if (Input.GetKeyDown(QoLConfig.KeyMenu))
            {
                Toggle();
            }

            if (IsWindowOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (Time.unscaledTime - _lastToggleTime >= 0.25f)
                {
                    _lastToggleTime = Time.unscaledTime;
                    Close();
                }
            }
        }
        #endregion [END] UPDATE
        #endregion [END] UNITY LIFECYCLE

        #region [START] CANVAS CONSTRUCTION
        #region [START] BUILD CANVAS UI
        private void BuildCanvasUI()
        {
            if (_canvasGO != null && _rootGO != null) return;

            if (_canvasGO == null)
            {
                _canvasGO = new GameObject("CollectionQoL_Canvas");
                _canvasGO.hideFlags = HideFlags.HideAndDontSave;
                _canvasGO.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;
                DontDestroyOnLoad(_canvasGO);

                _canvas = _canvasGO.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = 33000; // top-most, matches the sibling mods' menu layer

                _scaler = _canvasGO.AddComponent<CanvasScaler>();
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _scaler.referenceResolution = new Vector2(1920, 1080);
                _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                _scaler.matchWidthOrHeight = 0.5f;

                _raycaster = _canvasGO.AddComponent<GraphicRaycaster>();
            }

            if (_rootGO == null)
            {
                BuildWindow();
                SetLayerRecursively(_canvasGO, LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5);
                _rootGO.SetActive(false);
            }
        }
        #endregion [END] BUILD CANVAS UI

        #region [START] BUILD WINDOW
        private void BuildWindow()
        {
            _rootGO = new GameObject("Root_CollectionQoL");
            _rootGO.transform.SetParent(_canvasGO.transform, false);
            var rootRt = _rootGO.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var dimmerGO = new GameObject("Dimmer");
            dimmerGO.transform.SetParent(_rootGO.transform, false);
            var dimmerRt = dimmerGO.AddComponent<RectTransform>();
            dimmerRt.anchorMin = Vector2.zero;
            dimmerRt.anchorMax = Vector2.one;
            dimmerRt.offsetMin = Vector2.zero;
            dimmerRt.offsetMax = Vector2.zero;
            var dimmerImg = dimmerGO.AddComponent<Image>();
            dimmerImg.color = new Color(0f, 0f, 0f, 0.6f);
            dimmerImg.raycastTarget = false;

            _modWindowGO = new GameObject("CollectionQoL_Window");
            _modWindowGO.transform.SetParent(_rootGO.transform, false);
            var winRt = _modWindowGO.AddComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(1260, 720);
            winRt.anchoredPosition = Vector2.zero;

            var winImg = _modWindowGO.AddComponent<Image>();
            winImg.sprite = GetRoundedSprite(18);
            winImg.type = Image.Type.Sliced;
            winImg.color = ColBorder;
            AddInsetFill(_modWindowGO, 18, ColPanel);

            var railGO = BuildRail();
            railGO.transform.SetParent(_modWindowGO.transform, false);
            var railRt = railGO.GetComponent<RectTransform>();
            railRt.anchorMin = new Vector2(0, 0);
            railRt.anchorMax = new Vector2(0, 1);
            railRt.pivot = new Vector2(0, 0.5f);
            railRt.sizeDelta = new Vector2(248, 0);
            railRt.anchoredPosition = Vector2.zero;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(_modWindowGO.transform, false);
            var contentRt = contentGO.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 0);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.offsetMin = new Vector2(248, 0);
            contentRt.offsetMax = Vector2.zero;

            BuildFooter(contentGO);

            var screensHost = new GameObject("Screens");
            screensHost.transform.SetParent(contentGO.transform, false);
            var screensRt = screensHost.AddComponent<RectTransform>();
            screensRt.anchorMin = Vector2.zero;
            screensRt.anchorMax = Vector2.one;
            screensRt.offsetMin = new Vector2(0, 52);
            screensRt.offsetMax = Vector2.zero;

            _screens[0] = BuildScreenOverview(screensHost);
            _screens[1] = BuildScreenNetsHooks(screensHost);
            _screens[2] = BuildScreenPickupMagnet(screensHost);
            _screens[3] = BuildScreenDetectionHighlights(screensHost);
            _screens[4] = BuildScreenPriorityAlerts(screensHost);

            SelectScreen(0);

            BuildCloseButton();
        }
        #endregion [END] BUILD WINDOW

        #region [START] BUILD CLOSE BUTTON
        private void BuildCloseButton()
        {
            var go = new GameObject("CloseBtn");
            go.transform.SetParent(_modWindowGO.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(32, 32);
            rt.anchoredPosition = new Vector2(-16, -16);

            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite(16);
            img.type = Image.Type.Sliced;
            img.color = ColPanel2;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = ColPanel2;
            cb.highlightedColor = ColDanger;
            cb.pressedColor = ColDanger;
            btn.colors = cb;
            btn.onClick.AddListener(Close);

            var txt = CreateText(go, "✕", 15, FontStyle.Bold, ColTextMuted, TextAnchor.MiddleCenter);
            txt.raycastTarget = false;
            FillParent(txt.gameObject);
        }
        #endregion [END] BUILD CLOSE BUTTON

        // ---------------- RAIL (left navigation) ----------------
        #region [START] BUILD RAIL
        private GameObject BuildRail()
        {
            var railGO = new GameObject("Rail");
            var railImg = railGO.AddComponent<Image>();
            railImg.color = ColPanel2;

            var railLayout = railGO.AddComponent<VerticalLayoutGroup>();
            railLayout.padding = new RectOffset(14, 14, 20, 16);
            railLayout.spacing = 2;
            railLayout.childForceExpandWidth = true;
            railLayout.childForceExpandHeight = false;
            railLayout.childControlWidth = true;
            railLayout.childControlHeight = true;

            var brandGO = new GameObject("Brand");
            brandGO.transform.SetParent(railGO.transform, false);
            var brandLe = brandGO.AddComponent<LayoutElement>();
            brandLe.preferredHeight = 66;
            // A child whose OWN inner HorizontalLayoutGroup sets childForceExpandHeight = true
            // reports flexibleHeight = 1 to its parent (the group forces every one of its children
            // to at least 1 flexible unit on the cross axis, then advertises that total upward).
            // Without pinning it to 0 here, the rail's VerticalLayoutGroup hands surplus height to
            // this row and every nav item instead of only to the dedicated Spacer, inflating them
            // far past their preferredHeight. LayoutElement outranks a LayoutGroup, so 0 wins.
            brandLe.flexibleHeight = 0;
            var brandLayout = brandGO.AddComponent<HorizontalLayoutGroup>();
            brandLayout.childControlWidth = true;
            brandLayout.childControlHeight = true;
            brandLayout.spacing = 10;
            brandLayout.childAlignment = TextAnchor.MiddleLeft;
            brandLayout.childForceExpandWidth = false;
            brandLayout.childForceExpandHeight = true;

            var markGO = new GameObject("Mark");
            markGO.transform.SetParent(brandGO.transform, false);
            var markLe = markGO.AddComponent<LayoutElement>();
            markLe.preferredWidth = 38; markLe.preferredHeight = 38;
            var markImg = markGO.AddComponent<Image>();
            markImg.sprite = GetRoundedSprite(9);
            markImg.type = Image.Type.Sliced;
            markImg.color = ColAccent;
            var markTxt = CreateText(markGO, "C", 18, FontStyle.Bold, ColOnAccentTxt, TextAnchor.MiddleCenter);
            FillParent(markTxt.gameObject);

            var brandTextGO = new GameObject("BrandText");
            brandTextGO.transform.SetParent(brandGO.transform, false);
            var btLe = brandTextGO.AddComponent<LayoutElement>();
            btLe.flexibleWidth = 1f;
            var btLayout = brandTextGO.AddComponent<VerticalLayoutGroup>();
            btLayout.childControlWidth = true;
            btLayout.childControlHeight = true;
            btLayout.childForceExpandWidth = true;
            btLayout.childForceExpandHeight = false;
            btLayout.spacing = 1;

            var nameTxt = CreateText(brandTextGO, "Collection QoL", 16, FontStyle.Bold, ColText, TextAnchor.MiddleLeft);
            nameTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 17;

            var subGO = new GameObject("Sub");
            subGO.transform.SetParent(brandTextGO.transform, false);
            var subLe = subGO.AddComponent<LayoutElement>();
            subLe.preferredHeight = 15;
            var subTxt = CreateText(subGO, "Loot & Collection", 13, FontStyle.Normal, ColTextFaint, TextAnchor.MiddleLeft);
            FillParent(subTxt.gameObject);

            AddDivider(railGO.transform, 18);

            for (int i = 0; i < SCREEN_COUNT; i++)
            {
                int idx = i;
                var itemGO = BuildRailItem(idx, ScreenLabels[i], ScreenMonograms[i], () => SelectScreen(idx));
                itemGO.transform.SetParent(railGO.transform, false);
                var itemLe = itemGO.AddComponent<LayoutElement>();
                itemLe.preferredHeight = 44;
                itemLe.flexibleHeight = 0; // see Brand above - keeps nav items at 44, not stretched
                _railButtons[i] = itemGO.GetComponent<Button>();
            }

            var spacerGO = new GameObject("Spacer");
            spacerGO.transform.SetParent(railGO.transform, false);
            var spacerLe = spacerGO.AddComponent<LayoutElement>();
            spacerLe.flexibleHeight = 1f;

            AddDivider(railGO.transform, 10);

            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(railGO.transform, false);
            var statusLe = statusGO.AddComponent<LayoutElement>();
            statusLe.preferredHeight = 26;
            statusLe.flexibleHeight = 0; // see Brand above
            var statusLayout = statusGO.AddComponent<HorizontalLayoutGroup>();
            statusLayout.childControlWidth = true;
            statusLayout.childControlHeight = true;
            statusLayout.spacing = 7;
            statusLayout.padding = new RectOffset(6, 0, 0, 0);
            statusLayout.childAlignment = TextAnchor.MiddleLeft;
            statusLayout.childForceExpandWidth = false;
            statusLayout.childForceExpandHeight = true;

            var dotGO = new GameObject("Dot");
            dotGO.transform.SetParent(statusGO.transform, false);
            var dotLe = dotGO.AddComponent<LayoutElement>();
            dotLe.preferredWidth = 6; dotLe.preferredHeight = 6;
            var dotImg = dotGO.AddComponent<Image>();
            dotImg.sprite = GetRoundedSprite(3);
            dotImg.type = Image.Type.Sliced;
            dotImg.color = ColAccent;

            var statusTxt = CreateText(statusGO, QoLConfig.KeyMenu + " menu · " + QoLConfig.ChordLabel(QoLConfig.MagnetKey) + " magnet ready", 15f, FontStyle.Normal, ColTextFaint, TextAnchor.MiddleLeft);
            var stLe = statusTxt.gameObject.AddComponent<LayoutElement>();
            stLe.flexibleWidth = 1f;

            return railGO;
        }
        #endregion [END] BUILD RAIL

        #region [START] BUILD RAIL ITEM
        private GameObject BuildRailItem(int index, string label, string monogram, Action onClick)
        {
            var go = new GameObject("Rail_" + label);
            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite(9);
            img.type = Image.Type.Sliced;
            img.color = Color.clear;
            if (index >= 0) _railBg[index] = img;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(10, 8, 0, 0);
            layout.spacing = 9;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var barGO = new GameObject("Bar");
            barGO.transform.SetParent(go.transform, false);
            var barRt = barGO.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0, 0.5f);
            barRt.anchorMax = new Vector2(0, 0.5f);
            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.sizeDelta = new Vector2(3, 16);
            barRt.anchoredPosition = new Vector2(-11, 0);
            var barImg = barGO.AddComponent<Image>();
            barImg.sprite = GetRoundedSprite(2);
            barImg.type = Image.Type.Sliced;
            barImg.color = ColAccent;
            barGO.SetActive(false);
            if (index >= 0) _railBars[index] = barGO;

            var monoGO = new GameObject("Mono");
            monoGO.transform.SetParent(go.transform, false);
            var monoLe = monoGO.AddComponent<LayoutElement>();
            monoLe.preferredWidth = 20; monoLe.preferredHeight = 20;
            var monoImg = monoGO.AddComponent<Image>();
            monoImg.sprite = GetRoundedSprite(6);
            monoImg.type = Image.Type.Sliced;
            monoImg.color = ColBorderSoft;
            if (index >= 0) _railIcons[index] = monoImg;
            var monoTxt = CreateText(monoGO, monogram, 13, FontStyle.Bold, ColTextMuted, TextAnchor.MiddleCenter);
            FillParent(monoTxt.gameObject);

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lblLe = lblGO.AddComponent<LayoutElement>();
            lblLe.flexibleWidth = 1f;
            var lblTxt = CreateText(lblGO, label, 15.5f, FontStyle.Normal, ColTextMuted, TextAnchor.MiddleLeft);
            FillParent(lblTxt.gameObject);
            if (index >= 0) _railTexts[index] = lblTxt;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick());

            return go;
        }
        #endregion [END] BUILD RAIL ITEM

        #region [START] SELECT SCREEN
        public void SelectScreen(int index)
        {
            _activeScreen = index;
            for (int i = 0; i < SCREEN_COUNT; i++)
            {
                bool active = (i == index);
                if (_screens[i] != null) _screens[i].SetActive(active);
                if (_railBg[i] != null) _railBg[i].color = active ? ColAccentWash : Color.clear;
                if (_railTexts[i] != null) _railTexts[i].color = active ? ColText : ColTextMuted;
                if (_railIcons[i] != null) _railIcons[i].color = active ? ColAccent : ColBorderSoft;
                if (_railBars[i] != null) _railBars[i].SetActive(active);
            }

            if (index == 0) RefreshOverview();

            if (_screens[index] != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_screens[index].GetComponent<RectTransform>());
            }
        }
        #endregion [END] SELECT SCREEN

        // ---------------- SCREEN: OVERVIEW ----------------
        private Text _ovStatusNets, _ovStatusPickup, _ovStatusDetect, _ovStatusPriority;

        #region [START] BUILD SCREEN OVERVIEW
        private GameObject BuildScreenOverview(GameObject parent)
        {
            var screen = CreateScreenShell(parent, "Overview", "Everything Collection QoL is doing across your raft, at a glance.", null, out var body);

            AddGroupLabel(body, "Categories");
            var catRowGO = new GameObject("CatGrid");
            catRowGO.transform.SetParent(body.transform, false);
            var catLe = catRowGO.AddComponent<LayoutElement>();
            catLe.preferredHeight = 132;
            var catLayout = catRowGO.AddComponent<HorizontalLayoutGroup>();
            catLayout.childControlWidth = true;
            catLayout.childControlHeight = true;
            catLayout.spacing = 12;
            catLayout.childForceExpandWidth = true;
            catLayout.childForceExpandHeight = true;

            BuildCategoryCard(catRowGO.transform, "Nets & Hooks", "Auto-empty nets, bigger net range, faster/more accurate hooking.", out _ovStatusNets, () => SelectScreen(1));
            BuildCategoryCard(catRowGO.transform, "Pickup & Magnet", "Timed magnet, hands-free land pickup, underwater assist.", out _ovStatusPickup, () => SelectScreen(2));
            BuildCategoryCard(catRowGO.transform, "Detection & Highlights", "Debris tracking, resource brackets, scanner pulse, loot glow.", out _ovStatusDetect, () => SelectScreen(3));
            BuildCategoryCard(catRowGO.transform, "Priority & Alerts", "Barrel assist, priority order, sound alerts, floating radar.", out _ovStatusPriority, () => SelectScreen(4));

            AddGroupLabel(body, "Hotkeys");
            var hkCard = CreateCard(body);
            // Read the live bindings rather than hardcoding them - these are user-configurable, so
            // a hardcoded chip goes stale the moment a key is remapped.
            AddHotkeyRow(hkCard, QoLConfig.ChordLabel(QoLConfig.MagnetKey), "Magnetic Collector - timed loot pull toward you.");
            AddHotkeyRow(hkCard, QoLConfig.ChordLabel(QoLConfig.ScanKey), "Item Detector Scan - reveal nearby loot on the HUD.");
            AddHotkeyRow(hkCard, QoLConfig.ChordLabel(QoLConfig.PriorityCycleKey), "Cycle the resource priority preset.");
            AddHotkeyRow(hkCard, QoLConfig.ChordLabel(QoLConfig.HudToggleKey), "Show / hide the Collection QoL HUD overlays.");
            AddHotkeyRow(hkCard, "Ctrl + " + QoLConfig.DumpKey, "Write an R&D debug dump to the log (troubleshooting).");
            AddHotkeyRow(hkCard, QoLConfig.KeyMenu.ToString(), "Open / close this settings menu.");

            AddCallout(body, "Using our other mods?",
                "Some features here also appear in Sailor's Companion, Farmer's Companion and Inventory Master. Turn each one on in a single mod only, so it never applies twice.");

            return screen;
        }
        #endregion [END] BUILD SCREEN OVERVIEW

        #region [START] BUILD CATEGORY CARD
        private GameObject BuildCategoryCard(Transform parent, string title, string desc, out Text statusText, Action onClick)
        {
            var go = new GameObject("Cat_" + title);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite(11);
            img.type = Image.Type.Sliced;
            img.color = ColBorderSoft;
            var fillImg = AddInsetFill(go, 11, ColRow);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var nameTxt = CreateText(go, title, 16, FontStyle.Bold, ColText, TextAnchor.MiddleLeft);
            nameTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;

            var descTxt = CreateText(go, desc, 13.5f, FontStyle.Normal, ColTextMuted, TextAnchor.UpperLeft);
            var descLe = descTxt.gameObject.AddComponent<LayoutElement>();
            descLe.preferredHeight = 44;
            descLe.flexibleHeight = 1f;

            statusText = CreateText(go, "-- active", 16f, FontStyle.Bold, ColAccent, TextAnchor.MiddleLeft);
            statusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            var cb = btn.colors;
            cb.normalColor = ColRow;
            cb.highlightedColor = new Color(ColRow.r + 0.03f, ColRow.g + 0.03f, ColRow.b + 0.03f, 1f);
            cb.pressedColor = ColPanel;
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick());

            return go;
        }
        #endregion [END] BUILD CATEGORY CARD

        #region [START] REFRESH OVERVIEW
        private void RefreshOverview()
        {
            try
            {
                int nets = (QoLConfig.AutoEmptyNets ? 1 : 0) + (QoLConfig.NetRangeIncrease ? 1 : 0) + (QoLConfig.HookSpeedBoost ? 1 : 0) + (QoLConfig.HookAccuracyBoost ? 1 : 0);
                SetCategoryStatus(_ovStatusNets, nets, 4);

                int pickup = (QoLConfig.MagneticCollector ? 1 : 0) + (QoLConfig.HandPickupIsland ? 1 : 0) + (QoLConfig.UnderwaterLootAssist ? 1 : 0);
                SetCategoryStatus(_ovStatusPickup, pickup, 3);

                int detect = (QoLConfig.SmartDebrisTracking ? 1 : 0) + (QoLConfig.ResourceHighlight ? 1 : 0) + (QoLConfig.ItemDetectorScan ? 1 : 0) + (QoLConfig.LootGlow ? 1 : 0);
                SetCategoryStatus(_ovStatusDetect, detect, 4);

                int priority = (QoLConfig.BarrelTargetAssist ? 1 : 0) + (QoLConfig.PriorityPickup ? 1 : 0) + (QoLConfig.SoundAlerts ? 1 : 0) + (QoLConfig.FloatingLootTracker ? 1 : 0);
                SetCategoryStatus(_ovStatusPriority, priority, 4);
            }
            catch { }
        }
        #endregion [END] REFRESH OVERVIEW

        #region [START] SET CATEGORY STATUS
        private void SetCategoryStatus(Text t, int on, int total)
        {
            if (t == null) return;
            t.text = on + " of " + total + " active";
            t.color = on == total ? ColAccent : (on == 0 ? ColTextFaint : ColGold);
        }
        #endregion [END] SET CATEGORY STATUS

        // ---------------- SCREEN: NETS & HOOKS (31, 32, 33, 34) ----------------
        #region [START] BUILD SCREEN NETS HOOKS
        private GameObject BuildScreenNetsHooks(GameObject parent)
        {
            var screen = CreateScreenShell(parent, "Nets & Hooks", "Automates emptying nets and makes both nets and the fishing hook better at their job.", QoLConfig.KeyMenu.ToString(), out var body);

            AddGroupLabel(body, "Nets");
            var netCard = CreateCard(body);
            var e31 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec31, "Enabled");
            AddToggleRow(netCard, "31", "Auto Empty Nets", "Automatically empties nearby nets into your inventory. " + SiblingNote,
                () => e31 != null && e31.Value, v => { if (e31 != null) e31.Value = v; });
            var f31Range = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec31, "Range");
            AddStepperRow(netCard, "R", "Empty Range", "Only nets within this distance are auto-emptied.",
                5f, 150f, 5f, "m", () => f31Range != null ? f31Range.Value : 40f, v => { if (f31Range != null) f31Range.Value = v; }, true);

            var e32 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec32, "Enabled");
            AddToggleRow(netCard, "32", "Net Range Increase", "Bigger net catch area, so more drifting loot gets caught.",
                () => e32 != null && e32.Value, v => { if (e32 != null) e32.Value = v; });
            var f32Mult = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec32, "Multiplier");
            AddStepperRow(netCard, "M", "Range Multiplier", "How much bigger the net's trigger collider becomes.",
                1f, 3f, 0.1f, "x", () => f32Mult != null ? f32Mult.Value : 1.6f, v => { if (f32Mult != null) f32Mult.Value = v; }, true);

            AddGroupLabel(body, "Hook");
            var hookCard = CreateCard(body);
            var e33 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec33, "Enabled");
            AddToggleRow(hookCard, "33", "Hook Speed Boost", "Faster hook reeling and gathering. " + SiblingNote + " Two boosts multiply together.",
                () => e33 != null && e33.Value, v => { if (e33 != null) e33.Value = v; });
            var f33Mult = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec33, "Multiplier");
            AddStepperRow(hookCard, "S", "Speed Multiplier", "How much faster the hook reels and gathers.",
                1f, 4f, 0.1f, "x", () => f33Mult != null ? f33Mult.Value : 1.75f, v => { if (f33Mult != null) f33Mult.Value = v; }, true);

            var e34 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec34, "Enabled");
            AddToggleRow(hookCard, "34", "Hook Accuracy Boost", "Pulls near-miss items onto your hook automatically.",
                () => e34 != null && e34.Value, v => { if (e34 != null) e34.Value = v; });
            var f34Radius = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec34, "AssistRadius");
            AddStepperRow(hookCard, "A", "Assist Radius", "How far around the hook tip counts as a near-miss.",
                0.5f, 8f, 0.5f, "m", () => f34Radius != null ? f34Radius.Value : 2.5f, v => { if (f34Radius != null) f34Radius.Value = v; }, true);

            return screen;
        }
        #endregion [END] BUILD SCREEN NETS HOOKS

        // ---------------- SCREEN: PICKUP & MAGNET (35, 36, 42) ----------------
        #region [START] BUILD SCREEN PICKUP MAGNET
        private GameObject BuildScreenPickupMagnet(GameObject parent)
        {
            var screen = CreateScreenShell(parent, "Pickup & Magnet", "Hands-free item collection - on the raft, on land, and underwater.", QoLConfig.KeyMenu.ToString(), out var body);

            AddGroupLabel(body, "Magnetic Collector");
            var magCard = CreateCard(body);
            var e35 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec35, "Enabled");
            AddToggleRow(magCard, "35", "Magnetic Collector", "Timed loot magnet that pulls nearby items toward you on demand (" + QoLConfig.ChordLabel(QoLConfig.MagnetKey) + "). " + SiblingNote,
                () => e35 != null && e35.Value, v => { if (e35 != null) e35.Value = v; });
            var f35Radius = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec35, "Radius");
            AddStepperRow(magCard, "R", "Pull Radius", "How far the magnet reaches while active.",
                2f, 60f, 1f, "m", () => f35Radius != null ? f35Radius.Value : 18f, v => { if (f35Radius != null) f35Radius.Value = v; }, true);

            AddGroupLabel(body, "Manual Pickup Assist");
            var pickCard = CreateCard(body);
            var e36 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec36, "Enabled");
            AddToggleRow(pickCard, "36", "Hand Pickup (Island)", "Automatically picks up small resources while walking on land. " + SiblingNote,
                () => e36 != null && e36.Value, v => { if (e36 != null) e36.Value = v; });
            var f36Radius = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec36, "Radius");
            AddStepperRow(pickCard, "R", "Pickup Radius", "How close loot needs to be to get auto-collected on land.",
                0.5f, 8f, 0.5f, "m", () => f36Radius != null ? f36Radius.Value : 2.5f, v => { if (f36Radius != null) f36Radius.Value = v; }, true);

            var e42 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec42, "Enabled");
            AddToggleRow(pickCard, "42", "Underwater Loot Assist", "Highlights underwater loot and can auto-grab items right next to you while diving.",
                () => e42 != null && e42.Value, v => { if (e42 != null) e42.Value = v; }, true);
            var f42Range = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec42, "Range");
            AddStepperRow(pickCard, "R", "Marker Range", "How far underwater loot markers are shown.",
                5f, 80f, 1f, "m", () => f42Range != null ? f42Range.Value : 25f, v => { if (f42Range != null) f42Range.Value = v; }, true);

            return screen;
        }
        #endregion [END] BUILD SCREEN PICKUP MAGNET

        // ---------------- SCREEN: DETECTION & HIGHLIGHTS (37, 38, 39, 40) ----------------
        #region [START] BUILD SCREEN DETECTION HIGHLIGHTS
        private GameObject BuildScreenDetectionHighlights(GameObject parent)
        {
            var screen = CreateScreenShell(parent, "Detection & Highlights", "Finds loot for you and makes it easy to see, near and far.", QoLConfig.KeyMenu.ToString(), out var body);

            AddGroupLabel(body, "Tracking");
            var trackCard = CreateCard(body);
            var e37 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec37, "Enabled");
            AddToggleRow(trackCard, "37", "Smart Debris Tracking", "Debris panel with net / hook predictions for drifting loot.",
                () => e37 != null && e37.Value, v => { if (e37 != null) e37.Value = v; });
            var f37Range = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec37, "Range");
            AddStepperRow(trackCard, "R", "Tracking Range", "How far debris is tracked and predicted.",
                10f, 200f, 5f, "m", () => f37Range != null ? f37Range.Value : 70f, v => { if (f37Range != null) f37Range.Value = v; }, true);

            var e39 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec39, "Enabled");
            AddToggleRow(trackCard, "39", "Item Detector Scan", "Pulses a wide-radius scan (" + QoLConfig.ChordLabel(QoLConfig.ScanKey) + ") revealing loot on your HUD for a while. " + SiblingNote,
                () => e39 != null && e39.Value, v => { if (e39 != null) e39.Value = v; }, true);
            var f39Radius = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec39, "Radius");
            AddStepperRow(trackCard, "R", "Scan Radius", "How far a single scan pulse reveals loot.",
                10f, 200f, 5f, "m", () => f39Radius != null ? f39Radius.Value : 60f, v => { if (f39Radius != null) f39Radius.Value = v; }, true);

            AddGroupLabel(body, "Visual Cues");
            var visCard = CreateCard(body);
            var e38 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec38, "Enabled");
            AddToggleRow(visCard, "38", "Resource Highlight", "Draws brackets and labels on nearby loot so it's easy to spot.",
                () => e38 != null && e38.Value, v => { if (e38 != null) e38.Value = v; });
            var f38Range = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec38, "Range");
            AddStepperRow(visCard, "R", "Highlight Range", "How far the brackets and labels reach.",
                2f, 50f, 1f, "m", () => f38Range != null ? f38Range.Value : 12f, v => { if (f38Range != null) f38Range.Value = v; }, true);

            var e40 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec40, "Enabled");
            AddToggleRow(visCard, "40", "Loot Glow", "Adds a soft pulsing glow to valuable loot so it stands out at a distance.",
                () => e40 != null && e40.Value, v => { if (e40 != null) e40.Value = v; }, true);
            var f40Range = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec40, "Range");
            AddStepperRow(visCard, "R", "Glow Range", "How far away loot starts glowing.",
                2f, 80f, 1f, "m", () => f40Range != null ? f40Range.Value : 20f, v => { if (f40Range != null) f40Range.Value = v; }, true);

            return screen;
        }
        #endregion [END] BUILD SCREEN DETECTION HIGHLIGHTS

        // ---------------- SCREEN: PRIORITY & ALERTS (41, 43, 44, 45) ----------------
        #region [START] BUILD SCREEN PRIORITY ALERTS
        private GameObject BuildScreenPriorityAlerts(GameObject parent)
        {
            var screen = CreateScreenShell(parent, "Priority & Alerts", "Decides what loot matters most, and tells you when it's nearby.", QoLConfig.KeyMenu.ToString(), out var body);

            AddGroupLabel(body, "Targeting & Priority");
            var targetCard = CreateCard(body);
            var e41 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec41, "Enabled");
            AddToggleRow(targetCard, "41", "Barrel Target Assist", "Aim marker and bigger hook capture radius for barrels.",
                () => e41 != null && e41.Value, v => { if (e41 != null) e41.Value = v; });
            var f41Range = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec41, "AimRange");
            AddStepperRow(targetCard, "R", "Aim Range", "Max distance a barrel can be from you to get an aim marker.",
                5f, 80f, 1f, "m", () => f41Range != null ? f41Range.Value : 30f, v => { if (f41Range != null) f41Range.Value = v; }, true);

            var e43 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec43, "Enabled");
            AddToggleRow(targetCard, "43", "Resource Priority", "Uses a priority order instead of nearest-first when auto-collecting loot.",
                () => e43 != null && e43.Value, v => { if (e43 != null) e43.Value = v; }, true);
            AddPresetCycleRow(targetCard, "P", "Priority Preset", "Which order of resources counts as \"most wanted\" (also cycled with F7).");

            AddGroupLabel(body, "Alerts & Tracking");
            var alertCard = CreateCard(body);
            var e44 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec44, "Enabled");
            AddToggleRow(alertCard, "44", "Sound Alerts", "Plays alert tones when valuable loot drifts into range.",
                () => e44 != null && e44.Value, v => { if (e44 != null) e44.Value = v; });
            var f44Vol = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec44, "Volume");
            AddStepperRow(alertCard, "V", "Alert Volume", "How loud alert tones play.",
                0f, 100f, 5f, "%", () => f44Vol != null ? f44Vol.Value * 100f : 35f, v => { if (f44Vol != null) f44Vol.Value = v / 100f; }, true);

            var e45 = QoLConfigBinder.GetBool(QoLConfigBinder.Sec45, "Enabled");
            AddToggleRow(alertCard, "45", "Floating Loot Tracker", "Radar overlay with edge arrows pointing toward tracked loot.",
                () => e45 != null && e45.Value, v => { if (e45 != null) e45.Value = v; }, true);
            var f45Radar = QoLConfigBinder.GetFloat(QoLConfigBinder.Sec45, "RadarRange");
            AddStepperRow(alertCard, "R", "Radar Range", "How far the radar tracks loot.",
                10f, 200f, 5f, "m", () => f45Radar != null ? f45Radar.Value : 60f, v => { if (f45Radar != null) f45Radar.Value = v; }, true);

            return screen;
        }
        #endregion [END] BUILD SCREEN PRIORITY ALERTS

        #region [START] ADD HOTKEY ROW
        private void AddHotkeyRow(GameObject card, string key, string desc)
        {
            var rowGO = new GameObject("HkRow");
            rowGO.transform.SetParent(card.transform, false);
            rowGO.AddComponent<LayoutElement>().preferredHeight = 48;
            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(18, 18, 8, 8);
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            int existingRows = 0;
            foreach (Transform t in card.transform) { if (t.name == "HkRow") existingRows++; }
            if (existingRows > 1) AddRowSeparator(rowGO);

            var chipGO = new GameObject("Chip");
            chipGO.transform.SetParent(rowGO.transform, false);
            chipGO.AddComponent<LayoutElement>().preferredWidth = 82;
            var chipImg = chipGO.AddComponent<Image>();
            chipImg.sprite = GetRoundedSprite(6);
            chipImg.type = Image.Type.Sliced;
            chipImg.color = ColBorder;
            AddInsetFill(chipGO, 6, ColRow);
            var chipTxt = CreateText(chipGO, key, 14f, FontStyle.Bold, ColTextMuted, TextAnchor.MiddleCenter);
            FillParent(chipTxt.gameObject);

            var descTxt = CreateText(rowGO, desc, 14, FontStyle.Normal, ColTextMuted, TextAnchor.MiddleLeft);
            descTxt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }
        #endregion [END] ADD HOTKEY ROW

        #region [START] ADD PRESET CYCLE ROW
        private GameObject AddPresetCycleRow(GameObject card, string monogram, string title, string desc)
        {
            var rowGO = CreateRowShell(card, monogram, title, desc, true, out _);
            var btnGO = CreateGhostButton(rowGO.transform, SafePresetName(), () =>
            {
                try
                {
                    LootPriority.PresetIndex = (LootPriority.PresetIndex + 1) % QoLConfig.PriorityPresets.Length;
                    F44_SoundAlerts.Toast("Priority: " + LootPriority.PresetName);
                }
                catch { }
            });
            btnGO.AddComponent<LayoutElement>().preferredWidth = 150;
            var btnText = btnGO.GetComponentInChildren<Text>();
            if (btnText != null)
            {
                var btn = btnGO.GetComponent<Button>();
                btn.onClick.AddListener(() => { if (btnText != null) btnText.text = SafePresetName(); });
            }
            return rowGO;
        }
        #endregion [END] ADD PRESET CYCLE ROW

        #region [START] SAFE PRESET NAME
        private string SafePresetName()
        {
            try { return LootPriority.PresetName; }
            catch { return "Balanced"; }
        }
        #endregion [END] SAFE PRESET NAME

        // ---------------- shared shell / components ----------------
        #region [START] CREATE SCREEN SHELL
        private GameObject CreateScreenShell(GameObject parent, string title, string desc, string hotkey, out GameObject body)
        {
            var screen = new GameObject("Screen_" + title);
            screen.transform.SetParent(parent.transform, false);
            var screenRt = screen.AddComponent<RectTransform>();
            screenRt.anchorMin = Vector2.zero;
            screenRt.anchorMax = Vector2.one;
            screenRt.offsetMin = Vector2.zero;
            screenRt.offsetMax = Vector2.zero;

            var headGO = new GameObject("Head");
            headGO.transform.SetParent(screen.transform, false);
            var headRt = headGO.AddComponent<RectTransform>();
            headRt.anchorMin = new Vector2(0, 1);
            headRt.anchorMax = new Vector2(1, 1);
            headRt.pivot = new Vector2(0.5f, 1);
            headRt.sizeDelta = new Vector2(0, 112);
            headRt.anchoredPosition = Vector2.zero;

            var headLayout = headGO.AddComponent<HorizontalLayoutGroup>();
            headLayout.childControlWidth = true;
            headLayout.childControlHeight = true;
            headLayout.padding = new RectOffset(28, 60, 16, 12);
            headLayout.childForceExpandWidth = false;
            headLayout.childForceExpandHeight = true;

            var titleColGO = new GameObject("TitleCol");
            titleColGO.transform.SetParent(headGO.transform, false);
            var titleColLe = titleColGO.AddComponent<LayoutElement>();
            titleColLe.flexibleWidth = 1f;
            var titleColLayout = titleColGO.AddComponent<VerticalLayoutGroup>();
            titleColLayout.childControlWidth = true;
            titleColLayout.childControlHeight = true;
            titleColLayout.spacing = 3;
            titleColLayout.childForceExpandWidth = true;

            var titleTxt = CreateText(titleColGO, title, 24, FontStyle.Bold, ColText, TextAnchor.MiddleLeft);
            titleTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var descTxt = CreateText(titleColGO, desc, 15, FontStyle.Normal, ColTextMuted, TextAnchor.UpperLeft);
            var descLe = descTxt.gameObject.AddComponent<LayoutElement>();
            descLe.preferredHeight = 44;
            descLe.flexibleHeight = 1f;

            if (!string.IsNullOrEmpty(hotkey))
            {
                var hkGO = new GameObject("Hotkey");
                hkGO.transform.SetParent(headGO.transform, false);
                var hkLe = hkGO.AddComponent<LayoutElement>();
                hkLe.preferredWidth = 100;
                var hkLayout = hkGO.AddComponent<HorizontalLayoutGroup>();
                hkLayout.childControlWidth = true;
                hkLayout.childControlHeight = true;
                hkLayout.childAlignment = TextAnchor.MiddleRight;
                hkLayout.spacing = 6;
                hkLayout.childForceExpandWidth = false;
                hkLayout.childForceExpandHeight = true;

                var hkLbl = CreateText(hkGO, "Menu", 13, FontStyle.Normal, ColTextFaint, TextAnchor.MiddleRight);
                hkLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 34;
                CreateKeyChip(hkGO.transform, hotkey);
            }

            AddDivider(screen.transform, 0, headRt);

            var scrollGO = new GameObject("ScrollArea");
            scrollGO.transform.SetParent(screen.transform, false);
            var scrollRt = scrollGO.AddComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(28, 20);
            scrollRt.offsetMax = new Vector2(-26, -112);

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRt = viewportGO.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGO.AddComponent<RectMask2D>();

            body = new GameObject("Body");
            body.transform.SetParent(viewportGO.transform, false);
            var bodyRt = body.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0, 1);
            bodyRt.anchorMax = new Vector2(1, 1);
            bodyRt.pivot = new Vector2(0.5f, 1);
            bodyRt.anchoredPosition = Vector2.zero;
            bodyRt.sizeDelta = Vector2.zero;

            var bodyLayout = body.AddComponent<VerticalLayoutGroup>();
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.spacing = 14;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;

            var bodyFitter = body.AddComponent<ContentSizeFitter>();
            bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.content = bodyRt;
            scrollRect.viewport = viewportRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            return screen;
        }
        #endregion [END] CREATE SCREEN SHELL

        #region [START] ADD GROUP LABEL
        private void AddGroupLabel(GameObject parent, string text)
        {
            var go = new GameObject("GroupLabel");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<LayoutElement>().preferredHeight = 16;
            var t = CreateText(go, text.ToUpperInvariant(), 13, FontStyle.Bold, ColTextFaint, TextAnchor.MiddleLeft);
            FillParent(t.gameObject);
        }
        #endregion [END] ADD GROUP LABEL

        // Soft gold notice box, matching the one used by the sibling mods.
        #region [START] ADD CALLOUT
        private void AddCallout(GameObject parent, string title, string desc)
        {
            var go = new GameObject("Callout");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<LayoutElement>().preferredHeight = 88;
            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite(11);
            img.type = Image.Type.Sliced;
            img.color = new Color(ColGold.r, ColGold.g, ColGold.b, 0.12f);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.spacing = 2;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var titleTxt = CreateText(go, title, 15, FontStyle.Bold, ColGold, TextAnchor.MiddleLeft);
            titleTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            // Sized for two lines - Unity truncates a wrapped line that doesn't fit its box.
            var descTxt = CreateText(go, desc, 14, FontStyle.Normal, ColTextMuted, TextAnchor.UpperLeft);
            descTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
        }
        #endregion [END] ADD CALLOUT

        #region [START] CREATE CARD
        private GameObject CreateCard(GameObject parent)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite(11);
            img.type = Image.Type.Sliced;
            img.color = ColBorderSoft;
            AddInsetFill(go, 11, ColRow);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var le = go.AddComponent<LayoutElement>();
            le.flexibleHeight = 0f;

            return go;
        }
        #endregion [END] CREATE CARD

        #region [START] ADD ROW SEPARATOR
        private void AddRowSeparator(GameObject row)
        {
            var sepGO = new GameObject("Sep");
            sepGO.transform.SetParent(row.transform, false);
            sepGO.transform.SetAsFirstSibling();
            var sepRt = sepGO.AddComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0, 1);
            sepRt.anchorMax = new Vector2(1, 1);
            sepRt.pivot = new Vector2(0.5f, 1);
            sepRt.sizeDelta = new Vector2(0, 1);
            sepRt.anchoredPosition = Vector2.zero;
            var img = sepGO.AddComponent<Image>();
            img.color = ColBorderSoft;
        }
        #endregion [END] ADD ROW SEPARATOR

        #region [START] ADD TOGGLE ROW
        private GameObject AddToggleRow(GameObject card, string monogram, string title, string desc, Func<bool> getter, Action<bool> setter, bool secondary = false)
        {
            var rowGO = CreateRowShell(card, monogram, title, desc, secondary, out _);
            var sw = CreateToggleSwitch(rowGO.transform, getter(), setter);
            sw.AddComponent<LayoutElement>();
            return rowGO;
        }
        #endregion [END] ADD TOGGLE ROW

        #region [START] ADD STEPPER ROW
        private GameObject AddStepperRow(GameObject card, string monogram, string title, string desc, float min, float max, float step, string suffix, Func<float> getter, Action<float> setter, bool secondary = false)
        {
            var rowGO = CreateRowShell(card, monogram, title, desc, secondary, out _);

            var stepperGO = new GameObject("Stepper");
            stepperGO.transform.SetParent(rowGO.transform, false);
            stepperGO.AddComponent<LayoutElement>().preferredWidth = 175;
            var stepperLayout = stepperGO.AddComponent<HorizontalLayoutGroup>();
            stepperLayout.childControlWidth = true;
            stepperLayout.childControlHeight = true;
            stepperLayout.spacing = 8;
            stepperLayout.childAlignment = TextAnchor.MiddleRight;
            stepperLayout.childForceExpandWidth = false;
            stepperLayout.childForceExpandHeight = true;

            var trackGO = new GameObject("Track");
            trackGO.transform.SetParent(stepperGO.transform, false);
            trackGO.AddComponent<LayoutElement>().preferredWidth = 100;
            var track = trackGO.GetComponent<RectTransform>() ?? trackGO.AddComponent<RectTransform>();
            var trackImg = trackGO.AddComponent<Image>();
            trackImg.sprite = GetRoundedSprite(2);
            trackImg.type = Image.Type.Sliced;
            trackImg.color = ColBorder;

            var trackHeightGO = new GameObject("TH");
            trackHeightGO.transform.SetParent(trackGO.transform, false);
            var thRt = trackHeightGO.AddComponent<RectTransform>();
            thRt.anchorMin = new Vector2(0, 0.5f); thRt.anchorMax = new Vector2(1, 0.5f);
            thRt.sizeDelta = new Vector2(0, 5);
            thRt.anchoredPosition = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(trackGO.transform, false);
            var fillRt = fillGO.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0.5f);
            fillRt.anchorMax = new Vector2(0, 0.5f);
            fillRt.pivot = new Vector2(0, 0.5f);
            fillRt.sizeDelta = new Vector2(10, 5);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = GetRoundedSprite(2);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = ColAccent;

            var thumbGO = new GameObject("Thumb");
            thumbGO.transform.SetParent(trackGO.transform, false);
            var thumbRt = thumbGO.AddComponent<RectTransform>();
            thumbRt.anchorMin = new Vector2(0, 0.5f);
            thumbRt.anchorMax = new Vector2(0, 0.5f);
            thumbRt.pivot = new Vector2(0.5f, 0.5f);
            thumbRt.sizeDelta = new Vector2(12, 12);
            var thumbImg = thumbGO.AddComponent<Image>();
            thumbImg.sprite = GetRoundedSprite(6);
            thumbImg.type = Image.Type.Sliced;
            thumbImg.color = ColText;

            var valGO = new GameObject("Val");
            valGO.transform.SetParent(stepperGO.transform, false);
            valGO.AddComponent<LayoutElement>().preferredWidth = 56;
            var valTxt = CreateText(valGO, "", 14, FontStyle.Normal, ColText, TextAnchor.MiddleRight);
            FillParent(valTxt.gameObject);

            var slider = trackGO.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.fillRect = fillRt;
            slider.handleRect = thumbRt;
            slider.targetGraphic = thumbImg;

            string fmt = step < 1f ? "F1" : "F0";
            void Refresh(float v)
            {
                valTxt.text = v.ToString(fmt, CultureInfo.InvariantCulture) + suffix;
            }

            float initial = getter();
            slider.value = initial;
            Refresh(initial);
            slider.onValueChanged.AddListener(v =>
            {
                setter(v);
                Refresh(v);
            });

            return rowGO;
        }
        #endregion [END] ADD STEPPER ROW

        #region [START] CREATE ROW SHELL
        private GameObject CreateRowShell(GameObject card, string monogram, string title, string desc, bool secondary, out RectTransform rt)
        {
            var rowGO = new GameObject("Row");
            rowGO.transform.SetParent(card.transform, false);
            rt = rowGO.AddComponent<RectTransform>();
            rowGO.AddComponent<LayoutElement>().preferredHeight = 72;

            var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            int existingRows = 0;
            foreach (Transform t in card.transform) { if (t.name == "Row") existingRows++; }
            if (existingRows > 1) AddRowSeparator(rowGO);

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(rowGO.transform, false);
            iconGO.AddComponent<LayoutElement>().preferredWidth = 40;
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = GetRoundedSprite(11);
            iconImg.type = Image.Type.Sliced;
            iconImg.color = ColPanel2;
            var iconTxt = CreateText(iconGO, monogram, 14, FontStyle.Bold, secondary ? ColGold : ColAccent, TextAnchor.MiddleCenter);
            FillParent(iconTxt.gameObject);

            var textColGO = new GameObject("Text");
            textColGO.transform.SetParent(rowGO.transform, false);
            var textColLe = textColGO.AddComponent<LayoutElement>();
            textColLe.flexibleWidth = 1f;
            var textColLayout = textColGO.AddComponent<VerticalLayoutGroup>();
            textColLayout.childControlWidth = true;
            textColLayout.childControlHeight = true;
            textColLayout.childForceExpandWidth = true;
            textColLayout.spacing = 2;
            textColLayout.childAlignment = TextAnchor.MiddleLeft;

            var titleTxt = CreateText(textColGO, title, 18, FontStyle.Bold, ColText, TextAnchor.MiddleLeft);
            titleTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
            var descTxt = CreateText(textColGO, desc, 14, FontStyle.Normal, ColTextMuted, TextAnchor.UpperLeft);
            descTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

            return rowGO;
        }
        #endregion [END] CREATE ROW SHELL

        // ---------------- real animated toggle switch ----------------
        private const float SwitchW = 48, SwitchH = 27, KnobSize = 21, KnobMargin = 3;

        #region [START] CREATE TOGGLE SWITCH
        private GameObject CreateToggleSwitch(Transform parent, bool initial, Action<bool> onChange)
        {
            var go = new GameObject("Switch");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(SwitchW, SwitchH);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = SwitchW; le.preferredHeight = SwitchH;

            var trackImg = go.AddComponent<Image>();
            trackImg.sprite = GetRoundedSprite((int)(SwitchH / 2));
            trackImg.type = Image.Type.Sliced;
            trackImg.color = initial ? ColAccent : ColBorder;

            var knobGO = new GameObject("Knob");
            knobGO.transform.SetParent(go.transform, false);
            var knobRt = knobGO.AddComponent<RectTransform>();
            knobRt.anchorMin = new Vector2(0, 0.5f);
            knobRt.anchorMax = new Vector2(0, 0.5f);
            knobRt.pivot = new Vector2(0, 0.5f);
            knobRt.sizeDelta = new Vector2(KnobSize, KnobSize);
            knobRt.anchoredPosition = new Vector2(initial ? SwitchW - KnobSize - KnobMargin : KnobMargin, 0);
            var knobImg = knobGO.AddComponent<Image>();
            knobImg.sprite = GetRoundedSprite((int)(KnobSize / 2));
            knobImg.type = Image.Type.Sliced;
            knobImg.color = ColBg;

            bool state = initial;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = trackImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                state = !state;
                onChange(state);
                StartCoroutine(AnimateSwitch(knobRt, trackImg, state));
            });

            return go;
        }
        #endregion [END] CREATE TOGGLE SWITCH

        #region [START] ANIMATE SWITCH
        private IEnumerator AnimateSwitch(RectTransform knob, Image track, bool on)
        {
            float duration = 0.12f;
            float t = 0f;
            float fromX = knob.anchoredPosition.x;
            float toX = on ? SwitchW - KnobSize - KnobMargin : KnobMargin;
            Color fromC = track.color;
            Color toC = on ? ColAccent : ColBorder;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                knob.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, k), 0);
                track.color = Color.Lerp(fromC, toC, k);
                yield return null;
            }
            knob.anchoredPosition = new Vector2(toX, 0);
            track.color = toC;
        }
        #endregion [END] ANIMATE SWITCH

        // ---------------- misc small components ----------------
        #region [START] CREATE GHOST BUTTON
        private GameObject CreateGhostButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("GhostBtn");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite(8);
            img.type = Image.Type.Sliced;
            img.color = ColBorder;
            var fillImg = AddInsetFill(go, 8, ColPanel2);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            var cb = btn.colors;
            cb.normalColor = ColPanel2;
            cb.highlightedColor = ColAccentWash;
            cb.pressedColor = ColRow;
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go, label, 15f, FontStyle.Bold, ColText, TextAnchor.MiddleCenter);
            FillParent(txt.gameObject);
            return go;
        }
        #endregion [END] CREATE GHOST BUTTON

        #region [START] CREATE KEY CHIP
        private GameObject CreateKeyChip(Transform parent, string text)
        {
            var go = new GameObject("Chip");
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredWidth = 36;
            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite(5);
            img.type = Image.Type.Sliced;
            img.color = ColBorder;
            AddInsetFill(go, 5, ColRow);
            var txt = CreateText(go, text, 16f, FontStyle.Bold, ColTextMuted, TextAnchor.MiddleCenter);
            FillParent(txt.gameObject);
            return go;
        }
        #endregion [END] CREATE KEY CHIP

        #region [START] ADD DIVIDER
        private void AddDivider(Transform parent, float marginBottom, RectTransform anchorBelow = null)
        {
            if (anchorBelow != null)
            {
                var dGO = new GameObject("Divider");
                dGO.transform.SetParent(parent, false);
                var dRt = dGO.AddComponent<RectTransform>();
                dRt.anchorMin = new Vector2(0, 1);
                dRt.anchorMax = new Vector2(1, 1);
                dRt.pivot = new Vector2(0.5f, 1);
                dRt.sizeDelta = new Vector2(0, 1);
                dRt.anchoredPosition = new Vector2(0, -anchorBelow.sizeDelta.y);
                var img = dGO.AddComponent<Image>();
                img.color = ColBorderSoft;
                return;
            }

            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 1;
            var im = go.AddComponent<Image>();
            im.color = ColBorderSoft;
        }
        #endregion [END] ADD DIVIDER

        #region [START] BUILD FOOTER
        private void BuildFooter(GameObject parent)
        {
            var footGO = new GameObject("Footer");
            footGO.transform.SetParent(parent.transform, false);
            var fRt = footGO.AddComponent<RectTransform>();
            fRt.anchorMin = new Vector2(0, 0);
            fRt.anchorMax = new Vector2(1, 0);
            fRt.pivot = new Vector2(0.5f, 0);
            fRt.sizeDelta = new Vector2(0, 52);
            fRt.anchoredPosition = Vector2.zero;

            AddDivider(footGO.transform, 0);
            var topLine = footGO.transform.Find("Divider");
            if (topLine != null)
            {
                var tlRt = topLine.GetComponent<RectTransform>();
                tlRt.anchorMin = new Vector2(0, 1);
                tlRt.anchorMax = new Vector2(1, 1);
                tlRt.pivot = new Vector2(0.5f, 1);
                tlRt.sizeDelta = new Vector2(0, 1);
                tlRt.anchoredPosition = Vector2.zero;

                var tlLe = topLine.GetComponent<LayoutElement>();
                if (tlLe == null) tlLe = topLine.gameObject.AddComponent<LayoutElement>();
                tlLe.ignoreLayout = true;
            }

            var layout = footGO.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.padding = new RectOffset(28, 26, 8, 8);
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            // Pre-release: no update checker exists for this mod yet, so the footer is just a
            // static "About" line (name + version) rather than a live GitHub-polling badge like
            // the sibling mods have.
            var verGO = new GameObject("Ver");
            verGO.transform.SetParent(footGO.transform, false);
            verGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var verTxt = CreateText(verGO, QoLPlugin.NAME + "  <color=#8FA3A9>v" + QoLPlugin.VERSION + " · pre-release</color>", 15f, FontStyle.Normal, ColTextMuted, TextAnchor.MiddleLeft);
            verTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            FillParent(verTxt.gameObject);

            var hintGO = new GameObject("Hint");
            hintGO.transform.SetParent(footGO.transform, false);
            hintGO.AddComponent<LayoutElement>().preferredWidth = 420;
            var hintTxt = CreateText(hintGO, QoLConfig.ChordLabel(QoLConfig.MagnetKey) + " Magnet · " + QoLConfig.ChordLabel(QoLConfig.ScanKey) + " Scan · " + QoLConfig.ChordLabel(QoLConfig.PriorityCycleKey) + " Priority · " + QoLConfig.ChordLabel(QoLConfig.HudToggleKey) + " HUD · " + QoLConfig.KeyMenu + " Menu", 13f, FontStyle.Normal, ColTextFaint, TextAnchor.MiddleRight);
            hintTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            FillParent(hintTxt.gameObject);
        }
        #endregion [END] BUILD FOOTER

        // ---------------- rounded-rect sprite generator (9-sliced, cached by radius) ----------------
        private static readonly Dictionary<int, Sprite> _roundedSpriteCache = new Dictionary<int, Sprite>();

        #region [START] GET ROUNDED SPRITE
        private static Sprite GetRoundedSprite(int radius)
        {
            radius = Mathf.Max(2, radius);
            if (_roundedSpriteCache.TryGetValue(radius, out var cached) && cached != null) return cached;

            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inCornerX = x < radius || x >= size - radius;
                    bool inCornerY = y < radius || y >= size - radius;
                    float alpha = 1f;
                    if (inCornerX && inCornerY)
                    {
                        float cx = x < radius ? radius : size - radius - 1;
                        float cy = y < radius ? radius : size - radius - 1;
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.name = "CQ_Rounded_" + radius;
            _roundedSpriteCache[radius] = sprite;
            return sprite;
        }
        #endregion [END] GET ROUNDED SPRITE

        #region [START] ADD INSET FILL
        private Image AddInsetFill(GameObject go, int radius, Color fillColor, float inset = 1.5f)
        {
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(go.transform, false);
            fillGO.transform.SetAsFirstSibling();
            var rt = fillGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            var img = fillGO.AddComponent<Image>();
            img.sprite = GetRoundedSprite(radius);
            img.type = Image.Type.Sliced;
            img.color = fillColor;
            img.raycastTarget = false;
            fillGO.AddComponent<LayoutElement>().ignoreLayout = true;
            return img;
        }
        #endregion [END] ADD INSET FILL

        #region [START] CREATE TEXT
        private Text CreateText(GameObject parent, string text, float fontSize, FontStyle style, Color color, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = GetGameFont();
            t.fontSize = Mathf.RoundToInt(fontSize);
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }
        #endregion [END] CREATE TEXT

        #region [START] FILL PARENT
        private void FillParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        #endregion [END] FILL PARENT

        #region [START] SET LAYER RECURSIVELY
        private static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                var child = obj.transform.GetChild(i);
                if (child != null) SetLayerRecursively(child.gameObject, newLayer);
            }
        }
        #endregion [END] SET LAYER RECURSIVELY
        #endregion [END] CANVAS CONSTRUCTION
    }
    // ============================================================================
    // [END] CANVAS COLLECTION QoL SETTINGS UI
    // ============================================================================
    #endregion
}
