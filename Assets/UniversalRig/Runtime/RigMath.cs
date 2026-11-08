using UnityEngine;

namespace UniversalRig
{
    public static class RigMath
    {
        // Shortest-path, world-space rotation error in radians; q and -q are equivalent.
        public static Vector3 RotationError(Quaternion target, Quaternion current)
        {
            Quaternion q = (target * Quaternion.Inverse(current)).normalized;
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            Vector3 v = new Vector3(q.x, q.y, q.z);
            float length = v.magnitude;
            return length < 0.000001f ? Vector3.zero : v * (2f * Mathf.Atan2(length, q.w) / length);
        }

        public static Vector3 Planar(Vector3 value) => Vector3.ProjectOnPlane(value, Vector3.up);

        // Remove only room-scale motion the body actually completed. A blocked body must
        // never drag the tracking origin backwards by an unfulfilled movement request.
        public static Vector3 CompletedRoomStep(Vector3 actualStep, Vector3 requestedStep)
        {
            float length = requestedStep.magnitude;
            if (length < 0.000001f) return Vector3.zero;
            Vector3 direction = requestedStep / length;
            return direction * Mathf.Clamp(Vector3.Dot(actualStep, direction), 0f, length);
        }

        // Law-of-cosines elbow/knee position with a deterministic pole fallback.
        public static Vector3 SolveBend(Vector3 root, Vector3 target, Vector3 pole,
            float upperLength, float lowerLength, out Vector3 reachableTarget)
        {
            upperLength = Mathf.Max(0.001f, upperLength);
            lowerLength = Mathf.Max(0.001f, lowerLength);
            Vector3 delta = target - root;
            Vector3 direction = delta.sqrMagnitude < 0.000001f ? Vector3.forward : delta.normalized;
            float min = Mathf.Abs(upperLength - lowerLength) + 0.0001f;
            float max = upperLength + lowerLength - 0.0001f;
            float distance = Mathf.Clamp(delta.magnitude, min, max);
            reachableTarget = root + direction * distance;
            Vector3 bend = Vector3.ProjectOnPlane(pole - root, direction);
            if (bend.sqrMagnitude < 0.000001f)
                bend = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            return root + direction * along + bend.normalized * height;
        }
    }
}
