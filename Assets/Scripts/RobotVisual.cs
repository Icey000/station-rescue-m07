using UnityEngine;
namespace UnityAgentLab
{
    public sealed class RobotVisual : MonoBehaviour
    {
        [SerializeField] private Transform actor, magnet;
        [SerializeField] private RepairGame game;
        private PlayerMotor motor;
        public Vector3 Facing => transform.forward;
        public void Configure(Transform player, Transform coil, RepairGame controller)
        { actor = player; magnet = coil; game = controller; motor = actor.GetComponent<PlayerMotor>(); }
        private void Awake() { if (actor != null) motor = actor.GetComponent<PlayerMotor>(); }
        public void StepVisual(float deltaTime)
        {
            if (actor == null) return;
            if (motor == null) motor = actor.GetComponent<PlayerMotor>();
            transform.position = actor.position + Vector3.up * (0.015f * Mathf.Sin(Time.time * 4));
            if (motor != null && (motor.HasMovementInput || motor.BoostVisualRemaining > 0))
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(motor.FacingDirection, Vector3.up), 540 * deltaTime);
            if (magnet != null) magnet.localRotation = Quaternion.Euler(0,
                game != null && game.Held != null ? Mathf.Sin(Time.time * 3) * 5 : 0, 0);
        }
        private void LateUpdate() { StepVisual(Time.deltaTime); }
    }
}
