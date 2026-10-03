using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace UnityAgentLab
{
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] private RepairGame game;
        [SerializeField] private bool menuScene;
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject menuRoot, playRoot, actionRoot, pauseRoot, helpRoot, winRoot, languageRoot;
        [SerializeField] private Text title, subtitle, story, credits, controls, objective, stage, cargo, boost, clock, notification, intro, actionReason, pauseTitle, helpTitle, helpBody, winTitle, winScore, winStars;
        [SerializeField] private Text[] steps;
        [SerializeField] private Text[] cellNames;
        [SerializeField] private StationGlyph objectiveIcon, cargoIcon, cooldownRing;
        [SerializeField] private Button startButton, quitButton, pauseButton, actionButton, resumeButton, restartButton, menuButton, helpButton, closeButton, chineseButton, englishButton, winRestart, winMenu;
        private static Font font;
        private bool helpVisible;
        public Button ActionButton => actionButton;
        public Canvas InterfaceCanvas => canvas;
        public string DisplayedGoal => objective == null ? "" : objective.text;
        private static readonly Color Panel = new Color(0.025f, 0.07f, 0.105f, 0.93f);
        private static readonly Color Cyan = new Color(0.08f, 0.85f, 1);
        public void Configure(RepairGame value, bool menu) { game = value; menuScene = menu; }
        private RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        private GameObject PanelBox(string name, Transform parent, float x, float y, float w, float h, Color? tint = null)
        {
            RectTransform rect = Rect(name, parent, x, y, w, h); Image image = rect.gameObject.AddComponent<Image>();
            image.color = tint ?? Panel; image.raycastTarget = false; return rect.gameObject;
        }
        private Text Label(string name, Transform parent, float x, float y, float w, float h, int size, Color? tint = null)
        {
            Text label = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = font == null ? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") : font;
            label.fontSize = size; label.color = tint ?? Color.white; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private Button ButtonAt(string name, Transform parent, float x, float y, float w, float h, Color? tint = null)
        {
            GameObject obj = PanelBox(name, parent, x, y, w, h, tint ?? new Color(0.035f, 0.31f, 0.38f));
            obj.GetComponent<Image>().raycastTarget = true; Button button = obj.AddComponent<Button>();
            button.targetGraphic = obj.GetComponent<Image>(); button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(0.65f, 1, 1); colors.pressedColor = new Color(0.5f, 0.85f, 0.85f); colors.disabledColor = new Color(0.45f, 0.5f, 0.55f); button.colors = colors;
            Text text = Label("Label", obj.transform, 8, 3, w - 16, h - 6, 18); text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private StationGlyph Glyph(string name, Transform parent, float x, float y, float w, float h, GlyphKind kind)
        {
            var glyph = Rect(name, parent, x, y, w, h).gameObject.AddComponent<StationGlyph>();
            glyph.Kind = kind; glyph.color = Cyan; glyph.raycastTarget = false; return glyph;
        }
        public void BuildInterface()
        {
            if (canvas != null) return;
            GameObject root = new GameObject("StationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            menuRoot = Rect("Briefing", root.transform, 0, 0, 1280, 720).gameObject;
            PanelBox("BriefingBackdrop", menuRoot.transform, 0, 0, 790, 720, new Color(0.014f, 0.034f, 0.06f, 0.97f));
            title = Label("Title", menuRoot.transform, 44, 58, 695, 65, 46, Cyan);
            subtitle = Label("Subtitle", menuRoot.transform, 47, 128, 695, 38, 24);
            story = Label("Story", menuRoot.transform, 47, 195, 685, 145, 23);
            steps = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = PanelBox("Step" + i, menuRoot.transform, 44 + i * 239, 370, 224, 118, new Color(0.04f, 0.13f, 0.19f));
                Glyph("StepSymbol", card.transform, 15, 14, 43, 57, i == 0 ? GlyphKind.BlueCell : i == 1 ? GlyphKind.Lightning : GlyphKind.Dock);
                Label("StepNumber", card.transform, 71, 13, 80, 29, 20, Cyan).text = "0" + (i + 1);
                steps[i] = Label("StepTitle", card.transform, 17, 77, 204, 36, 20);
            }
            controls = Label("Controls", menuRoot.transform, 47, 515, 695, 56, 18, new Color(0.68f, 0.81f, 0.87f));
            startButton = ButtonAt("Start", menuRoot.transform, 44, 600, 300, 60);
            // Buttons and labels are serialized into the scene; game rules stay in RepairGame.
            helpButton = ButtonAt("Help", menuRoot.transform, 362, 600, 370, 60);
            credits = Label("Credits", menuRoot.transform, 48, 675, 728, 28, 13, new Color(0.46f, 0.63f, 0.7f));

            playRoot = Rect("HUD", root.transform, 0, 0, 1280, 720).gameObject;
            var goal = PanelBox("CurrentObjective", playRoot.transform, 22, 20, 650, 90);
            objectiveIcon = Glyph("RequiredCell", goal.transform, 16, 16, 45, 58, GlyphKind.BlueCell);
            stage = Label("Stage", goal.transform, 80, 12, 540, 24, 15, Cyan);
            objective = Label("Goal", goal.transform, 80, 39, 540, 43, 22);
            var inventory = PanelBox("Inventory", playRoot.transform, 22, 625, 290, 76);
            cargoIcon = Glyph("CargoIcon", inventory.transform, 13, 10, 44, 56, GlyphKind.BlueCell);
            cargo = Label("Cargo", inventory.transform, 72, 20, 204, 48, 18);
            var booster = PanelBox("Booster", playRoot.transform, 328, 625, 230, 76);
            cooldownRing = Glyph("Cooldown", booster.transform, 13, 10, 55, 55, GlyphKind.Ring);
            Label("Shift", booster.transform, 22, 28, 40, 20, 12).text = "Shift";
            boost = Label("BoostState", booster.transform, 82, 13, 137, 57, 18);
            var time = PanelBox("MissionClock", playRoot.transform, 575, 641, 123, 60);
            clock = Label("Clock", time.transform, 12, 8, 105, 45, 26, Cyan);
            pauseButton = ButtonAt("Pause", playRoot.transform, 970, 20, 137, 42);
            intro = Label("Intro", playRoot.transform, 365, 584, 805, 28, 16, new Color(0.67f, 0.82f, 0.88f));
            notification = Label("Notice", playRoot.transform, 335, 128, 740, 57, 20, new Color(0.54f, 1, 0.8f));
            actionRoot = Rect("ContextAction", playRoot.transform, 0, 0, 330, 92).gameObject;
            actionButton = ButtonAt("Interact", actionRoot.transform, 0, 0, 330, 43);
            actionReason = Label("Reason", actionRoot.transform, 6, 49, 320, 42, 16, new Color(1, 0.8f, 0.42f));

            winRoot = PanelBox("Victory", root.transform, 312, 200, 656, 350, new Color(0.025f, 0.105f, 0.13f, 0.98f));
            winTitle = Label("WinTitle", winRoot.transform, 28, 30, 600, 55, 34, Cyan);
            winStars = Label("Stars", winRoot.transform, 30, 100, 592, 60, 38, new Color(1, 0.72f, 0.18f));
            winScore = Label("Score", winRoot.transform, 30, 176, 594, 61, 19);
            winRestart = ButtonAt("Again", winRoot.transform, 30, 263, 282, 56);
            winMenu = ButtonAt("Menu", winRoot.transform, 334, 263, 292, 56);
            pauseRoot = PanelBox("PauseScreen", root.transform, 0, 0, 1280, 720, new Color(0.006f, 0.016f, 0.03f, 0.92f));
            pauseRoot.GetComponent<Image>().raycastTarget = true;
            pauseTitle = Label("PauseTitle", pauseRoot.transform, 370, 163, 620, 64, 36, Cyan);
            resumeButton = ButtonAt("Resume", pauseRoot.transform, 380, 260, 520, 60);
            restartButton = ButtonAt("Restart", pauseRoot.transform, 380, 335, 520, 60);
            menuButton = ButtonAt("Menu", pauseRoot.transform, 380, 410, 520, 60);
            Button pauseHelp = ButtonAt("Help", pauseRoot.transform, 380, 485, 520, 60);
            pauseHelp.gameObject.name = "PauseHelp";

            helpRoot = PanelBox("HelpScreen", root.transform, 0, 0, 1280, 720, new Color(0.008f, 0.03f, 0.054f, 0.98f));
            helpRoot.GetComponent<Image>().raycastTarget = true;
            helpTitle = Label("HelpTitle", helpRoot.transform, 45, 30, 870, 53, 32, Cyan);
            helpBody = Label("HelpText", helpRoot.transform, 48, 113, 848, 545, 18);
            Glyph("BlueReference", helpRoot.transform, 968, 220, 80, 125, GlyphKind.BlueCell);
            Glyph("OrangeReference", helpRoot.transform, 1081, 220, 80, 125, GlyphKind.OrangeCell);
            cellNames = new[] { Label("BlueName", helpRoot.transform, 920, 365, 162, 80, 17), Label("OrangeName", helpRoot.transform, 1090, 365, 164, 80, 17) };
            closeButton = ButtonAt("CloseHelp", helpRoot.transform, 953, 593, 280, 57);
            languageRoot = Rect("LanguageSelection", root.transform, 956, 78, 302, 42).gameObject;
            chineseButton = ButtonAt("Chinese", languageRoot.transform, 0, 0, 145, 38);
            englishButton = ButtonAt("English", languageRoot.transform, 154, 0, 145, 38);
            SetButton(chineseButton, "中文"); SetButton(englishButton, "English");
            quitButton = ButtonAt("Quit", root.transform, 1122, 20, 136, 42, new Color(0.42f, 0.1f, 0.16f));
            Stretch(menuRoot); Stretch(playRoot); Stretch(pauseRoot); Stretch(helpRoot);
            pauseRoot.GetComponent<Image>().color = new Color(.006f, .016f, .03f, .97f);
            helpRoot.GetComponent<Image>().color = new Color(.008f, .03f, .054f, 1);
            RightAnchor(quitButton.gameObject, 22, 20); RightAnchor(pauseButton.gameObject, 173, 20);
            RightAnchor(languageRoot, 22, 78);
            BottomAnchor(inventory, 22, 18); BottomAnchor(booster, 328, 18); BottomAnchor(time, 575, 18);
            BottomAnchor(intro.gameObject, 365, 108);
            RectTransform victory = winRoot.GetComponent<RectTransform>(); victory.anchorMin = victory.anchorMax = new Vector2(.5f, .5f);
            victory.pivot = new Vector2(.5f, .5f); victory.anchoredPosition = Vector2.zero;
            pauseRoot.SetActive(false); helpRoot.SetActive(false); winRoot.SetActive(false); actionRoot.SetActive(false);
            RefreshStatic();
        }
        private static void Stretch(GameObject obj)
        {
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void RightAnchor(GameObject obj, float right, float top)
        {
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-right, -top);
        }
        private static void BottomAnchor(GameObject obj, float left, float bottom)
        {
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(left, bottom);
        }
        private void Awake()
        {
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
            BuildInterface();
            foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.font = font;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("StationUIInput", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            startButton.onClick.AddListener(GameFlow.StartGame);
            quitButton.onClick.AddListener(GameFlow.Quit);
            pauseButton.onClick.AddListener(() => ShowPause(!GameFlow.IsPaused));
            resumeButton.onClick.AddListener(() => ShowPause(false));
            restartButton.onClick.AddListener(GameFlow.Restart); menuButton.onClick.AddListener(GameFlow.Menu);
            helpButton.onClick.AddListener(ShowHelp); closeButton.onClick.AddListener(CloseHelp);
            pauseRoot.transform.Find("PauseHelp").GetComponent<Button>().onClick.AddListener(ShowHelp);
            winRestart.onClick.AddListener(GameFlow.Restart); winMenu.onClick.AddListener(GameFlow.Menu);
            chineseButton.onClick.AddListener(() => LanguageService.Set(GameLanguage.Chinese));
            englishButton.onClick.AddListener(() => LanguageService.Set(GameLanguage.English));
            actionButton.onClick.AddListener(() => game?.Player.GetComponent<PlayerInteraction>().RequestInteract());
            LanguageService.Changed += RefreshStatic; RefreshStatic();
        }
        private void OnDestroy() { LanguageService.Changed -= RefreshStatic; }
        private static void SetButton(Button button, string text) { if (button != null) button.GetComponentInChildren<Text>().text = text; }
        private void RefreshStatic()
        {
            if (title == null) return;
            title.text = LanguageService.Text("title"); subtitle.text = LanguageService.Text("subtitle");
            story.text = LanguageService.Text("story"); credits.text = LanguageService.Text("credits");
            controls.text = LanguageService.Text("intro.controls");
            for (int i = 0; i < 3; i++) steps[i].text = LanguageService.Text("step" + (i + 1));
            pauseTitle.text = LanguageService.Text("paused"); helpTitle.text = LanguageService.Text("help"); helpBody.text = LanguageService.Text("help.body");
            cellNames[0].text = LanguageService.Text("blue"); cellNames[1].text = LanguageService.Text("orange");
            winTitle.text = LanguageService.Text("won");
            foreach (var pair in new[] { (startButton,"start"), (quitButton,"quit"), (pauseButton,"pause"), (resumeButton,"resume"), (restartButton,"restart"), (menuButton,"menu"), (helpButton,"help"), (closeButton,"close"), (winRestart,"restart"), (winMenu,"menu") })
                SetButton(pair.Item1, LanguageService.Text(pair.Item2));
            SetButton(pauseRoot.transform.Find("PauseHelp").GetComponent<Button>(), LanguageService.Text("help"));
            chineseButton.interactable = LanguageService.Current != GameLanguage.Chinese;
            englishButton.interactable = LanguageService.Current != GameLanguage.English;
            menuRoot.SetActive(menuScene); playRoot.SetActive(!menuScene);
            languageRoot.SetActive(menuScene || GameFlow.IsPaused);
        }
        private void ShowPause(bool value)
        {
            if (menuScene) return;
            if (helpVisible) { CloseHelp(); return; }
            GameFlow.SetPaused(value); pauseRoot.SetActive(value); languageRoot.SetActive(value);
        }
        private void ShowHelp()
        { helpVisible = true; GameFlow.SetPaused(true); helpRoot.SetActive(true); languageRoot.SetActive(true); }
        private void CloseHelp()
        { helpVisible = false; helpRoot.SetActive(false); GameFlow.SetPaused(!menuScene && pauseRoot.activeSelf); languageRoot.SetActive(menuScene || GameFlow.IsPaused); }
        private void Update()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame)
                { if (helpVisible) CloseHelp(); else ShowPause(!GameFlow.IsPaused); }
                if (menuScene && !helpVisible && keys.enterKey.wasPressedThisFrame) GameFlow.StartGame();
                if (!menuScene && !helpVisible && keys.rKey.wasPressedThisFrame) GameFlow.Restart();
            }
            if (game == null || menuScene) return;
            objective.text = game.Goal;
            int index = game.Phase == RepairPhase.NotInstalled ? 0 : game.Phase == RepairPhase.Powering ? 1 : 2;
            stage.text = "0" + (index + 1) + "  /  " + LanguageService.Text("step" + (index + 1));
            objectiveIcon.Kind = game.Phase == RepairPhase.Installed || game.Phase == RepairPhase.Complete ? GlyphKind.Dock
                : game.RequiredKind == ModuleKind.BlueCircle ? GlyphKind.BlueCell : GlyphKind.OrangeCell;
            cargoIcon.gameObject.SetActive(game.Held != null);
            if (game.Held != null) cargoIcon.Kind = game.Held.Kind == ModuleKind.BlueCircle ? GlyphKind.BlueCell : GlyphKind.OrangeCell;
            cargo.text = LanguageService.Text("carry") + "\n" + (game.Held == null ? LanguageService.Text("empty") : LanguageService.Cell(game.Held.Kind));
            PlayerMotor motor = game.Player.GetComponent<PlayerMotor>();
            cooldownRing.Fill = 1 - motor.CooldownRemaining / motor.CooldownDuration;
            boost.text = LanguageService.Text("boost") + "  " + (motor.CooldownRemaining > 0 ? motor.CooldownRemaining.ToString("0.0") + "s" : LanguageService.Text("ready"));
            if (motor.BoostVisualRemaining > 0) boost.text += "\n" + LanguageService.Text("speed") + " " + motor.PlanarSpeed.ToString("0.0") + "/11.5";
            int seconds = Mathf.FloorToInt(game.MissionTime); clock.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            notification.text = game.Feedback;
            notification.color = new Color(.54f, 1, .8f, game.FeedbackOpacity);
            intro.text = game.Elapsed < 8 ? LanguageService.Text("intro.controls") : "";
            InteractionContext context = game.GetInteractionContext();
            actionRoot.SetActive(context.Target != null && !GameFlow.IsPaused && game.Phase != RepairPhase.Complete);
            if (context.Target != null)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(context.Target.position + Vector3.up * 0.8f);
                float scale = canvas.scaleFactor;
                RectTransform rect = actionRoot.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(Mathf.Clamp(screen.x / scale - 165, 18, Screen.width / scale - 348),
                    -Mathf.Clamp((Screen.height - screen.y) / scale + 25, 190, Screen.height / scale - 183));
                actionButton.interactable = context.Enabled; SetButton(actionButton, "E  ·  " + context.Action); actionReason.text = context.Reason;
            }
            winRoot.SetActive(game.Phase == RepairPhase.Complete);
            if (game.Phase == RepairPhase.Complete)
            {
                winStars.text = new string('★', game.Stars) + new string('☆', 3 - game.Stars);
                winScore.text = LanguageService.Text("score", game.MissionTime.ToString("0.0"), game.PenaltySeconds.ToString("0"));
            }
        }
    }
}
