using System.Collections.Generic;
using ThienKhuyet.Characters;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Enemies;
using ThienKhuyet.Gfx;
using ThienKhuyet.Narrative;
using ThienKhuyet.Player;
using ThienKhuyet.World;
using UnityEngine;
using UnityEngine.UI;

namespace ThienKhuyet.UI
{
    /// <summary>In-game HUD: vitals, realm/EXP, technique bar, quest tracker, minimap, prompts, lock-on, enemy and boss bars, damage numbers.</summary>
    public sealed class HudView
    {
        sealed class EnemyBar
        {
            public RectTransform root;
            public Image fill, poise, track;
            public Text name;
            public EnemyBrain brain;
        }

        sealed class DamageNumber
        {
            public Text text;
            public Vector2 pos, vel;
            public float life, max;
        }

        readonly RectTransform root, canvasRect;
        readonly Image hpFill, hpTrack, stFill, stTrack, qiFill, qiTrack, expFill;
        readonly Text hpText, realmText, buffText, clockText;
        readonly Image[] skillIcon = new Image[4], skillCd = new Image[4], skillFrame = new Image[4];
        readonly Text[] skillKey = new Text[4], skillCost = new Text[4];
        readonly Image healIcon;
        readonly Text healCount;
        readonly Text trackerTitle, trackerBody;
        readonly RectTransform trackerPanel;
        readonly RawImage miniMap, miniFog;
        readonly Image miniArrow;
        readonly Text promptText;
        readonly RectTransform promptRoot;
        readonly Image lockRing;
        readonly Text zoneText, subZoneText;
        readonly CanvasGroup zoneGroup;
        readonly RectTransform hintRoot;
        readonly Text hintText;
        readonly CanvasGroup hintGroup;
        readonly RectTransform bossRoot;
        readonly Image bossFill, bossPoise;
        readonly Text bossName;
        readonly List<EnemyBar> enemyBars = new List<EnemyBar>();
        readonly List<DamageNumber> numbers = new List<DamageNumber>();
        readonly Dictionary<EnemyBrain, float> recentlyHit = new Dictionary<EnemyBrain, float>();

        Texture2D fogTex;
        Color32[] fogPixels;
        EnemyBrain boss;
        float stFlash, qiFlash, zoneTimer, hintTimer, trackerTimer = 0.2f;
        string lastTrackerKey = "";
        float bossShown;

