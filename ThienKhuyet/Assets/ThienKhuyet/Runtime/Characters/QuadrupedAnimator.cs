using System;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Characters
{
    /// <summary>Trot gait, idle behaviours and keyframed action clips (bite, pounce, howl, hit, death) for the beast rig.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class QuadrupedAnimator : MonoBehaviour, ICharacterAnimation
    {
        const int N = 21;
        public QuadrupedRig rig;
        public bool grounded = true;
        public float speedMultiplier = 1f;
        public bool alert;

        readonly Vector3[] loco = new Vector3[N];
        readonly Vector3[] clipE = new Vector3[N];
        readonly Quaternion[] rot = new Quaternion[N];
        Vector3 lastPos, smoothLocalVel;
        float phase, time, alertW;

        PoseClip action;
        string actionId;
        float actionTime, actionSpeed = 1f, actionW, fadeIn = 0.08f, fadeOut = 0.14f;
        bool ending;

        Color flashColor;
        float flashTime, flashDur;
        MaterialPropertyBlock block;

        public Transform AnimRoot => rig != null ? rig.transform : transform;
        public float ActionTime => actionTime;
        public bool ActionActive => action != null && !ending;
        public string CurrentActionId => actionId;
        public Vector3 LocalVelocity => smoothLocalVel;

        void Awake()
        {
            if (rig == null) rig = GetComponentInChildren<QuadrupedRig>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            lastPos = transform.position;
        }

        public void PlayAction(string clip, float speed = 1f, float fade = -1f)
        {
            PoseClip c = PoseLibrary.Quadruped(clip);
            if (c == null) { Debug.LogWarning("[QuadrupedAnimator] unknown clip '" + clip + "'"); return; }
            action = c;
            actionId = clip;
            actionTime = 0f;
            actionSpeed = Mathf.Max(0.05f, speed);
            fadeIn = fade >= 0f ? fade : c.blendIn;
            fadeOut = c.blendOut;
            ending = false;
            if (fadeIn <= 0.0001f) actionW = 1f;
        }

        public void StopAction(float fade = -1f)
        {
            if (action == null) return;
            ending = true;
            fadeOut = fade >= 0f ? fade : action.blendOut;
        }

        public bool IsPlayingAction(string clip) { return action != null && !ending && actionId == clip; }

        public float ActionLength(string clip)
        {
            PoseClip c = PoseLibrary.Quadruped(clip);
            return c != null ? c.length : 0f;
        }

        public void SetExpression(string expression) { }
        public void SetLookTarget(Transform target, float weight) { }
        public void SetSpeaking(bool speaking) { }

        public void Flash(Color color, float duration)
        {
            flashColor = color;
            flashDur = Mathf.Max(0.02f, duration);
            flashTime = flashDur;
        }

        public void ResetMotion()
        {
            lastPos = transform.position;
            smoothLocalVel = Vector3.zero;
        }

        void LateUpdate()
        {
            if (rig == null || rig.joints[0] == null) return;
            float dt = Time.deltaTime;
            time += dt;
            Transform root = rig.model.parent != null ? rig.model.parent : rig.model;
            if (dt > 0f)
            {
                Vector3 v = (root.position - lastPos) / dt;
                lastPos = root.position;
                v.y = 0f;
                if (v.magnitude > 40f) v = Vector3.zero;
                smoothLocalVel = Vector3.Lerp(smoothLocalVel, root.InverseTransformDirection(v), 1f - Mathf.Exp(-14f * dt));
            }
            float speed = new Vector2(smoothLocalVel.x, smoothLocalVel.z).magnitude;
            if (speed > 0.05f)
            {
                float stride = Mathf.Lerp(1.0f, 2.2f, Mathf.Clamp01((speed - 1.5f) / 5f));
                phase = Mathf.Repeat(phase + speed / stride * Mathf.PI * 2f * dt * speedMultiplier, Mathf.PI * 2f);
            }
            alertW = Mathx.Damp(alertW, alert ? 1f : 0f, 5f, dt);
            Vector3 body = Gait(speed);
            for (int j = 0; j < N; j++) rot[j] = ToQuat(j, loco[j]);

            Vector3 hips = body;
            if (action != null)
            {
                actionTime += dt * actionSpeed;
                if (!ending)
                {
                    actionW = Mathf.MoveTowards(actionW, 1f, (fadeIn > 0.0001f ? 1f / fadeIn : 1000f) * dt);
                    if (!action.loop && actionTime >= action.length && !action.hold) ending = true;
                }
                if (ending)
                {
                    actionW = Mathf.MoveTowards(actionW, 0f, (fadeOut > 0.0001f ? 1f / fadeOut : 1000f) * dt);
                    if (actionW <= 0.0001f) { action = null; actionId = null; ending = false; actionW = 0f; }
                }
                if (action != null && actionW > 0.0001f)
                {
                    for (int j = 0; j < N; j++) clipE[j] = loco[j];
                    Vector3 ch = action.Sample(actionTime, clipE);
                    float w = Mathx.SmoothStep01(actionW);
                    for (int j = 0; j < N; j++)
                        if (action.mask[j]) rot[j] = Q.Nlerp(rot[j], ToQuat(j, clipE[j]), w);
                    hips = Vector3.Lerp(body, ch, w);
                }
            }
            for (int j = 0; j < N; j++) rig.joints[j].localRotation = rot[j];
            rig.joints[Rigs.QBody].localPosition = QuadrupedDims.LocalOffset[Rigs.QBody] + hips;

            if (flashTime > 0f && rig.body != null)
            {
                flashTime -= dt;
                float k = Mathf.Clamp01(flashTime / flashDur);
                rig.body.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", flashColor * (k * k));
                rig.body.SetPropertyBlock(block);
                if (flashTime <= 0f) rig.body.SetPropertyBlock(null);
            }
        }

        Quaternion ToQuat(int j, Vector3 e)
        {
            float m = Rigs.Quadruped.mirror[j];
            return Q.EulerZXY(e.x, e.y * m, e.z * m);
        }

        Vector3 Gait(float speed)
        {
            float amp = Mathf.Clamp01(speed / 1.0f);
            float run = Mathf.Clamp01((speed - 1.8f) / 4f);
            float s = (float)Math.Sin(phase), c = (float)Math.Cos(phase);
            float idle = 1f - amp;
            float breath = (float)Math.Sin(time * 2.1f);
            float A = Mathf.Lerp(22f, 44f, run) * amp;
            float K = Mathf.Lerp(36f, 70f, run) * amp;

            for (int j = 0; j < N; j++) loco[j] = Vector3.zero;
            // trot: front-right with hind-left
            loco[Rigs.QFlR] = new Vector3(-s * A, 0f, 0f);
            loco[Rigs.QFlL] = new Vector3(s * A, 0f, 0f);
            loco[Rigs.QHlR] = new Vector3(s * A * 0.9f, 0f, 0f);
            loco[Rigs.QHlL] = new Vector3(-s * A * 0.9f, 0f, 0f);
            loco[Rigs.QFlRl] = new Vector3(Mathf.Max(0f, c) * K + 4f, 0f, 0f);
            loco[Rigs.QFlLl] = new Vector3(Mathf.Max(0f, -c) * K + 4f, 0f, 0f);
            loco[Rigs.QHlRl] = new Vector3(-Mathf.Max(0f, -c) * K * 0.8f - 6f, 0f, 0f);
            loco[Rigs.QHlLl] = new Vector3(-Mathf.Max(0f, c) * K * 0.8f - 6f, 0f, 0f);
            loco[Rigs.QFlRp] = new Vector3(-Mathf.Max(0f, c) * K * 0.5f, 0f, 0f);
            loco[Rigs.QFlLp] = new Vector3(-Mathf.Max(0f, -c) * K * 0.5f, 0f, 0f);
            loco[Rigs.QHlRp] = new Vector3(Mathf.Max(0f, -c) * K * 0.6f + 8f, 0f, 0f);
            loco[Rigs.QHlLp] = new Vector3(Mathf.Max(0f, c) * K * 0.6f + 8f, 0f, 0f);

            loco[Rigs.QSpine] = new Vector3(0f, s * 5f * amp, 0f);
            loco[Rigs.QChest] = new Vector3(-(float)Math.Sin(phase * 2f) * 2.5f * amp + breath * 0.9f * idle, -s * 4f * amp, 0f);
            loco[Rigs.QNeck] = new Vector3(-6f * alertW + (float)Math.Sin(phase * 2f) * 3f * amp - 4f * run, 0f, 0f);
            loco[Rigs.QHead] = new Vector3(6f * alertW - 6f * run, (float)Math.Sin(time * 0.7f) * 14f * idle * (1f - alertW), 0f);
            loco[Rigs.QTail1] = new Vector3(8f * run - 12f * idle, (float)Math.Sin(time * 1.6f + 1f) * 14f * idle + (float)Math.Cos(phase) * 16f * amp, 0f);
            loco[Rigs.QTail2] = new Vector3(0f, (float)Math.Sin(time * 1.6f) * 10f * idle + (float)Math.Cos(phase + 0.6f) * 14f * amp, 0f);
            loco[Rigs.QTail3] = new Vector3(0f, (float)Math.Sin(time * 1.6f - 1f) * 12f * idle + (float)Math.Cos(phase + 1.2f) * 14f * amp, 0f);
            loco[Rigs.QJaw] = new Vector3(Mathf.Max(0f, run - 0.4f) * 8f, 0f, 0f);
            return new Vector3(0f, (float)Math.Cos(phase * 2f) * (0.012f + 0.02f * run) * amp - 0.01f * run, 0f);
        }
    }
}
