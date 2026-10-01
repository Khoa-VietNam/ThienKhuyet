using System;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThienKhuyet.UI
{
    /// <summary>Code-driven uGUI helpers with a consistent xianxia look: ink panels, gold trim, jade accents.</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.035f, 0.045f, 0.06f, 0.9f);
        public static readonly Color InkSoft = new Color(0.06f, 0.075f, 0.1f, 0.78f);
        public static readonly Color Gold = new Color(0.84f, 0.68f, 0.32f, 1f);
        public static readonly Color GoldDim = new Color(0.55f, 0.45f, 0.24f, 1f);
        public static readonly Color Jade = new Color(0.42f, 0.9f, 0.76f, 1f);
        public static readonly Color Paper = new Color(0.95f, 0.91f, 0.8f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.62f, 0.6f, 1f);
        public static readonly Color Danger = new Color(0.92f, 0.32f, 0.28f, 1f);
        public static readonly Color HpColor = new Color(0.85f, 0.2f, 0.22f, 1f);
        public static readonly Color StaminaColor = new Color(0.5f, 0.82f, 0.32f, 1f);
        public static readonly Color QiColor = new Color(0.3f, 0.65f, 0.98f, 1f);
        public static readonly Color ExpColor = new Color(0.95f, 0.78f, 0.3f, 1f);

        static Font font;
        static Sprite box, circle, vgrad, white, glow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            font = null;
            box = circle = vgrad = white = glow = null;
        }

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                try
                {
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans", "Segoe UI", "Arial", "Liberation Sans", "DejaVu Sans", "Helvetica", "Tahoma" }, 32);
                }
                catch (Exception)
                {
                    font = null;
                }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // ------------------------------------------------------------------ sprites
        public static Sprite White
        {
            get
            {
                if (white != null) return white;
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false, false);
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                t.SetPixels32(px);
                t.Apply(false);
                white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                return white;
            }
        }

        /// <summary>9-sliced rounded rectangle.</summary>
        public static Sprite Box9
        {
            get
            {
                if (box != null) return box;
                const int n = 48;
                const float r = 12f;
                Texture2D t = ProcTex.Sprite(n, (u, v) =>
                {
                    float px = (u * 0.5f + 0.5f) * n, py = (v * 0.5f + 0.5f) * n;
                    float dx = Mathf.Max(r - px, px - (n - r), 0f), dy = Mathf.Max(r - py, py - (n - r), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }, "ui_box");
                t.filterMode = FilterMode.Bilinear;
                box = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return box;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                Texture2D t = ProcTex.Disc(64, 0.04f);
                circle = Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }

        public static Sprite Glow
        {
            get
            {
                if (glow != null) return glow;
                Texture2D t = ProcTex.SoftCircle(64, 0.0f);
                glow = Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
                return glow;
            }
        }

        public static Sprite VGradient
        {
            get
            {
                if (vgrad != null) return vgrad;
                Texture2D t = ProcTex.Sprite(64, (u, v) => new Color(1f, 1f, 1f, Mathf.Clamp01(v * 0.5f + 0.5f)), "ui_vgrad");
                t.wrapMode = TextureWrapMode.Clamp;
                vgrad = Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
                return vgrad;
            }
        }

        // ------------------------------------------------------------------ rect helpers
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        // ------------------------------------------------------------------ widgets
        public static Image Img(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite == box ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, bool border = true)
        {
            Image bg = Img(parent, name, color, Box9, false);
            if (border)
            {
                Outline o = bg.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return bg;
        }

        public static Text Txt(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal, bool shadow = true)
        {
            RectTransform rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
                sh.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return t;
        }

        public static Button Btn(Transform parent, string name, string label, Action onClick, int fontSize = 26, Color? tint = null)
        {
            Image bg = Panel(parent, name, tint ?? new Color(0.09f, 0.11f, 0.14f, 0.92f), true);
            bg.raycastTarget = true;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.0f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.65f, 1f);
            colors.selectedColor = new Color(1.15f, 1.12f, 1.0f, 1f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            Text t = Txt(bg.transform, "Label", label, fontSize, Paper, TextAnchor.MiddleCenter, FontStyle.Normal, true);
            Stretch(t.rectTransform, 8f, 4f, 8f, 4f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (onClick != null)
            {
                btn.onClick.AddListener(() =>
                {
                    Audio.AudioManager.Instance?.Sfx2D("ui_click", 0.6f);
                    onClick();
                });
            }
            var hover = bg.gameObject.AddComponent<HoverSound>();
            return btn;
        }

        public static void SetLabel(Button b, string text)
        {
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        /// <summary>Horizontal fill bar: dark track + coloured fill (Image.Type.Filled).</summary>
        public static Image Bar(Transform parent, string name, Color fill, out Image track)
        {
            track = Img(parent, name, new Color(0.02f, 0.02f, 0.03f, 0.85f), Box9);
            Outline o = track.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            o.effectDistance = new Vector2(1f, -1f);
            Image f = Img(track.transform, "Fill", fill, White);
            Stretch(f.rectTransform, 2f, 2f, 2f, 2f);
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillOrigin = 0;
            return f;
        }

        public static ScrollRect Scroll(Transform parent, string name, out RectTransform content, float spacing = 6f, float padding = 8f)
        {
            RectTransform root = Rect(parent, name);
            var sr = root.gameObject.AddComponent<ScrollRect>();
            var maskImg = root.gameObject.AddComponent<Image>();
            maskImg.color = new Color(0f, 0f, 0f, 0.0f);
            root.gameObject.AddComponent<RectMask2D>();
            RectTransform c = Rect(root, "Content");
            c.anchorMin = new Vector2(0f, 1f);
            c.anchorMax = new Vector2(1f, 1f);
            c.pivot = new Vector2(0.5f, 1f);
            c.sizeDelta = Vector2.zero;
            var vlg = c.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            var fitter = c.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = c;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40f;
            sr.viewport = root;
            content = c;
            return sr;
        }

        public static LayoutElement Fixed(GameObject go, float height, float width = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (width > 0f) { le.minWidth = width; le.preferredWidth = width; }
            return le;
        }

        public static Slider MakeSlider(Transform parent, string name, float min, float max, float value, Action<float> onChange)
        {
            RectTransform root = Rect(parent, name);
            var slider = root.gameObject.AddComponent<Slider>();
            Image bg = Img(root, "Track", new Color(0.02f, 0.02f, 0.03f, 0.9f), Box9);
            Place(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 10f));
            Anchor(bg.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -5f), new Vector2(0f, 5f));
            RectTransform fillArea = Rect(root, "FillArea");
            Anchor(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(4f, -4f), new Vector2(-4f, 4f));
            Image fill = Img(fillArea, "Fill", Jade * 0.9f, Box9);
            Stretch(fill.rectTransform);
            RectTransform handleArea = Rect(root, "HandleArea");
            Stretch(handleArea, 8f, 0f, 8f, 0f);
            Image handle = Img(handleArea, "Handle", Gold, Circle, true);
            handle.rectTransform.sizeDelta = new Vector2(22f, 22f);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.direction = Slider.Direction.LeftToRight;
            if (onChange != null) slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        public static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }
    }

    /// <summary>Plays the hover tick when the pointer enters a button.</summary>
    public sealed class HoverSound : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            var b = GetComponent<Button>();
            if (b != null && b.interactable) Audio.AudioManager.Instance?.Sfx2D("ui_hover", 0.35f);
        }
    }
}
