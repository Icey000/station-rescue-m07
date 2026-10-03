using UnityEngine;
namespace UnityAgentLab
{
    public sealed class DockGate : MonoBehaviour
    {
        [SerializeField] private string dockName;
        [SerializeField] private Transform leftDoor, rightDoor, boardingAnchor;
        [SerializeField] private Renderer[] lights;
        [SerializeField] private GameObject boardingBeam;
        private float opening;
        private bool powered;
        private MaterialPropertyBlock properties;
        public string DockName => LanguageService.Text(dockName.StartsWith("A") ? "dock.a" : dockName.StartsWith("B") ? "dock.b" : "dock.c");
        public Transform BoardingAnchor => boardingAnchor == null ? transform : boardingAnchor;
        public bool IsOpen => powered && opening >= 0.999f;
        public float Opening => opening;
        public void Configure(string name, Transform left, Transform right, Renderer[] lamps, GameObject beacon)
        { dockName = name; leftDoor = left; rightDoor = right; lights = lamps; boardingBeam = beacon; }
        public void SetBoardingAnchor(Transform value) { boardingAnchor = value; }
        public void SetPowered(bool value, bool selected)
        {
            powered = value && selected;
            if (!powered) opening = 0;
            Apply();
        }
        public void StepAnimation(float deltaTime)
        {
            opening = Mathf.MoveTowards(opening, powered ? 1 : 0, deltaTime * 2);
            Apply();
        }
        private void Apply()
        {
            if (boardingBeam != null) boardingBeam.SetActive(IsOpen);
            if (leftDoor != null) leftDoor.localPosition = new Vector3(-0.48f - opening * 0.92f, 1.05f, 0);
            if (rightDoor != null) rightDoor.localPosition = new Vector3(0.48f + opening * 0.92f, 1.05f, 0);
            foreach (Transform door in new[] { leftDoor, rightDoor })
                if (door != null && door.TryGetComponent(out Collider collider)) collider.enabled = !IsOpen;
            if (properties == null) properties = new MaterialPropertyBlock();
            Color color = IsOpen ? new Color(0.12f, 1, 0.6f) : new Color(0.9f, 0.19f, 0.08f);
            properties.SetColor("_BaseColor", color); properties.SetColor("_EmissionColor", color * (IsOpen ? 3 : 1.4f));
            if (lights != null) foreach (Renderer lamp in lights) if (lamp != null) lamp.SetPropertyBlock(properties);
        }
    }
}
