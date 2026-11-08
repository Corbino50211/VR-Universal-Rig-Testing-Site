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

        // Heading of a head pose. Stable when looking straight up or down: the head's up
        // vector supplies the heading as the forward vector becomes vertical.
        public static Quaternion Yaw(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 heading = Planar(forward - rotation * Vector3.up * forward.y);
            return heading.sqrMagnitude < 0.000001f ? Quaternion.identity : Quaternion.LookRotation(heading, Vector3.up);
        }

        // Ride-height spring acceleration (excluding gravity support), clamped both ways so a
        // step-up or landing can never launch the body.
        public static float GroundSpring(float error, float relativeVelocity, float spring, float damping,
            float maximumLift, float maximumSnap) =>
            Mathf.Clamp(spring * error - damping * relativeVelocity, -maximumSnap, maximumLift);

        // Climbing: move the body so the tracked controller target meets the locked hand.
        // Includes gravity support; speed and total acceleration are bounded so climbing can't
        // pump energy or launch the player.
        public static Vector3 ClimbAcceleration(Vector3 handMinusTarget, Vector3 roomVelocity, Vector3 velocity,
            Vector3 gravity, float gain, float maximumSpeed, float maximumAcceleration, float dt)
        {
            Vector3 desired = Vector3.ClampMagnitude(handMinusTarget * gain, maximumSpeed) + roomVelocity;
            return Vector3.ClampMagnitude((desired - velocity) / dt - gravity, maximumAcceleration);
        }

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

        // Impulse along direction, clamped so a shot cannot exceed a known momentum budget.
        public static Vector3 BoundedImpulse(Vector3 direction, float strength, float maximum)
        {
            float mag = direction.magnitude;
            if (mag < 0.000001f || maximum <= 0f || strength <= 0f) return Vector3.zero;
            return direction / mag * Mathf.Min(strength, maximum);
        }

        // Direction inside a cone. radius01/turn01 are 0..1; zero degrees stays on forward.
        public static Vector3 ConeDirection(Vector3 forward, float degrees, float radius01, float turn01)
        {
            forward = forward.sqrMagnitude < 0.000001f ? Vector3.forward : forward.normalized;
            float radius = Mathf.Tan(Mathf.Max(0f, degrees) * Mathf.Deg2Rad) * Mathf.Sqrt(Mathf.Clamp01(radius01));
            float turn = Mathf.Clamp01(turn01) * (Mathf.PI * 2f);
            Vector3 axis = Mathf.Abs(forward.y) < 0.99f ? Vector3.up : Vector3.right;
            Vector3 side = Vector3.Cross(forward, axis).normalized;
            Vector3 up = Vector3.Cross(side, forward);
            return (forward + (side * Mathf.Cos(turn) + up * Mathf.Sin(turn)) * radius).normalized;
        }

        // First non-ignored hit. Obstructed when that hit is at or behind the muzzle
        // (distance from a point behind the muzzle is within backoff + slack), so a buried
        // muzzle cannot select a target past the contact.
        public static int SelectShotHit(float[] distances, bool[] ignored, int count, float backoff, float slack,
            out bool obstructed)
        {
            obstructed = false;
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            int n = Mathf.Min(count, distances == null ? 0 : distances.Length);
            for (int i = 0; i < n; ++i)
            {
                if (ignored != null && i < ignored.Length && ignored[i]) continue;
                if (distances[i] < 0f || distances[i] >= bestDistance) continue;
                bestDistance = distances[i];
                best = i;
            }
            if (best >= 0) obstructed = bestDistance <= backoff + Mathf.Max(0f, slack);
            return best;
        }
    }
}
