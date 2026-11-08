using System.Collections.Generic;
using UnityEngine;

namespace UniversalRig
{
    // Physical player body: an upright capsule floating on a ride-height ground spring
    // (steps below settings.stepHeight pass under it), bounded head-relative locomotion,
    // ceiling-tested crouching, jumping, climbing from Climbable holds, and the camera's
    // tracking-space bookkeeping. Sole writer of the Tracking Space transform.
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PhysicsRig : MonoBehaviour
    {
        public RigSettings settings;
        public RigInput input;
        public PhysicsHand leftHand;
        public PhysicsHand rightHand;
        public bool autoCalibrateOnFirstTracking = true;
        public Rigidbody Body { get; private set; }
        public CapsuleCollider Capsule { get; private set; }
        public bool Grounded { get; private set; }
        public bool Climbing { get; private set; }
        public bool HeadObstructed { get; private set; }
        public bool Calibrated { get; private set; }
        public float HeadSeparation { get; private set; }
        public float Top => Capsule.center.y + Capsule.height * 0.5f;
        // Where the head pose says the body should stand (under the neck, not the eyes).
        public Vector3 RootTarget => settings.RootFromEye(input.head.position, input.head.rotation);
        public Vector3 ShoulderPosition(bool left) =>
            settings.ShoulderFromEye(input.head.position, input.head.rotation, left);

        private Vector3 previousBodyPosition, requestedRoomStep, renderOffset, walkVelocity, requestedWalkStep;
        private bool turnArmed = true, hadHeadTracking;
        private float jumpLockUntil;
        private readonly RaycastHit[] groundHits = new RaycastHit[8];
        private readonly RaycastHit[] headHits = new RaycastHit[8];
        private readonly Collider[] overlaps = new Collider[16];
        private readonly HashSet<Collider> bodyIgnored = new HashSet<Collider>();
        private readonly List<Collider> pendingRestore = new List<Collider>();
        private readonly List<Collider> colliderBuffer = new List<Collider>();

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Capsule = GetComponent<CapsuleCollider>();
            if (!settings || !input || !input.trackingSpace || !input.head)
            {
                Debug.LogError("Universal Rig: assign settings, input and tracked nodes before playing.", this);
                enabled = false;
                return;
            }
            Body.mass = settings.bodyMass;
            Body.useGravity = true;
            Body.isKinematic = false;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxDepenetrationVelocity = 3f;
            Body.solverIterations = 12;
            Body.solverVelocityIterations = 4;
            Capsule.direction = 1;
            Capsule.radius = settings.bodyRadius;
            SetTop(Mathf.Clamp(settings.standingEyeHeight + settings.headClearance,
                settings.minimumHeight, settings.maximumHeight));
            previousBodyPosition = Body.position;
        }

        private void OnEnable()
        {
            if (Body) previousBodyPosition = Body.position;
            requestedRoomStep = Vector3.zero;
        }

        private void OnDisable()
        {
            if (input && input.trackingSpace) input.trackingSpace.position -= renderOffset;
            renderOffset = Vector3.zero;
        }

