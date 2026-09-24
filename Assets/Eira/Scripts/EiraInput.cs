using UnityEngine;
using UnityEngine.InputSystem;

namespace EiraGame
{
    // Lectura de input: prefiere el Input System (Keyboard/Mouse.current).
    // Si no hay dispositivo activo, cae al Input Manager legacy para que WASD
    // y el ratón funcionen siempre (aunque falte un dispositivo del Input System).
    public static class EiraInput
    {
        static bool HasKeyboard => Keyboard.current != null;
        static bool HasMouse => Mouse.current != null;

        static bool Down(Key k, KeyCode legacy)
        {
            if (HasKeyboard) return Keyboard.current[k].isPressed;
            return Input.GetKey(legacy);
        }

        static bool WasPressed(Key k, KeyCode legacy)
        {
            if (HasKeyboard) return Keyboard.current[k].wasPressedThisFrame;
            return Input.GetKeyDown(legacy);
        }

        public static Vector2 MoveAxis()
        {
            if (HasKeyboard)
            {
                float x = 0f, y = 0f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                if (Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                return new Vector2(x, y).normalized;
            }
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        }

        public static bool Sprint()
        {
            if (HasKeyboard) return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        public static bool CrouchHeld()
        {
            if (HasKeyboard) return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed;
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        }

        public static bool JumpDown()
        {
            if (HasKeyboard) return Keyboard.current.spaceKey.wasPressedThisFrame;
            return Input.GetKeyDown(KeyCode.Space);
        }

        public static bool InteractDown()
        {
            if (HasKeyboard) return Keyboard.current.eKey.wasPressedThisFrame;
            return Input.GetKeyDown(KeyCode.E);
        }

        public static bool AbilityDown()
        {
            if (HasKeyboard) return Keyboard.current.aKey.wasPressedThisFrame;
            return Input.GetKeyDown(KeyCode.A);
        }

        public static bool AdvanceDown()
        {
            if (HasKeyboard) return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame;
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);
        }

        public static bool ToggleMissionsDown()
        {
            if (HasKeyboard) return Keyboard.current.tabKey.wasPressedThisFrame;
            return Input.GetKeyDown(KeyCode.Tab);
        }

        public static bool SkipDown()
        {
            if (HasKeyboard) return Keyboard.current.escapeKey.wasPressedThisFrame;
            return Input.GetKeyDown(KeyCode.Escape);
        }

        public static Vector2 LookDelta(float sens = 1f)
        {
            if (HasMouse)
            {
                var d = Mouse.current.delta.ReadValue() * sens;
                if (Mathf.Abs(d.x) > 12f || Mathf.Abs(d.y) > 12f) return Vector2.zero;
                return d;
            }
            return new Vector2(Input.GetAxis("Mouse X") * sens, Input.GetAxis("Mouse Y") * sens);
        }
    }
}