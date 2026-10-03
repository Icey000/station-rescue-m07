using UnityEngine;

namespace UnityAgentLab
{
    public sealed class PowerStationVisual : MonoBehaviour
    {
        [SerializeField] private GameObject roundSocket, squareSocket;
        [SerializeField] private Renderer[] lamps;
        [SerializeField] private Transform turbine;
        [SerializeField] private Light powerGlow;
        public bool Powered { get; private set; }
        public void Configure(GameObject round, GameObject square, Renderer[] indicators, Transform rotor)
        { roundSocket = round; squareSocket = square; lamps = indicators; turbine = rotor; }
        public void ConfigureGlow(Light value) { powerGlow = value; }
        public void SetRequest(ModuleKind kind)
        {
            if (roundSocket != null) roundSocket.SetActive(kind == ModuleKind.BlueCircle);
            if (squareSocket != null) squareSocket.SetActive(kind == ModuleKind.RedTriangle);
        }
        public void SetPowered(bool value)
        {
            Powered = value;
            if (powerGlow != null) powerGlow.intensity = value ? 2.6f : 0;
            var block = new MaterialPropertyBlock();
            Color color = value ? new Color(0.1f, 1f, 0.62f) : new Color(0.95f, 0.35f, 0.08f);
            block.SetColor("_BaseColor", color); block.SetColor("_EmissionColor", color * (value ? 3f : 0.8f));
            foreach (Renderer lamp in lamps) lamp.SetPropertyBlock(block);
        }
        private void Update()
        { if (Powered && turbine != null) turbine.Rotate(0, Time.deltaTime * 140, 0, Space.Self); }
    }
}
