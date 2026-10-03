using UnityEngine;

namespace UnityAgentLab
{
    public sealed class ModuleSpawner : MonoBehaviour
    {
        [SerializeField] private RepairModule bluePrefab;
        [SerializeField] private RepairModule redPrefab;
        [SerializeField] private Transform blueShelf;
        [SerializeField] private Transform redShelf;
        public RepairModule[] Modules { get; private set; }
        public void Configure(RepairModule blue, RepairModule red, Transform bluePoint, Transform redPoint)
        {
            bluePrefab = blue; redPrefab = red; blueShelf = bluePoint; redShelf = redPoint;
        }
        public void SetHomes(Transform bluePoint, Transform orangePoint)
        {
            if (Modules != null) throw new System.InvalidOperationException("Choose homes before spawning.");
            blueShelf = bluePoint; redShelf = orangePoint;
        }
        public void SpawnIfNeeded()
        {
            if (Modules != null) return;
            Modules = new[] { Spawn(bluePrefab, blueShelf, "blue-01"),
                Spawn(redPrefab, redShelf, "red-01") };
        }
        private RepairModule Spawn(RepairModule prefab, Transform shelf, string id)
        {
            RepairModule module = Instantiate(prefab, shelf.position, prefab.transform.rotation, transform);
            module.name = id;
            module.Initialize(id);
            return module;
        }
    }
}
