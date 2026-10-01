using System;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Characters
{
    /// <summary>Animation facade used by combat, AI and cinematics, so the procedural backend can be swapped for an Animator later.</summary>
    public interface ICharacterAnimation
    {
        Transform AnimRoot { get; }
        void PlayAction(string clip, float speed = 1f, float fadeIn = -1f);
        void StopAction(float fadeOut = -1f);
        bool IsPlayingAction(string clip);
        float ActionLength(string clip);
        void SetExpression(string expression);
        void SetLookTarget(Transform target, float weight);
        void SetSpeaking(bool speaking);
    }

    /// <summary>Pure locomotion pose generator (walk/run/idle) - no engine calls, unit-testable.</summary>
    public static class HumanoidGait
    {
        public const float WalkRef = 2.2f, RunRef = 6.5f;

        public static float StrideLength(float speed)
        {
            float run01 = Mathf.Clamp01((speed - 2f) / 5f);
            return Mathf.Lerp(1.5f, 3.0f, run01);
        }

        /// <summary>
        /// Writes joint euler angles (authored right-side semantic) for a gait pose.
        /// fwd/lat: unit direction of travel in character space (z forward, x right). armed: 0..1 weapon-ready stance.
        /// </summary>
        public static Vector3 Evaluate(float phase, float speed, float fwd, float lat, float time, float armed, Vector3[] e)
        {
            float amp = Mathf.Clamp01(speed / 1.2f);                 // 0 when idle, 1 when moving
            float run01 = Mathf.Clamp01((speed - 2.2f) / 4.5f);
            float s = (float)Math.Sin(phase), c = (float)Math.Cos(phase);
            float sL = -s, cL = -c;

            // idle breathing / weight shift (fades out when moving)
            float idle = 1f - amp;
            float breath = (float)Math.Sin(time * 1.7f);
            float sway = (float)Math.Sin(time * 0.6f);

            float thighA = Mathf.Lerp(24f, 52f, run01) * amp;
            float kneeA = Mathf.Lerp(34f, 78f, run01) * amp;
            float armA = Mathf.Lerp(20f, 56f, run01) * amp * (1f - 0.75f * armed);
            float lean = (3f + 11f * run01) * amp;

            // legs: x<0 forward. Strafing uses the z (abduction) component.
            e[Rigs.ThR] = new Vector3(-s * thighA * fwd - 2f * idle, 0f, 2f + s * thighA * 0.55f * lat + 1.5f * sway * idle);
            e[Rigs.ThL] = new Vector3(-sL * thighA * fwd - 2f * idle, 0f, 2f + sL * thighA * 0.55f * lat - 1.5f * sway * idle);
            float kr = Mathf.Max(0f, c) * kneeA + 4f + 2f * idle;
            float kl = Mathf.Max(0f, cL) * kneeA + 4f + 2f * idle;
            e[Rigs.KnR] = new Vector3(kr, 0f, 0f);
            e[Rigs.KnL] = new Vector3(kl, 0f, 0f);
            e[Rigs.AnR] = new Vector3(Mathf.Max(0f, -c) * 14f * amp - kr * 0.25f, 0f, 0f);
            e[Rigs.AnL] = new Vector3(Mathf.Max(0f, -cL) * 14f * amp - kl * 0.25f, 0f, 0f);

            // arms swing opposite to the legs
            float elbow = 14f + 28f * run01 * amp;
            e[Rigs.ArR] = new Vector3(s * armA * fwd + 5f + breath * 0.8f * idle, 0f, 8f + 2f * idle * sway);
            e[Rigs.ArL] = new Vector3(sL * armA * fwd + 5f + breath * 0.8f * idle, 0f, 8f - 2f * idle * sway);
            e[Rigs.FoR] = new Vector3(-elbow - Mathf.Max(0f, -s) * 14f * amp, 0f, 0f);
            e[Rigs.FoL] = new Vector3(-elbow - Mathf.Max(0f, -sL) * 14f * amp, 0f, 0f);
            e[Rigs.HaR] = Vector3.zero;
            e[Rigs.HaL] = Vector3.zero;

            // torso counter-rotation and lean
            e[Rigs.Hp] = new Vector3(lean * 0.4f * fwd, s * 7f * amp * fwd, -sway * 1.4f * idle + s * 2f * amp * lat);
            e[Rigs.Sp] = new Vector3(lean * 0.3f * fwd + breath * 0.5f * idle, -s * 4f * amp * fwd, 0f);
            e[Rigs.Ch] = new Vector3(lean * 0.4f * fwd + breath * 1.1f * idle, -s * 5f * amp * fwd, 0f);
            e[Rigs.Nk] = new Vector3(-lean * 0.3f * fwd, 0f, 0f);
            e[Rigs.Hd] = new Vector3(-lean * 0.3f * fwd - breath * 0.4f * idle, 0f, 0f);

            // hips bob: highest at mid-stance, lower when running
            float bob = (float)Math.Cos(phase * 2f) * (0.012f + 0.03f * run01) * amp;
            return new Vector3(0f, bob - 0.018f * amp - 0.012f * run01 * amp - 0.003f * breath * idle, 0.02f * lean / 14f);
        }
    }

    /// <summary>
    /// Drives a <see cref="HumanoidRig"/>: procedural locomotion, weapon-ready stance, keyframed action clips (attacks, hits,
    /// cutscene acting), facial expressions, look-at, spring bones and hit flashes. Everything is blended in quaternion space.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HumanoidAnimator : MonoBehaviour, ICharacterAnimation
    {
        public HumanoidRig rig;

        // external inputs
        public bool grounded = true;
        public bool combatStance;
        public CultivationStanceKind stanceKind = CultivationStanceKind.Sword;
        public float speedMultiplier = 1f;

        readonly Vector3[] locoEuler = new Vector3[17];
        readonly Vector3[] clipEuler = new Vector3[17];
        readonly Vector3[] stanceEuler = new Vector3[17];
        readonly Vector3[] airEuler = new Vector3[17];
        readonly Quaternion[] rot = new Quaternion[17];

        Vector3 lastRootPos;
        Vector3 smoothLocalVel;
        float phase;
        float time;
        float armedW, airW;

        // action layer
        PoseClip action;
        string actionId;
        float actionTime, actionSpeed = 1f, actionW, actionFadeIn = 0.08f, actionFadeOut = 0.14f;
        bool actionEnding;

        // face / look
        string expression = "neutral";
        float blinkTimer = 2f, blinkT;
        bool speaking;
        Transform lookTarget;
        float lookWeight, lookYaw, lookPitch;
        float browTargetL, browTargetR, browLift, mouthW = 1f, mouthH = 1f, eyeOpen = 1f;
        float browAngL, browAngR, browY, mouthSx = 1f, mouthSy = 1f, eyeSy = 1f;

        // hit flash
        Color flashColor;
        float flashTime, flashDuration;
        MaterialPropertyBlock block;
        TrailRenderer trail;

        public Transform AnimRoot => rig != null ? rig.transform : transform;
        public string CurrentActionId => actionId;
        public bool ActionActive => action != null && !actionEnding;
        public float ActionNormalized => action == null ? 1f : Mathf.Clamp01(actionTime / Mathf.Max(0.01f, action.length));
        public float ActionTime => actionTime;
        public Vector3 LocalVelocity => smoothLocalVel;

        public enum CultivationStanceKind { None = 0, Sword = 1, Fist = 2, Spear = 3, Staff = 4 }

        public static CultivationStanceKind StanceFor(WeaponFamily f, bool armed)
        {
            if (!armed) return CultivationStanceKind.None;
            switch (f)
            {
                case WeaponFamily.Fist: return CultivationStanceKind.Fist;
                case WeaponFamily.Spear: return CultivationStanceKind.Spear;
                case WeaponFamily.Staff: return CultivationStanceKind.Staff;
                default: return CultivationStanceKind.Sword;
            }
        }

        void Awake()
        {
            if (rig == null) rig = GetComponentInChildren<HumanoidRig>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            lastRootPos = transform.position;
            if (rig != null && rig.weaponTip != null && trail == null) trail = rig.weaponTip.GetComponent<TrailRenderer>();
        }

        public void SetTrail(bool on)
        {
            if (rig != null && rig.weaponTip != null && trail == null) trail = rig.weaponTip.GetComponent<TrailRenderer>();
            if (trail == null) return;
            if (on && !trail.emitting) trail.Clear();
            trail.emitting = on;
        }

        // ------------------------------------------------------------------ ICharacterAnimation
        public void PlayAction(string clip, float speed = 1f, float fadeIn = -1f)
        {
            PoseClip c = PoseLibrary.Humanoid(clip);
            if (c == null)
            {
                Debug.LogWarning("[HumanoidAnimator] unknown clip '" + clip + "'");
                return;
            }
            action = c;
            actionId = clip;
            actionTime = 0f;
            actionSpeed = Mathf.Max(0.05f, speed);
            actionFadeIn = fadeIn >= 0f ? fadeIn : c.blendIn;
            actionFadeOut = c.blendOut;
            actionEnding = false;
            if (actionFadeIn <= 0.0001f) actionW = 1f;
        }

        public void StopAction(float fadeOut = -1f)
        {
            if (action == null) return;
            actionEnding = true;
            actionFadeOut = fadeOut >= 0f ? fadeOut : action.blendOut;
        }

        public bool IsPlayingAction(string clip)
        {
            return action != null && !actionEnding && actionId == clip;
        }

        public float ActionLength(string clip)
        {
            PoseClip c = PoseLibrary.Humanoid(clip);
            return c != null ? c.length : 0f;
        }

        public void SetExpression(string e)
        {
            expression = string.IsNullOrEmpty(e) ? "neutral" : e;
            switch (expression)
            {
                case "smile": browTargetL = browTargetR = 0f; browLift = 0.002f; mouthW = 1.6f; mouthH = 2.0f; eyeOpen = 0.8f; break;
                case "angry": browTargetL = -24f; browTargetR = 24f; browLift = -0.004f; mouthW = 0.9f; mouthH = 1.0f; eyeOpen = 0.8f; break;
                case "sad": browTargetL = 20f; browTargetR = -20f; browLift = 0.0f; mouthW = 0.7f; mouthH = 0.8f; eyeOpen = 0.75f; break;
                case "surprised": browTargetL = browTargetR = 0f; browLift = 0.014f; mouthW = 0.8f; mouthH = 3.2f; eyeOpen = 1.35f; break;
                case "pain": browTargetL = -14f; browTargetR = 14f; browLift = -0.003f; mouthW = 1.3f; mouthH = 2.2f; eyeOpen = 0.4f; break;
                case "closed": browTargetL = browTargetR = 0f; browLift = 0f; mouthW = 1f; mouthH = 1f; eyeOpen = 0.08f; break;
                case "serious": browTargetL = -10f; browTargetR = 10f; browLift = -0.002f; mouthW = 0.9f; mouthH = 0.8f; eyeOpen = 0.9f; break;
                default: browTargetL = browTargetR = 0f; browLift = 0f; mouthW = 1f; mouthH = 1f; eyeOpen = 1f; break;
            }
        }

        public void SetLookTarget(Transform target, float weight)
        {
            lookTarget = target;
            lookWeight = target == null ? 0f : Mathf.Clamp01(weight);
        }

        public void SetSpeaking(bool value)
        {
            speaking = value;
        }

        public void Flash(Color color, float duration)
        {
            flashColor = color;
            flashDuration = Mathf.Max(0.02f, duration);
            flashTime = flashDuration;
        }

        /// <summary>Instantly snaps locomotion state (after teleports / cutscene cuts).</summary>
        public void ResetMotion()
        {
            lastRootPos = transform.position;
            smoothLocalVel = Vector3.zero;
            for (int i = 0; i < rig.springs.Count; i++) { rig.springs[i].angle = Vector2.zero; rig.springs[i].velocity = Vector2.zero; }
        }

        // ------------------------------------------------------------------ update
        void LateUpdate()
        {
            if (rig == null || rig.joints[0] == null) return;
            float dt = Time.deltaTime;
            time += dt;

            Transform root = rig.model.parent != null ? rig.model.parent : rig.model;
            UpdateVelocity(root, dt);

            float speed = new Vector2(smoothLocalVel.x, smoothLocalVel.z).magnitude;
            float fwd = speed > 0.05f ? smoothLocalVel.z / speed : 1f;
            float lat = speed > 0.05f ? smoothLocalVel.x / speed : 0f;
            if (speed > 0.05f)
            {
                float stride = HumanoidGait.StrideLength(speed);
                phase = Mathf.Repeat(phase + (speed / stride) * Mathf.PI * 2f * dt * speedMultiplier, Mathf.PI * 2f);
            }

            armedW = Mathx.Damp(armedW, combatStance ? 1f : 0f, 6f, dt);
            airW = Mathx.Damp(airW, grounded ? 0f : 1f, grounded ? 14f : 9f, dt);

            Vector3 hips = HumanoidGait.Evaluate(phase, speed, fwd, lat, time, armedW, locoEuler);
            // stance overlay
            if (armedW > 0.001f && stanceKind != CultivationStanceKind.None)
            {
                PoseClip st = PoseLibrary.Humanoid("stance_" + stanceKind.ToString().ToLowerInvariant());
                if (st != null)
                {
                    st.Sample(0f, stanceEuler);
                    for (int j = 0; j < 17; j++)
                        if (st.mask[j]) locoEuler[j] = Vector3.Lerp(locoEuler[j], stanceEuler[j], armedW);
                    hips += st.hips[0] * armedW;
                }
            }
            // airborne overlay
            if (airW > 0.001f)
            {
                PoseClip fall = PoseLibrary.Humanoid("fall");
                if (fall != null)
                {
                    fall.Sample(0f, airEuler);
                    for (int j = 0; j < 17; j++)
                        if (fall.mask[j]) locoEuler[j] = Vector3.Lerp(locoEuler[j], airEuler[j], airW);
                }
            }

            UpdateLook(dt);
            locoEuler[Rigs.Nk].y += lookYaw * 0.4f;
            locoEuler[Rigs.Hd].y += lookYaw * 0.6f;
            locoEuler[Rigs.Hd].x += lookPitch;

            // locomotion -> quaternions
            for (int j = 0; j < 17; j++) rot[j] = ToQuat(j, locoEuler[j]);

            // action layer
            Vector3 hipsFinal = hips;
            if (action != null)
            {
                actionTime += dt * actionSpeed;
                if (!actionEnding)
                {
                    float inRate = actionFadeIn > 0.0001f ? 1f / actionFadeIn : 1000f;
                    actionW = Mathf.MoveTowards(actionW, 1f, inRate * dt);
                    bool finished = !action.loop && actionTime >= action.length;
                    if (finished && !action.hold) actionEnding = true;
                }
                if (actionEnding)
                {
                    float outRate = actionFadeOut > 0.0001f ? 1f / actionFadeOut : 1000f;
                    actionW = Mathf.MoveTowards(actionW, 0f, outRate * dt);
                    if (actionW <= 0.0001f)
                    {
                        action = null;
                        actionId = null;
                        actionEnding = false;
                        actionW = 0f;
                    }
                }
                if (action != null && actionW > 0.0001f)
                {
                    for (int j = 0; j < 17; j++) clipEuler[j] = locoEuler[j];
                    Vector3 clipHips = action.Sample(actionTime, clipEuler);
                    float w = Mathx.SmoothStep01(actionW);
                    for (int j = 0; j < 17; j++)
                    {
                        if (!action.mask[j]) continue;
                        rot[j] = Q.Nlerp(rot[j], ToQuat(j, clipEuler[j]), w);
                    }
                    hipsFinal = Vector3.Lerp(hips, clipHips, w);
                }
            }

            for (int j = 0; j < 17; j++) rig.joints[j].localRotation = rot[j];
            rig.joints[Rigs.Hp].localPosition = HumanoidDims.LocalOffset[Rigs.Hp] + hipsFinal;

            UpdateFace(dt);
            UpdateSprings(dt, root);
            UpdateFlash(dt);
        }

        Quaternion ToQuat(int joint, Vector3 e)
        {
            float m = Rigs.Humanoid.mirror[joint];
            return Q.EulerZXY(e.x, e.y * m, e.z * m);
        }

        void UpdateVelocity(Transform root, float dt)
        {
            if (dt <= 0f) return;
            Vector3 d = root.position - lastRootPos;
            lastRootPos = root.position;
            Vector3 v = d / dt;
            v.y = 0f;
            if (v.magnitude > 40f) v = Vector3.zero; // teleport / cutscene cut
            Vector3 local = root.InverseTransformDirection(v);
            smoothLocalVel = Vector3.Lerp(smoothLocalVel, local, 1f - Mathf.Exp(-14f * dt));
        }

        void UpdateLook(float dt)
        {
            float tYaw = 0f, tPitch = 0f;
            float w = lookTarget != null ? lookWeight : 0f;
            if (lookTarget != null)
            {
                Vector3 toT = lookTarget.position - rig.HeadWorldPosition;
                Vector3 local = rig.model.InverseTransformDirection(toT);
                float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                if (Mathf.Abs(yaw) > 100f) w = 0f; // do not twist the neck backwards
                tYaw = Mathf.Clamp(yaw, -65f, 65f) * w;
                tPitch = Mathf.Clamp(pitch, -28f, 28f) * w;
            }
            lookYaw = Mathx.Damp(lookYaw, tYaw, 8f, dt);
            lookPitch = Mathx.Damp(lookPitch, tPitch, 8f, dt);
        }

        void UpdateFace(float dt)
        {
            if (rig.browL == null) return;
            blinkTimer -= dt;
            if (blinkTimer <= 0f)
            {
                blinkT = 0.14f;
                blinkTimer = UnityEngine.Random.Range(2.5f, 5.5f);
            }
            float blink = 1f;
            if (blinkT > 0f)
            {
                blinkT -= dt;
                blink = Mathf.Abs(blinkT / 0.14f - 0.5f) * 2f;   // closes and reopens
            }
            browAngL = Mathx.Damp(browAngL, browTargetL, 12f, dt);
            browAngR = Mathx.Damp(browAngR, browTargetR, 12f, dt);
            browY = Mathx.Damp(browY, browLift, 12f, dt);
            float talk = speaking ? (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(time * 11f)) * (0.6f + 0.4f * Mathf.Sin(time * 3.1f))) : 1f;
            mouthSx = Mathx.Damp(mouthSx, mouthW, 12f, dt);
            mouthSy = Mathx.Damp(mouthSy, speaking ? mouthH * (1.2f + 2.2f * (talk - 0.55f)) : mouthH, 16f, dt);
            eyeSy = Mathx.Damp(eyeSy, eyeOpen, 14f, dt);

            rig.browL.localRotation = Quaternion.Euler(0f, 0f, browAngL);
            rig.browR.localRotation = Quaternion.Euler(0f, 0f, browAngR);
            Vector3 bl = rig.browL.localPosition, br = rig.browR.localPosition;
            float baseY = 1.672f - HumanoidDims.BindPositions[Rigs.Hd].y;
            rig.browL.localPosition = new Vector3(bl.x, baseY + browY, bl.z);
            rig.browR.localPosition = new Vector3(br.x, baseY + browY, br.z);
            float eyes = eyeSy * blink;
            rig.eyeL.localScale = new Vector3(1f, Mathf.Max(0.08f, eyes), 1f);
            rig.eyeR.localScale = new Vector3(1f, Mathf.Max(0.08f, eyes), 1f);
            rig.mouth.localScale = new Vector3(mouthSx, mouthSy, 1f);
        }

        void UpdateSprings(float dt, Transform root)
        {
            if (rig.springs.Count == 0) return;
            float step = Mathf.Min(dt, 0.033f);
            Vector3 lv = smoothLocalVel;
            for (int i = 0; i < rig.springs.Count; i++)
            {
                SpringBone s = rig.springs[i];
                if (s.t == null) continue;
                float targetPitch = Mathf.Clamp(lv.z * 4.2f, -s.maxAngle, s.maxAngle) * s.influence + (float)Math.Sin(time * 1.3f + i) * 1.5f * s.influence;
                float targetRoll = Mathf.Clamp(-lv.x * 4.2f, -s.maxAngle, s.maxAngle) * s.influence;
                Vector2 target = new Vector2(targetPitch, targetRoll);
                Vector2 acc = (target - s.angle) * s.stiffness - s.velocity * s.damping;
                s.velocity += acc * step;
                s.angle += s.velocity * step;
                s.angle.x = Mathf.Clamp(s.angle.x, -s.maxAngle * 1.3f, s.maxAngle * 1.3f);
                s.angle.y = Mathf.Clamp(s.angle.y, -s.maxAngle * 1.3f, s.maxAngle * 1.3f);
                s.t.localRotation = s.rest * Quaternion.Euler(s.angle.x, 0f, s.angle.y);
            }
        }

        void UpdateFlash(float dt)
        {
            if (rig.body == null) return;
            if (flashTime > 0f)
            {
                flashTime -= dt;
                float k = Mathf.Clamp01(flashTime / flashDuration);
                rig.body.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", flashColor * (k * k));
                rig.body.SetPropertyBlock(block);
                if (flashTime <= 0f) rig.body.SetPropertyBlock(null);
            }
        }
    }
}
