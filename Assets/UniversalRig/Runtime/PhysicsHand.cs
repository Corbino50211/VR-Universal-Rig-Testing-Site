using System.Collections.Generic;
using UnityEngine;

namespace UniversalRig
{
    // Dynamic hand driven toward the controller grip pose with bounded force/torque.
    // A reach joint ties the wrist to the avatar's shoulder (arm length), so the physical hand
    // can never leave the visible arm's reach. Forces from a loaded hand (touching or holding)
    // are pushed back into the body; Climbable grips lock the hand and let PhysicsRig climb.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PhysicsHand : MonoBehaviour
    {
        public RigSettings settings;
        public RigInput input;
        public PhysicsRig rig;
        public bool isLeft;
        [Tooltip("Controller-specific grip pose correction. Tune per headset; zero = OpenXR grip pose.")]
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public Rigidbody Body { get; private set; }
        public PhysicsGrabbable HeldObject { get; private set; }
        public GripPoint ActiveGrip { get; private set; }
        // True while gripping a Climbable surface (hand locked to the world/kinematic body).
        public bool Anchored { get; private set; }
        public bool Holding => HeldObject || Anchored;
        public float TrackingError { get; private set; }
        public float Grip => !input ? 0f : (isLeft ? input.LeftGrip : input.RightGrip);
        public float Trigger => !input ? 0f : (isLeft ? input.LeftTrigger : input.RightTrigger);
        public void Pulse(float amplitude, float duration) { if (input) input.Pulse(isLeft, amplitude, duration); }
        public Vector3 WristOffset => isLeft ? settings.leftWristOffset : settings.rightWristOffset;
        public Vector3 TargetPosition => Target.TransformPoint(positionOffset);
        public Quaternion TargetRotation => Target.rotation * Quaternion.Euler(rotationOffset);
        private Transform Target => isLeft ? input.leftTarget : input.rightTarget;

        private ConfigurableJoint grabJoint, reachJoint;
        private readonly List<GripPoint> gripBuffer = new List<GripPoint>(8);
        private bool jointControlsRotation;
        private Collider climbCollider;
        private readonly Collider[] nearby = new Collider[32];
        private readonly RaycastHit[] gripObstructions = new RaycastHit[32];
        private bool gripArmed = true;
        private bool hadTracking;
        private int contactSteps;
        private Vector3 previousTarget;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            if (!settings || !input || !rig)
            {
                Debug.LogError("Universal Rig: physical hand has missing settings/input/rig.", this);
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

        private void Start()
        {
            if (!enabled || !rig.Body) return;
            reachJoint = gameObject.AddComponent<ConfigurableJoint>();
            reachJoint.connectedBody = rig.Body;
            reachJoint.autoConfigureConnectedAnchor = false;
            reachJoint.anchor = WristOffset;
            reachJoint.xMotion = reachJoint.yMotion = reachJoint.zMotion = ConfigurableJointMotion.Limited;
            reachJoint.angularXMotion = reachJoint.angularYMotion = reachJoint.angularZMotion = ConfigurableJointMotion.Free;
            reachJoint.linearLimit = new SoftJointLimit { limit = settings.PhysicalArmReach };
            reachJoint.enableCollision = false;
            UpdateReach();
        }

        // The shoulder follows the head pose (last known pose while untracked); keep the
        // joint's body-side anchor on it.
        private void UpdateReach()
        {
            if (!reachJoint) return;
            reachJoint.connectedAnchor = Quaternion.Inverse(rig.Body.rotation) *
                (rig.ShoulderPosition(isLeft) - rig.Body.position);
        }

        private void FixedUpdate()
        {
            UpdateReach();
            bool tracked = input.HeadTracked && (isLeft ? input.LeftTracked : input.RightTracked) && Target;
            if (!tracked)
            {
                Release();
                // Tracking loss removes all pull forces; damp in place, never fly to the origin.
                Body.AddForce(-Body.linearVelocity * 12f, ForceMode.Acceleration);
                Body.AddTorque(-Body.angularVelocity * 12f, ForceMode.Acceleration);
                hadTracking = false;
                gripArmed = false;
                return;
            }
            Vector3 targetPosition = TargetPosition;
            Quaternion targetRotation = TargetRotation;
            Vector3 targetVelocity = hadTracking
                ? Vector3.ClampMagnitude((targetPosition - previousTarget) / Time.fixedDeltaTime, 8f) : Vector3.zero;
            previousTarget = targetPosition;
            hadTracking = true;
            Vector3 error = targetPosition - Body.position;
            TrackingError = error.magnitude;
            Vector3 force = Vector3.ClampMagnitude(Vector3.ClampMagnitude(error, settings.maximumPositionError) *
                settings.handPositionSpring + (targetVelocity - Body.linearVelocity) * settings.handPositionDamping,
                settings.maximumHandForce);
            Body.AddForce(force, ForceMode.Force);
            // Equal and opposite: a hand pushing on something (or carrying a load) pushes the body.
            // Free-air swings are excluded; the legs absorb those in reality.
            if (!Anchored && (HeldObject || contactSteps > 0))
                rig.Body.AddForce(-force * settings.handReaction, ForceMode.Force);
            if (contactSteps > 0) --contactSteps;
            Vector3 angularAcceleration = RigMath.RotationError(targetRotation, Body.rotation) * settings.handRotationSpring
                - Body.angularVelocity * settings.handRotationDamping;
            Quaternion inertiaRotation = Body.rotation * Body.inertiaTensorRotation;
            Vector3 torque = inertiaRotation * Vector3.Scale(Body.inertiaTensor,
                Quaternion.Inverse(inertiaRotation) * angularAcceleration);
            Body.AddTorque(Vector3.ClampMagnitude(torque, settings.maximumHandTorque), ForceMode.Force);

            if (HeldObject && !grabJoint) Release();
            if (Anchored && (!grabJoint || !climbCollider || !climbCollider.enabled || !climbCollider.gameObject.activeInHierarchy))
            {
                Release();
                gripArmed = false;
            }
            if (HeldObject && grabJoint)
            {
                bool primary = ActiveGrip ? ActiveGrip.role == GripPoint.Role.Primary || HeldObject.HoldingHands == 1 : HeldObject.PrimaryHand == this;
                if (primary != jointControlsRotation)
                {
                    Vector3 anchor = grabJoint.anchor;
                    Vector3 connectedAnchor = grabJoint.connectedAnchor;
                    RemoveJoint();
                    // A fresh joint captures the current relative rotation on handover.
                    // Locking the old free joint would restore its original grip rotation.
                    CreateJoint(HeldObject.Body, anchor, connectedAnchor, primary, settings.grabBreakForce, settings.grabBreakTorque, ActiveGrip);
                }
            }
            float grip = Grip;
            if (grip <= settings.gripRelease) { Release(); gripArmed = true; }
            else if (grip >= settings.gripPress)
            {
                // A closed hand can meet an object. Stop retrying once something is held.
                if (!Holding && gripArmed) TryGrab();
                if (Holding) gripArmed = false;
            }
        }

        private void OnCollisionStay(Collision _) { contactSteps = 2; }

        private void TryGrab()
        {
            if (TryGrabGrip()) return;
            int count = Physics.OverlapSphereNonAlloc(Body.position, settings.grabRadius, nearby,
                settings.grabbableMask, QueryTriggerInteraction.Ignore);
            PhysicsGrabbable bestItem = null;
            Collider bestCollider = null;
            Vector3 bestPoint = Vector3.zero;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; ++i)
            {
                Collider candidate = nearby[i];
                if (!candidate) continue;
                Rigidbody body = candidate.attachedRigidbody;
                PhysicsGrabbable item = body ? body.GetComponent<PhysicsGrabbable>() : null;
                if (item)
                {
                    if (!item.isActiveAndEnabled || item.authoredGripsOnly || item.Body.isKinematic || item.HoldingHands >= item.maximumHands) continue;
                }
                else if ((body && !body.isKinematic) || !candidate.GetComponentInParent<Climbable>()) continue;
                Vector3 point = candidate.ClosestPoint(Body.position);
                float distance = (point - Body.position).sqrMagnitude;
                if (distance >= bestDistance) continue;
                // A proximity sphere alone would allow grabbing through a thin wall.
                if (Physics.Linecast(Body.position, point, out RaycastHit hit, settings.environmentMask,
                    QueryTriggerInteraction.Ignore) && hit.collider != candidate && (!item || hit.rigidbody != item.Body)) continue;
                bestItem = item;
                bestCollider = candidate;
                bestPoint = point;
                bestDistance = distance;
            }
            if (!bestCollider) return;
            Vector3 anchor = transform.InverseTransformPoint(bestPoint);
            if (bestItem)
            {
                if (!bestItem.TryHold(this)) return;
                HeldObject = bestItem;
                CreateJoint(bestItem.Body, anchor, bestItem.transform.InverseTransformPoint(bestPoint),
                    bestItem.PrimaryHand == this, settings.grabBreakForce, settings.grabBreakTorque);
                rig.IgnoreHeldCollisions(bestItem);
                return;
            }
            Rigidbody kinematic = bestCollider.attachedRigidbody;
            CreateJoint(kinematic, anchor, kinematic ? kinematic.transform.InverseTransformPoint(bestPoint) : bestPoint,
                true, Mathf.Infinity, Mathf.Infinity);
            climbCollider = bestCollider;
            Anchored = true;
        }

        // Authored grips win over the closest point. The first hand takes a primary grip when
        // the object has one; the other hand takes a support grip.
        private bool TryGrabGrip()
        {
            GripPoint best = null;
            PhysicsGrabbable bestItem = null;
            float bestScore = float.PositiveInfinity;
            foreach (GripPoint grip in GripPoint.Active)
            {
                if (!grip || !grip.isActiveAndEnabled || !grip.Allows(isLeft)) continue;
                PhysicsGrabbable item = grip.GetComponentInParent<PhysicsGrabbable>();
                if (!item || !item.isActiveAndEnabled || item.Body.isKinematic || item.HoldingHands >= item.maximumHands) continue;
                if ((settings.grabbableMask.value & (1 << item.gameObject.layer)) == 0) continue;
                if ((item.PrimaryHand && item.PrimaryHand.ActiveGrip == grip) ||
                    (item.SecondaryHand && item.SecondaryHand.ActiveGrip == grip)) continue;
                float radius = Mathf.Max(0.01f, grip.captureRadius);
                if ((grip.transform.position - Body.position).sqrMagnitude > radius * radius) continue;
                float score = (grip.transform.position - TargetPosition).sqrMagnitude;
                if (score >= bestScore || Blocked(grip.transform.position, item)) continue;
                // Snap only to a pose the configured arm can actually reach.
                Vector3 wrist = grip.transform.TransformPoint(WristOffset);
                if (Vector3.Distance(rig.ShoulderPosition(isLeft), wrist) > settings.PhysicalArmReach) continue;
                best = grip;
                bestItem = item;
                bestScore = score;
            }
            if (!best || !bestItem.TryHold(this)) return false;
            HeldObject = bestItem;
            ActiveGrip = best;
            Body.position = best.transform.position;
            Body.rotation = best.transform.rotation;
            Body.linearVelocity = bestItem.Body.GetPointVelocity(Body.position);
            Body.angularVelocity = bestItem.Body.angularVelocity;
            CreateJoint(bestItem.Body, Vector3.zero, bestItem.transform.InverseTransformPoint(best.transform.position),
                best.role == GripPoint.Role.Primary || bestItem.HoldingHands == 1, settings.grabBreakForce, settings.grabBreakTorque, best);
            rig.IgnoreHeldCollisions(bestItem);
            return true;
        }

        // A socket consumes this grab; require an actual release before another grab.
        public void ReleaseToSocket() { Release(); gripArmed = false; }

        private bool Blocked(Vector3 point, PhysicsGrabbable item)
        {
            Vector3 delta = point - Body.position;
            if (delta.sqrMagnitude < 0.000001f) return false;
            int count = Physics.RaycastNonAlloc(Body.position, delta.normalized, gripObstructions,
                delta.magnitude, settings.environmentMask, QueryTriggerInteraction.Ignore);
            if (count == gripObstructions.Length) return true;
            for (int i = 0; i < count; i++)
            {
                Rigidbody hit = gripObstructions[i].rigidbody;
                if (hit == Body || hit == item.Body || (rig && (hit == rig.Body ||
                    (rig.leftHand && hit == rig.leftHand.Body) || (rig.rightHand && hit == rig.rightHand.Body)))) continue;
                if (SameWeapon(hit, item.Body)) continue;
                return true;
            }
            return false;
        }

        static bool SameWeapon(Rigidbody hit, Rigidbody item)
        {
            if (!hit || !item) return false;
            Rigidbody root = Anchor(item);
            return hit == root || Anchor(hit) == root;
        }

        static Rigidbody Anchor(Rigidbody body)
        {
            Joint[] joints = body.GetComponents<Joint>();
            for (int i = 0; i < joints.Length; ++i)
                if (joints[i] && joints[i].connectedBody) return joints[i].connectedBody;
            return body;
        }

        private void CreateJoint(Rigidbody connected, Vector3 anchor, Vector3 connectedAnchor, bool lockRotation,
            float breakForce, float breakTorque, GripPoint grip = null)
        {
            grabJoint = gameObject.AddComponent<ConfigurableJoint>();
            grabJoint.connectedBody = connected; // null = anchored to the world
            grabJoint.autoConfigureConnectedAnchor = false;
            grabJoint.anchor = anchor;
            grabJoint.connectedAnchor = connectedAnchor;
            grabJoint.xMotion = grabJoint.yMotion = grabJoint.zMotion = ConfigurableJointMotion.Locked;
            // First hand establishes orientation; second supplies position only so two
            // competing wrist orientations cannot over-constrain a long object.
            jointControlsRotation = lockRotation;
            ConfigurableJointMotion angular = lockRotation && !(grip && grip.isActionHandle) ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
            grabJoint.angularXMotion = grabJoint.angularYMotion = grabJoint.angularZMotion = angular;
            grabJoint.enableCollision = false;
            grabJoint.breakForce = breakForce;
            grabJoint.breakTorque = breakTorque;
            grabJoint.projectionMode = JointProjectionMode.None;
            if (!grip || !grip.drivesAction || grip.actionTravel <= 0f || !HeldObject) return;
            Vector3 worldAxis = HeldObject.transform.TransformDirection(grip.actionAxisLocal);
            if (worldAxis.sqrMagnitude < 0.000001f) return;
            grabJoint.axis = Quaternion.Inverse(transform.rotation) * worldAxis.normalized;
            grabJoint.xMotion = ConfigurableJointMotion.Limited;
            grabJoint.linearLimit = new SoftJointLimit { limit = grip.actionTravel };
        }

        public void Release()
        {
            RemoveJoint();
            PhysicsGrabbable item = HeldObject;
            HeldObject = null;
            ActiveGrip = null;
            Anchored = false;
            climbCollider = null;
            if (item)
            {
                item.Release(this);
                if (rig) rig.ReleaseHeldCollisions(item);
            }
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
            }
            grabJoint = null;
        }

        private void OnJointBreak(float _) { Release(); gripArmed = false; }
        private void OnDisable() { Release(); hadTracking = false; gripArmed = false; }
    }
}
