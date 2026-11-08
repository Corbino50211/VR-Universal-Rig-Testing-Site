using UnityEngine;

namespace UniversalRig
{
    [CreateAssetMenu(menuName = "Universal Rig/Rig Settings")]
    public sealed class RigSettings : ScriptableObject
    {
        [Header("Fixed avatar dimensions (metres)")]
        [Min(0.5f)] public float standingEyeHeight = 1.65f;
        [Min(0.1f)] public float bodyRadius = 0.2f;
        [Min(0.4f)] public float minimumHeight = 0.7f;
        [Min(1f)] public float maximumHeight = 2.1f;
        [Min(0f)] public float headClearance = 0.1f;
        [Min(1f)] public float bodyMass = 80f;

        [Header("Locomotion")]
        [Min(0f)] public float walkSpeed = 2.5f;
        [Min(0f)] public float acceleration = 15f;
        [Min(0f)] public float airAcceleration = 3f;
        [Min(0f)] public float roomFollowSpeed = 3f;
        [Min(0f)] public float roomFollowGain = 8f;
        [Min(0f)] public float jumpHeight = 0.8f;
        [Range(0f, 80f)] public float maximumSlope = 50f;
        [Min(0.01f)] public float groundProbeDistance = 0.08f;
        [Min(0f)] public float smoothTurnSpeed = 90f;
        public bool snapTurn = false;
        [Range(15f, 90f)] public float snapAngle = 30f;
        [Range(0f, 0.5f)] public float stickDeadzone = 0.15f;

        [Header("Physical hands: bounded force/torque, not target teleportation")]
        [Min(0.1f)] public float handMass = 1f;
        [Min(0f)] public float handPositionSpring = 650f;
        [Min(0f)] public float handPositionDamping = 45f;
        [Min(0f)] public float maximumHandForce = 180f;
        [Tooltip("Angular acceleration gain; converted to torque using the hand's inertia tensor.")]
        [Min(0f)] public float handRotationSpring = 120f;
        [Min(0f)] public float handRotationDamping = 22f;
        [Min(0f)] public float maximumHandTorque = 25f;
        [Min(0.01f)] public float maximumPositionError = 0.35f;
        [Min(0.01f)] public float grabRadius = 0.12f;
        [Min(0f)] public float grabBreakForce = 1200f;
        [Min(0f)] public float grabBreakTorque = 120f;
        [Range(0f, 1f)] public float gripPress = 0.65f;
        [Range(0f, 1f)] public float gripRelease = 0.35f;

        [Header("Queries: builder excludes only this rig's physics layer")]
        public LayerMask environmentMask = ~0;
        public LayerMask grabbableMask = ~0;

        private void OnValidate()
        {
            minimumHeight = Mathf.Max(minimumHeight, bodyRadius * 2f);
            maximumHeight = Mathf.Max(maximumHeight, minimumHeight);
            gripRelease = Mathf.Min(gripRelease, gripPress);
        }
    }
}
