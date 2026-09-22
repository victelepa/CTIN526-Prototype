using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RaceSabotage
{
    public enum PlayerSlot
    {
        One,
        Two
    }

    /// <summary>
    /// Day 1 input: two halves of one keyboard read off the Input System's
    /// low-level device API. Replace with an .inputactions asset + PlayerInput
    /// once gamepads are needed.
    /// P1: A/D move, W jump, S item, LeftShift interact.
    /// P2: Left/Right move, Up jump, Down item, RightShift interact.
    /// Values are polled on access rather than cached in Update, so no other
    /// component can read them a frame late depending on script execution order.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] PlayerSlot slot = PlayerSlot.One;

        Keyboard _keyboard;
        KeyControl _left;
        KeyControl _right;
        KeyControl _jump;
        KeyControl _item;
        KeyControl _interact;

        public PlayerSlot Slot
        {
            get => slot;
            set
            {
                slot = value;
                _keyboard = null;
            }
        }

        public float MoveX => Resolve() ? (_right.isPressed ? 1f : 0f) - (_left.isPressed ? 1f : 0f) : 0f;
        public bool JumpPressed => Resolve() && _jump.wasPressedThisFrame;
        public bool JumpHeld => Resolve() && _jump.isPressed;
        public bool UseItemPressed => Resolve() && _item.wasPressedThisFrame;
        public bool InteractPressed => Resolve() && _interact.wasPressedThisFrame;

        bool Resolve()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (keyboard == _keyboard) return true;

            _keyboard = keyboard;
            if (slot == PlayerSlot.One)
            {
                _left = keyboard.aKey;
                _right = keyboard.dKey;
                _jump = keyboard.wKey;
                _item = keyboard.sKey;
                _interact = keyboard.leftShiftKey;
            }
            else
            {
                _left = keyboard.leftArrowKey;
                _right = keyboard.rightArrowKey;
                _jump = keyboard.upArrowKey;
                _item = keyboard.downArrowKey;
                _interact = keyboard.rightShiftKey;
            }

            return true;
        }
    }
}
