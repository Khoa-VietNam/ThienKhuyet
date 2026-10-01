using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace ThienKhuyet.Core
{
    /// <summary>
    /// All gameplay input through the Input System package, defined in code (no .inputactions asset to keep in sync).
    /// Keyboard/mouse and gamepad bindings. Gameplay actions are masked while a cutscene/dialogue/menu holds an input lock.
    /// </summary>
    public sealed class GameInput
    {
        readonly InputActionMap map = new InputActionMap("Gameplay");
        readonly InputAction move, look, jump, sprint, dodge, light, heavy, block, lockOn, interact, meditate, quickUse, skip, advance, menu;
        readonly InputAction[] skills = new InputAction[4];
        readonly InputAction[] screens = new InputAction[7];
        readonly HashSet<string> locks = new HashSet<string>();

        public static readonly string[] ScreenNames = { "inventory", "character", "cultivation", "skills", "quests", "map", "pause" };

        public GameInput()
        {
            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            look = map.AddAction("Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            dodge = Button("Dodge", "<Keyboard>/leftCtrl", "<Gamepad>/buttonEast");
            light = Button("LightAttack", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            heavy = Button("HeavyAttack", "<Mouse>/rightButton", "<Gamepad>/rightTrigger");
            block = Button("Block", "<Keyboard>/q", "<Gamepad>/leftTrigger");
            lockOn = Button("LockOn", "<Mouse>/middleButton", "<Gamepad>/rightStickPress");
            lockOn.AddBinding("<Keyboard>/t");
            interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonNorth");
            meditate = Button("Meditate", "<Keyboard>/g", "<Gamepad>/dpad/down");
            quickUse = Button("QuickUse", "<Keyboard>/r", "<Gamepad>/leftShoulder");
            skip = Button("Skip", "<Keyboard>/escape", "<Gamepad>/start");
            advance = Button("Advance", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            advance.AddBinding("<Keyboard>/enter");
            advance.AddBinding("<Mouse>/leftButton");
            advance.AddBinding("<Keyboard>/e");
            menu = Button("Menu", "<Keyboard>/escape", "<Gamepad>/start");

            string[] keys = { "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4" };
            string[] pads = { "<Gamepad>/dpad/up", "<Gamepad>/dpad/right", "<Gamepad>/rightShoulder", "<Gamepad>/dpad/left" };
            for (int i = 0; i < 4; i++) skills[i] = Button("Skill" + (i + 1), keys[i], pads[i]);

            string[] screenKeys = { "<Keyboard>/i", "<Keyboard>/c", "<Keyboard>/v", "<Keyboard>/k", "<Keyboard>/j", "<Keyboard>/m", "<Keyboard>/tab" };
            for (int i = 0; i < screens.Length; i++)
            {
                screens[i] = map.AddAction("Screen_" + ScreenNames[i], InputActionType.Button);
                screens[i].AddBinding(screenKeys[i]);
            }
        }

        InputAction Button(string name, string key, string pad)
        {
            InputAction a = map.AddAction(name, InputActionType.Button);
            if (!string.IsNullOrEmpty(key)) a.AddBinding(key);
            if (!string.IsNullOrEmpty(pad)) a.AddBinding(pad);
            return a;
        }

        public void Enable() { map.Enable(); }
        public void Disable() { map.Disable(); }

        public void Dispose()
        {
            map.Disable();
            map.Dispose();
        }

        // ------------------------------------------------------------------ locks
        public void PushLock(string key) { locks.Add(key); }
        public void PopLock(string key) { locks.Remove(key); }
        public bool IsLocked => locks.Count > 0;
        public bool HasLock(string key) { return locks.Contains(key); }
        public void ClearLocks() { locks.Clear(); }

        bool Open => locks.Count == 0;

        // ------------------------------------------------------------------ gameplay queries
        public Vector2 Move => Open ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public Vector2 Look => Open ? look.ReadValue<Vector2>() : Vector2.zero;
        public bool LookIsGamepad => look.activeControl != null && look.activeControl.device is Gamepad;
        public bool JumpPressed => Open && jump.WasPressedThisFrame();
        public bool SprintHeld => Open && sprint.IsPressed();
        public bool DodgePressed => Open && dodge.WasPressedThisFrame();
        public bool LightPressed => Open && light.WasPressedThisFrame();
        public bool HeavyPressed => Open && heavy.WasPressedThisFrame();
        public bool HeavyHeld => Open && heavy.IsPressed();
        public bool HeavyReleased => Open && heavy.WasReleasedThisFrame();
        public bool BlockHeld => Open && block.IsPressed();
        public bool BlockPressed => Open && block.WasPressedThisFrame();
        public bool LockOnPressed => Open && lockOn.WasPressedThisFrame();
        public bool InteractPressed => Open && interact.WasPressedThisFrame();
        public bool MeditatePressed => Open && meditate.WasPressedThisFrame();
        public bool QuickUsePressed => Open && quickUse.WasPressedThisFrame();
        public bool SkillPressed(int slot) { return Open && slot >= 0 && slot < 4 && skills[slot].WasPressedThisFrame(); }
        public bool AnyMoveHeld => Open && move.ReadValue<Vector2>().sqrMagnitude > 0.04f;

        // ------------------------------------------------------------------ UI-level queries (never locked)
        public bool ScreenPressed(int index) { return screens[index].WasPressedThisFrame(); }
        public bool MenuPressed => menu.WasPressedThisFrame();
        public bool AdvancePressed => advance.WasPressedThisFrame();
        public bool SkipHeld => skip.IsPressed();
        public bool SkipPressed => skip.WasPressedThisFrame();

        // ------------------------------------------------------------------ UI module
        /// <summary>Creates the EventSystem with the Input System UI module (the legacy StandaloneInputModule must not be used).</summary>
        public static GameObject CreateEventSystem(Transform parent)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return UnityEngine.EventSystems.EventSystem.current.gameObject;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            return go;
        }
    }
}
