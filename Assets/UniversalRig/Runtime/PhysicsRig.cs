using UnityEngine;

namespace UniversalRig
{
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
        public bool Grounded { get; private set; }
        public float CapsuleHeight => capsule ? capsule.height : 0f;
        public bool HeadObstructed { get; private set; }
        public bool Calibrated { get; private set; }

        private CapsuleCollider capsule;
        private Vector3 previousBodyPosition, requestedRoomStep;
        private bool turnArmed = true;
        private float nextJumpTime;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
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
            capsule.direction = 1;
            capsule.radius = settings.bodyRadius;
            SetHeight(Mathf.Clamp(settings.standingEyeHeight + settings.headClearance,
                settings.minimumHeight, settings.maximumHeight));
            previousBodyPosition = Body.position;
        }

        private void OnEnable()
        {
            if (Body) previousBodyPosition = Body.position;
            requestedRoomStep = Vector3.zero;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 completed = Body.position - previousBodyPosition;
            input.trackingSpace.position += completed - RigMath.CompletedRoomStep(completed, requestedRoomStep);
            previousBodyPosition = Body.position;
            requestedRoomStep = Vector3.zero;
            input.RefreshPoses();

            bool calibrate = input.ConsumeCalibration();
            bool jump = input.ConsumeJump();
            if (!input.HeadTracked)
            {
                Body.AddForce(Vector3.ClampMagnitude(-RigMath.Planar(Body.linearVelocity) / dt,
                    settings.acceleration), ForceMode.Acceleration);
                Grounded = false;
                HeadObstructed = true;
                return;
            }
            if ((!Calibrated && autoCalibrateOnFirstTracking) || calibrate) CalibrateStanding();

            Turn(dt);
            UpdateHeight();
            Vector3 origin = Body.position + Vector3.up * (settings.bodyRadius + 0.04f);
            RaycastHit hit = default;
            Grounded = Time.fixedTime >= nextJumpTime && Physics.SphereCast(origin,
                settings.bodyRadius * 0.9f, Vector3.down, out hit,
                settings.groundProbeDistance + 0.04f + settings.bodyRadius * 0.1f,
                settings.environmentMask, QueryTriggerInteraction.Ignore) &&
                Vector3.Angle(hit.normal, Vector3.up) <= settings.maximumSlope;

            Vector3 forward = RigMath.Planar(input.head.forward);
            if (forward.sqrMagnitude < 0.001f) forward = input.trackingSpace.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 stick = input.Move.magnitude >= settings.stickDeadzone ? input.Move : Vector2.zero;
            Vector3 walk = (forward * stick.y + right * stick.x) * settings.walkSpeed;
            Vector3 roomError = RigMath.Planar(input.head.position - Body.position);
            Vector3 roomVelocity = Vector3.ClampMagnitude(roomError * settings.roomFollowGain, settings.roomFollowSpeed);
            Vector3 desired = walk + roomVelocity;
            if (Grounded) desired = Vector3.ProjectOnPlane(desired, hit.normal);
            Vector3 velocity = Grounded ? Vector3.ProjectOnPlane(Body.linearVelocity, hit.normal) : RigMath.Planar(Body.linearVelocity);
            Vector3 acceleration = Vector3.ClampMagnitude((desired - velocity) / dt,
                Grounded ? settings.acceleration : settings.airAcceleration);
            Body.AddForce(acceleration, ForceMode.Acceleration);
            requestedRoomStep = roomVelocity * dt;

            if (jump && Grounded)
            {
                float speed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * settings.jumpHeight);
                Body.AddForce(Vector3.up * Mathf.Max(0f, speed - Body.linearVelocity.y), ForceMode.VelocityChange);
                Grounded = false;
                nextJumpTime = Time.fixedTime + 0.2f;
            }
            HeadObstructed = Physics.CheckSphere(input.head.position, 0.09f,
                settings.environmentMask, QueryTriggerInteraction.Ignore);
        }

        public bool CalibrateStanding()
        {
            if (!input.HeadTracked || (leftHand && leftHand.HeldObject) || (rightHand && rightHand.HeldObject)) return false;
            if (Calibrated && (!Grounded || Body.linearVelocity.sqrMagnitude > 0.09f)) return false;
            float measured = input.RawHeadHeight;
            if (measured < 0.5f || measured > 2.5f) return false;
            input.trackingScale = Mathf.Clamp(settings.standingEyeHeight / measured, 0.5f, 1.5f);
            input.RefreshPoses();
            input.trackingSpace.position += RigMath.Planar(Body.position - input.head.position);
            requestedRoomStep = Vector3.zero;
            Calibrated = true;
            return true;
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

        private void UpdateHeight()
        {
            float height = Mathf.Clamp(input.head.position.y - Body.position.y + settings.headClearance,
                settings.minimumHeight, settings.maximumHeight);
            height = Mathf.MoveTowards(capsule.height, height, 3f * Time.fixedDeltaTime);
            // Test the proposed volume before growing under a ceiling. Inset prevents
            // the floor contact from vetoing every attempted stand-up.
            float radius = settings.bodyRadius - 0.01f;
            Vector3 bottom = Body.position + Vector3.up * (settings.bodyRadius + 0.02f);
            Vector3 top = Body.position + Vector3.up * (height - settings.bodyRadius);
            if (height <= capsule.height || !Physics.CheckCapsule(bottom, top, radius,
                settings.environmentMask, QueryTriggerInteraction.Ignore)) SetHeight(height);
        }

        private void SetHeight(float height)
        {
            capsule.height = Mathf.Max(settings.bodyRadius * 2f, height);
            capsule.center = Vector3.up * (capsule.height * 0.5f);
        }
    }
}
