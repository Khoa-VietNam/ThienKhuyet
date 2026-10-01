using ThienKhuyet.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThienKhuyet.Player
{
    /// <summary>
    /// Third-person camera: damped orbit with shoulder offset, obstacle avoidance, lock-on framing, FOV kicks and restrained shake.
    /// Cinematics take over through <see cref="BeginCinematic"/> / <see cref="SetCinematicPose"/> and hand back with a smooth blend.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public Transform target;
        public float distance = 4.6f;
        public float pivotHeight = 1.55f;
        public float shoulder = 0.45f;
        public float minPitch = -35f, maxPitch = 68f;

        float yaw, pitch = 14f;
        Vector3 pivot;
        float curDist = 4.6f;
        Transform lockTarget;
        float lockBlend;

        bool cinematic;
        Vector3 cinPos;
        Quaternion cinRot = Quaternion.identity;
        float cinFov = 60f;
        float blendBackT = 1f, blendBackDur = 1f;
        Vector3 blendFromPos;
        Quaternion blendFromRot;
        float blendFromFov;

        float shakeAmp, shakeT, shakeDur;
        float fovPunch, fovCurrent = 62f;
        float sprintKick;
        float freezeInput;

        public bool IsCinematic => cinematic;
        public float Yaw => yaw;
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        public Vector3 PlanarRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        public static CameraRig Create(Camera camera)
        {
            var rig = camera.gameObject.GetComponent<CameraRig>();
            if (rig == null) rig = camera.gameObject.AddComponent<CameraRig>();
            rig.cam = camera;
            camera.tag = GameTags.MainCamera;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 1400f;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            return rig;
        }

        public void Follow(Transform t, bool snap = true)
        {
            target = t;
            if (snap && t != null)
            {
                yaw = t.eulerAngles.y;
                pitch = 14f;
                pivot = t.position + Vector3.up * pivotHeight;
                curDist = distance;
            }
        }

        public void SnapBehind()
        {
            if (target == null) return;
            yaw = target.eulerAngles.y;
            pitch = 14f;
            pivot = target.position + Vector3.up * pivotHeight;
        }

        public void SetYaw(float y) { yaw = y; }

        public void SetLockTarget(Transform t)
        {
            lockTarget = t;
        }

        public void Shake(float amplitude, float duration)
        {
            float scale = Game.Settings != null ? Game.Settings.cameraShake : 0.6f;
            amplitude *= scale;
            if (amplitude <= 0.001f) return;
            float remaining = shakeDur > 0f ? shakeAmp * Mathf.Clamp01(1f - shakeT / shakeDur) : 0f;
            if (amplitude >= remaining)
            {
                shakeAmp = amplitude;
                shakeDur = Mathf.Max(0.05f, duration);
                shakeT = 0f;
            }
        }

        public void PunchFov(float degrees)
        {
            fovPunch = Mathf.Max(fovPunch, degrees);
        }

        public void SetSprintKick(float k)
        {
            sprintKick = k;
        }

        // ------------------------------------------------------------------ cinematic hand-over
        public void BeginCinematic()
        {
            cinematic = true;
            cinPos = transform.position;
            cinRot = transform.rotation;
            cinFov = cam != null ? cam.fieldOfView : 60f;
            blendBackT = 1f;
        }

        public void SetCinematicPose(Vector3 pos, Quaternion rot, float fov)
        {
            cinPos = pos;
            cinRot = rot;
            cinFov = fov;
        }

        public void EndCinematic(float blendSeconds = 0.9f)
        {
            if (!cinematic) return;
            cinematic = false;
            blendFromPos = cinPos;
            blendFromRot = cinRot;
            blendFromFov = cinFov;
            blendBackDur = Mathf.Max(0.01f, blendSeconds);
            blendBackT = 0f;
            fovCurrent = cinFov;
            if (target != null)
            {
                // continue from the cinematic's heading so the hand-back is not a swing
                Vector3 e = cinRot.eulerAngles;
                yaw = target.eulerAngles.y;
                pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), minPitch, maxPitch);
                pivot = target.position + Vector3.up * pivotHeight;
            }
        }

        // ------------------------------------------------------------------ update
        void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            if (cinematic)
            {
                Vector3 shake = ComputeShake(dt, out Quaternion shakeRot);
                transform.SetPositionAndRotation(cinPos + shake, cinRot * shakeRot);
                cam.fieldOfView = cinFov;
                return;
            }

            if (target == null) return;
            GameSettings s = Game.Settings;
            bool inputOpen = Game.Input != null && !Game.Input.IsLocked && Game.Mode == GameMode.Playing;

            if (inputOpen)
            {
                Vector2 look = Game.Input.Look;
                float sens = s != null ? (Game.Input.LookIsGamepad ? s.gamepadSensitivity : s.mouseSensitivity) : 1f;
                float invert = s != null && s.invertY ? -1f : 1f;
                if (Game.Input.LookIsGamepad)
                {
                    yaw += look.x * 150f * sens * dt;
                    pitch -= look.y * 110f * sens * dt * invert;
                }
                else
                {
                    yaw += look.x * 0.09f * sens;
                    pitch -= look.y * 0.09f * sens * invert;
                }
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            Vector3 targetPivot = target.position + Vector3.up * pivotHeight;
            float desiredDist = distance;
            if (lockTarget != null)
            {
                Vector3 to = lockTarget.position - target.position;
                float flat = new Vector2(to.x, to.z).magnitude;
                if (flat > 0.5f)
                {
                    float desiredYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    yaw = Mathx.DampAngle(yaw, desiredYaw, 6f, dt);
                    float desiredPitch = Mathf.Clamp(18f - flat * 0.9f + (to.y * -2f), 6f, 30f);
                    pitch = Mathx.Damp(pitch, desiredPitch, 4f, dt);
                    targetPivot += new Vector3(to.x, 0f, to.z) * 0.14f;
                    desiredDist = distance + Mathf.Clamp(flat * 0.08f, 0f, 1.6f);
                }
                lockBlend = Mathx.Damp(lockBlend, 1f, 5f, dt);
            }
            else lockBlend = Mathx.Damp(lockBlend, 0f, 5f, dt);

            pivot = new Vector3(Mathx.Damp(pivot.x, targetPivot.x, 14f, dt), Mathx.Damp(pivot.y, targetPivot.y, 9f, dt), Mathx.Damp(pivot.z, targetPivot.z, 14f, dt));

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 right = rot * Vector3.right;
            Vector3 anchor = pivot + right * shoulder;
            Vector3 back = rot * Vector3.back;

            // obstacle avoidance: pull in instantly, ease out
            float allowed = desiredDist;
            if (Physics.SphereCast(anchor, 0.26f, back, out RaycastHit hit, desiredDist, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.55f, hit.distance - 0.08f);
            if (allowed < curDist) curDist = allowed;
            else curDist = Mathx.Damp(curDist, allowed, 3f, dt);

            Vector3 pos = anchor + back * curDist;
            Vector3 shakeOff = ComputeShake(dt, out Quaternion shakeQ);
            Quaternion finalRot = rot * shakeQ;
            Vector3 finalPos = pos + shakeOff;

            float baseFov = s != null ? s.fov : 62f;
            fovPunch = Mathf.MoveTowards(fovPunch, 0f, dt * 18f);
            float wantFov = baseFov + sprintKick + fovPunch;
            fovCurrent = Mathx.Damp(fovCurrent, wantFov, 8f, dt);

            if (blendBackT < 1f)
            {
                blendBackT = Mathf.Min(1f, blendBackT + dt / blendBackDur);
                float k = Mathx.SmootherStep01(blendBackT);
                finalPos = Vector3.Lerp(blendFromPos, finalPos, k);
                finalRot = Quaternion.Slerp(blendFromRot, finalRot, k);
                cam.fieldOfView = Mathf.Lerp(blendFromFov, fovCurrent, k);
            }
            else cam.fieldOfView = fovCurrent;
            transform.SetPositionAndRotation(finalPos, finalRot);
        }

        Vector3 ComputeShake(float dt, out Quaternion rot)
        {
            rot = Quaternion.identity;
            if (shakeDur <= 0f) return Vector3.zero;
            shakeT += dt;
            if (shakeT >= shakeDur)
            {
                shakeDur = 0f;
                shakeAmp = 0f;
                return Vector3.zero;
            }
            float k = 1f - shakeT / shakeDur;
            float amp = shakeAmp * k * k;
            float t = Time.unscaledTime * 28f;
            Vector3 off = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (amp * 0.5f);
            rot = Quaternion.Euler((Mathf.PerlinNoise(t, 5.1f) - 0.5f) * amp * 4f, (Mathf.PerlinNoise(9.2f, t) - 0.5f) * amp * 4f, (Mathf.PerlinNoise(t, 1.9f) - 0.5f) * amp * 3f);
            return transform.rotation * off;
        }
    }
}
