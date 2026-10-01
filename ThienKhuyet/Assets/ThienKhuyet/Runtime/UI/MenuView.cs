using System;
using System.Collections.Generic;
using System.Text;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Gfx;
using ThienKhuyet.Items;
using ThienKhuyet.Narrative;
using ThienKhuyet.Player;
using ThienKhuyet.SaveLoad;
using ThienKhuyet.World;
using UnityEngine;
using UnityEngine.UI;

namespace ThienKhuyet.UI
{
    /// <summary>Pause / character menu with tabs: inventory and equipment, character, cultivation, techniques, quests, map, settings, save/load.</summary>
    public sealed class MenuView
    {
        public const int TabInventory = 0, TabCharacter = 1, TabCultivation = 2, TabSkills = 3, TabQuests = 4, TabMap = 5, TabSettings = 6, TabSave = 7;
        static readonly string[] TabKeys = { "ui.inventory", "ui.character", "ui.cultivation", "ui.skills", "ui.quests", "ui.map", "ui.settings", "ui.saveload" };

        readonly RectTransform root, content, tabColumn;
        readonly List<Button> tabs = new List<Button>();
        readonly UIRoot ui;
        int current = -1;
        int selectedItem = -1;
        EquipSlot selectedEquip = EquipSlot.None;
        string selectedSkill;
        string selectedQuest;
        string mapPoi;
        string confirmKey;
        float cooldownClick;

        public bool IsOpen => root.gameObject.activeSelf;
        public int CurrentTab => current;

        public MenuView(Transform parent, UIRoot ui)
        {
            this.ui = ui;
            root = UIKit.Rect(parent, "Menu");
            UIKit.Stretch(root);
            Image dim = UIKit.Img(root, "Dim", new Color(0f, 0.01f, 0.02f, 0.82f), UIKit.White, true);
            UIKit.Stretch(dim.rectTransform);
            Image frame = UIKit.Panel(root, "Frame", new Color(0.035f, 0.045f, 0.06f, 0.96f));
            UIKit.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1680f, 940f));

