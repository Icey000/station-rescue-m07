using UnityEngine;

namespace UnityAgentLab
{
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private RepairGame game;
        private bool requested;
        public void Configure(RepairGame value) { game = value; }
        public void RequestInteract() { if (!GameFlow.IsPaused) requested = true; }
        public void CancelRequests() { requested = false; }
        private void FixedUpdate()
        {
            if (!requested) return;
            requested = false;
            if (game != null) game.TryInteract();
        }
        private void OnDisable() { requested = false; }
    }
}
