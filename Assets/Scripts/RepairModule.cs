using UnityEngine;

namespace UnityAgentLab
{
    public enum ModuleKind { BlueCircle = 0, RedTriangle = 1 }
    public enum ModuleState { OnShelf = 0, Carried = 1, Installed = 2, Dropped = 3, Returning = 4 }
    public sealed class RepairModule : MonoBehaviour
    {
        [SerializeField] private ModuleKind kind;
        private Vector3 home, returnStart, returnEntry;
        private Quaternion homeRotation;
        private Transform carrier;
        private Rigidbody body;
        private Collider solid;
        private float returnElapsed;
        public ModuleKind Kind => kind;
        public ModuleState State { get; private set; }
        public string UniqueId { get; private set; }
        public void Configure(ModuleKind value) { kind = value; }
        public void Initialize(string id)
        {
            UniqueId = id; home = transform.position; homeRotation = transform.rotation;
            body = GetComponent<Rigidbody>(); solid = GetComponent<Collider>();
            ReturnHome();
        }
        private void PhysicsMode(bool dropped)
        {
            if (solid != null) solid.isTrigger = !dropped;
            if (body == null) return;
            body.isKinematic = !dropped;
            if (dropped) body.WakeUp();
        }
        public bool PickUp(Transform player)
        {
            if ((State != ModuleState.OnShelf && State != ModuleState.Dropped) || player == null) return false;
            PhysicsMode(false); State = ModuleState.Carried; carrier = player; return true;
        }
        public void Drop(Vector3 position)
        {
            carrier = null; State = ModuleState.Dropped;
            transform.SetPositionAndRotation(position, homeRotation);
            if (body != null)
            {
                body.position = position; body.rotation = homeRotation;
                PhysicsMode(true); body.linearVelocity = new Vector3(0, -1, 0); body.angularVelocity = Vector3.zero;
                Collider actor = FindFirstObjectByType<PlayerMotor>()?.GetComponent<Collider>();
                if (actor != null && solid != null) Physics.IgnoreCollision(actor, solid);
            }
        }
        public void BeginReturn(Vector3 entry)
        {
            carrier = null; PhysicsMode(false); State = ModuleState.Returning;
            returnStart = transform.position; returnEntry = entry; returnElapsed = 0;
        }
        public void ReturnHome()
        {
            carrier = null; PhysicsMode(false); State = ModuleState.OnShelf;
            transform.SetPositionAndRotation(home, homeRotation);
        }
        public void InstallAt(Vector3 socket)
        {
            carrier = null; PhysicsMode(false); State = ModuleState.Installed;
            transform.SetPositionAndRotation(socket, homeRotation);
        }
        public void StepReturn(float deltaTime)
        {
            if (State != ModuleState.Returning) return;
            returnElapsed += deltaTime;
            transform.position = Vector3.Lerp(returnStart, returnEntry, Mathf.SmoothStep(0, 1, returnElapsed / 0.5f));
            if (returnElapsed >= 0.7f) ReturnHome();
        }
        private void LateUpdate()
        {
            if (State == ModuleState.Carried && carrier != null)
                transform.SetPositionAndRotation(carrier.position + Vector3.up * 1.35f, homeRotation);
            StepReturn(Time.deltaTime);
        }
    }
}
