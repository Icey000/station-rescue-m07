using UnityEngine;
namespace UnityAgentLab
{
    public sealed class PowerRouteController : MonoBehaviour
    {
        [SerializeField] private LineRenderer[] routes;
        private Vector3[][] paths;
        private float[] lengths;
        public int Selected { get; private set; } = -1;
        public float Progress { get; private set; }
        public void Configure(LineRenderer[] lines) { routes = lines; CapturePaths(); ResetRoutes(); }
        private void Awake() { CapturePaths(); ResetRoutes(); }
        private void CapturePaths()
        {
            if (routes == null) return;
            paths = new Vector3[routes.Length][]; lengths = new float[routes.Length];
            for (int i = 0; i < routes.Length; i++)
            {
                paths[i] = new Vector3[routes[i].positionCount]; routes[i].GetPositions(paths[i]);
                for (int j = 1; j < paths[i].Length; j++) lengths[i] += Vector3.Distance(paths[i][j - 1], paths[i][j]);
            }
        }
        public void ResetRoutes()
        {
            Selected = -1; Progress = 0;
            if (routes != null) foreach (LineRenderer line in routes) line.enabled = false;
        }
        public void SetProgress(int selected, float progress)
        {
            if (paths == null) CapturePaths();
            Selected = selected; Progress = Mathf.Clamp01(progress);
            if (routes == null) return;
            for (int i = 0; i < routes.Length; i++)
            {
                LineRenderer line = routes[i]; line.enabled = i == selected && Progress > 0;
                if (!line.enabled) continue;
                float remaining = lengths[i] * Progress;
                var visible = new System.Collections.Generic.List<Vector3> { paths[i][0] };
                for (int j = 1; j < paths[i].Length; j++)
                {
                    float segment = Vector3.Distance(paths[i][j - 1], paths[i][j]);
                    if (remaining >= segment) { visible.Add(paths[i][j]); remaining -= segment; }
                    else { visible.Add(Vector3.Lerp(paths[i][j - 1], paths[i][j], segment == 0 ? 1 : remaining / segment)); break; }
                }
                if (visible.Count < 2) visible.Add(visible[0]);
                line.positionCount = visible.Count; line.SetPositions(visible.ToArray());
            }
        }
    }
}
