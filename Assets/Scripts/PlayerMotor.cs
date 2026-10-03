using UnityEngine;

namespace UnityAgentLab
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField, Min(0f)] private float boostImpulse = 5.5f;
        [SerializeField, Min(0f)] private float boostCooldown = 1.1f;
        [SerializeField, Min(0f)] private float boostDecay = 7f;
        [SerializeField, Min(0f)] private float maxPlanarSpeed = 11.5f;
        private Rigidbody body;
        private Vector2 moveInput, lastDirection = Vector2.up;
        private Vector3 boostVelocity;
        private bool boostRequested;
        private float cooldownRemaining, stunRemaining, boostVisualRemaining;
        public event System.Action Boosted;
        public float CooldownRemaining => cooldownRemaining;
        public float CooldownDuration => boostCooldown;
        public float StunRemaining => stunRemaining;
        public float BoostVisualRemaining => boostVisualRemaining;
        public float PlanarSpeed => body == null ? 0 : new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        public Vector3 FacingDirection => new Vector3(lastDirection.x, 0, lastDirection.y);
        public bool HasMovementInput => moveInput.sqrMagnitude > 0.001f;
        public int BoostsUsed { get; private set; }
        public void RequestBoost() { if (!GameFlow.IsPaused && stunRemaining <= 0) boostRequested = true; }
        public void CancelRequests() { boostRequested = false; }
        public void ResetMotion()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            moveInput = Vector2.zero; lastDirection = Vector2.up; boostVelocity = Vector3.zero;
            boostRequested = false; cooldownRemaining = stunRemaining = boostVisualRemaining = 0;
            BoostsUsed = 0; body.linearVelocity = body.angularVelocity = Vector3.zero;
        }
        public void Stun(float duration)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            stunRemaining = Mathf.Max(stunRemaining, duration);
            boostVelocity = Vector3.zero; boostVisualRemaining = 0; CancelRequests();
            Vector3 velocity = body.linearVelocity; velocity.x = velocity.z = 0;
            body.linearVelocity = velocity; body.angularVelocity = Vector3.zero;
        }
        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
            if (HasMovementInput) lastDirection = moveInput.normalized;
        }
        private void Awake() { body = GetComponent<Rigidbody>(); }
        private void FixedUpdate() { Step(Time.fixedDeltaTime); }
        public void Step(float deltaTime)
        {
            if (deltaTime <= 0 || GameFlow.IsPaused) return;
            if (body == null) body = GetComponent<Rigidbody>();
            cooldownRemaining = Mathf.Max(0, cooldownRemaining - deltaTime);
            boostVisualRemaining = Mathf.Max(0, boostVisualRemaining - deltaTime);
            if (stunRemaining > 0)
            {
                stunRemaining = Mathf.Max(0, stunRemaining - deltaTime); CancelRequests(); return;
            }
            boostVelocity = Vector3.MoveTowards(boostVelocity, Vector3.zero, boostDecay * deltaTime);
            Vector3 target = Vector3.ClampMagnitude(new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed + boostVelocity, maxPlanarSpeed);
            Vector3 current = body.linearVelocity; current.y = 0;
            Vector3 change = Vector3.ClampMagnitude(target - current, acceleration * deltaTime);
            body.AddForce(change, ForceMode.VelocityChange);
            Vector3 predicted = current + change;
            bool request = boostRequested; boostRequested = false;
            if (request && cooldownRemaining <= 0)
            {
                Vector3 impulse = FacingDirection * boostImpulse;
                body.AddForce(impulse, ForceMode.Impulse);
                boostVelocity = Vector3.ClampMagnitude(boostVelocity + impulse / body.mass, maxPlanarSpeed);
                predicted += impulse / body.mass;
                cooldownRemaining = boostCooldown; boostVisualRemaining = 0.5f; BoostsUsed++; Boosted?.Invoke();
            }
            // Apply a compensating force rather than changing the vertical collision response.
            Vector3 correction = Vector3.ClampMagnitude(predicted, maxPlanarSpeed) - predicted;
            if (correction.sqrMagnitude > 0.000001f) body.AddForce(correction, ForceMode.VelocityChange);
        }
        private void OnDisable() { moveInput = Vector2.zero; boostVisualRemaining = 0; CancelRequests(); }
    }
}
