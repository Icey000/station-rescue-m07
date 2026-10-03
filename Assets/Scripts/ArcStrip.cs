using UnityEngine;
namespace UnityAgentLab
{
    public sealed class ArcStrip : MonoBehaviour
    {
        [SerializeField] private RepairGame game;
        [SerializeField] private Vector2 size = new Vector2(2, 6);
        [SerializeField] private Renderer floor;
        [SerializeField] private GameObject arcs;
        [SerializeField] private float phaseOffset;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private Light glow;
        [SerializeField] private AudioSource crackle;
        [SerializeField] private Renderer[] electrodes;
        private LineRenderer[] bolts;
        private Vector3[][] restingPaths;
        private MaterialPropertyBlock properties;
        public const float Cycle = 4.5f, OnDuration = 2f;
        public bool IsLive => (game == null || game.Phase == RepairPhase.NotInstalled) && IsLiveAt(game == null ? 0 : game.Elapsed);
        public bool IsLiveAt(float time) => Mathf.Repeat(time + phaseOffset, Cycle) < OnDuration;
        public float SafeSeconds => IsLive ? 0 : Cycle - Mathf.Repeat(game == null ? 0 : game.Elapsed + phaseOffset, Cycle);
        public Vector2 Size => size;
        public void Configure(RepairGame controller, Vector2 extent, Renderer surface, GameObject lines, float offset)
        { game = controller; size = extent; floor = surface; arcs = lines; phaseOffset = offset; }
        public void ConfigureEffects(ParticleSystem particles, Light light, AudioSource sound)
        {
            sparks = particles; glow = light; crackle = sound;
            var heads = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) if (renderer.name == "ElectrodeHead") heads.Add(renderer);
            electrodes = heads.ToArray();
        }
        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;
        }
        private void Awake()
        {
            if (arcs == null) return;
            bolts = arcs.GetComponentsInChildren<LineRenderer>(true); restingPaths = new Vector3[bolts.Length][];
            for (int i = 0; i < bolts.Length; i++)
            { restingPaths[i] = new Vector3[bolts[i].positionCount]; bolts[i].GetPositions(restingPaths[i]); }
        }
        private void Update()
        {
            bool live = IsLive;
            if (arcs != null) arcs.SetActive(live);
            if (live && bolts != null)
                for (int i = 0; i < bolts.Length; i++) for (int j = 1; j < restingPaths[i].Length - 1; j++)
                    bolts[i].SetPosition(j, restingPaths[i][j] + Vector3.up * Mathf.Sin(Time.time * 18 + j * 4 + i) * 0.12f);
            if (floor != null)
            {
                if (properties == null) properties = new MaterialPropertyBlock();
                Color color = live ? new Color(1, 0.28f, 0.05f) : new Color(0.13f, 0.23f, 0.27f);
                properties.SetColor("_BaseColor", color); properties.SetColor("_EmissionColor", color * (live ? 0.7f : 0.02f));
                floor.SetPropertyBlock(properties);
            }
            if (glow != null) glow.intensity = live ? 2.5f : 0;
            if (electrodes != null)
            {
                var head = new MaterialPropertyBlock();
                Color color = live ? new Color(.64f, .48f, 1) : new Color(.15f, .2f, .26f);
                head.SetColor("_BaseColor", color); head.SetColor("_EmissionColor", color * (live ? 5 : 0));
                foreach (Renderer renderer in electrodes) if (renderer != null) renderer.SetPropertyBlock(head);
            }
            if (sparks != null)
            {
                if (live && !sparks.isPlaying) sparks.Play();
                if (!live && sparks.isPlaying) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (crackle != null)
            {
                if (live && !GameFlow.IsPaused && !crackle.isPlaying) crackle.Play();
                if ((!live || GameFlow.IsPaused) && crackle.isPlaying) crackle.Stop();
            }
        }
        private void OnDisable() { if (crackle != null) crackle.Stop(); }
    }
}
