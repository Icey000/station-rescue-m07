using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityAgentLab
{
    /// <summary>Keyboard adapter; future agent input can use the same motor.</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class HumanInput : MonoBehaviour
    {
        private PlayerMotor motor;
        private PlayerInteraction interaction;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            interaction = GetComponent<PlayerInteraction>();
        }

        private void Update()
        {
            if (GameFlow.IsPaused) { motor.SetMoveInput(Vector2.zero); motor.CancelRequests(); interaction?.CancelRequests(); return; }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                motor.SetMoveInput(Vector2.zero);
                return;
            }

            bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            bool forward = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
            bool back = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
            motor.SetMoveInput(new Vector2(
                (right ? 1f : 0f) - (left ? 1f : 0f),
                (forward ? 1f : 0f) - (back ? 1f : 0f)));
            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
                motor.RequestBoost();
            if (keyboard.eKey.wasPressedThisFrame && interaction != null)
                interaction.RequestInteract();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && motor != null)
            {
                motor.SetMoveInput(Vector2.zero);
                motor.CancelRequests();
                if (interaction != null) interaction.CancelRequests();
            }
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.SetMoveInput(Vector2.zero);
                motor.CancelRequests();
            }
            if (interaction != null) interaction.CancelRequests();
        }
    }
}