        public HudView(Transform parent, RectTransform canvasRect)
        {
            this.canvasRect = canvasRect;
            root = UIKit.Rect(parent, "Hud");
            UIKit.Stretch(root);

            // ---- vitals (top-left)
            RectTransform vit = UIKit.Rect(root, "Vitals");
            UIKit.Place(vit, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(38f, -34f), new Vector2(460f, 150f));
            hpFill = UIKit.Bar(vit, "Hp", UIKit.HpColor, out hpTrack);
            UIKit.Place(hpTrack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(440f, 28f));
            hpText = UIKit.Txt(hpTrack.transform, "Text", "", 19, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(hpText.rectTransform);
            stFill = UIKit.Bar(vit, "St", UIKit.StaminaColor, out stTrack);
            UIKit.Place(stTrack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -36f), new Vector2(370f, 15f));
            qiFill = UIKit.Bar(vit, "Qi", UIKit.QiColor, out qiTrack);
            UIKit.Place(qiTrack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -56f), new Vector2(370f, 15f));
            realmText = UIKit.Txt(vit, "Realm", "", 23, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(realmText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -80f), new Vector2(440f, 30f));
            Image expTrack;
            expFill = UIKit.Bar(vit, "Exp", UIKit.ExpColor, out expTrack);
            UIKit.Place(expTrack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -112f), new Vector2(440f, 8f));
            buffText = UIKit.Txt(vit, "Buffs", "", 18, UIKit.Jade, TextAnchor.UpperLeft);
            UIKit.Place(buffText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -126f), new Vector2(440f, 26f));

            // ---- technique bar (bottom centre)
            RectTransform bar = UIKit.Rect(root, "Skills");
            UIKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(520f, 92f));
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                Image frame = UIKit.Panel(bar, "Slot" + i, new Color(0.04f, 0.05f, 0.07f, 0.85f));
                UIKit.Place(frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 100f, 0f), new Vector2(88f, 88f));
                skillFrame[i] = frame;
                skillIcon[i] = UIKit.Img(frame.transform, "Icon", Color.white, null);
                UIKit.Stretch(skillIcon[i].rectTransform, 8f, 8f, 8f, 8f);
                skillIcon[i].preserveAspect = true;
                skillCd[i] = UIKit.Img(frame.transform, "Cd", new Color(0f, 0f, 0f, 0.7f), UIKit.White);
                UIKit.Stretch(skillCd[i].rectTransform);
                skillCd[i].type = Image.Type.Filled;
                skillCd[i].fillMethod = Image.FillMethod.Radial360;
                skillCd[i].fillOrigin = 2;
                skillCd[i].fillClockwise = false;
                skillKey[i] = UIKit.Txt(frame.transform, "Key", (i + 1).ToString(), 22, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Stretch(skillKey[i].rectTransform, 6f, 0f, 0f, 3f);
                skillCost[i] = UIKit.Txt(frame.transform, "Cost", "", 18, UIKit.QiColor, TextAnchor.LowerRight);
                UIKit.Stretch(skillCost[i].rectTransform, 0f, 2f, 6f, 0f);
            }
            Image healFrame = UIKit.Panel(bar, "Heal", new Color(0.04f, 0.05f, 0.07f, 0.85f));
            UIKit.Place(healFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(430f, 0f), new Vector2(88f, 88f));
            healIcon = UIKit.Img(healFrame.transform, "Icon", Color.white, null);
            UIKit.Stretch(healIcon.rectTransform, 10f, 10f, 10f, 10f);
            healIcon.preserveAspect = true;
            Text healKey = UIKit.Txt(healFrame.transform, "Key", "R", 22, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(healKey.rectTransform, 6f, 0f, 0f, 3f);
            healCount = UIKit.Txt(healFrame.transform, "Count", "", 22, Color.white, TextAnchor.LowerRight, FontStyle.Bold);
            UIKit.Stretch(healCount.rectTransform, 0f, 2f, 8f, 0f);

            // ---- minimap (top-right)
            RectTransform mini = UIKit.Rect(root, "MiniMap");
            UIKit.Place(mini, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-38f, -34f), new Vector2(230f, 230f));
            Image ring = UIKit.Img(mini, "Ring", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.9f), UIKit.Circle);
            UIKit.Stretch(ring.rectTransform, -4f, -4f, -4f, -4f);
            Image maskImg = UIKit.Img(mini, "Mask", Color.white, UIKit.Circle);
            UIKit.Stretch(maskImg.rectTransform);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            miniMap = MakeRaw(maskImg.transform, "Map");
            miniFog = MakeRaw(maskImg.transform, "Fog");
            miniArrow = UIKit.Img(maskImg.transform, "Arrow", UIKit.Jade, ArrowSprite());
            UIKit.Place(miniArrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            clockText = UIKit.Txt(root, "Clock", "", 21, UIKit.Paper, TextAnchor.UpperRight);
            UIKit.Place(clockText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-38f, -272f), new Vector2(420f, 28f));

            // ---- quest tracker
            Image tp = UIKit.Panel(root, "Tracker", new Color(0.03f, 0.04f, 0.06f, 0.66f), false);
            trackerPanel = tp.rectTransform;
            UIKit.Place(trackerPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-38f, -308f), new Vector2(470f, 140f));
            trackerTitle = UIKit.Txt(trackerPanel, "Title", "", 25, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(trackerTitle.rectTransform, 16f, 10f, 14f, 8f);
            trackerTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            trackerTitle.rectTransform.offsetMin = new Vector2(16f, -42f);
            trackerBody = UIKit.Txt(trackerPanel, "Body", "", 21, UIKit.Paper, TextAnchor.UpperLeft);
            UIKit.Stretch(trackerBody.rectTransform, 16f, 8f, 14f, 44f);

            // ---- prompt
            promptRoot = UIKit.Panel(root, "Prompt", new Color(0.03f, 0.04f, 0.06f, 0.85f)).rectTransform;
            UIKit.Place(promptRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(430f, 58f));
            promptText = UIKit.Txt(promptRoot, "Text", "", 27, UIKit.Paper, TextAnchor.MiddleCenter);
            UIKit.Stretch(promptText.rectTransform, 10f, 0f, 10f, 0f);
            promptRoot.gameObject.SetActive(false);

            // ---- lock-on ring
            lockRing = UIKit.Img(root, "LockRing", new Color(1f, 0.9f, 0.5f, 0.95f), RingSprite());
            lockRing.rectTransform.sizeDelta = new Vector2(70f, 70f);
            lockRing.gameObject.SetActive(false);

            // ---- zone banner
            RectTransform zone = UIKit.Rect(root, "Zone");
            UIKit.Place(zone, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1100f, 140f));
            zoneGroup = zone.gameObject.AddComponent<CanvasGroup>();
            zoneGroup.alpha = 0f;
            zoneText = UIKit.Txt(zone, "Name", "", 56, UIKit.Paper, TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.Stretch(zoneText.rectTransform, 0f, 40f, 0f, 0f);
            subZoneText = UIKit.Txt(zone, "Sub", "", 26, UIKit.Gold, TextAnchor.LowerCenter);
            UIKit.Stretch(subZoneText.rectTransform, 0f, 0f, 0f, 90f);

            // ---- hint
            hintRoot = UIKit.Panel(root, "Hint", new Color(0.03f, 0.04f, 0.06f, 0.78f), true).rectTransform;
            UIKit.Place(hintRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(520f, 150f));
            hintGroup = hintRoot.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            hintText = UIKit.Txt(hintRoot, "Text", "", 23, UIKit.Paper, TextAnchor.MiddleLeft);
            UIKit.Stretch(hintText.rectTransform, 18f, 8f, 14f, 8f);

            // ---- boss bar
            bossRoot = UIKit.Rect(root, "Boss");
            UIKit.Place(bossRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 70f));
            bossName = UIKit.Txt(bossRoot, "Name", "", 30, UIKit.Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.Stretch(bossName.rectTransform, 0f, 30f, 0f, 0f);
            Image bTrack;
            bossFill = UIKit.Bar(bossRoot, "Hp", new Color(0.78f, 0.15f, 0.18f), out bTrack);
            UIKit.Place(bTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(900f, 20f));
            Image pTrack;
            bossPoise = UIKit.Bar(bossRoot, "Poise", new Color(0.95f, 0.8f, 0.3f), out pTrack);
            UIKit.Place(pTrack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(900f, 6f));
            bossRoot.gameObject.SetActive(false);

            // ---- pools
            for (int i = 0; i < 14; i++) enemyBars.Add(MakeEnemyBar());
            for (int i = 0; i < 28; i++)
            {
                Text t = UIKit.Txt(root, "Dmg" + i, "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.sizeDelta = new Vector2(200f, 50f);
                t.gameObject.SetActive(false);
                numbers.Add(new DamageNumber { text = t });
            }

            EventBus.Subscribe<DamageEvent>(OnDamage);
            EventBus.Subscribe<ZoneChangedEvent>(OnZone);
            EventBus.Subscribe<PoiDiscoveredEvent>(OnPoi);
        }

        public RectTransform Root => root;

        static RawImage MakeRaw(Transform parent, string name)
        {
            RectTransform rt = UIKit.Rect(parent, name);
            UIKit.Stretch(rt);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            return raw;
        }

        static Sprite arrow, ring;

        static Sprite ArrowSprite()
        {
            if (arrow != null) return arrow;
            Texture2D t = ProcTex.Sprite(64, (u, v) =>
            {
                float y = v * 0.5f + 0.5f;
                float halfW = (1f - y) * 0.9f * 0.5f + 0.02f;
                float a = (y > 0.08f && y < 0.95f && Mathf.Abs(u) < halfW) ? 1f : 0f;
                float notch = (y < 0.35f && Mathf.Abs(u) < (0.35f - y) * 0.9f) ? 0f : 1f;
                return new Color(1f, 1f, 1f, a * notch);
            }, "ui_arrow");
            arrow = Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
            return arrow;
        }

        static Sprite RingSprite()
        {
            if (ring != null) return ring;
            Texture2D t = ProcTex.Sprite(128, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);
                float seg = Mathf.Abs(Mathf.Sin(ang * 2f)) > 0.35f ? 1f : 0f;
                float a = (d > 0.78f && d < 0.92f ? 1f : 0f) * seg + (d > 0.96f ? 0f : 0f);
                float tick = (d > 0.55f && d < 0.7f && Mathf.Abs(u) < 0.045f) || (d > 0.55f && d < 0.7f && Mathf.Abs(v) < 0.045f) ? 1f : 0f;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a + tick));
            }, "ui_lock_ring");
            ring = Sprite.Create(t, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
            return ring;
        }

        EnemyBar MakeEnemyBar()
        {
            var b = new EnemyBar();
            b.root = UIKit.Rect(root, "EnemyBar");
            b.root.sizeDelta = new Vector2(150f, 40f);
            b.name = UIKit.Txt(b.root, "Name", "", 20, UIKit.Paper, TextAnchor.LowerCenter);
            UIKit.Place(b.name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(220f, 26f));
            b.fill = UIKit.Bar(b.root, "Hp", UIKit.HpColor, out b.track);
            UIKit.Place(b.track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(130f, 10f));
            b.root.gameObject.SetActive(false);
            return b;
        }

        // ------------------------------------------------------------------ events
        void OnZone(ZoneChangedEvent e)
        {
            if (e.zoneId == "wild" || string.IsNullOrEmpty(e.zoneId)) return;
            ShowBanner(Loc.T("zone." + e.zoneId), Loc.T("zone." + e.zoneId + ".sub"));
        }

        void OnPoi(PoiDiscoveredEvent e)
        {
            PoiDef p = WorldLayout.Poi(e.poiId);
            if (p == null) return;
            ShowBanner(Loc.T(p.nameKey), Loc.T("ui.discovered"));
            Audio.AudioManager.Instance?.Sfx2D("chime_soft", 0.6f);
        }

        public void ShowBanner(string title, string sub)
        {
            zoneText.text = title;
            subZoneText.text = string.IsNullOrEmpty(sub) || sub.StartsWith("[") ? "" : sub;
            zoneTimer = 4.2f;
        }

        void OnDamage(DamageEvent e)
        {
            if (e.info.silent || e.result.dealt <= 0f && !e.result.blocked && !e.result.parried) return;
            var brain = e.victim != null ? e.victim.GetComponent<EnemyBrain>() : null;
            if (brain != null) recentlyHit[brain] = Time.time;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(e.position + Vector3.up * 0.6f);
            if (sp.z < 0f) return;
            DamageNumber n = null;
            for (int i = 0; i < numbers.Count; i++) if (!numbers[i].text.gameObject.activeSelf) { n = numbers[i]; break; }
            if (n == null) n = numbers[0];
            bool toPlayer = e.victim == Game.PlayerObject;
            string txt;
            Color c;
            if (e.result.parried) { txt = Loc.T("ui.parry"); c = new Color(0.6f, 0.95f, 1f); }
            else if (e.result.blocked) { txt = Mathf.RoundToInt(e.result.dealt).ToString(); c = new Color(0.7f, 0.8f, 1f); }
            else
            {
                txt = Mathf.RoundToInt(e.result.dealt).ToString();
                c = toPlayer ? new Color(1f, 0.35f, 0.3f) : (e.result.crit ? new Color(1f, 0.85f, 0.2f) : Color.white);
                if (e.info.type == Data.DamageType.Fire) c = Color.Lerp(c, new Color(1f, 0.55f, 0.2f), 0.6f);
                else if (e.info.type == Data.DamageType.Ice) c = Color.Lerp(c, new Color(0.6f, 0.9f, 1f), 0.6f);
                else if (e.info.type == Data.DamageType.Lightning) c = Color.Lerp(c, new Color(0.85f, 0.8f, 1f), 0.6f);
            }
            n.text.text = e.result.crit ? txt + "!" : txt;
            n.text.color = c;
            n.text.fontSize = e.result.crit ? 46 : (toPlayer ? 38 : 32);
            Vector2 canvasPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out canvasPos);
            n.pos = canvasPos + new Vector2(Random.Range(-30f, 30f), 0f);
            n.vel = new Vector2(Random.Range(-25f, 25f), 150f);
            n.max = n.life = 0.9f;
            n.text.gameObject.SetActive(true);
        }

        public void SetBoss(EnemyBrain b)
        {
            boss = b;
            bossRoot.gameObject.SetActive(b != null);
            if (b != null) bossName.text = Loc.T(b.def.nameKey);
        }

        public void ClearBoss(EnemyBrain b)
        {
            if (boss == b) SetBoss(null);
        }

        public void ShowHint(string text, float seconds)
        {
            hintText.text = text;
            hintTimer = seconds;
        }

        public void FlashStamina() { stFlash = 0.35f; }
        public void FlashQi() { qiFlash = 0.35f; }

        // ------------------------------------------------------------------ per-frame
        public void SetVisible(bool v)
        {
            root.gameObject.SetActive(v);
        }

        public void Tick(float dt)
        {
            GameSession s = Game.Session;
            Vitals v = Game.PlayerVitals;
            if (s == null || v == null || Game.PlayerObject == null) return;
            PlayerState ps = s.player;

            // vitals
            hpFill.fillAmount = Mathf.MoveTowards(hpFill.fillAmount, v.hp / v.MaxHp, dt * 1.5f);
            hpText.text = Mathf.CeilToInt(v.hp) + " / " + Mathf.CeilToInt(v.MaxHp);
            stFill.fillAmount = v.stamina / v.MaxStamina;
            qiFill.fillAmount = v.MaxQi > 0f ? v.qi / v.MaxQi : 0f;
            qiTrack.gameObject.SetActive(v.MaxQi > 0f);
            stFlash = Mathf.Max(0f, stFlash - dt);
            qiFlash = Mathf.Max(0f, qiFlash - dt);
            stFill.color = stFlash > 0f ? Color.Lerp(UIKit.StaminaColor, UIKit.Danger, stFlash * 3f) : UIKit.StaminaColor;
            qiFill.color = qiFlash > 0f ? Color.Lerp(UIKit.QiColor, UIKit.Danger, qiFlash * 3f) : UIKit.QiColor;
            hpTrack.color = v.hp / v.MaxHp < 0.25f ? Color.Lerp(new Color(0.02f, 0.02f, 0.03f, 0.85f), new Color(0.4f, 0f, 0f, 0.9f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) : new Color(0.02f, 0.02f, 0.03f, 0.85f);
            Game.PostFx?.SetLowHealth(Mathf.Clamp01((0.35f - v.hp / v.MaxHp) / 0.35f));

            realmText.text = Loc.T("realm." + ps.cultivation.Realm + ".name") + "  ·  " + Loc.T("ui.stage", ps.cultivation.Stage)
                + (ps.cultivation.ReadyForBreakthrough ? "  " + Loc.T("ui.breakthrough_ready") : "");
            expFill.fillAmount = ps.cultivation.Progress01;
            buffText.text = BuffList(v);

            // techniques
            PlayerCombat combat = Game.Player != null ? Game.Player.combat : null;
            for (int i = 0; i < 4; i++)
            {
                string id = ps.skillSlots[i];
                SkillDef sk = ContentDB.Skill(id);
                bool has = sk != null;
                skillIcon[i].enabled = has;
                skillCd[i].enabled = has;
                skillCost[i].text = has ? Mathf.RoundToInt(ps.QiCost(sk)).ToString() : "";
                if (!has) { skillFrame[i].color = new Color(0.04f, 0.05f, 0.07f, 0.55f); continue; }
                skillIcon[i].sprite = ItemIcons.Get(sk.icon, sk.color);
                bool usable = ps.SkillUsable(sk) && v.qi >= ps.QiCost(sk);
                skillIcon[i].color = usable ? Color.white : new Color(0.45f, 0.45f, 0.5f, 0.8f);
                skillCd[i].fillAmount = combat != null ? combat.CooldownFraction(sk) : 0f;
                skillFrame[i].color = new Color(0.04f, 0.05f, 0.07f, 0.88f);
            }
            string healId = Game.Manager != null ? Game.Manager.QuickHealItemId : null;
            ItemDef heal = ContentDB.Item(healId);
            healIcon.enabled = heal != null;
            if (heal != null) healIcon.sprite = ItemIcons.Get(heal);
            healCount.text = heal != null ? ps.inventory.Count(healId).ToString() : "";

            UpdateMinimap(s);
            UpdateTracker(dt, s);
            UpdatePrompt();
            UpdateLock();
            UpdateEnemyBars(dt);
            UpdateNumbers(dt);
            UpdateBanner(dt);
            UpdateHint(dt);
            UpdateBoss();
        }

        string BuffList(Vitals v)
        {
            string text = "";
            if (v.HasBuff("iron_body")) text += Loc.T("skill.iron_body.name") + "  ";
            if (v.status.Has("burn")) text += Loc.T("status.burn") + "  ";
            if (v.status.Has("slow")) text += Loc.T("status.slow") + "  ";
            if (v.status.Has("shield")) text += Loc.T("status.shield") + "  ";
            return text;
        }

        void UpdateMinimap(GameSession s)
        {
            WorldManager w = Game.World;
            if (w == null || !w.Ready || w.MapTexture == null) return;
            Vector3 p = Game.PlayerObject.transform.position;
            if (miniMap.texture != w.MapTexture) miniMap.texture = w.MapTexture;
            float half = 150f;
            float size = WorldLayout.Size;
            var uv = new Rect((p.x - half) / size, (p.z - half) / size, half * 2f / size, half * 2f / size);
            miniMap.uvRect = uv;
            if (fogTex == null)
            {
                fogTex = new Texture2D(WorldManager.FogRes, WorldManager.FogRes, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "fog" };
                fogPixels = new Color32[WorldManager.FogRes * WorldManager.FogRes];
                miniFog.texture = fogTex;
                w.FogDirty = true;
            }
            if (w.FogDirty)
            {
                for (int i = 0; i < fogPixels.Length; i++) fogPixels[i] = w.Fog[i] != 0 ? new Color32(0, 0, 0, 0) : new Color32(8, 10, 14, 235);
                fogTex.SetPixels32(fogPixels);
                fogTex.Apply(false);
                w.FogDirty = false;
            }
            miniFog.uvRect = uv;
            miniArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Game.PlayerObject.transform.eulerAngles.y);
            float t = s.timeOfDay;
            string[] hours = { "Tý", "Sửu", "Dần", "Mão", "Thìn", "Tỵ", "Ngọ", "Mùi", "Thân", "Dậu", "Tuất", "Hợi" };
            int idx = Mathf.FloorToInt(((t + 1f) % 24f) / 2f) % 12;
            clockText.text = Loc.T("ui.day", s.day) + "  ·  " + Loc.T("ui.hour", hours[idx]) + (w.Env != null && w.Env.IsNight ? "  ☾" : "  ☀");
        }

        void UpdateTracker(float dt, GameSession s)
        {
            trackerTimer -= dt;
            if (trackerTimer > 0f) return;
            trackerTimer = 0.25f;
            QuestProgress q = s.quests != null ? s.quests.Tracked : null;
            if (q == null)
            {
                trackerPanel.gameObject.SetActive(false);
                return;
            }
            trackerPanel.gameObject.SetActive(true);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < q.def.objectives.Count; i++)
            {
                ObjectiveDef o = q.def.objectives[i];
                string line = Loc.T(o.textKey);
                if (o.count > 1 && o.type != ObjectiveType.Flag && o.type != ObjectiveType.Realm) line += "  (" + Mathf.Min(q.counts[i], o.count) + "/" + o.count + ")";
                sb.Append(q.complete[i] ? "<color=#8fd8a0>✓ " : "○ ").Append(line).Append(q.complete[i] ? "</color>" : "").Append('\n');
            }
            string key = q.def.id + sb;
            if (key == lastTrackerKey) return;
            lastTrackerKey = key;
            trackerTitle.text = Loc.T(q.def.titleKey);
            trackerBody.text = sb.ToString().TrimEnd('\n');
            float h = 54f + trackerBody.preferredHeight + 14f;
            trackerPanel.sizeDelta = new Vector2(470f, Mathf.Max(90f, h));
        }

        void UpdatePrompt()
        {
            Interactor it = Game.Player != null ? Game.Player.interactor : null;
            string key = Game.Mode == GameMode.Playing && it != null ? it.PromptKey : null;
            if (string.IsNullOrEmpty(key)) { promptRoot.gameObject.SetActive(false); return; }
            promptRoot.gameObject.SetActive(true);
            promptText.text = "[E]  " + Loc.T(key);
        }

        void UpdateLock()
        {
            PlayerCombat c = Game.Player != null ? Game.Player.combat : null;
            Camera cam = Camera.main;
            if (c == null || cam == null || !c.lockOn.Active || Game.Mode != GameMode.Playing) { lockRing.gameObject.SetActive(false); return; }
            Vector3 sp = cam.WorldToScreenPoint(c.lockOn.Target.Center);
            if (sp.z < 0f) { lockRing.gameObject.SetActive(false); return; }
            lockRing.gameObject.SetActive(true);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out local);
            lockRing.rectTransform.anchoredPosition = local;
            lockRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 40f);
        }

        void UpdateEnemyBars(float dt)
        {
            Camera cam = Camera.main;
            int used = 0;
            if (cam != null && Game.Mode == GameMode.Playing && Game.PlayerObject != null)
            {
                Vector3 pp = Game.PlayerObject.transform.position;
                IReadOnlyList<EnemyBrain> all = EnemyBrain.All;
                for (int i = 0; i < all.Count && used < enemyBars.Count; i++)
                {
                    EnemyBrain b = all[i];
                    if (b == null || b.State == EState.Dead || b.def.boss) continue;
                    float d = Vector3.Distance(pp, b.transform.position);
                    bool hit = recentlyHit.TryGetValue(b, out float t) && Time.time - t < 4f;
                    bool locked = Game.Player != null && Game.Player.combat.lockOn.Target == b.vitals;
                    if (d > 34f || !(hit || locked || b.IsAggro && d < 18f)) continue;
                    Vector3 sp = cam.WorldToScreenPoint(b.transform.position + Vector3.up * (1.95f * Mathf.Max(0.6f, b.def.scale) + 0.35f));
                    if (sp.z < 0f) continue;
                    EnemyBar bar = enemyBars[used++];
                    bar.root.gameObject.SetActive(true);
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out local);
                    bar.root.anchoredPosition = local;
                    bar.fill.fillAmount = b.vitals.hp / b.vitals.MaxHp;
                    bar.name.text = Loc.T(b.def.nameKey);
                    bar.name.color = b.def.elite ? new Color(1f, 0.75f, 0.35f) : UIKit.Paper;
                    bar.track.rectTransform.sizeDelta = new Vector2(b.def.elite ? 170f : 130f, 10f);
                }
            }
            for (int i = used; i < enemyBars.Count; i++) if (enemyBars[i].root.gameObject.activeSelf) enemyBars[i].root.gameObject.SetActive(false);
        }

        void UpdateNumbers(float dt)
        {
            for (int i = 0; i < numbers.Count; i++)
            {
                DamageNumber n = numbers[i];
                if (!n.text.gameObject.activeSelf) continue;
                n.life -= dt;
                if (n.life <= 0f) { n.text.gameObject.SetActive(false); continue; }
                n.vel.y -= 120f * dt;
                n.pos += n.vel * dt;
                n.text.rectTransform.anchoredPosition = n.pos;
                Color c = n.text.color;
                c.a = Mathf.Clamp01(n.life / (n.max * 0.5f));
                n.text.color = c;
                float k = 1f + Mathf.Clamp01(n.life / n.max - 0.7f) * 1.5f;
                n.text.rectTransform.localScale = Vector3.one * k;
            }
        }

        void UpdateBanner(float dt)
        {
            if (zoneTimer > 0f)
            {
                zoneTimer -= dt;
                float a = Mathf.Clamp01(Mathf.Min(zoneTimer, 4.2f - zoneTimer) * 1.6f);
                zoneGroup.alpha = a;
            }
            else zoneGroup.alpha = 0f;
        }

        void UpdateHint(float dt)
        {
            if (hintTimer > 0f)
            {
                hintTimer -= dt;
                hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, 1f, dt * 4f);
                hintRoot.sizeDelta = new Vector2(520f, Mathf.Max(90f, hintText.preferredHeight + 26f));
            }
            else hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, 0f, dt * 3f);
        }

        void UpdateBoss()
        {
            if (boss == null) return;
            if (boss.State == EState.Dead) { SetBoss(null); return; }
            bossFill.fillAmount = Mathf.MoveTowards(bossFill.fillAmount, boss.vitals.hp / boss.vitals.MaxHp, 0.8f * Time.unscaledDeltaTime);
            bossPoise.fillAmount = boss.vitals.poise / boss.vitals.MaxPoise;
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<DamageEvent>(OnDamage);
            EventBus.Unsubscribe<ZoneChangedEvent>(OnZone);
            EventBus.Unsubscribe<PoiDiscoveredEvent>(OnPoi);
        }
    }
}
