using UnityEngine;
using UnityEngine.InputSystem;

// Device mapping only; the C++ InputFrame boundary owns the gameplay intent.
public static class UnityPlayerInput
{
    public struct Sample
    {
        public Vector2 move;
        public bool interactPressed;
        public bool jumpPressed;
    }

    public static Sample Read()
    {
        Sample result = new Sample();
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) result.move.x -= 1;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) result.move.x += 1;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) result.move.y -= 1;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) result.move.y += 1;
            result.interactPressed = keyboard.eKey.wasPressedThisFrame;
            result.jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        }
        Gamepad controller = Gamepad.current;
        if (controller != null)
        {
            Vector2 stick = controller.leftStick.ReadValue();
            Vector2 dpad = controller.dpad.ReadValue();
            Vector2 movement = dpad.sqrMagnitude > stick.sqrMagnitude ? dpad : stick;
            if (movement.sqrMagnitude > result.move.sqrMagnitude) result.move = movement;
            result.interactPressed |= controller.buttonSouth.wasPressedThisFrame;
            result.jumpPressed |= controller.buttonEast.wasPressedThisFrame;
        }
        return result;
    }
}
