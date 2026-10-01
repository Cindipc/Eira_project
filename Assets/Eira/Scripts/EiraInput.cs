using UnityEngine;
using UnityEngine.InputSystem;

namespace EiraGame
{
    public static class EiraInput
    {
        private static Keyboard Keyboard =>
            UnityEngine.InputSystem.Keyboard.current;

        private static Mouse Mouse =>
            UnityEngine.InputSystem.Mouse.current;

        private static Gamepad Gamepad =>
            UnityEngine.InputSystem.Gamepad.current;

        // =========================================================
        // MOVIMIENTO
        // =========================================================

        public static Vector2 MoveAxis()
        {
            Vector2 keyboard = Vector2.zero;

            if (Keyboard != null)
            {
                if (Keyboard.wKey.isPressed ||
                    Keyboard.upArrowKey.isPressed)
                    keyboard.y += 1f;

                if (Keyboard.sKey.isPressed ||
                    Keyboard.downArrowKey.isPressed)
                    keyboard.y -= 1f;

                if (Keyboard.dKey.isPressed ||
                    Keyboard.rightArrowKey.isPressed)
                    keyboard.x += 1f;

                if (Keyboard.aKey.isPressed ||
                    Keyboard.leftArrowKey.isPressed)
                    keyboard.x -= 1f;
            }

            Vector2 controller = Vector2.zero;

            if (Gamepad != null)
            {
                controller = Gamepad.leftStick.ReadValue();
            }

            if (controller.sqrMagnitude > 0.01f)
                return Vector2.ClampMagnitude(controller, 1f);

            return Vector2.ClampMagnitude(keyboard, 1f);
        }

        // =========================================================
        // CORRER
        // =========================================================

        public static bool Sprint()
        {
            if (Keyboard != null)
            {
                if (Keyboard.leftShiftKey.isPressed ||
                    Keyboard.rightShiftKey.isPressed)
                {
                    return true;
                }
            }

            if (Gamepad != null &&
                Gamepad.leftStickButton.isPressed)
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // AGACHARSE
        // =========================================================

        public static bool CrouchHeld()
        {
            if (Keyboard != null)
            {
                if (Keyboard.leftCtrlKey.isPressed ||
                    Keyboard.cKey.isPressed)
                {
                    return true;
                }
            }

            if (Gamepad != null &&
                Gamepad.buttonEast.isPressed)
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // SALTO
        // =========================================================

        public static bool JumpDown()
        {
            bool keyboardJump = false;
            bool controllerJump = false;

            if (Keyboard != null)
            {
                keyboardJump =
                    Keyboard.spaceKey.wasPressedThisFrame;
            }

            if (Gamepad != null)
            {
                controllerJump =
                    Gamepad.buttonSouth.wasPressedThisFrame;
            }

            return keyboardJump || controllerJump;
        }

        // =========================================================
        // SALTO MANTENIDO (altura variable)
        // =========================================================

        public static bool JumpHeld()
        {
            if (Keyboard != null)
            {
                if (Keyboard.spaceKey.isPressed)
                    return true;
            }

            if (Gamepad != null &&
                Gamepad.buttonSouth.isPressed)
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // INTERACTUAR
        // =========================================================

        public static bool InteractDown()
        {
            if (Keyboard != null &&
                Keyboard.eKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad != null &&
                Gamepad.buttonWest.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // HABILIDAD
        // =========================================================

        public static bool AbilityDown()
        {
            if (Keyboard != null &&
                (Keyboard.fKey.wasPressedThisFrame ||
                 Keyboard.qKey.wasPressedThisFrame ||
                 Mouse != null && Mouse.rightButton.wasPressedThisFrame))
            {
                return true;
            }

            if (Gamepad != null &&
                (Gamepad.buttonNorth.wasPressedThisFrame ||
                 Gamepad.leftTrigger.wasPressedThisFrame))
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // REINICIAR
        // =========================================================

        public static bool RestartDown()
        {
            if (Keyboard != null &&
                Keyboard.rKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad != null &&
                Gamepad.selectButton.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // ATAQUE
        // =========================================================

        public static bool AttackDown()
        {
            if (Mouse != null &&
                Mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad != null &&
                Gamepad.rightTrigger.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Disparo mantenido. Es lo que permite mantener pulsado y seguir
        /// disparando, en vez de un disparo suelto por pulsación.
        /// </summary>
        public static bool AttackHeld()
        {
            if (Mouse != null &&
                Mouse.leftButton.isPressed)
            {
                return true;
            }

            if (Gamepad != null &&
                Gamepad.rightTrigger.isPressed)
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // HABILIDAD CON CLICK DERECHO
        // =========================================================

        public static bool AbilityMouseDown()
        {
            if (Mouse != null &&
                Mouse.rightButton.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad != null &&
                Gamepad.leftTrigger.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // CÁMARA
        // =========================================================

        public static Vector2 LookDelta(float sensitivity = 1f)
        {
            Vector2 mouse = Vector2.zero;

            if (Mouse != null)
            {
                mouse = Mouse.delta.ReadValue() * sensitivity;

                if (mouse.sqrMagnitude > 10000f)
                    mouse = Vector2.zero;
            }

            Vector2 stick = Vector2.zero;

            if (Gamepad != null)
            {
                stick = Gamepad.rightStick.ReadValue();

                if (stick.sqrMagnitude > 0.001f)
                    return stick * 5f;
            }

            return mouse;
        }

        // =========================================================
        // DIÁLOGOS
        // =========================================================

        public static bool AdvanceDown()
        {
            if (Keyboard != null)
            {
                if (Keyboard.enterKey.wasPressedThisFrame ||
                    Keyboard.spaceKey.wasPressedThisFrame ||
                    Keyboard.eKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            if (Gamepad != null)
            {
                if (Gamepad.buttonSouth.wasPressedThisFrame ||
                    Gamepad.buttonWest.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }

        // =========================================================
        // MISIONES
        // =========================================================

        public static bool ToggleMissionsDown()
        {
            if (Keyboard != null &&
                Keyboard.tabKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad != null &&
                Gamepad.dpad.up.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // PAUSA
        // =========================================================

        public static bool SkipDown()
        {
            if (Keyboard != null &&
                Keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad != null &&
                Gamepad.startButton.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }
    }
}