        // Presentation only: shift the camera by the body's interpolation offset so locomotion
        // is smooth at any display rate. Removed again before the next physics step.
        private void LateUpdate()
        {
            Vector3 offset = transform.position - Body.position;
            input.trackingSpace.position += offset - renderOffset;
            renderOffset = offset;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            input.trackingSpace.position -= renderOffset;
            renderOffset = Vector3.zero;
            // Carry the camera with the body, except room-scale motion the user already walked.
            // The ideal walk-only displacement is removed first so stick walking in another
            // direction can't hide (or fake) the room-scale part.
            Vector3 completed = Body.position - previousBodyPosition;
            input.trackingSpace.position += completed - RigMath.CompletedRoomStep(completed - requestedWalkStep, requestedRoomStep);
            previousBodyPosition = Body.position;
            requestedRoomStep = requestedWalkStep = Vector3.zero;
            input.RefreshPoses();
            RestoreReleasedCollisions();

            bool calibrate = input.ConsumeCalibration();
            bool jump = input.ConsumeJump();
            bool recenter = input.ConsumeRecenter();
            bool tracked = input.HeadTracked;
            if (tracked)
            {
                // Runtime recenter or reacquired tracking: put the head back over the body
                // instead of letting room-follow chase a jumped head pose through walls.
                if (!hadHeadTracking && Calibrated) recenter = true;
                if ((!Calibrated && autoCalibrateOnFirstTracking && input.OriginReady) || calibrate) CalibrateStanding();
                else if (recenter) Recenter();
                Turn(dt);
                UpdateHeight(dt);
            }
            hadHeadTracking = tracked;

            bool hasGround = ProbeGround(out RaycastHit ground);
            Climbing = tracked && ((leftHand && leftHand.Anchored) || (rightHand && rightHand.Anchored));
            if (!Climbing && tracked) Locomote(dt, hasGround, ground);
            else walkVelocity = Vector3.zero;
            if (Climbing) Climb(dt);
            else if (!tracked) Body.AddForce(Vector3.ClampMagnitude(-RigMath.Planar(Body.linearVelocity) / dt,
                settings.acceleration), ForceMode.Acceleration);

            if (jump && tracked && Grounded && !Climbing)
            {
                float speed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * settings.jumpHeight);
                Body.AddForce(Vector3.up * Mathf.Max(0f, speed - Body.linearVelocity.y), ForceMode.VelocityChange);
                jumpLockUntil = Time.fixedTime + 0.25f;
            }
            ApplyGroundSpring(hasGround, ground);
            HeadSeparation = tracked ? RigMath.Planar(RootTarget - Body.position).magnitude : 0f;
            HeadObstructed = !tracked || HeadBlocked(input.head.position);
        }

        private void Locomote(float dt, bool hasGround, RaycastHit ground)
        {
            Vector2 stick = input.Move.magnitude >= settings.stickDeadzone ? input.Move : Vector2.zero;
            Vector3 walk = RigMath.Yaw(input.head.rotation) * new Vector3(stick.x, 0f, stick.y) * settings.walkSpeed;
            Vector3 roomVelocity = Vector3.ClampMagnitude(RigMath.Planar(RootTarget - Body.position) *
                settings.roomFollowGain, settings.roomFollowSpeed);
            Vector3 groundVelocity = Grounded && hasGround && ground.rigidbody
                ? RigMath.Planar(ground.rigidbody.GetPointVelocity(ground.point)) : Vector3.zero;
            Vector3 velocity = RigMath.Planar(Body.linearVelocity) - groundVelocity;
            float limit = Grounded ? settings.acceleration : settings.airAcceleration;
            Body.AddForce(Vector3.ClampMagnitude((walk + roomVelocity - velocity) / dt, limit), ForceMode.Acceleration);
            walkVelocity = Vector3.MoveTowards(walkVelocity, walk, limit * dt);
            requestedWalkStep = (walkVelocity + groundVelocity) * dt;
            requestedRoomStep = roomVelocity * dt;
        }

        private void Climb(float dt)
        {
            Vector3 error = Vector3.zero;
            int count = 0;
            if (leftHand && leftHand.Anchored) { error += leftHand.Body.position - leftHand.TargetPosition; ++count; }
            if (rightHand && rightHand.Anchored) { error += rightHand.Body.position - rightHand.TargetPosition; ++count; }
            // Room-scale still applies while hanging (lean over a ledge to mantle onto it). It is
            // booked as room-scale, so the tracked hands don't move and the grip stays matched.
            Vector3 roomVelocity = Vector3.ClampMagnitude(RigMath.Planar(RootTarget - Body.position) *
                settings.roomFollowGain, settings.roomFollowSpeed);
            Body.AddForce(RigMath.ClimbAcceleration(error / count, roomVelocity, Body.linearVelocity, Physics.gravity,
                settings.climbGain, settings.maximumClimbSpeed, settings.maximumClimbAcceleration, dt),
                ForceMode.Acceleration);
            requestedRoomStep = roomVelocity * dt;
        }

        private bool ProbeGround(out RaycastHit ground)
        {
            float radius = settings.bodyRadius * 0.6f;
            Vector3 origin = Body.position + Vector3.up * (settings.stepHeight + radius);
            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, groundHits,
                settings.stepHeight + settings.groundSnapDistance, settings.environmentMask, QueryTriggerInteraction.Ignore);
            ground = default;
            bool found = false;
            for (int i = 0; i < count; ++i)
            {
                RaycastHit hit = groundHits[i];
                // Skip initial overlaps and anything held: standing on your own held box is the
                // classic self-launch exploit.
                if (hit.distance <= 0f || bodyIgnored.Contains(hit.collider)) continue;
                if (found && hit.distance >= ground.distance) continue;
                ground = hit;
                found = true;
            }
            return found;
        }

        private void ApplyGroundSpring(bool hasGround, RaycastHit ground)
        {
            Grounded = false;
            if (!hasGround || Time.fixedTime < jumpLockUntil ||
                Vector3.Angle(ground.normal, Vector3.up) > settings.maximumSlope) return;
            float error = settings.stepHeight - ground.distance; // ground height above the feet
            Grounded = error > -0.05f;
            if (Climbing && error <= 0f) return; // only a one-sided floor while hanging
            Vector3 groundVelocity = ground.rigidbody ? ground.rigidbody.GetPointVelocity(ground.point) : Vector3.zero;
            float lift = RigMath.GroundSpring(error, Body.linearVelocity.y - groundVelocity.y,
                settings.groundSpring, settings.groundDamping, settings.maximumGroundLift, settings.maximumGroundSnap);
            float acceleration = lift + (Climbing ? 0f : -Physics.gravity.y);
            Body.AddForce(Vector3.up * acceleration, ForceMode.Acceleration);
            if (ground.rigidbody && !ground.rigidbody.isKinematic)
                ground.rigidbody.AddForceAtPosition(Vector3.down * (acceleration * Body.mass), ground.point);
        }

        public bool CalibrateStanding()
        {
            if (!input.HeadTracked || (leftHand && leftHand.HeldObject) || (rightHand && rightHand.HeldObject) || Climbing) return false;
            if (Calibrated && (!Grounded || Body.linearVelocity.sqrMagnitude > 0.09f)) return false;
            float measured = input.RawHeadHeight;
            if (measured < 0.5f || measured > 2.5f) return false;
            input.trackingScale = Mathf.Clamp(settings.standingEyeHeight / measured, 0.5f, 1.5f);
            input.RefreshPoses();
            Recenter();
            Calibrated = true;
            return true;
        }

        // Horizontal only; never changes scale or height and never moves the body.
        public void Recenter()
        {
            input.trackingSpace.position += RigMath.Planar(Body.position - RootTarget);
            requestedRoomStep = Vector3.zero;
        }

        private void Turn(float dt)
        {
            float stick = input.Turn.x;
            float degrees = 0f;
            if (settings.snapTurn)
            {
                if (Mathf.Abs(stick) < 0.25f) turnArmed = true;
                if (turnArmed && Mathf.Abs(stick) > 0.7f)
                {
                    degrees = Mathf.Sign(stick) * settings.snapAngle;
                    turnArmed = false;
                }
            }
            else if (Mathf.Abs(stick) > settings.stickDeadzone) degrees = stick * settings.smoothTurnSpeed * dt;
            if (degrees != 0f) input.trackingSpace.RotateAround(input.head.position, Vector3.up, degrees);
        }

        private void UpdateHeight(float dt)
        {
            float current = Top;
            float top = Mathf.MoveTowards(current, Mathf.Clamp(input.head.position.y - Body.position.y +
                settings.headClearance, settings.minimumHeight, settings.maximumHeight), 3f * dt);
            if (top > current)
            {
                // Test the grown volume before standing up under a ceiling; the inset keeps
                // touching walls from vetoing every stand-up. Held items don't count.
                float radius = settings.bodyRadius - 0.01f;
                Vector3 bottom = Body.position + Vector3.up * (settings.stepHeight + settings.bodyRadius);
                Vector3 upper = Body.position + Vector3.up * (top - radius); // full height, no vertical inset
                int count = Physics.OverlapCapsuleNonAlloc(bottom, upper, radius, overlaps,
                    settings.environmentMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; ++i)
                    if (!bodyIgnored.Contains(overlaps[i])) return;
            }
            SetTop(top);
        }

        private void SetTop(float top)
        {
            Capsule.height = Mathf.Max(settings.bodyRadius * 2f, top - settings.stepHeight);
            Capsule.center = Vector3.up * (settings.stepHeight + Capsule.height * 0.5f);
        }

        // Head inside geometry, or geometry between the body and the head (a head pushed
        // through a thin wall/ceiling ends up outside it, so an overlap test alone misses it).
        public bool HeadBlocked(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, 0.09f, overlaps,
                settings.environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; ++i)
                if (!bodyIgnored.Contains(overlaps[i])) return true;
            Vector3 from = (Time.inFixedTimeStep ? Body.position : transform.position) +
                Vector3.up * (Top - settings.bodyRadius);
            Vector3 path = position - from;
            count = Physics.RaycastNonAlloc(from, path.normalized, headHits, path.magnitude,
                settings.environmentMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; ++i)
                if (!bodyIgnored.Contains(headHits[i].collider)) return true;
            return false;
        }

        // Held items never collide with their holder's body, so they can't be used to push or
        // lift yourself. Collision returns after release once the item no longer overlaps.
        public void IgnoreHeldCollisions(PhysicsGrabbable item)
        {
            item.GetComponentsInChildren(colliderBuffer);
            foreach (Collider collider in colliderBuffer)
            {
                if (bodyIgnored.Add(collider)) Physics.IgnoreCollision(Capsule, collider, true);
                pendingRestore.Remove(collider);
            }
        }

        public void ReleaseHeldCollisions(PhysicsGrabbable item)
        {
            if (!item || item.HoldingHands > 0) return;
            item.GetComponentsInChildren(colliderBuffer);
            foreach (Collider collider in colliderBuffer)
                if (bodyIgnored.Contains(collider) && !pendingRestore.Contains(collider)) pendingRestore.Add(collider);
        }

        public bool IgnoresBody(Collider collider) => collider && bodyIgnored.Contains(collider);

        // Extra colliders on a held weapon (a seated magazine, a slide) that are not children of it.
        public void IgnoreCollider(Collider collider)
        {
            if (!collider || !Capsule) return;
            if (bodyIgnored.Add(collider)) Physics.IgnoreCollision(Capsule, collider, true);
            pendingRestore.Remove(collider);
        }

        public void QueueRestoreCollider(Collider collider)
        {
            if (!collider || !bodyIgnored.Contains(collider) || pendingRestore.Contains(collider)) return;
            pendingRestore.Add(collider);
        }

        private void RestoreReleasedCollisions()
        {
            for (int i = pendingRestore.Count - 1; i >= 0; --i)
            {
                Collider collider = pendingRestore[i];
                if (collider && collider.enabled && collider.gameObject.activeInHierarchy &&
                    Physics.ComputePenetration(Capsule, Body.position, Body.rotation, collider,
                        collider.transform.position, collider.transform.rotation, out _, out _)) continue;
                if (collider) Physics.IgnoreCollision(Capsule, collider, false);
                bodyIgnored.Remove(collider);
                pendingRestore.RemoveAt(i);
            }
        }
    }
}
