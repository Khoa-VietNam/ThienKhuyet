using ThienKhuyet.Characters;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Enemies
{
    /// <summary>Builds a complete enemy (rig, animator, collider, vitals, AI) from an <see cref="EnemyDef"/>.</summary>
    public static class EnemyFactory
    {
        public static EnemyBrain Create(EnemyDef def, Vector3 pos, string spawnId, bool minion = false)
        {
            if (def == null) return null;
            var go = new GameObject(def.id);
            go.layer = GameLayers.Enemy;
            float ground = Game.World != null ? Game.World.GroundHeightAt(pos) : pos.y;
            go.transform.position = new Vector3(pos.x, Mathf.Max(ground, pos.y - 2f) + 0.05f, pos.z);
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            var cc = go.AddComponent<CharacterController>();
            bool beast = def.rig == EnemyRigKind.Wolf || def.rig == EnemyRigKind.Boar;
            float s = Mathf.Max(0.4f, def.scale);
            if (beast)
            {
                cc.radius = 0.38f * s; cc.height = Mathf.Max(0.9f * s, cc.radius * 2f + 0.05f); cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            }
            else
            {
                cc.radius = 0.34f * s; cc.height = 1.75f * s; cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            }
            cc.stepOffset = 0.35f * s;
            cc.slopeLimit = 50f;
            cc.minMoveDistance = 0f;
            cc.skinWidth = 0.04f;

            var vitals = go.AddComponent<Vitals>();
            vitals.team = Team.Enemy;
            vitals.realm = def.realm;
            vitals.stats.SetBase(StatId.MaxHp, def.maxHp);
            vitals.stats.SetBase(StatId.Attack, def.attack);
            vitals.stats.SetBase(StatId.Defense, def.defense);
            vitals.stats.SetBase(StatId.Poise, def.poise);
            vitals.stats.SetBase(StatId.MaxStamina, 100f);
            vitals.stunResist = def.stunResist;
            vitals.SetFull();
            var aim = new GameObject("AimPoint").transform;
            aim.SetParent(go.transform, false);
            aim.localPosition = new Vector3(0f, cc.height * 0.62f, 0f);
            vitals.aimPoint = aim;

            ICharacterAnimation anim;
            if (beast)
            {
                QuadrupedRig rig = QuadrupedBuilder.Build(def.appearance, def.rig == EnemyRigKind.Boar ? BeastStyle.Boar : BeastStyle.Wolf, go.transform, "Model", def.glow);
                rig.model.localScale = Vector3.one * s;
                var qa = go.AddComponent<QuadrupedAnimator>();
                qa.rig = rig;
                anim = qa;
            }
            else
            {
                CharacterAppearance app = def.appearance.Clone();
                app.height = Mathf.Max(0.5f, app.height * s);
                HumanoidRig rig = HumanoidBuilder.Build(app, go.transform, "Model");
                if (def.hasWeapon) AttachWeapon(rig, def);
                var ha = go.AddComponent<HumanoidAnimator>();
                ha.rig = rig;
                ha.stanceKind = HumanoidAnimator.StanceFor(def.weapon, def.hasWeapon);
                ha.combatStance = false;
                anim = ha;
            }

            var brain = go.AddComponent<EnemyBrain>();
            brain.vitals = vitals;
            brain.cc = cc;
            brain.isMinion = minion;
            brain.Init(def, anim, spawnId);
            return brain;
        }

        static void AttachWeapon(HumanoidRig rig, EnemyDef def)
        {
            switch (def.weapon)
            {
                case WeaponFamily.Staff:
                    WeaponBuilder.Attach(rig, WeaponFamily.Staff, new Color(0.22f, 0.15f, 0.14f), new Color(0.85f, 0.2f, 0.3f));
                    break;
                case WeaponFamily.Spear:
                    WeaponBuilder.Attach(rig, WeaponFamily.Spear, new Color(0.75f, 0.78f, 0.82f), new Color(0.75f, 0.15f, 0.12f));
                    break;
                case WeaponFamily.Blade:
                    WeaponBuilder.Attach(rig, WeaponFamily.Blade, new Color(0.7f, 0.72f, 0.76f), new Color(0.6f, 0.5f, 0.3f));
                    break;
                case WeaponFamily.Fist:
                    break;
                default:
                    WeaponBuilder.Attach(rig, WeaponFamily.Sword, new Color(0.62f, 0.64f, 0.68f), new Color(0.55f, 0.4f, 0.25f));
                    break;
            }
            if (def.attacks.Length > 0 && def.attacks[0].vfx == "arrow") WeaponBuilder.AttachBow(rig, new Color(0.4f, 0.27f, 0.15f));
        }
    }
}
