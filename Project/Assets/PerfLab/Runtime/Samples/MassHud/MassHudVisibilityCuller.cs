using UnityEngine;

namespace PerfLab.Samples.MassHud
{
    internal sealed class MassHudVisibilityCuller : MonoBehaviour
    {
        private readonly Plane[] _planes = new Plane[6];
        private Camera _camera;
        private Vector3[] _anchors;
        private bool[] _visible;

        public int Version { get; private set; }

        public void Initialize(Camera targetCamera, Vector3[] anchors)
        {
            _camera = targetCamera;
            _anchors = anchors;
            _visible = new bool[anchors.Length];
            Refresh();
        }

        public bool IsVisible(int hudIndex) => _visible[hudIndex];

        private void Update() => Refresh();

        private void Refresh()
        {
            GeometryUtility.CalculateFrustumPlanes(_camera, _planes);
            bool changed = false;
            for (int i = 0; i < _anchors.Length; i++)
            {
                bool visible = ContainsHud(_anchors[i]);
                changed |= visible != _visible[i];
                _visible[i] = visible;
            }
            if (changed) Version++;
        }

        private bool ContainsHud(Vector3 anchor)
        {
            const float radius = 1.45f;
            for (int i = 0; i < _planes.Length; i++)
                if (_planes[i].GetDistanceToPoint(anchor) < -radius) return false;
            return true;
        }
    }
}