            tabColumn = UIKit.Rect(frame.transform, "Tabs");
            UIKit.Anchor(tabColumn, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(18f, 18f), new Vector2(268f, -18f));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int idx = i;
                Button b = UIKit.Btn(tabColumn, "Tab" + i, "", () => Open(idx), 26);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -i * 64f), new Vector2(250f, 56f));
                tabs.Add(b);
            }
            Button resume = UIKit.Btn(tabColumn, "Resume", "", Close, 24, new Color(0.1f, 0.2f, 0.16f, 0.95f));
            UIKit.Place((RectTransform)resume.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(250f, 54f));
            Button mainMenu = UIKit.Btn(tabColumn, "MainMenu", "", () => Confirm("ui.confirm_menu", () => Game.Manager.ReturnToMainMenu()), 24);
            UIKit.Place((RectTransform)mainMenu.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 74f), new Vector2(250f, 54f));
            Button quit = UIKit.Btn(tabColumn, "Quit", "", () => Confirm("ui.confirm_quit", () => Game.Manager.QuitGame()), 24);
            UIKit.Place((RectTransform)quit.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(250f, 54f));

            content = UIKit.Rect(frame.transform, "Content");
            UIKit.Anchor(content, Vector2.zero, Vector2.one, new Vector2(296f, 18f), new Vector2(-18f, -18f));
            root.gameObject.SetActive(false);
            Loc.Changed += RelabelStatic;
            RelabelStatic();
        }

        void RelabelStatic()
        {
            for (int i = 0; i < tabs.Count; i++) UIKit.SetLabel(tabs[i], Loc.T(TabKeys[i]));
            UIKit.SetLabel(tabColumn.Find("Resume").GetComponent<Button>(), Loc.T("ui.resume"));
            UIKit.SetLabel(tabColumn.Find("MainMenu").GetComponent<Button>(), Loc.T("ui.main_menu"));
            UIKit.SetLabel(tabColumn.Find("Quit").GetComponent<Button>(), Loc.T("ui.quit"));
        }

        // ------------------------------------------------------------------ open / close
        public void Open(int tab)
        {
            if (!root.gameObject.activeSelf) Audio.AudioManager.Instance?.Sfx2D("ui_open", 0.7f);
            root.gameObject.SetActive(true);
            current = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            for (int i = 0; i < tabs.Count; i++)
                tabs[i].GetComponent<Image>().color = i == current ? new Color(0.28f, 0.22f, 0.1f, 0.98f) : new Color(0.09f, 0.11f, 0.14f, 0.92f);
            Rebuild();
        }

        public void Close()
        {
            if (!root.gameObject.activeSelf) return;
            Audio.AudioManager.Instance?.Sfx2D("ui_close", 0.7f);
            root.gameObject.SetActive(false);
            Game.Manager?.OnMenuClosed();
        }

        public void Rebuild()
        {
            if (!root.gameObject.activeSelf || current < 0) return;
            for (int i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(content.GetChild(i).gameObject);
            switch (current)
            {
                case TabInventory: BuildInventory(); break;
                case TabCharacter: BuildCharacter(); break;
                case TabCultivation: BuildCultivation(); break;
                case TabSkills: BuildSkills(); break;
                case TabQuests: BuildQuests(); break;
                case TabMap: BuildMap(); break;
                case TabSettings: BuildSettings(); break;
                case TabSave: BuildSave(); break;
            }
            if (!string.IsNullOrEmpty(confirmKey)) { }
        }

        public void Tick(float dt)
        {
            if (!IsOpen) return;
            cooldownClick -= dt;
            if (current == TabMap) UpdateMapMarkers();
            if (UiNav.Cancel && cooldownClick <= 0f)
            {
                if (confirmPanel != null) { UnityEngine.Object.Destroy(confirmPanel.gameObject); confirmPanel = null; }
                else Close();
            }
        }

        // ------------------------------------------------------------------ generic builders
        Text Title(Transform parent, string text, float x, float y, float w = 600f)
        {
            Text t = UIKit.Txt(parent, "Title", text, 34, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, 44f));
            return t;
        }

        RectTransform Section(Transform parent, string name, float x, float y, float w, float h, bool filled = true)
        {
            Image p = UIKit.Panel(parent, name, filled ? new Color(0.05f, 0.065f, 0.085f, 0.9f) : new Color(0f, 0f, 0f, 0f), filled);
            UIKit.Place(p.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h));
            return p.rectTransform;
        }

        RectTransform confirmPanel;

        void Confirm(string textKey, Action yes)
        {
            if (confirmPanel != null) UnityEngine.Object.Destroy(confirmPanel.gameObject);
            Image dim = UIKit.Img(root, "Confirm", new Color(0f, 0f, 0f, 0.65f), UIKit.White, true);
            UIKit.Stretch(dim.rectTransform);
            confirmPanel = dim.rectTransform;
            Image box = UIKit.Panel(dim.transform, "Box", new Color(0.05f, 0.06f, 0.08f, 0.98f));
            UIKit.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 280f));
            Text t = UIKit.Txt(box.transform, "Text", Loc.T(textKey), 30, UIKit.Paper, TextAnchor.MiddleCenter);
            UIKit.Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 100f), new Vector2(-30f, -20f));
            Button y = UIKit.Btn(box.transform, "Yes", Loc.T("ui.yes"), () => { UnityEngine.Object.Destroy(confirmPanel.gameObject); confirmPanel = null; yes(); }, 28);
            UIKit.Place((RectTransform)y.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 28f), new Vector2(240f, 60f));
            Button n = UIKit.Btn(box.transform, "No", Loc.T("ui.no"), () => { UnityEngine.Object.Destroy(confirmPanel.gameObject); confirmPanel = null; cooldownClick = 0.2f; }, 28);
            UIKit.Place((RectTransform)n.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 28f), new Vector2(240f, 60f));
            cooldownClick = 0.25f;
        }

        // ------------------------------------------------------------------ INVENTORY
        void BuildInventory()
        {
            PlayerState ps = Game.Session.player;
            Title(content, Loc.T("ui.inventory"), 10f, -4f);
            Text stones = UIKit.Txt(content, "Stones", "◆ " + Loc.T("item.spirit_stone.name") + ": " + ps.inventory.Count("spirit_stone"), 26, UIKit.Jade, TextAnchor.UpperRight);
            UIKit.Place(stones.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -10f), new Vector2(520f, 36f));

            // equipment row
            RectTransform eq = Section(content, "Equip", 10f, -60f, 760f, 128f);
            for (int i = 0; i < Equipment.AllSlots.Length; i++)
            {
                EquipSlot slot = Equipment.AllSlots[i];
                string id = ps.equipment.Get(slot);
                ItemDef def = ContentDB.Item(id);
                Button b = SlotButton(eq, "Eq" + i, def, 0, false, def != null && selectedEquip == slot);
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f + i * 122f, 8f), new Vector2(110f, 90f));
                Text lbl = UIKit.Txt(eq, "L" + i, Loc.T("slot." + slot.ToString().ToLowerInvariant()), 17, UIKit.Muted, TextAnchor.UpperCenter);
                UIKit.Place(lbl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f + i * 122f, -4f), new Vector2(110f, 24f));
                EquipSlot cap = slot;
                b.onClick.AddListener(() => { selectedEquip = cap; selectedItem = -1; Rebuild(); });
            }

            // grid
            RectTransform grid = Section(content, "Grid", 10f, -200f, 760f, 640f);
            int cols = 8;
            for (int i = 0; i < ps.inventory.SlotCount; i++)
            {
                ItemStack st = ps.inventory.GetSlot(i);
                ItemDef def = st.IsEmpty ? null : ContentDB.Item(st.itemId);
                Button b = SlotButton(grid, "S" + i, def, st.count, true, i == selectedItem);
                int r = i / cols, c = i % cols;
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f + c * 92f, -14f - r * 100f), new Vector2(84f, 90f));
                int cap = i;
                b.onClick.AddListener(() => { selectedItem = cap; selectedEquip = EquipSlot.None; Rebuild(); });
            }
            Button sort = UIKit.Btn(content, "Sort", Loc.T("ui.sort"), () => { ps.inventory.Sort(id => { ItemDef d = ContentDB.Item(id); return d == null ? id : ((int)d.type).ToString("00") + (9 - (int)d.rarity) + id; }); Rebuild(); }, 22);
            UIKit.Place((RectTransform)sort.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 6f), new Vector2(180f, 44f));

            BuildItemDetails(ps);
        }

        void BuildItemDetails(PlayerState ps)
        {
            RectTransform det = Section(content, "Details", 790f, -60f, 580f, 780f);
            string id = null;
            bool equipped = false;
            if (selectedEquip != EquipSlot.None) { id = ps.equipment.Get(selectedEquip); equipped = true; }
            else if (selectedItem >= 0 && selectedItem < ps.inventory.SlotCount) { ItemStack st = ps.inventory.GetSlot(selectedItem); if (!st.IsEmpty) id = st.itemId; }
            ItemDef d = ContentDB.Item(id);
            if (d == null)
            {
                Text hint = UIKit.Txt(det, "Hint", Loc.T("ui.select_item"), 26, UIKit.Muted, TextAnchor.MiddleCenter);
                UIKit.Stretch(hint.rectTransform, 20f, 20f, 20f, 20f);
                return;
            }
            Image icon = UIKit.Img(det, "Icon", Color.white, ItemIcons.Get(d));
            UIKit.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(110f, 110f));
            Text name = UIKit.Txt(det, "Name", Loc.T(d.nameKey), 34, d.RarityColor, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -24f), new Vector2(400f, 80f));
            Text type = UIKit.Txt(det, "Type", Loc.T("itemtype." + d.type.ToString().ToLowerInvariant()) + "  ·  " + Loc.T("rarity." + (int)d.rarity), 22, UIKit.Muted, TextAnchor.UpperLeft);
            UIKit.Place(type.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -98f), new Vector2(400f, 34f));

            var sb = new StringBuilder();
            sb.AppendLine(Loc.T(d.descKey));
            sb.AppendLine();
            if (d.type == ItemType.Weapon) sb.AppendLine("<color=#e8c566>" + Loc.T("family." + d.family.ToString().ToLowerInvariant()) + "</color>");
            for (int i = 0; i < d.mods.Length; i++) sb.AppendLine(FormatMod(d.mods[i]));
            if (d.restoreHp > 0f) sb.AppendLine(Loc.T("ui.restore_hp", Mathf.RoundToInt(d.restoreHp)));
            if (d.restoreQi > 0f) sb.AppendLine(Loc.T("ui.restore_qi", Mathf.RoundToInt(d.restoreQi)));
            if (d.restoreStamina > 0f) sb.AppendLine(Loc.T("ui.restore_stamina", Mathf.RoundToInt(d.restoreStamina)));
            if (!string.IsNullOrEmpty(d.teachSkill)) sb.AppendLine("<color=#7ee8c4>" + Loc.T("ui.teaches", Loc.T("skill." + d.teachSkill + ".name")) + "</color>");
            if (d.reqRealm > 0) sb.AppendLine((ps.Realm >= d.reqRealm ? "<color=#a8d8a0>" : "<color=#e86a60>") + Loc.T("ui.requires_realm", Loc.T("realm." + d.reqRealm + ".name")) + "</color>");
            if (d.value > 0) sb.AppendLine("\n" + Loc.T("ui.value", d.value));
            Text body = UIKit.Txt(det, "Body", sb.ToString(), 24, UIKit.Paper, TextAnchor.UpperLeft);
            UIKit.Anchor(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 100f), new Vector2(-24f, -150f));

            float x = 24f;
            if (equipped)
            {
                Button un = UIKit.Btn(det, "Unequip", Loc.T("ui.unequip"), () => { Game.Manager.UnequipSlot(selectedEquip); Rebuild(); }, 24);
                UIKit.Place((RectTransform)un.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 24f), new Vector2(230f, 56f));
            }
            else
            {
                if (d.IsEquipment)
                {
                    bool can = ps.CanEquip(d, out _);
                    Button eqb = UIKit.Btn(det, "Equip", Loc.T("ui.equip"), () => { Game.Manager.EquipItem(id); Rebuild(); }, 24);
                    eqb.interactable = can;
                    UIKit.Place((RectTransform)eqb.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 24f), new Vector2(230f, 56f));
                    x += 246f;
                }
                if (d.IsUsable)
                {
                    Button use = UIKit.Btn(det, "Use", Loc.T(d.type == ItemType.Manual ? "ui.learn" : "ui.use"), () => { Game.Manager.UseItem(id); Rebuild(); }, 24);
                    UIKit.Place((RectTransform)use.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 24f), new Vector2(230f, 56f));
                    x += 246f;
                }
                if (d.type != ItemType.Quest)
                {
                    Button drop = UIKit.Btn(det, "Drop", Loc.T("ui.drop"), () => { Game.Manager.DropItem(id, 1); Rebuild(); }, 24);
                    UIKit.Place((RectTransform)drop.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 24f), new Vector2(150f, 56f));
                }
            }
        }

        static string FormatMod(StatMod m)
        {
            string name = Loc.T("stat." + m.stat.ToString().ToLowerInvariant());
            var sb = new StringBuilder();
            if (Mathf.Abs(m.flat) > 0.0001f)
            {
                bool pctStat = m.stat == StatId.CritChance || m.stat == StatId.CritDamage || m.stat == StatId.CooldownReduction || m.stat == StatId.ExpGain || m.stat == StatId.LootLuck;
                sb.Append(pctStat ? "+" + Mathf.RoundToInt(m.flat * 100f) + "% " + name : (m.flat > 0 ? "+" : "") + Math.Round(m.flat, 1) + " " + name);
            }
            if (Mathf.Abs(m.pct) > 0.0001f)
            {
                if (sb.Length > 0) sb.Append("  ");
                sb.Append("+" + Mathf.RoundToInt(m.pct * 100f) + "% " + name);
            }
            return "<color=#9fe0c0>" + sb + "</color>";
        }

        Button SlotButton(Transform parent, string name, ItemDef def, int count, bool showCount, bool selected)
        {
            Image bg = UIKit.Panel(parent, name, selected ? new Color(0.26f, 0.2f, 0.1f, 0.98f) : new Color(0.07f, 0.09f, 0.12f, 0.95f), true);
            bg.raycastTarget = true;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.3f, 1.25f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            if (def != null)
            {
                Outline o = bg.GetComponent<Outline>();
                o.effectColor = new Color(def.RarityColor.r, def.RarityColor.g, def.RarityColor.b, 0.9f);
                Image icon = UIKit.Img(bg.transform, "Icon", Color.white, ItemIcons.Get(def));
                UIKit.Stretch(icon.rectTransform, 8f, 12f, 8f, 8f);
                icon.preserveAspect = true;
                if (showCount && count > 1)
                {
                    Text c = UIKit.Txt(bg.transform, "Count", count.ToString(), 20, Color.white, TextAnchor.LowerRight, FontStyle.Bold);
                    UIKit.Stretch(c.rectTransform, 0f, 2f, 6f, 0f);
                }
            }
            bg.gameObject.AddComponent<HoverSound>();
            return b;
        }

        // ------------------------------------------------------------------ CHARACTER
        void BuildCharacter()
        {
            PlayerState ps = Game.Session.player;
            Vitals v = Game.PlayerVitals;
            Title(content, Loc.T("ui.character"), 10f, -4f);
            RectTransform left = Section(content, "Stats", 10f, -60f, 640f, 800f);
            var sb = new StringBuilder();
            sb.AppendLine("<color=#e8c566>" + Loc.T("realm." + ps.Realm + ".name") + " · " + Loc.T("ui.stage", ps.cultivation.Stage) + "</color>");
            sb.AppendLine(Loc.T("ui.path") + ": <color=#7ee8c4>" + Loc.T("path." + ps.BuildName) + "</color>");
            sb.AppendLine();
            sb.AppendLine(Row(StatId.MaxHp, v.MaxHp)); sb.AppendLine(Row(StatId.MaxStamina, v.MaxStamina)); sb.AppendLine(Row(StatId.MaxQi, v.MaxQi));
            sb.AppendLine(Row(StatId.Attack, v.Attack)); sb.AppendLine(Row(StatId.SpellPower, v.SpellPower)); sb.AppendLine(Row(StatId.Defense, v.Defense));
            sb.AppendLine(Row(StatId.Poise, v.MaxPoise));
            sb.AppendLine(RowPct(StatId.CritChance)); sb.AppendLine(RowPct(StatId.CritDamage, 1.5f));
            sb.AppendLine(Row(StatId.MoveSpeed, v.stats.Get(StatId.MoveSpeed) * 100f, "%"));
            sb.AppendLine(Row(StatId.StaminaRegen, v.stats.Get(StatId.StaminaRegen), "/s")); sb.AppendLine(Row(StatId.QiRegen, v.stats.Get(StatId.QiRegen), "/s"));
            sb.AppendLine(RowPct(StatId.CooldownReduction)); sb.AppendLine(RowPct(StatId.ExpGain));
            sb.AppendLine();
            sb.AppendLine(Loc.T("ui.playtime") + ": " + TimeSpan.FromSeconds(Game.Session.playSeconds).ToString(@"hh\:mm\:ss") + "     " + Loc.T("ui.deaths") + ": " + Game.Session.deaths);
            Text t = UIKit.Txt(left, "Text", sb.ToString(), 27, UIKit.Paper, TextAnchor.UpperLeft);
            UIKit.Stretch(t.rectTransform, 28f, 20f, 28f, 20f);
            t.lineSpacing = 1.15f;

            RectTransform right = Section(content, "Attr", 670f, -60f, 700f, 800f);
            Text at = UIKit.Txt(right, "Title", Loc.T("ui.attributes") + (ps.attributes.unspent > 0 ? "   <color=#e8c566>" + Loc.T("ui.points", ps.attributes.unspent) + "</color>" : ""), 30, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(at.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -20f), new Vector2(640f, 44f));
            for (int i = 0; i < (int)AttributeId.Count; i++)
            {
                AttributeId id = (AttributeId)i;
                float y = -90f - i * 118f;
                Text n = UIKit.Txt(right, "N" + i, Loc.T("attr." + id.ToString().ToLowerInvariant()) + "  <color=#e8c566>" + ps.attributes.Get(id) + "</color>", 30, UIKit.Paper, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(520f, 40f));
                Text d = UIKit.Txt(right, "D" + i, Loc.T("attr." + id.ToString().ToLowerInvariant() + ".desc"), 21, UIKit.Muted, TextAnchor.UpperLeft);
                UIKit.Place(d.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, y - 40f), new Vector2(560f, 60f));
                if (ps.attributes.unspent > 0)
                {
                    Button b = UIKit.Btn(right, "Plus" + i, "+", () => { Game.Manager.SpendAttribute(id); Rebuild(); }, 34);
                    UIKit.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, y), new Vector2(64f, 64f));
                }
            }
        }

        static string Row(StatId id, float value, string suffix = "")
        {
            return Loc.T("stat." + id.ToString().ToLowerInvariant()) + ":  <color=#ffffff>" + Math.Round(value, 1) + suffix + "</color>";
        }

        static string RowPct(StatId id, float add = 0f)
        {
            float v = Game.PlayerVitals.stats.Get(id) + add;
            return Loc.T("stat." + id.ToString().ToLowerInvariant()) + ":  <color=#ffffff>" + Mathf.RoundToInt(v * 100f) + "%</color>";
        }

        // ------------------------------------------------------------------ CULTIVATION
        void BuildCultivation()
        {
            PlayerState ps = Game.Session.player;
            Title(content, Loc.T("ui.cultivation"), 10f, -4f);
            RectTransform ladder = Section(content, "Ladder", 10f, -60f, 520f, 800f);
            for (int r = 0; r < ContentDB.RealmCount; r++)
            {
                bool done = r < ps.Realm, cur = r == ps.Realm;
                Color c = cur ? UIKit.Gold : (done ? UIKit.Jade : UIKit.Muted);
                Text n = UIKit.Txt(ladder, "R" + r, (done ? "✓  " : (cur ? "◆  " : "○  ")) + Loc.T("realm." + r + ".name"), cur ? 32 : 28, c, TextAnchor.UpperLeft, cur ? FontStyle.Bold : FontStyle.Normal);
                UIKit.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -24f - r * 124f), new Vector2(470f, 40f));
                Text d = UIKit.Txt(ladder, "RD" + r, Loc.T("realm." + r + ".desc"), 20, cur ? UIKit.Paper : UIKit.Muted, TextAnchor.UpperLeft);
                UIKit.Place(d.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -64f - r * 124f), new Vector2(440f, 64f));
            }

            RectTransform right = Section(content, "Detail", 550f, -60f, 820f, 800f);
            RealmDef realm = ContentDB.Realm(ps.Realm);
            Text head = UIKit.Txt(right, "Head", Loc.T("realm." + ps.Realm + ".name") + "  ·  " + Loc.T("ui.stage", ps.cultivation.Stage) + " / " + realm.stages, 34, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(760f, 46f));
            Image track;
            Image fill = UIKit.Bar(right, "Exp", UIKit.ExpColor, out track);
            UIKit.Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -76f), new Vector2(760f, 26f));
            fill.fillAmount = ps.cultivation.Progress01;
            Text expText = UIKit.Txt(track.transform, "T", Mathf.FloorToInt(ps.cultivation.Exp) + " / " + Mathf.FloorToInt(ps.cultivation.ExpRequired), 18, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            UIKit.Stretch(expText.rectTransform);

            var sb = new StringBuilder();
            sb.AppendLine("<color=#e8c566>" + Loc.T("ui.how_to_cultivate") + "</color>");
            sb.AppendLine(Loc.T("ui.cultivate_text"));
            sb.AppendLine();
            if (realm.unlocks != null && realm.unlocks.Length > 0)
            {
                sb.AppendLine("<color=#7ee8c4>" + Loc.T("ui.unlocks") + "</color>");
                for (int i = 0; i < realm.unlocks.Length; i++) sb.AppendLine(" • " + Loc.T("unlock." + realm.unlocks[i]));
                sb.AppendLine();
            }
            if (!ps.cultivation.AtLastRealm)
            {
                sb.AppendLine("<color=#e8c566>" + Loc.T("ui.breakthrough_req", Loc.T("realm." + (ps.Realm + 1) + ".name")) + "</color>");
                sb.AppendLine((ps.cultivation.ReadyForBreakthrough ? "<color=#a8d8a0>✓ " : "<color=#e86a60>✗ ") + Loc.T("ui.req_exp") + "</color>");
                for (int i = 0; i < realm.breakthroughItems.Length; i++)
                {
                    ItemCost c = realm.breakthroughItems[i];
                    int have = ps.inventory.Count(c.itemId);
                    sb.AppendLine((have >= c.count ? "<color=#a8d8a0>✓ " : "<color=#e86a60>✗ ") + Loc.T("item." + c.itemId + ".name") + "  " + have + "/" + c.count + "</color>");
                }
                if (!string.IsNullOrEmpty(realm.breakthroughFlag))
                {
                    bool has = Game.Session.flags.Has(realm.breakthroughFlag);
                    sb.AppendLine((has ? "<color=#a8d8a0>✓ " : "<color=#e86a60>✗ ") + Loc.T("req." + realm.breakthroughFlag) + "</color>");
                }
                if (realm.requiresMeditationSpot)
                {
                    bool at = Game.World != null && Game.PlayerObject != null && Game.World.IsMeditationSpot(Game.PlayerObject.transform.position);
                    sb.AppendLine((at ? "<color=#a8d8a0>✓ " : "<color=#e86a60>✗ ") + Loc.T("ui.req_spot") + "</color>");
                }
                if (realm.breakthroughQiPercent > 0f) sb.AppendLine("   " + Loc.T("ui.req_qi", Mathf.RoundToInt(realm.breakthroughQiPercent * 100f)));
                sb.AppendLine("   " + Loc.T("ui.success_chance", Mathf.RoundToInt(Game.Manager.BreakthroughChance() * 100f)));
                if (!string.IsNullOrEmpty(realm.breakthroughHintKey)) sb.AppendLine("\n<i>" + Loc.T(realm.breakthroughHintKey) + "</i>");
            }
            else sb.AppendLine(Loc.T("ui.max_realm"));
            Text body = UIKit.Txt(right, "Body", sb.ToString(), 24, UIKit.Paper, TextAnchor.UpperLeft);
            UIKit.Anchor(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 110f), new Vector2(-28f, -120f));

            if (!ps.cultivation.AtLastRealm)
            {
                Button b = UIKit.Btn(right, "Breakthrough", Loc.T("ui.breakthrough"), () => { Game.Manager.TryBreakthrough(); }, 30, new Color(0.22f, 0.17f, 0.07f, 0.98f));
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(420f, 70f));
                b.interactable = Game.Manager.CanBreakthrough(out _);
            }
        }

        // ------------------------------------------------------------------ SKILLS
        void BuildSkills()
        {
            PlayerState ps = Game.Session.player;
            Title(content, Loc.T("ui.skills"), 10f, -4f);
            RectTransform list = Section(content, "List", 10f, -60f, 560f, 800f);
            ScrollRect sr = UIKit.Scroll(list, "Scroll", out RectTransform c, 8f, 12f);
            UIKit.Stretch(((RectTransform)sr.transform), 4f, 4f, 4f, 4f);
            if (ps.learnedSkills.Count == 0)
            {
                Text empty = UIKit.Txt(c, "Empty", Loc.T("ui.no_skills"), 24, UIKit.Muted, TextAnchor.UpperLeft);
                UIKit.Fixed(empty.gameObject, 120f);
            }
            for (int i = 0; i < ps.learnedSkills.Count; i++)
            {
                SkillDef s = ContentDB.Skill(ps.learnedSkills[i]);
                if (s == null) continue;
                string id = s.id;
                Button b = UIKit.Btn(c, "Skill" + i, "", () => { selectedSkill = id; Rebuild(); }, 24);
                UIKit.Fixed(b.gameObject, 84f);
                b.GetComponent<Image>().color = selectedSkill == id ? new Color(0.26f, 0.2f, 0.1f, 0.98f) : new Color(0.09f, 0.11f, 0.14f, 0.92f);
                Text lab = b.GetComponentInChildren<Text>();
                lab.alignment = TextAnchor.MiddleLeft;
                lab.text = Loc.T(s.nameKey) + "\n<size=19><color=#8fb4ff>" + Loc.T("ui.qi_cost") + " " + Mathf.RoundToInt(ps.QiCost(s)) + "</color>   <color=#c8c8c0>" + Loc.T("ui.cooldown") + " " + Math.Round(ps.Cooldown(s), 1) + "s</color>" + (ps.SkillUsable(s) ? "" : "   <color=#e86a60>" + Loc.T("realm." + s.reqRealm + ".name") + "</color>") + "</size>";
                UIKit.Stretch(lab.rectTransform, 112f, 4f, 10f, 4f);
                Image icon = UIKit.Img(b.transform, "Icon", Color.white, ItemIcons.Get(s.icon, s.color));
                UIKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(72f, 72f));
            }

            RectTransform det = Section(content, "Detail", 590f, -60f, 780f, 480f);
            SkillDef sel = ContentDB.Skill(selectedSkill);
            if (sel == null)
            {
                Text hint = UIKit.Txt(det, "Hint", Loc.T("ui.select_skill"), 26, UIKit.Muted, TextAnchor.MiddleCenter);
                UIKit.Stretch(hint.rectTransform, 20f, 20f, 20f, 20f);
            }
            else
            {
                Image icon = UIKit.Img(det, "Icon", Color.white, ItemIcons.Get(sel.icon, sel.color));
                UIKit.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -26f), new Vector2(110f, 110f));
                Text n = UIKit.Txt(det, "Name", Loc.T(sel.nameKey), 36, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Place(n.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -26f), new Vector2(580f, 48f));
                Text meta = UIKit.Txt(det, "Meta", Loc.T("ui.qi_cost") + " " + Mathf.RoundToInt(ps.QiCost(sel)) + "   ·   " + Loc.T("ui.cooldown") + " " + Math.Round(ps.Cooldown(sel), 1) + "s   ·   " + Loc.T("element." + sel.element.ToString().ToLowerInvariant()), 22, UIKit.Muted, TextAnchor.UpperLeft);
                UIKit.Place(meta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -80f), new Vector2(580f, 34f));
                Text d = UIKit.Txt(det, "Desc", Loc.T(sel.descKey), 25, UIKit.Paper, TextAnchor.UpperLeft);
                UIKit.Anchor(d.rectTransform, Vector2.zero, Vector2.one, new Vector2(26f, 100f), new Vector2(-26f, -150f));
                Text assign = UIKit.Txt(det, "Assign", Loc.T("ui.assign_slot"), 22, UIKit.Muted, TextAnchor.LowerLeft);
                UIKit.Place(assign.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 84f), new Vector2(400f, 30f));
                for (int i = 0; i < 4; i++)
                {
                    int slot = i;
                    Button b = UIKit.Btn(det, "Slot" + i, (i + 1).ToString(), () => { ps.SetSkillSlot(slot, sel.id); Rebuild(); }, 30);
                    UIKit.Place((RectTransform)b.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f + i * 96f, 20f), new Vector2(84f, 56f));
                    if (ps.skillSlots[i] == sel.id) b.GetComponent<Image>().color = new Color(0.26f, 0.2f, 0.1f, 0.98f);
                }
            }
            RectTransform slots = Section(content, "Slots", 590f, -560f, 780f, 300f);
            Text st = UIKit.Txt(slots, "T", Loc.T("ui.equipped_skills"), 28, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(st.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -18f), new Vector2(700f, 40f));
            for (int i = 0; i < 4; i++)
            {
                SkillDef s = ContentDB.Skill(ps.skillSlots[i]);
                Image frame = UIKit.Panel(slots, "F" + i, new Color(0.07f, 0.09f, 0.12f, 0.95f));
                UIKit.Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f + i * 184f, -80f), new Vector2(170f, 180f));
                Text k = UIKit.Txt(frame.transform, "K", (i + 1).ToString(), 26, UIKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Stretch(k.rectTransform, 10f, 0f, 0f, 6f);
                if (s != null)
                {
                    Image ic = UIKit.Img(frame.transform, "I", Color.white, ItemIcons.Get(s.icon, s.color));
                    UIKit.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(84f, 84f));
                    Text nm = UIKit.Txt(frame.transform, "N", Loc.T(s.nameKey), 20, UIKit.Paper, TextAnchor.LowerCenter);
                    UIKit.Anchor(nm.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(6f, 6f), new Vector2(-6f, 0f));
                }
            }
        }

        // ------------------------------------------------------------------ QUESTS
        void BuildQuests()
        {
            QuestLog q = Game.Quests;
            Title(content, Loc.T("ui.quests"), 10f, -4f);
            RectTransform list = Section(content, "List", 10f, -60f, 560f, 800f);
            ScrollRect sr = UIKit.Scroll(list, "Scroll", out RectTransform c, 8f, 12f);
            UIKit.Stretch(((RectTransform)sr.transform), 4f, 4f, 4f, 4f);
            var rows = new List<QuestProgress>();
            foreach (QuestProgress p in q.Active) rows.Add(p);
            rows.Sort((a, b) => b.def.main.CompareTo(a.def.main));
            int activeCount = rows.Count;
            foreach (QuestProgress p in q.Finished) rows.Add(p);
            if (rows.Count == 0)
            {
                Text empty = UIKit.Txt(c, "Empty", Loc.T("ui.no_quests"), 24, UIKit.Muted, TextAnchor.UpperLeft);
                UIKit.Fixed(empty.gameObject, 100f);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                QuestProgress p = rows[i];
                string id = p.def.id;
                string prefix = p.status == "done" ? "<color=#8fd8a0>✓ " : (p.status == "failed" ? "<color=#e86a60>✗ " : (p.def.main ? "<color=#e8c566>★ " : "<color=#ffffff>• "));
                Button b = UIKit.Btn(c, "Q" + i, "", () => { selectedQuest = id; Rebuild(); }, 24);
                UIKit.Fixed(b.gameObject, 62f);
                Text lab = b.GetComponentInChildren<Text>();
                lab.alignment = TextAnchor.MiddleLeft;
                lab.text = prefix + Loc.T(p.def.titleKey) + "</color>" + (q.TrackedId == id ? "   <color=#7ee8c4>◉</color>" : "");
                UIKit.Stretch(lab.rectTransform, 18f, 2f, 10f, 2f);
                b.GetComponent<Image>().color = selectedQuest == id ? new Color(0.26f, 0.2f, 0.1f, 0.98f) : new Color(0.09f, 0.11f, 0.14f, 0.92f);
            }

            RectTransform det = Section(content, "Detail", 590f, -60f, 780f, 800f);
            QuestProgress sel = string.IsNullOrEmpty(selectedQuest) ? null : q.Progress(selectedQuest);
            if (sel == null)
            {
                Text hint = UIKit.Txt(det, "Hint", Loc.T("ui.select_quest"), 26, UIKit.Muted, TextAnchor.MiddleCenter);
                UIKit.Stretch(hint.rectTransform, 20f, 20f, 20f, 20f);
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("<color=#e8c566><size=38>" + Loc.T(sel.def.titleKey) + "</size></color>");
            sb.AppendLine("<color=#9a9a94>" + Loc.T(sel.def.main ? "ui.main_quest" : "ui.side_quest") + "  ·  " + Loc.T("ui.act", sel.def.act) + "</color>");
            sb.AppendLine();
            sb.AppendLine(Loc.T(sel.def.descKey));
            sb.AppendLine();
            for (int i = 0; i < sel.def.objectives.Count; i++)
            {
                ObjectiveDef o = sel.def.objectives[i];
                string line = Loc.T(o.textKey);
                if (o.count > 1 && o.type != ObjectiveType.Flag && o.type != ObjectiveType.Realm) line += "  (" + Mathf.Min(sel.counts[i], o.count) + "/" + o.count + ")";
                sb.AppendLine((sel.complete[i] ? "<color=#8fd8a0>✓ " : "○ ") + line + (sel.complete[i] ? "</color>" : ""));
            }
            if (sel.def.rewardExp > 0f || sel.def.rewardItems.Count > 0 || sel.def.rewardStones > 0)
            {
                sb.AppendLine();
                sb.Append("<color=#e8c566>" + Loc.T("ui.rewards") + ":</color> ");
                if (sel.def.rewardExp > 0f) sb.Append(Mathf.RoundToInt(sel.def.rewardExp) + " " + Loc.T("ui.exp") + "   ");
                if (sel.def.rewardStones > 0) sb.Append(sel.def.rewardStones + " " + Loc.T("item.spirit_stone.name") + "   ");
                for (int i = 0; i < sel.def.rewardItems.Count; i++) sb.Append(Loc.T("item." + sel.def.rewardItems[i].itemId + ".name") + " ×" + sel.def.rewardItems[i].count + "   ");
            }
            Text body = UIKit.Txt(det, "Body", sb.ToString(), 25, UIKit.Paper, TextAnchor.UpperLeft);
            UIKit.Stretch(body.rectTransform, 28f, 100f, 28f, 24f);
            body.lineSpacing = 1.1f;
            if (sel.status == "active")
            {
                Button track = UIKit.Btn(det, "Track", Loc.T(q.TrackedId == sel.def.id ? "ui.tracked" : "ui.track"), () => { q.TrackedId = sel.def.id; Rebuild(); }, 26);
                UIKit.Place((RectTransform)track.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(340f, 60f));
            }
        }

        // ------------------------------------------------------------------ MAP
        readonly List<RectTransform> mapMarkers = new List<RectTransform>();
        RectTransform playerMarker, mapRect;
        Text mapInfo;
        Texture2D fullFog;
        RawImage fogImage;

        void BuildMap()
        {
            WorldManager w = Game.World;
            Title(content, Loc.T("ui.map"), 10f, -4f);
            mapMarkers.Clear();
            RectTransform frame = Section(content, "MapFrame", 10f, -60f, 860f, 860f);
            mapRect = UIKit.Rect(frame, "Map");
            UIKit.Place(mapRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 820f));
            var raw = mapRect.gameObject.AddComponent<RawImage>();
            raw.texture = w != null ? w.MapTexture : null;
            RectTransform fogRt = UIKit.Rect(mapRect, "Fog");
            UIKit.Stretch(fogRt);
            fogImage = fogRt.gameObject.AddComponent<RawImage>();
            if (fullFog == null) fullFog = new Texture2D(WorldManager.FogRes, WorldManager.FogRes, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[WorldManager.FogRes * WorldManager.FogRes];
            for (int i = 0; i < px.Length; i++) px[i] = w != null && w.Fog[i] != 0 ? new Color32(0, 0, 0, 0) : new Color32(10, 12, 16, 235);
            fullFog.SetPixels32(px);
            fullFog.Apply(false);
            fogImage.texture = fullFog;
            fogImage.raycastTarget = false;
            raw.raycastTarget = false;

            if (w != null && w.Pois != null)
            {
                for (int i = 0; i < w.Pois.instances.Count; i++)
                {
                    PoiInstance inst = w.Pois.instances[i];
                    bool known = Game.Session.discoveredPois.Contains(inst.def.id);
                    if (inst.def.hidden && !known) continue;
                    if (!known && !IsRevealed(w, inst.center)) continue;
                    RectTransform m = UIKit.Rect(mapRect, "P_" + inst.def.id);
                    UIKit.Place(m, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), WorldToMap(inst.center), new Vector2(26f, 26f));
                    Image img = m.gameObject.AddComponent<Image>();
                    img.sprite = UIKit.Circle;
                    img.color = known ? UIKit.Gold : UIKit.Muted;
                    var btn = m.gameObject.AddComponent<Button>();
                    btn.targetGraphic = img;
                    string pid = inst.def.id;
                    btn.onClick.AddListener(() => { mapPoi = pid; UpdateMapInfo(); });
                    mapMarkers.Add(m);
                }
            }
            playerMarker = UIKit.Rect(mapRect, "Player");
            UIKit.Place(playerMarker, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            var pimg = playerMarker.gameObject.AddComponent<Image>();
            pimg.sprite = UIKit.Circle;
            pimg.color = UIKit.Jade;
            mapInfo = UIKit.Txt(content, "Info", "", 26, UIKit.Paper, TextAnchor.UpperLeft);
            UIKit.Place(mapInfo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(900f, -80f), new Vector2(460f, 400f));
            UpdateMapInfo();
            UpdateMapMarkers();
        }

        static bool IsRevealed(WorldManager w, Vector3 p)
        {
            float cell = WorldLayout.Size / WorldManager.FogRes;
            int x = Mathf.Clamp((int)(p.x / cell), 0, WorldManager.FogRes - 1), z = Mathf.Clamp((int)(p.z / cell), 0, WorldManager.FogRes - 1);
            return w.Fog[z * WorldManager.FogRes + x] != 0;
        }

        Vector2 WorldToMap(Vector3 p)
        {
            return new Vector2(p.x / WorldLayout.Size * 820f, p.z / WorldLayout.Size * 820f);
        }

        void UpdateMapInfo()
        {
            if (mapInfo == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("<color=#e8c566>" + Loc.T("ui.legend") + "</color>");
            sb.AppendLine("<color=#7ee8c4>●</color> " + Loc.T("ui.you"));
            sb.AppendLine("<color=#d8ae52>●</color> " + Loc.T("ui.discovered_place"));
            sb.AppendLine("<color=#9e9e98>●</color> " + Loc.T("ui.rumored_place"));
            if (!string.IsNullOrEmpty(mapPoi))
            {
                sb.AppendLine();
                sb.AppendLine("<size=32><color=#e8c566>" + Loc.T("poi." + mapPoi) + "</color></size>");
                string desc = "poi." + mapPoi + ".desc";
                if (Loc.Has(desc)) sb.AppendLine(Loc.T(desc));
            }
            QuestProgress tr = Game.Quests != null ? Game.Quests.Tracked : null;
            if (tr != null)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#e8c566>" + Loc.T("ui.tracked_quest") + ":</color> " + Loc.T(tr.def.titleKey));
            }
            mapInfo.text = sb.ToString();
        }

        void UpdateMapMarkers()
        {
            if (playerMarker == null || Game.PlayerObject == null) return;
            playerMarker.anchoredPosition = WorldToMap(Game.PlayerObject.transform.position);
            playerMarker.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f));
        }

        // ------------------------------------------------------------------ SETTINGS
        void BuildSettings()
        {
            GameSettings s = Game.Settings;
            Title(content, Loc.T("ui.settings"), 10f, -4f);
            RectTransform panel = Section(content, "Panel", 10f, -60f, 1370f, 800f);
            float y = -26f;
            void Slider(string key, float min, float max, float value, Action<float> set, string fmt = "0.00")
            {
                Text l = UIKit.Txt(panel, "L", Loc.T(key), 26, UIKit.Paper, TextAnchor.MiddleLeft);
                UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, y), new Vector2(420f, 40f));
                Text val = UIKit.Txt(panel, "V", value.ToString(fmt), 24, UIKit.Gold, TextAnchor.MiddleRight);
                UIKit.Place(val.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(920f, y), new Vector2(120f, 40f));
                UnityEngine.UI.Slider sl = UIKit.MakeSlider(panel, "S", min, max, value, v => { set(v); val.text = v.ToString(fmt); s.Apply(); s.Save(); });
                UIKit.Place((RectTransform)sl.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470f, y - 4f), new Vector2(430f, 32f));
                y -= 54f;
            }
            void Toggle(string key, bool value, Action<bool> set)
            {
                Text l = UIKit.Txt(panel, "L", Loc.T(key), 26, UIKit.Paper, TextAnchor.MiddleLeft);
                UIKit.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, y), new Vector2(420f, 40f));
                bool state = value;
                Button b = null;
                b = UIKit.Btn(panel, "T", Loc.T(state ? "ui.on" : "ui.off"), () => { state = !state; set(state); UIKit.SetLabel(b, Loc.T(state ? "ui.on" : "ui.off")); s.Apply(); s.Save(); }, 24);
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470f, y), new Vector2(160f, 42f));
                y -= 54f;
            }
            Slider("opt.master", 0f, 1f, s.masterVolume, v => s.masterVolume = v);
            Slider("opt.music", 0f, 1f, s.musicVolume, v => s.musicVolume = v);
            Slider("opt.sfx", 0f, 1f, s.sfxVolume, v => s.sfxVolume = v);
            Slider("opt.mouse", 0.2f, 3f, s.mouseSensitivity, v => s.mouseSensitivity = v);
            Slider("opt.gamepad", 0.2f, 3f, s.gamepadSensitivity, v => s.gamepadSensitivity = v);
            Slider("opt.fov", 50f, 90f, s.fov, v => s.fov = v, "0");
            Slider("opt.shake", 0f, 1f, s.cameraShake, v => s.cameraShake = v);
            Slider("opt.subscale", 0.7f, 1.6f, s.subtitleScale, v => s.subtitleScale = v);
            Toggle("opt.invert", s.invertY, v => s.invertY = v);
            Toggle("opt.subtitles", s.subtitles, v => s.subtitles = v);
            Toggle("opt.fullscreen", s.fullscreen, v => s.fullscreen = v);
            Toggle("opt.vsync", s.vsync, v => s.vsync = v);

            // quality and language cyclers
            Text ql = UIKit.Txt(panel, "QL", Loc.T("opt.quality"), 26, UIKit.Paper, TextAnchor.MiddleLeft);
            UIKit.Place(ql.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, y), new Vector2(420f, 40f));
            Button qb = null;
            qb = UIKit.Btn(panel, "Q", Loc.T("quality." + (int)s.quality), () => { s.quality = (QualityLevel)(((int)s.quality + 1) % 3); UIKit.SetLabel(qb, Loc.T("quality." + (int)s.quality)); s.Apply(); s.Save(); }, 24);
            UIKit.Place((RectTransform)qb.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470f, y), new Vector2(240f, 42f));
            y -= 54f;
            Text ll = UIKit.Txt(panel, "LL", Loc.T("opt.language"), 26, UIKit.Paper, TextAnchor.MiddleLeft);
            UIKit.Place(ll.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, y), new Vector2(420f, 40f));
            Button lb = null;
            lb = UIKit.Btn(panel, "Lang", Loc.T("lang." + s.language), () =>
            {
                int idx = Array.IndexOf(Loc.Languages, s.language);
                s.language = Loc.Languages[(idx + 1) % Loc.Languages.Length];
                Loc.Load(s.language);
                s.Save();
                Rebuild();
            }, 24);
            UIKit.Place((RectTransform)lb.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(470f, y), new Vector2(240f, 42f));
        }

        // ------------------------------------------------------------------ SAVE / LOAD
        void BuildSave()
        {
            Title(content, Loc.T("ui.saveload"), 10f, -4f);
            RectTransform panel = Section(content, "Panel", 10f, -60f, 1370f, 800f);
            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                int slot = i;
                SaveMeta meta = SaveSystem.ReadMeta(slot);
                float y = -24f - i * 150f;
                Image row = UIKit.Panel(panel, "Slot" + i, new Color(0.07f, 0.09f, 0.12f, 0.95f));
                UIKit.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, y), new Vector2(1320f, 130f));
                string title = slot == 0 ? Loc.T("ui.autosave") : Loc.T("ui.slot", slot);
                string info = meta == null ? "<color=#8a8a84>" + Loc.T("ui.empty_slot") + "</color>"
                    : "<color=#e8c566>" + Loc.T("realm." + meta.realm + ".name") + " · " + Loc.T("ui.stage", meta.stage) + "</color>   " + Loc.T("poi." + meta.location) + "\n<color=#a0a09a>" + meta.when + "   ·   " + TimeSpan.FromSeconds(meta.playSeconds).ToString(@"hh\:mm\:ss") + "</color>";
                Text t = UIKit.Txt(row.transform, "T", "<size=30><b>" + title + "</b></size>\n" + info, 25, UIKit.Paper, TextAnchor.MiddleLeft);
                UIKit.Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 6f), new Vector2(-700f, -6f));
                if (slot != 0)
                {
                    Button save = UIKit.Btn(row.transform, "Save", Loc.T("ui.save"), () =>
                    {
                        if (meta != null) Confirm("ui.confirm_overwrite", () => { Game.Manager.SaveToSlot(slot); Rebuild(); });
                        else { Game.Manager.SaveToSlot(slot); Rebuild(); }
                    }, 26);
                    UIKit.Place((RectTransform)save.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-480f, 0f), new Vector2(210f, 62f));
                }
                Button load = UIKit.Btn(row.transform, "Load", Loc.T("ui.load"), () => Confirm("ui.confirm_load", () => Game.Manager.LoadFromSlot(slot)), 26);
                UIKit.Place((RectTransform)load.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(210f, 62f));
                load.interactable = meta != null;
                if (slot != 0)
                {
                    Button del = UIKit.Btn(row.transform, "Del", Loc.T("ui.delete"), () => Confirm("ui.confirm_delete", () => { SaveSystem.Delete(slot); Rebuild(); }), 26);
                    UIKit.Place((RectTransform)del.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(200f, 62f));
                    del.interactable = meta != null;
                }
            }
        }
    }
}
