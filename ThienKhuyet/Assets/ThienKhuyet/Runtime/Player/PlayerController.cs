using ThienKhuyet.Audio;
using ThienKhuyet.Characters;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Player
{
    public enum PState { Locomotion = 0, Attack, Block, Dodge, Cast, Stagger, Dead, Locked, Meditate, Dash }

    /// <summary>
    /// Third-person character controller: camera-relative movement, sprint, jump (with coyote time and buffering), dodge with i-frames,
    /// swimming, knockback, stagger and death. Combat actions are driven by <see cref="PlayerCombat"/> through the state machine.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IKnockbackable
    {
        public CharacterController cc;
        public Vitals vitals;
        public HumanoidRig rig;
        public HumanoidAnimator anim;
        public PlayerCombat combat;
        public Interactor interactor;

        public float walkSpeed = 2.7f, runSpeed = 5.5f, sprintSpeed = 8.4f;
        public float jumpSpeed = 8.2f, gravity = 24f;

        public PState State { get; private set; } = PState.Locomotion;
        public bool IsGrounded { get; private set; } = true;
        public bool IsSprinting { get; private set; }
        public bool IsSwimming { get; private set; }
        public float StateTime => stateTime;
        public Vector3 PlanarVelocity => planarVel;
        public bool CanAct => State == PState.Locomotion && Game.Mode == GameMode.Playing;
        public bool CanInteract => State == PState.Locomotion && Game.Mode == GameMode.Playing;
        public bool Alive => State != PState.Dead;
        public bool InWater { get; private set; }

        Vector3 planarVel, external, externalDecayVel;
        float vertVel, coyote, jumpBuffer, stateTime, stepAccum, staggerTime, dodgeTime, dashTime, dashDuration;
        bool airJumpUsed, wasGrounded = true, sprintBlocked;
        Vector3 dodgeDir, dashDir;
        float dashSpeed;
        float meditateTick;
        bool meditateAtSpot;
        float deathTimer;
        Vector3 lastGroundedPos;

        void Awake()
        {
            if (cc == null) cc = GetComponent<CharacterController>();
        }

        void OnEnable()
        {
            if (vitals != null)
            {
                vitals.Died += OnDied;
                vitals.Staggered += OnStaggered;
                vitals.Damaged += OnDamaged;
            }
        }

        void OnDisable()
        {
            if (vitals != null)
            {
                vitals.Died -= OnDied;
                vitals.Staggered -= OnStaggered;
                vitals.Damaged -= OnDamaged;
            }
        }

        public void Bind(Vitals v)
        {
            if (vitals != null)
            {
                vitals.Died -= OnDied;
                vitals.Staggered -= OnStaggered;
                vitals.Damaged -= OnDamaged;
            }
            vitals = v;
            vitals.Died += OnDied;
            vitals.Staggered += OnStaggered;
            vitals.Damaged += OnDamaged;
        }

        // ------------------------------------------------------------------ state
        public void SetState(PState s)
        {
            if (State == s) return;
            if (State == PState.Meditate) StopMeditationEffects();
            State = s;
            stateTime = 0f;
        }

        public void FaceDirection(Vector3 dir, float rate, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            float target = Mathx.DirToYaw(dir);
            float y = Mathx.DampAngle(transform.eulerAngles.y, target, rate, dt);
            transform.rotation = Quaternion.Euler(0f, y, 0f);
        }

        public void FaceInstant(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, Mathx.DirToYaw(dir), 0f);
        }

        /// <summary>Extra displacement for this frame (attack lunges).</summary>
        public void AddMove(Vector3 worldDelta)
        {
            pendingMove += worldDelta;
        }

        Vector3 pendingMove;

        public void Knockback(Vector3 velocity)
        {
            if (State == PState.Dead || State == PState.Dodge || State == PState.Dash) return;
            external += velocity;
        }

        public void Dash(Vector3 dir, float distance, float duration)
        {
            dashDir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            dashDuration = Mathf.Max(0.1f, duration);
            dashSpeed = distance / dashDuration;
            dashTime = 0f;
            vitals.GrantIFrames(dashDuration * 0.85f);
            SetState(PState.Dash);
        }

        // ------------------------------------------------------------------ teleport / control hand-over
        public void Teleport(Vector3 pos, float yaw)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = true;
            planarVel = Vector3.zero;
            external = Vector3.zero;
            vertVel = 0f;
            lastGroundedPos = pos;
            if (anim != null) anim.ResetMotion();
            if (!was) cc.enabled = false;
            if (Game.Camera != null && Game.Camera.target == transform) Game.Camera.SnapBehind();
        }

        /// <summary>Cutscenes take direct control of the transform (CharacterController off) and give it back afterwards.</summary>
        public void SetControlled(bool controlled)
        {
            if (controlled)
            {
                combat?.CancelAll();
                SetState(PState.Locked);
                cc.enabled = false;
                planarVel = Vector3.zero;
                external = Vector3.zero;
            }
            else
            {
                cc.enabled = true;
                if (State != PState.Dead) SetState(PState.Locomotion);
                vertVel = 0f;
                planarVel = Vector3.zero;
                if (anim != null) anim.ResetMotion();
            }
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || cc == null || !cc.enabled) return;
            stateTime += dt;

            GameInput input = Game.Input;
            bool open = input != null && !input.IsLocked && Game.Mode == GameMode.Playing;
            Vector2 mv = open ? input.Move : Vector2.zero;
            Vector3 camF = Game.Camera != null ? Game.Camera.PlanarForward : Vector3.forward;
            Vector3 camR = Game.Camera != null ? Game.Camera.PlanarRight : Vector3.right;
            Vector3 wish = camF * mv.y + camR * mv.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            UpdateEnvironment();
            float speedMul = vitals.stats.Get(StatId.MoveSpeed) * vitals.status.MoveMultiplier * (InWater ? (IsSwimming ? 0.55f : 0.8f) : 1f);
            Vector3 desired = Vector3.zero;
            bool snapVelocity = false;

            // buffered jump
            if (open && input.JumpPressed) jumpBuffer = 0.14f;
            else jumpBuffer -= dt;
            coyote = IsGrounded ? 0.12f : coyote - dt;

            IsSprinting = false;
            switch (State)
            {
                case PState.Locomotion:
                    {
                        float mag = mv.magnitude;
                        float speed = mag < 0.45f ? walkSpeed : runSpeed;
                        bool wantSprint = open && input.SprintHeld && mag > 0.5f && !sprintBlocked && vitals.stamina > 1f && !IsSwimming;
                        if (wantSprint) { speed = sprintSpeed; IsSprinting = true; vitals.stamina = Mathf.Max(0f, vitals.stamina - 11f * dt); vitals.staminaRegenBlocked = true; }
                        else vitals.staminaRegenBlocked = false;
                        if (vitals.stamina <= 0.5f) sprintBlocked = true;
                        else if (vitals.stamina > 18f) sprintBlocked = false;
                        desired = wish * speed * speedMul * (mag < 0.45f ? mag / 0.45f * 0.8f + 0.2f : 1f);
                        Transform lt = combat != null && combat.lockOn.Active ? combat.lockOn.Target.transform : null;
                        if (lt != null) FaceDirection(lt.position - transform.position, 16f, dt);
                        else if (wish.sqrMagnitude > 0.01f) FaceDirection(wish, 16f, dt);

                        if (open && input.DodgePressed) TryDodge(wish);
                        else if (jumpBuffer > 0f) TryJump();
                        if (open && input.MeditatePressed && !vitals.IsDead) TryMeditate();
                        break;
                    }
                case PState.Attack:
                case PState.Cast:
                    desired = wish * runSpeed * speedMul * (combat != null ? combat.MoveMultiplier : 0.2f);
                    vitals.staminaRegenBlocked = false;
                    if (open && input.DodgePressed && combat != null && combat.CanCancelIntoDodge) TryDodge(wish);
                    break;
                case PState.Block:
                    {
                        desired = wish * walkSpeed * 0.8f * speedMul;
                        Transform lt = combat != null && combat.lockOn.Active ? combat.lockOn.Target.transform : null;
                        if (lt != null) FaceDirection(lt.position - transform.position, 14f, dt);
                        else FaceDirection(camF, 10f, dt);
                        if (open && input.DodgePressed) TryDodge(wish);
                        break;
                    }
                case PState.Dodge:
                    {
                        dodgeTime += dt;
                        float k = Mathf.Clamp01(dodgeTime / 0.46f);
                        float spd = Mathf.Lerp(10.8f, 3.2f, Mathx.EaseOutCubic(k));
                        desired = dodgeDir * spd * Mathf.Max(0.7f, speedMul);
                        snapVelocity = true;
                        if (dodgeTime >= 0.46f) SetState(PState.Locomotion);
                        else if (dodgeTime > 0.3f && open && (input.LightPressed || input.HeavyPressed)) { SetState(PState.Locomotion); combat?.QueueFromDodge(input.HeavyPressed); }
                        break;
                    }
                case PState.Dash:
                    dashTime += dt;
                    desired = dashDir * dashSpeed;
                    snapVelocity = true;
                    if (dashTime >= dashDuration) SetState(PState.Locomotion);
                    break;
                case PState.Stagger:
                    staggerTime += dt;
                    if (staggerTime >= 0.95f || (staggerTime > 0.5f && open && input.DodgePressed)) SetState(PState.Locomotion);
                    break;
                case PState.Meditate:
                    UpdateMeditation(dt, open, input);
                    break;
                case PState.Dead:
                    deathTimer += dt;
                    break;
            }

            // acceleration
            float accel = IsGrounded ? (desired.sqrMagnitude > planarVel.sqrMagnitude ? 34f : 42f) : 9f;
            if (snapVelocity) planarVel = desired;
            else planarVel = Vector3.MoveTowards(planarVel, desired, accel * dt);

            // external (knockback) velocity decays
            external = Vector3.SmoothDamp(external, Vector3.zero, ref externalDecayVel, 0.18f, 100f, dt);

            // vertical motion
            if (IsSwimming)
            {
                float surface = Game.World != null ? Game.World.WaterHeightAt(transform.position) : -1000f;
                float targetY = surface - 1.2f;
                vertVel = Mathf.MoveTowards(vertVel, (targetY - transform.position.y) * 3f, 30f * dt);
                vitals.stamina = Mathf.Max(0f, vitals.stamina - 3f * dt);
            }
            else
            {
                if (IsGrounded && vertVel < 0f) vertVel = -3f;
                else vertVel = Mathf.Max(-48f, vertVel - gravity * dt);
            }

            Vector3 motion = (planarVel + external) * dt + Vector3.up * (vertVel * dt) + pendingMove;
            pendingMove = Vector3.zero;
            float preY = transform.position.y;
            cc.Move(motion);
            bool groundedNow = cc.isGrounded;
            if (!groundedNow && wasGrounded && vertVel <= 0.1f && State != PState.Dead)
            {
                // stick to the ground when walking down slopes
                if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 0.55f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                {
                    cc.Move(Vector3.down * Mathf.Max(0f, hit.distance - 0.2f + 0.02f));
                    groundedNow = true;
                }
            }
            HandleLanding(groundedNow);
            IsGrounded = groundedNow;
            wasGrounded = groundedNow;
            if (groundedNow) lastGroundedPos = transform.position;

            if (anim != null)
            {
                anim.grounded = IsGrounded || IsSwimming;
                anim.combatStance = State == PState.Block || State == PState.Attack || State == PState.Cast || (combat != null && combat.InCombatStance);
                anim.stanceKind = HumanoidAnimator.StanceFor(vitals != null && Game.Session != null ? Game.Session.player.WeaponFamilyNow : WeaponFamily.Fist, true);
            }
            if (Game.Camera != null) Game.Camera.SetSprintKick(IsSprinting ? 5f : 0f);
            Footsteps(dt, speedMul);

            // fell out of the world: bring back to the last safe ground
            if (transform.position.y < -60f && State != PState.Dead) Teleport(lastGroundedPos + Vector3.up * 1f, transform.eulerAngles.y);
        }

        void UpdateEnvironment()
        {
            InWater = false;
            IsSwimming = false;
            if (Game.World == null) return;
            float surface = Game.World.WaterHeightAt(transform.position);
            if (surface < -500f) return;
            float depth = surface - transform.position.y;
            if (depth > 0.25f) InWater = true;
            if (depth > 1.1f) IsSwimming = true;
        }

        void TryJump()
        {
            if (!CanAct) return;
            bool ground = coyote > 0f;
            bool air = !ground && !airJumpUsed && Game.Session != null && Game.Session.player.HasUnlock("air_dash");
            if (!ground && !air && !IsSwimming) return;
            if (!vitals.SpendStamina(8f)) return;
            vertVel = jumpSpeed * (air ? 0.9f : 1f);
            jumpBuffer = 0f;
            coyote = 0f;
            IsGrounded = false;
            if (air) { airJumpUsed = true; Vfx.Play("dash_trail", transform.position + Vector3.up * 0.3f, Quaternion.identity, 0.8f); }
            anim?.PlayAction("jump_up", 1.3f);
            AudioManager.Instance?.Sfx("jump", transform.position, 0.6f);
            EventBus.Publish(new PlayerActionEvent { action = "jump" });
        }

        void HandleLanding(bool groundedNow)
        {
            if (groundedNow && !wasGrounded)
            {
                float impact = -vertVel;
                airJumpUsed = false;
                if (impact > 8f)
                {
                    AudioManager.Instance?.Sfx("land", transform.position, Mathf.Clamp01(impact / 18f));
                    Vfx.Play("dust", transform.position + Vector3.up * 0.05f, Quaternion.identity, Mathf.Clamp(impact / 12f, 0.6f, 1.5f));
                    if (State == PState.Locomotion && impact > 11f) anim?.PlayAction("land", 1f);
                    Game.Camera?.Shake(Mathf.Clamp(impact * 0.004f, 0f, 0.09f), 0.15f);
                }
                if (impact > 17f) vitals.DamageDirect((impact - 17f) * 5f, DamageType.True);
            }
            else if (groundedNow) airJumpUsed = false;
        }

        void TryDodge(Vector3 wish)
        {
            if (State == PState.Dead || State == PState.Dodge) return;
            if (!vitals.SpendStamina(18f)) { Game.UI?.FlashStamina(); return; }
            combat?.CancelAll();
            dodgeDir = wish.sqrMagnitude > 0.05f ? wish.normalized : -transform.forward;
            if (wish.sqrMagnitude <= 0.05f && combat != null && combat.lockOn.Active) dodgeDir = -transform.forward;
            dodgeTime = 0f;
            vitals.GrantIFrames(0.3f);
            SetState(PState.Dodge);
            FaceInstant(wish.sqrMagnitude > 0.05f ? wish : transform.forward);
            anim?.PlayAction("dodge_roll", 1f);
            AudioManager.Instance?.Sfx("dodge", transform.position, 0.7f);
            Vfx.Play("dust", transform.position + Vector3.up * 0.05f, Quaternion.identity, 0.7f);
            EventBus.Publish(new PlayerActionEvent { action = "dodge" });
        }

        // ------------------------------------------------------------------ reactions
        void OnDamaged(DamageInfo info, DamageResult r)
        {
            if (State == PState.Dead || r.dealt <= 0f || r.blocked || info.silent) return;
            if (State == PState.Meditate) SetState(PState.Locomotion);
            if (State == PState.Locomotion && !r.staggered) anim?.PlayAction("hit_light", 1.2f);
            if (r.dealt > 0f) anim?.SetExpression("pain");
        }

        void OnStaggered(DamageInfo info)
        {
            if (State == PState.Dead) return;
            combat?.CancelAll();
            staggerTime = 0f;
            SetState(PState.Stagger);
            anim?.PlayAction(info.heavy ? "stagger" : "hit_heavy", 1.1f);
        }

        void OnDied(DamageInfo info)
        {
            combat?.CancelAll();
            SetState(PState.Dead);
            deathTimer = 0f;
            anim?.PlayAction("death", 1f);
            anim?.SetExpression("closed");
            AudioManager.Instance?.Sfx2D("die_player", 1f);
            GameFeel.SlowMo(0.3f, 0.6f);
            Game.Camera?.Shake(0.18f, 0.4f);
            EventBus.Publish(new PlayerDiedEvent());
            Game.Manager?.OnPlayerDied();
        }

        public void Respawn(Vector3 pos, float yaw)
        {
            Teleport(pos, yaw);
            vitals.Revive(0.6f);
            vitals.stamina = vitals.MaxStamina;
            SetState(PState.Locomotion);
            anim?.StopAction(0.1f);
            anim?.SetExpression("neutral");
            EventBus.Publish(new PlayerRespawnedEvent());
        }

        // ------------------------------------------------------------------ meditation
        void TryMeditate()
        {
            if (combat != null && combat.InCombatStance) { Game.UI?.Toast(Loc.T("toast.cannot_meditate_combat")); return; }
            if (!IsGrounded) return;
            StartMeditation(Game.World != null && Game.World.IsMeditationSpot(transform.position));
        }

        public void StartMeditation(bool atSpot)
        {
            if (State == PState.Dead) return;
            combat?.CancelAll();
            meditateAtSpot = atSpot;
            meditateTick = 0f;
            SetState(PState.Meditate);
            anim?.PlayAction("meditate", 1f);
            anim?.SetExpression("closed");
            medFx = Vfx.Play("meditate_aura", transform.position + Vector3.up * 0.05f, Quaternion.identity, 1f);
            EventBus.Publish(new MeditationEvent { started = true, atSpot = atSpot });
            EventBus.Publish(new PlayerActionEvent { action = "meditate" });
        }

        GameObject medFx;

        void StopMeditationEffects()
        {
            anim?.StopAction(0.4f);
            anim?.SetExpression("neutral");
            if (medFx != null) { Vfx.Stop(medFx); medFx = null; }
            EventBus.Publish(new MeditationEvent { started = false, atSpot = meditateAtSpot });
        }

        void UpdateMeditation(float dt, bool open, GameInput input)
        {
            if (medFx != null) medFx.transform.position = transform.position + Vector3.up * 0.05f;
            vitals.staminaRegenBlocked = false;
            meditateTick += dt;
            vitals.RestoreStamina(30f * dt);
            vitals.Heal(vitals.MaxHp * 0.012f * dt * (meditateAtSpot ? 3f : 1f));
            vitals.RestoreQi(vitals.stats.Get(StatId.QiRegen) * 2.2f * dt * (meditateAtSpot ? 2.5f : 1f));
            if (meditateTick >= 1f)
            {
                meditateTick -= 1f;
                Game.Manager?.OnMeditationTick(meditateAtSpot);
            }
            bool interrupt = open && (input.AnyMoveHeld || input.JumpPressed || input.DodgePressed || input.LightPressed || input.HeavyPressed || input.MeditatePressed);
            if (interrupt) SetState(PState.Locomotion);
        }

        // ------------------------------------------------------------------ footsteps
        void Footsteps(float dt, float speedMul)
        {
            float speed = new Vector2(planarVel.x, planarVel.z).magnitude;
            if (!IsGrounded && !IsSwimming || speed < 0.8f || State == PState.Dodge || State == PState.Dead) { if (speed < 0.8f) stepAccum = 0f; return; }
            float stride = Mathf.Lerp(1.5f, 2.5f, Mathf.Clamp01((speed - 2.5f) / 6f));
            stepAccum += speed * dt;
            if (stepAccum < stride) return;
            stepAccum = 0f;
            string surface = InWater ? "water" : (Game.World != null ? Game.World.SurfaceAt(transform.position) : "dirt");
            AudioManager.Instance?.Sfx("step_" + surface, transform.position, Mathf.Clamp(speed / 8f, 0.25f, 0.8f), 1f, 0.12f);
        }
    }
}
