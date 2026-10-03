using UnityEngine;
namespace UnityAgentLab
{
    public sealed class BoostVisual : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private ParticleSystem exhaust;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Light lamp;
        [SerializeField] private Renderer[] nozzles;
        private MaterialPropertyBlock block;
        public bool IsThrustVisible { get; private set; }
        public int Pulses { get; private set; }
        public void Configure(PlayerMotor value, ParticleSystem particles, TrailRenderer streak, Light glow, Renderer[] outlets)
        { motor = value; exhaust = particles; trail = streak; lamp = glow; nozzles = outlets; }
        private void OnEnable() { if (motor != null) motor.Boosted += Pulse; }
        private void Pulse() { Pulses++; }
        private void LateUpdate()
        {
            IsThrustVisible = motor != null && motor.BoostVisualRemaining > 0 && motor.StunRemaining <= 0;
            float strength = IsThrustVisible ? Mathf.Clamp01(motor.BoostVisualRemaining / 0.18f) : 0;
            if (exhaust != null)
            {
                var emission = exhaust.emission; emission.rateOverTime = 65 * strength;
                if (IsThrustVisible && !exhaust.isPlaying) exhaust.Play();
                if (!IsThrustVisible && exhaust.isPlaying) exhaust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (trail != null) trail.emitting = IsThrustVisible;
            if (lamp != null) lamp.intensity = strength * 3;
            if (block == null) block = new MaterialPropertyBlock();
            Color color = new Color(0.05f, 0.65f, 1);
            block.SetColor("_BaseColor", color);
            block.SetColor("_EmissionColor", color * (0.15f + strength * 4));
            if (nozzles != null) foreach (Renderer nozzle in nozzles) if (nozzle != null) nozzle.SetPropertyBlock(block);
        }
        private void OnDisable()
        {
            if (motor != null) motor.Boosted -= Pulse;
            if (exhaust != null) exhaust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (trail != null) { trail.emitting = false; trail.Clear(); }
            if (lamp != null) lamp.intensity = 0;
            IsThrustVisible = false;
        }
    }
}
