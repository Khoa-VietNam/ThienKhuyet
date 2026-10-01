using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Enemies;
using UnityEngine;
using UnityEngine.UI;

namespace ThienKhuyet.UI
{
    /// <summary>Runtime uGUI composition root. Builds the HUD, menus, dialogue and subtitle layers without scene prefabs.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class UIRoot : MonoBehaviour
    {
        sealed class ToastEntry
        {
            public RectTransform root;
            public CanvasGroup group;
            public float remaining;
        }

        Canvas canvas;
        RectTransform canvasRect;
        HudView hud;
        MenuView menu;
        DialogueView dialogue;
        SubtitleView subtitles;
        readonly List<ToastEntry> toasts = new List<ToastEntry>();
        RectTransform loadingRoot, mainMenuRoot, gameOverRoot, endingRoot, fadeRoot, barsRoot;
        Image loadingFill, fadeImage;
        Text loadingLabel, loadingPercent;
        CanvasGroup fadeGroup;
        float fadeTarget, fadeSpeed = 4f;
        Action<int> choiceCallback;
        bool messageMode;
        bool initialized;

        public RectTransform Root => canvasRect;
        public bool IsMenuOpen => menu != null && menu.IsOpen;
        public bool IsDialogueVisible => dialogue != null && dialogue.Visible && !messageMode;
        public bool DialogueIsTyping => dialogue != null && dialogue.IsTyping;
        public bool HasDialogueChoices => dialogue != null && dialogue.ChoicesVisible;
        public int ChosenDialogueChoice => dialogue != null ? dialogue.Chosen : -1;

        public static UIRoot Create(Transform parent)
        {
            var go = new GameObject("UIRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var ui = go.AddComponent<UIRoot>();
            ui.Build();
            return ui;
        }

        void Build()
        {
            if (initialized) return;
            initialized = true;
            canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasRect = (RectTransform)transform;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            GameInput.CreateEventSystem(transform.parent);
            hud = new HudView(transform, canvasRect);
            menu = new MenuView(transform, this);
            dialogue = new DialogueView(transform);
            subtitles = new SubtitleView(transform);
            BuildLoading();
            BuildFade();
            BuildCinematicBars();
            loadingRoot.gameObject.SetActive(true);
            SetLoading(0f, Loc.T("ui.loading"));
        }

        void BuildLoading()
        {
            loadingRoot = UIKit.Rect(transform, "Loading");
            UIKit.Stretch(loadingRoot);
            Image bg = UIKit.Img(loadingRoot, "Backdrop", new Color(0.018f, 0.028f, 0.045f, 1f), UIKit.White);
            UIKit.Stretch(bg.rectTransform);
            Image titleLine = UIKit.Img(loadingRoot, "GoldLine", UIKit.Gold, UIKit.White);
            UIKit.Place(titleLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -75f), new Vector2(480f, 2f));
            loadingLabel = UIKit.Txt(loadingRoot, "Label", Loc.T("ui.loading"), 29, UIKit.Paper, TextAnchor.MiddleCenter);
            UIKit.Place(loadingLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -23f), new Vector2(900f, 50f));
            Image track;
            loadingFill = UIKit.Bar(loadingRoot, "Progress", UIKit.Jade, out track);
            UIKit.Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -112f), new Vector2(480f, 12f));
            loadingPercent = UIKit.Txt(loadingRoot, "Percent", "0%", 17, UIKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, false);
            UIKit.Place(loadingPercent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -142f), new Vector2(200f, 25f));
        }

        void BuildFade()
        {
            fadeRoot = UIKit.Rect(transform, "ScreenFade");
            UIKit.Stretch(fadeRoot);
            fadeImage = UIKit.Img(fadeRoot, "Image", Color.black, UIKit.White);
            UIKit.Stretch(fadeImage.rectTransform);
            fadeGroup = fadeRoot.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
            fadeGroup.alpha = 0f;
            fadeRoot.SetAsLastSibling();
        }

        void BuildCinematicBars()
        {
            barsRoot = UIKit.Rect(transform, "CinematicBars");
            UIKit.Stretch(barsRoot);
            Image top = UIKit.Img(barsRoot, "Top", Color.black, UIKit.White);
            UIKit.Anchor(top.rectTransform, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            top.rectTransform.sizeDelta = new Vector2(0f, 0f);
            Image bottom = UIKit.Img(barsRoot, "Bottom", Color.black, UIKit.White);
            UIKit.Anchor(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            barsRoot.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!initialized) return;
            float dt = Time.unscaledDeltaTime;
            if (hud != null) hud.Tick(dt);
            if (menu != null) menu.Tick(dt);
            if (dialogue != null) dialogue.Tick(dt);
            if (subtitles != null) subtitles.Tick(dt);
            UpdateFade(dt);
            UpdateToasts(dt);
            UpdateMessageInput();
            UpdateChoiceInput();
        }

        void UpdateFade(float dt)
        {
            if (fadeGroup == null) return;
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, fadeTarget, Mathf.Max(0.01f, fadeSpeed) * dt);
            fadeRoot.SetAsLastSibling();
        }

        void UpdateToasts(float dt)
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                ToastEntry t = toasts[i];
                t.remaining -= dt;
                float alpha = Mathf.Clamp01(Mathf.Min(t.remaining, 3.1f - t.remaining) * 2.2f);
                t.group.alpha = alpha;
                t.root.anchoredPosition = new Vector2(0f, -88f - i * 62f + (1f - alpha) * 14f);
                if (t.remaining <= 0f)
                {
                    Destroy(t.root.gameObject);
                    toasts.RemoveAt(i);
                }
            }
        }

        void UpdateMessageInput()
        {
            if (!messageMode || dialogue == null || Game.Input == null) return;
            if (!Game.Input.AdvancePressed && !dialogue.ConsumeClick()) return;
            if (dialogue.IsTyping) dialogue.Complete();
            else
            {
                messageMode = false;
                dialogue.Hide();
                Game.Input.PopLock("message");
                if (Game.Mode == GameMode.Dialogue) Game.Mode = GameMode.Playing;
            }
        }

        void UpdateChoiceInput()
        {
            if (choiceCallback == null || dialogue == null || dialogue.Chosen < 0) return;
            int chosen = dialogue.Chosen;
            Action<int> callback = choiceCallback;
            choiceCallback = null;
            dialogue.Hide();
            callback(chosen);
        }

        public void SetVisible(bool visible)
        {
            if (hud != null) hud.SetVisible(visible);
        }

        public void FlashStamina() { hud?.FlashStamina(); }
        public void FlashQi() { hud?.FlashQi(); }

        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string message = Loc.Has(text) ? Loc.T(text) : text;
            if (toasts.Count >= 5)
            {
                ToastEntry old = toasts[0];
                if (old.root != null) Destroy(old.root.gameObject);
                toasts.RemoveAt(0);
            }
            Image bg = UIKit.Panel(transform, "Toast", new Color(0.025f, 0.04f, 0.052f, 0.94f), true);
            RectTransform root = bg.rectTransform;
            UIKit.Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f - toasts.Count * 62f), new Vector2(560f, 48f));
            Text txt = UIKit.Txt(root, "Text", message, 22, UIKit.Paper, TextAnchor.MiddleCenter);
            UIKit.Stretch(txt.rectTransform, 12f, 2f, 12f, 2f);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            toasts.Add(new ToastEntry { root = root, group = group, remaining = 3.1f });
            EventBus.Publish(new ToastEvent { text = text, kind = 0 });
        }

        public void ShowBoss(EnemyBrain brain) { hud?.SetBoss(brain); }
        public void HideBoss(EnemyBrain brain) { hud?.ClearBoss(brain); }
        public void ShowHint(string text, float seconds = 4f) { hud?.ShowHint(Loc.Has(text) ? Loc.T(text) : text, seconds); }
        public void ShowBanner(string title, string sub) { hud?.ShowBanner(title, sub); }

        public void OpenMenu(int tab, bool preserveMainMenu = false)
        {
            if (menu == null) return;
            if (!preserveMainMenu) HideMainMenu();
            menu.Open(tab);
        }

        public void CloseMenu()
        {
            if (menu != null) menu.Close();
        }

        public void HideMenu()
        {
            if (menu != null && menu.IsOpen) menu.Close();
        }

        public void BeginDialogueLine(string speaker, string text, Color color, bool narration)
        {
            messageMode = false;
            if (choiceCallback != null) choiceCallback = null;
            dialogue.ShowLine(speaker, text, color, narration);
        }

        public void CompleteDialogueLine() { dialogue?.Complete(); }
        public bool ConsumeDialogueClick() { return dialogue != null && dialogue.ConsumeClick(); }
        public void ShowDialogueChoices(List<string> texts, List<bool> enabled) { dialogue?.ShowChoices(texts, enabled); }
        public void HideDialogueChoices() { dialogue?.ClearChoices(); }
        public void HideDialogue()
        {
            if (dialogue != null) dialogue.Hide();
            if (choiceCallback != null) choiceCallback = null;
        }

        public void ShowCutsceneChoice(string prompt, List<string> options, Action<int> onChosen)
        {
            if (dialogue == null || options == null || options.Count == 0) return;
            choiceCallback = onChosen;
            string title = Loc.T("ui.choice");
            dialogue.ShowLine(title, Loc.Has(prompt) ? Loc.T(prompt) : prompt, UIKit.Gold, false);
            dialogue.Complete();
            dialogue.ShowChoices(options, null);
        }

        public void ShowMessage(string titleKey, string textKey)
        {
            if (dialogue == null || messageMode) return;
            HideMainMenu();
            messageMode = true;
            if (Game.Input != null) Game.Input.PushLock("message");
            Game.Mode = GameMode.Dialogue;
            dialogue.ShowLine(Loc.TOrSelf(titleKey), Loc.TOrSelf(textKey), UIKit.Gold, false);
        }

        public void ShowSubtitle(string who, string text, Color color, float scale = 1f)
        {
            if (Game.Settings != null && !Game.Settings.subtitles) return;
            string speaker = Loc.Has(who) ? Loc.T(who) : who;
            string line = Loc.Has(text) ? Loc.T(text) : text;
            subtitles?.Show(speaker, line, color, scale);
        }

        public void HideSubtitle() { subtitles?.Hide(); }
        public void HideSubtitleImmediate() { subtitles?.ImmediateHide(); }

        public void FadeTo(Color color, float alpha, float seconds)
        {
            if (fadeImage == null) return;
            fadeImage.color = color;
            fadeTarget = Mathf.Clamp01(alpha);
            fadeSpeed = seconds <= 0f ? 100f : 1f / seconds;
            if (seconds <= 0f) fadeGroup.alpha = fadeTarget;
            fadeRoot.SetAsLastSibling();
        }

        public void SetCinematicBars(bool visible)
        {
            if (barsRoot == null) return;
            barsRoot.gameObject.SetActive(visible);
            RectTransform top = (RectTransform)barsRoot.Find("Top");
            RectTransform bottom = (RectTransform)barsRoot.Find("Bottom");
            float height = Screen.height > 0 ? Mathf.Min(108f, Screen.height * 0.1f) : 90f;
            top.sizeDelta = new Vector2(0f, height);
            bottom.sizeDelta = new Vector2(0f, height);
        }

        public void SetLoading(float progress, string stage)
        {
            if (loadingRoot == null) return;
            loadingRoot.gameObject.SetActive(true);
            loadingFill.fillAmount = Mathf.Clamp01(progress);
            loadingPercent.text = Mathf.RoundToInt(progress * 100f) + "%";
            loadingLabel.text = Loc.TOrSelf(stage);
            loadingRoot.SetAsLastSibling();
        }

        public void FinishLoading()
        {
            if (loadingRoot != null) loadingRoot.gameObject.SetActive(false);
        }

        public void ShowMainMenu(bool hasSave, Action newGame, Action continueGame, Action settings, Action quit)
        {
            HideMenu();
            if (mainMenuRoot != null) Destroy(mainMenuRoot.gameObject);
            Image dim = UIKit.Img(transform, "MainMenu", new Color(0.015f, 0.025f, 0.04f, 0.92f), UIKit.White);
            mainMenuRoot = dim.rectTransform;
            UIKit.Stretch(mainMenuRoot);
            Text title = UIKit.Txt(mainMenuRoot, "Title", "<color=#e8c566>天缺</color>\n<color=#cfe1df>THIÊN KHUYẾT</color>", 64, UIKit.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 200f));
            Text subtitle = UIKit.Txt(mainMenuRoot, "Subtitle", Loc.T("ui.main_subtitle"), 25, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Place(subtitle.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), new Vector2(0f, 128f), new Vector2(980f, 50f));
            RectTransform buttons = UIKit.Rect(mainMenuRoot, "Buttons");
            UIKit.Place(buttons, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 330f));
            Button start = UIKit.Btn(buttons, "New", Loc.T("ui.new_game"), newGame, 28, new Color(0.1f, 0.17f, 0.16f, 0.97f));
            UIKit.Place((RectTransform)start.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(390f, 64f));
            Button resume = UIKit.Btn(buttons, "Continue", Loc.T("ui.continue"), continueGame, 26);
            UIKit.Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(390f, 58f));
            resume.interactable = hasSave;
            Button opts = UIKit.Btn(buttons, "Settings", Loc.T("ui.settings"), settings, 26);
            UIKit.Place((RectTransform)opts.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(390f, 58f));
            Button exit = UIKit.Btn(buttons, "Quit", Loc.T("ui.quit"), quit, 26);
            UIKit.Place((RectTransform)exit.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -236f), new Vector2(390f, 58f));
        }

        public void HideMainMenu()
        {
            if (mainMenuRoot != null)
            {
                Destroy(mainMenuRoot.gameObject);
                mainMenuRoot = null;
            }
        }

        public void ShowGameOver(Action retry, Action mainMenu)
        {
            ClearOverlay(ref gameOverRoot);
            Image bg = UIKit.Img(transform, "GameOver", new Color(0.025f, 0.01f, 0.018f, 0.92f), UIKit.White);
            gameOverRoot = bg.rectTransform;
            UIKit.Stretch(gameOverRoot);
            Text title = UIKit.Txt(gameOverRoot, "Title", Loc.T("ui.you_fell"), 60, UIKit.Danger, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 90f));
            Button a = UIKit.Btn(gameOverRoot, "Retry", Loc.T("ui.return_shrine"), retry, 28);
            UIKit.Place((RectTransform)a.transform, new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.5f), new Vector2(-170f, 0f), new Vector2(320f, 64f));
            Button b = UIKit.Btn(gameOverRoot, "Menu", Loc.T("ui.main_menu"), mainMenu, 28);
            UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.5f), new Vector2(170f, 0f), new Vector2(320f, 64f));
        }

        public void HideGameOver() { ClearOverlay(ref gameOverRoot); }

        public void ShowEnding(string endingId, Action mainMenu)
        {
            ClearOverlay(ref endingRoot);
            Image bg = UIKit.Img(transform, "Ending", new Color(0.02f, 0.03f, 0.055f, 0.96f), UIKit.White);
            endingRoot = bg.rectTransform;
            UIKit.Stretch(endingRoot);
            string titleKey = endingId == "return" ? "ending.return.title" : "ending.stay.title";
            string bodyKey = endingId == "return" ? "ending.return.body" : "ending.stay.body";
            Text title = UIKit.Txt(endingRoot, "Title", Loc.T(titleKey), 54, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 100f));
            Text body = UIKit.Txt(endingRoot, "Body", Loc.T(bodyKey), 28, UIKit.Paper, TextAnchor.UpperCenter);
            UIKit.Place(body.rectTransform, new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1150f, 220f));
            Button b = UIKit.Btn(endingRoot, "Menu", Loc.T("ui.main_menu"), mainMenu, 28);
            UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 64f));
        }

        static void ClearOverlay(ref RectTransform root)
        {
            if (root == null) return;
            Destroy(root.gameObject);
            root = null;
        }

        void OnDestroy()
        {
            if (hud != null) hud.Dispose();
        }
    }
}
