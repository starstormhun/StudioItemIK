using UnityEngine;

namespace StudioItemIK {
    internal static class Extensions {
#if KK
        public static bool TryGetComponent<T>(this Component comp, out T result) {
            result = comp.GetComponent<T>();
            return result != null;
        }

        public static Vector3 ClosestPointOnPlane(this Plane plane, Vector3 point) {
            var dist = plane.GetDistanceToPoint(point);
            return point - plane.normal * dist;
        }

        public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) {
            Vector3 rotaxis = Vector3.Cross(from, to);
            from = Vector3.ProjectOnPlane(from, rotaxis);
            to = Vector3.ProjectOnPlane(to, rotaxis);
            float angle = Vector3.Angle(from, to);
            if ((Quaternion.AngleAxis(angle, rotaxis) * from).normalized == to.normalized) {
                return Vector3.Dot(rotaxis, axis) < 0 ? -angle : angle;
            } else {
                return Vector3.Dot(rotaxis, axis) < 0 ? angle : -angle;
            }
        }
#endif
    }
}
