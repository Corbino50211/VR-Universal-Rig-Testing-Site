using UnityEngine;

namespace UniversalRig
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsHand : MonoBehaviour
    {
        public RigSettings settings;
        public RigInput input;
        public bool isLeft;
        [Tooltip("Controller-local offsets to tune for the actual headset/controller.")]
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public Rigidbody Body { get; private set; }
        public PhysicsGrabbable HeldObject { get; private set; }
        public float TrackingError { get; private set; }
        public float Trigger => !input ? 0f : (isLeft ? input.LeftTrigger : input.RightTrigger);
        private ConfigurableJoint grabJoint;
        private bool jointControlsRotation;
        private readonly Collider[] nearby = new Collider[32];
        private bool gripArmed = true;
        private bool hadTracking;
        private Vector3 previousTarget;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            if (!settings || !input)
            {
                Debug.LogError("Universal Rig: physical hand has missing settings/input.", this);
                enabled = false;
                return;
            }
            Body.mass = settings.handMass;
            Body.useGravity = false;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 25f;
            Body.maxDepenetrationVelocity = 3f;
            Body.solverIterations = 12;
            Body.solverVelocityIterations = 4;
        }

        private void FixedUpdate()
        {
            Transform target = isLeft ? input.leftTarget : input.rightTarget;
            bool tracked = input.HeadTracked && (isLeft ? input.LeftTracked : input.RightTracked) && target;
            if (!tracked)
            {
                Release();
                // Tracking loss removes all pull forces; damp instead of flying to (0,0,0).
                Body.AddForce(-Body.linearVelocity * 12f, ForceMode.Acceleration);
                Body.AddTorque(-Body.angularVelocity * 12f, ForceMode.Acceleration);
                hadTracking = false;
                gripArmed = false;
                return;
            }
            Vector3 targetPosition = target.TransformPoint(positionOffset);
            Quaternion targetRotation = target.rotation * Quaternion.Euler(rotationOffset);
            Vector3 targetVelocity = hadTracking
                ? Vector3.ClampMagnitude((targetPosition - previousTarget) / Time.fixedDeltaTime, 8f) : Vector3.zero;
            previousTarget = targetPosition;
            hadTracking = true;
            Vector3 error = targetPosition - Body.position;
            TrackingError = error.magnitude;
            Vector3 force = Vector3.ClampMagnitude(error, settings.maximumPositionError) * settings.handPositionSpring
                + (targetVelocity - Body.linearVelocity) * settings.handPositionDamping;
            Body.AddForce(Vector3.ClampMagnitude(force, settings.maximumHandForce), ForceMode.Force);
            Vector3 angularAcceleration = RigMath.RotationError(targetRotation, Body.rotation) * settings.handRotationSpring
                - Body.angularVelocity * settings.handRotationDamping;
            Quaternion inertiaRotation = Body.rotation * Body.inertiaTensorRotation;
            Vector3 torque = inertiaRotation * Vector3.Scale(Body.inertiaTensor,
                Quaternion.Inverse(inertiaRotation) * angularAcceleration);
            Body.AddTorque(Vector3.ClampMagnitude(torque, settings.maximumHandTorque), ForceMode.Force);

            float grip = isLeft ? input.LeftGrip : input.RightGrip;
            if (HeldObject && !grabJoint) Release();
            if (HeldObject && grabJoint)
            {
                bool primary = HeldObject.PrimaryHand == this;
                if (primary != jointControlsRotation)
                {
                    Vector3 anchor = grabJoint.anchor;
                    Vector3 connectedAnchor = grabJoint.connectedAnchor;
                    RemoveJoint();
                    // A fresh joint captures the current relative rotation on handover.
                    // Locking the old free joint would restore its original grip rotation.
                    CreateJoint(HeldObject, anchor, connectedAnchor, primary);
                }
            }
            if (grip <= settings.gripRelease) { Release(); gripArmed = true; }
            else if (gripArmed && grip >= settings.gripPress)
            {
                gripArmed = false;
                TryGrab();
            }
        }

        private void TryGrab()
        {
            int count = Physics.OverlapSphereNonAlloc(Body.position, settings.grabRadius, nearby,
                settings.grabbableMask, QueryTriggerInteraction.Ignore);
            PhysicsGrabbable best = null;
            Vector3 bestPoint = Vector3.zero;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; ++i)
            {
                Collider candidate = nearby[i];
                if (!candidate || !candidate.attachedRigidbody) continue;
                PhysicsGrabbable item = candidate.attachedRigidbody.GetComponent<PhysicsGrabbable>();
                if (!item || !item.isActiveAndEnabled || item.Body.isKinematic || item.HoldingHands >= item.maximumHands) continue;
                Vector3 point = candidate.ClosestPoint(Body.position);
                float distance = (point - Body.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                // A proximity sphere alone would allow grabbing through a thin wall.
                if (Physics.Linecast(Body.position, point, out RaycastHit hit,
                    settings.environmentMask, QueryTriggerInteraction.Ignore) && hit.rigidbody != item.Body) continue;
                best = item;
                bestPoint = point;
                bestDistance = distance;
            }
            if (!best || !best.TryHold(this)) return;
            HeldObject = best;
            CreateJoint(best, transform.InverseTransformPoint(bestPoint),
                best.transform.InverseTransformPoint(bestPoint), best.PrimaryHand == this);
        }

        private void CreateJoint(PhysicsGrabbable item, Vector3 anchor, Vector3 connectedAnchor, bool primary)
        {
            grabJoint = gameObject.AddComponent<ConfigurableJoint>();
            grabJoint.connectedBody = item.Body;
            grabJoint.autoConfigureConnectedAnchor = false;
            grabJoint.anchor = anchor;
            grabJoint.connectedAnchor = connectedAnchor;
            grabJoint.xMotion = grabJoint.yMotion = grabJoint.zMotion = ConfigurableJointMotion.Locked;
            // First hand establishes orientation; second supplies position only so two
            // competing wrist orientations cannot over-constrain a long object.
            jointControlsRotation = primary;
            ConfigurableJointMotion angular = primary
                ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
            grabJoint.angularXMotion = grabJoint.angularYMotion = grabJoint.angularZMotion = angular;
            grabJoint.enableCollision = false;
            grabJoint.breakForce = settings.grabBreakForce;
            grabJoint.breakTorque = settings.grabBreakTorque;
            grabJoint.projectionMode = JointProjectionMode.None;
        }

        public void Release()
        {
            RemoveJoint();
            if (HeldObject) HeldObject.Release(this);
            HeldObject = null;
            // Preserve solver velocity on release; tracked velocity injection would
            // add energy twice and fling objects when a physical hand is blocked.
        }

        private void RemoveJoint()
        {
            // Disable the constraint immediately, before Destroy's end-of-frame cleanup.
            if (grabJoint)
            {
                grabJoint.xMotion = grabJoint.yMotion = grabJoint.zMotion = ConfigurableJointMotion.Free;
                grabJoint.angularXMotion = grabJoint.angularYMotion = grabJoint.angularZMotion = ConfigurableJointMotion.Free;
                Destroy(grabJoint);
                grabJoint = null;
            }
        }

        private void OnJointBreak(float _) { Release(); gripArmed = false; }
        private void OnDisable() { Release(); hadTracking = false; gripArmed = false; }
    }
}
