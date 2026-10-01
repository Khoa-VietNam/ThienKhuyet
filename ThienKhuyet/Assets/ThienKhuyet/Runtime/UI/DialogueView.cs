using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ThienKhuyet.UI
{
    /// <summary>Frame-level UI navigation (keyboard and gamepad) read directly from the Input System so menus work without an actions asset.</summary>
    public static class UiNav
    {
        public static bool Up => Pressed(Key.W, Key.UpArrow) || (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame);
        public static bool Down => Pressed(Key.S, Key.DownArrow) || (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame);
        public static bool Left => Pressed(Key.A, Key.LeftArrow) || (Gamepad.current != null && Gamepad.current.dpad.left.wasPressedThisFrame);
        public static bool Right => Pressed(Key.D, Key.RightArrow) || (Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame);
        public static bool Confirm => Pressed(Key.Enter, Key.Space, Key.E) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        public static bool Cancel => Pressed(Key.Escape, Key.Backspace) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        static bool Pressed(params Key[] keys)
        {
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            for (int i = 0; i < keys.Length; i++) if (k[keys[i]].wasPressedThisFrame) return true;
            return false;
        }

        public static bool NumberPressed(int index)
        {
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            Key key = index == 0 ? Key.Digit1 : (index == 1 ? Key.Digit2 : (index == 2 ? Key.Digit3 : Key.Digit4));
            return k[key].wasPressedThisFrame;
        }
    }

    /// <summary>Dialogue box with typewriter text, speaker name plate and up to four choices.</summary>
    public sealed class DialogueView
    {
        readonly RectTransform root, panel, choiceHolder;
        readonly Text nameText, bodyText, hint;
        readonly Image namePlate;
        readonly List<Button> choiceButtons = new List<Button>();
        readonly List<Text> choiceTexts = new List<Text>();
        string fullText = "";
        float revealed;
        int selected;
        int choiceCount;
        bool advanceClicked;
        float blink;

        public int Chosen { get; private set; } = -1;
        public bool IsTyping => revealed < fullText.Length;
        public bool ChoicesVisible => choiceCount > 0;
        public bool Visible => root.gameObject.activeSelf;

        public DialogueView(Transform parent)
        {
            root = UIKit.Rect(parent, "Dialogue");
            UIKit.Stretch(root);
            root.gameObject.SetActive(false);

            panel = UIKit.Panel(root, "Panel", new Color(0.03f, 0.04f, 0.06f, 0.9f)).rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1420f, 250f));
            var click = panel.gameObject.AddComponent<Button>();
            click.targetGraphic = panel.GetComponent<Image>();
            panel.GetComponent<Image>().raycastTarget = true;
            click.transition = Selectable.Transition.None;
            click.onClick.AddListener(() => advanceClicked = true);

            namePlate = UIKit.Panel(panel, "Name", new Color(0.1f, 0.12f, 0.16f, 0.98f));
            UIKit.Place(namePlate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(340f, 52f));
            nameText = UIKit.Txt(namePlate.transform, "Text", "", 30, UIKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(nameText.rectTransform, 6f, 0f, 6f, 0f);

            bodyText = UIKit.Txt(panel, "Body", "", 31, UIKit.Paper, TextAnchor.UpperLeft);
            bodyText.supportRichText = false;
            UIKit.Stretch(bodyText.rectTransform, 44f, 28f, 44f, 48f);
            hint = UIKit.Txt(panel, "Hint", "▼", 26, UIKit.Gold, TextAnchor.LowerRight);
            UIKit.Stretch(hint.rectTransform, 20f, 10f, 28f, 10f);

            choiceHolder = UIKit.Rect(root, "Choices");
            UIKit.Place(choiceHolder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 320f), new Vector2(980f, 10f));
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                Button b = UIKit.Btn(choiceHolder, "Choice" + i, "", () => { Chosen = idx; }, 28);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, i * 74f), new Vector2(980f, 64f));
                b.gameObject.SetActive(false);
                choiceButtons.Add(b);
                Text t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                UIKit.Stretch(t.rectTransform, 24f, 4f, 16f, 4f);
                choiceTexts.Add(t);
            }
        }

        public void ShowLine(string speaker, string text, Color nameColor, bool narration)
        {
            root.gameObject.SetActive(true);
            ClearChoices();
            fullText = text ?? "";
            revealed = 0f;
            bodyText.text = "";
            bool hasName = !string.IsNullOrEmpty(speaker) && !narration;
            namePlate.gameObject.SetActive(hasName);
            nameText.text = speaker ?? "";
            nameText.color = nameColor;
            bodyText.fontStyle = narration ? FontStyle.Italic : FontStyle.Normal;
            bodyText.color = narration ? new Color(0.8f, 0.86f, 0.92f) : UIKit.Paper;
            advanceClicked = false;
            hint.gameObject.SetActive(false);
        }

        public void Complete()
        {
            revealed = fullText.Length;
            bodyText.text = fullText;
        }

        /// <summary>True once per advance request (mouse click on the box).</summary>
        public bool ConsumeClick()
        {
            bool c = advanceClicked;
            advanceClicked = false;
            return c;
        }

        public void ShowChoices(List<string> texts, List<bool> enabled)
        {
            choiceCount = Mathf.Min(4, texts.Count);
            Chosen = -1;
            selected = 0;
            for (int i = 0; i < 4; i++)
            {
                bool on = i < choiceCount;
                choiceButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                // the first choice sits at the top of the stack
                var rt = (RectTransform)choiceButtons[i].transform;
                rt.anchoredPosition = new Vector2(0f, (choiceCount - 1 - i) * 74f);
                choiceTexts[i].text = (i + 1) + ".  " + texts[i];
                choiceButtons[i].interactable = enabled == null || i >= enabled.Count || enabled[i];
            }
            hint.gameObject.SetActive(false);
            HighlightSelected();
        }

        public void ClearChoices()
        {
            choiceCount = 0;
            for (int i = 0; i < choiceButtons.Count; i++) choiceButtons[i].gameObject.SetActive(false);
        }

        public void Hide()
        {
            ClearChoices();
            root.gameObject.SetActive(false);
        }

        void HighlightSelected()
        {
            for (int i = 0; i < choiceCount; i++)
            {
                Image bg = choiceButtons[i].GetComponent<Image>();
                bg.color = i == selected ? new Color(0.25f, 0.2f, 0.1f, 0.98f) : new Color(0.09f, 0.11f, 0.14f, 0.92f);
                choiceTexts[i].color = i == selected ? UIKit.Gold : UIKit.Paper;
            }
        }

        public void Tick(float dt)
        {
            if (!root.gameObject.activeSelf) return;
            if (IsTyping)
            {
                float speed = 62f;
                float before = revealed;
                revealed = Mathf.Min(fullText.Length, revealed + speed * dt);
                if ((int)revealed != (int)before) bodyText.text = fullText.Substring(0, (int)revealed);
            }
            else
            {
                blink += dt;
                if (choiceCount == 0)
                {
                    hint.gameObject.SetActive(true);
                    hint.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.5f + 0.5f * Mathf.Sin(blink * 5f));
                }
            }
            if (choiceCount > 0)
            {
                if (UiNav.Up) { selected = (selected + choiceCount - 1) % choiceCount; HighlightSelected(); }
                else if (UiNav.Down) { selected = (selected + 1) % choiceCount; HighlightSelected(); }
                for (int i = 0; i < choiceCount; i++) if (UiNav.NumberPressed(i) && choiceButtons[i].interactable) Chosen = i;
                if (UiNav.Confirm && choiceButtons[selected].interactable && !IsTyping) Chosen = selected;
            }
        }
    }

    /// <summary>Cinematic subtitles: speaker name and line near the bottom of the screen with a soft backdrop.</summary>
    public sealed class SubtitleView
    {
        readonly RectTransform root;
        readonly Text speaker, line;
        readonly Image backdrop;
        readonly CanvasGroup group;
        float target;

        public SubtitleView(Transform parent)
        {
            root = UIKit.Rect(parent, "Subtitles");
            UIKit.Stretch(root);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            backdrop = UIKit.Img(root, "Backdrop", new Color(0f, 0f, 0f, 0.55f), UIKit.VGradient);
            backdrop.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            UIKit.Place(backdrop.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(2000f, 300f));
            speaker = UIKit.Txt(root, "Speaker", "", 28, UIKit.Gold, TextAnchor.LowerCenter, FontStyle.Bold);
            UIKit.Place(speaker.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1400f, 40f));
            line = UIKit.Txt(root, "Line", "", 36, Color.white, TextAnchor.UpperCenter, FontStyle.Normal);
            line.supportRichText = false;
            UIKit.Place(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1500f, 120f));
        }

        public void Show(string who, string text, Color color, float scale)
        {
            speaker.text = who ?? "";
            speaker.color = color;
            line.text = text ?? "";
            speaker.fontSize = Mathf.RoundToInt(28f * scale);
            line.fontSize = Mathf.RoundToInt(36f * scale);
            target = 1f;
        }

        public void Hide()
        {
            target = 0f;
        }

        public void ImmediateHide()
        {
            target = 0f;
            group.alpha = 0f;
        }

        public void Tick(float dt)
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, dt * 6f);
        }
    }
}
