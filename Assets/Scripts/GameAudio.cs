using UnityEngine;

namespace UnityAgentLab
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameAudio : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private AudioClip[] clips;
        private AudioSource source;
        public void Configure(PlayerMotor value, AudioClip[] sounds) { motor = value; clips = sounds; }
        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0f; source.volume = 0.35f;
        }
        private void Start() { if (motor != null) motor.Boosted += Boost; }
        private void Boost() { Play(0); }
        public void Play(int index)
        {
            if (source != null && clips != null && index < clips.Length && clips[index] != null)
                source.PlayOneShot(clips[index]);
        }
        private void OnDestroy() { if (motor != null) motor.Boosted -= Boost; }
    }
}
