using UnityEngine;

namespace UniversalRig
{
    // Authored hand pose on a grabbable. PhysicsHand.TryGrab prefers this over the closest
    // collider point and builds the hold joint with the hand aligned to this transform.
    // The transform's rotation is the OpenXR grip frame used by RigAvatarFitter:
    // +Z pinky-to-index along the fist tube, +Y back toward the wrist.
    public sealed class GripPoint : MonoBehaviour
    {
        public enum Side { Either, Left, Right }
        public enum Role { Primary, Support }

        public Side side = Side.Either;
        public Role role = Role.Primary;
        [Range(0f, 1f)] public float fingerCurl = 1f;
        [Tooltip("When set, the hold joint can slide along actionAxisLocal (gun local space) up to actionTravel.")]
        public bool drivesAction;
        [Tooltip("Moving handle with its own constrained rigidbody; the hand may rotate freely around it.")]
        public bool isActionHandle;
        public Vector3 actionAxisLocal = Vector3.back;
        public float actionTravel = 0.05f;

        public bool Allows(bool isLeft) =>
            side == Side.Either || (isLeft ? side == Side.Left : side == Side.Right);
    }
}
