using UnityEngine;

namespace UniversalRig
{
    // One asset per game. The "Fixed avatar" block is measured from the avatar skeleton by
    // Tools/Universal Rig/Fit Avatar (or the scene builder); change avatarModel and re-run it
    // to use a different fixed avatar in another game. Everything else is tuning.
    [CreateAssetMenu(menuName = "Universal Rig/Rig Settings")]
    public sealed class RigSettings : ScriptableObject
    {
        [Header("Fixed avatar (measured by Fit Avatar; positions are avatar-root space at rest)")]
        [Tooltip("Humanoid model used by the editor builder/fitter. Not used for runtime switching.")]
        public GameObject avatarModel;
        [Min(0.5f)] public float standingEyeHeight = 1.65f;
        public Vector3 eyePosition = new Vector3(0f, 1.65f, 0.1f);
        public Vector3 headBonePosition = new Vector3(0f, 1.55f, 0f);
        public Vector3 leftShoulderPosition = new Vector3(-0.19f, 1.43f, -0.05f);
        public Vector3 rightShoulderPosition = new Vector3(0.19f, 1.43f, -0.05f);
        [Min(0.1f)] public float hipHeight = 0.95f;
        [Tooltip("Shoulder-to-wrist length. The physical reach limit and the visual arm both use it.")]
        [Min(0.2f)] public float armLength = 0.5f;
        [Tooltip("Wrist position in the OpenXR grip-pose frame (+Z toward thumb side along the fist tube).")]
        public Vector3 leftWristOffset = new Vector3(-0.03f, 0.08f, 0f);
        public Vector3 rightWristOffset = new Vector3(0.03f, 0.08f, 0f);
        [Tooltip("Hand bone rotation expressed in the grip-pose frame.")]
        public Quaternion leftHandBoneRotation = Quaternion.identity;
        public Quaternion rightHandBoneRotation = Quaternion.identity;
        public Vector3 handColliderCenter = new Vector3(0f, 0.02f, 0f);
        public Vector3 handColliderSize = new Vector3(0.05f, 0.1f, 0.09f);

        [Header("Body")]
        [Min(0.1f)] public float bodyRadius = 0.2f;
        [Min(0.4f)] public float minimumHeight = 0.7f;
        [Min(1f)] public float maximumHeight = 2.1f;
        [Min(0f)] public float headClearance = 0.1f;
        [Min(1f)] public float bodyMass = 80f;
        [Tooltip("The capsule floats this high on a ground spring; steps lower than this are walked over.")]
        [Range(0.1f, 0.45f)] public float stepHeight = 0.3f;
        [Min(0f)] public float groundSpring = 300f;
        [Min(0f)] public float groundDamping = 32f;
        [Tooltip("Maximum upward spring acceleration beyond gravity support (m/s^2).")]
        [Min(0f)] public float maximumGroundLift = 60f;
        [Tooltip("Maximum downward ground-snap acceleration (m/s^2).")]
        [Min(0f)] public float maximumGroundSnap = 20f;
        [Min(0f)] public float groundSnapDistance = 0.15f;

        [Header("Locomotion")]
        [Min(0f)] public float walkSpeed = 2.5f;
        [Min(0f)] public float acceleration = 15f;
        [Min(0f)] public float airAcceleration = 3f;
        [Min(0f)] public float roomFollowSpeed = 3f;
        [Min(0f)] public float roomFollowGain = 8f;
        [Min(0f)] public float jumpHeight = 0.8f;
        [Range(0f, 80f)] public float maximumSlope = 50f;
        [Min(0f)] public float smoothTurnSpeed = 90f;
        public bool snapTurn = false;
        [Range(15f, 90f)] public float snapAngle = 30f;
        [Range(0f, 0.5f)] public float stickDeadzone = 0.15f;
        [Tooltip("Fade when the head is further than this from the body's neck position.")]
        [Min(0.1f)] public float maximumHeadSeparation = 0.5f;

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
        [Tooltip("Fraction of a loaded hand's drive force pushed back into the body (Newton's third law).")]
        [Range(0f, 1f)] public float handReaction = 1f;
        [Min(0.01f)] public float grabRadius = 0.12f;
        [Min(0f)] public float grabBreakForce = 1200f;
        [Min(0f)] public float grabBreakTorque = 120f;
        [Range(0f, 1f)] public float gripPress = 0.65f;
        [Range(0f, 1f)] public float gripRelease = 0.35f;

        [Header("Climbing (Climbable surfaces only)")]
        [Min(0f)] public float climbGain = 12f;
        [Tooltip("Body speed limit while climbing; also bounds the speed carried into a release.")]
        [Min(0f)] public float maximumClimbSpeed = 2.5f;
        [Tooltip("Total climb acceleration limit including gravity support (m/s^2).")]
        [Min(10f)] public float maximumClimbAcceleration = 30f;

        [Header("Queries: builder excludes only this rig's physics layer")]
        public LayerMask environmentMask = ~0;
        public LayerMask grabbableMask = ~0;

        // Head bone and shoulder positions derived from the camera pose. The physical reach
        // anchor and the visual avatar both use these, so physical and visible arms agree.
        public Vector3 HeadBoneFromEye(Vector3 eye, Quaternion headRotation) =>
            eye - headRotation * (eyePosition - headBonePosition);

        public Vector3 ShoulderFromEye(Vector3 eye, Quaternion headRotation, bool left)
        {
            Quaternion yaw = RigMath.Yaw(headRotation);
            Vector3 shoulder = left ? leftShoulderPosition : rightShoulderPosition;
            return HeadBoneFromEye(eye, headRotation) + yaw * (shoulder - headBonePosition);
        }

        // Feet/body position implied by the head pose (below the neck, not the eyes).
        public Vector3 RootFromEye(Vector3 eye, Quaternion headRotation) =>
            HeadBoneFromEye(eye, headRotation) - RigMath.Yaw(headRotation) * headBonePosition;

        private void OnValidate()
        {
            minimumHeight = Mathf.Max(minimumHeight, stepHeight + bodyRadius * 2f + 0.05f);
            maximumHeight = Mathf.Max(maximumHeight, minimumHeight);
            gripRelease = Mathf.Min(gripRelease, gripPress);
        }
    }
}
