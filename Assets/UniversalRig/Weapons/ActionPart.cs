using UnityEngine;

namespace UniversalRig.Weapons
{
    // Slide, bolt or pump. The handle is its own rigidbody, jointed to the gun along one axis.
    // It is spawned unparented so a child rigidbody never rides the gun's transform.
    [DefaultExecutionOrder(-50)]
    public sealed class ActionPart : MonoBehaviour
    {
        public Firearm firearm;
        public Transform visual;
        public Transform marker;
        public Vector3 openAxisLocal = Vector3.back;
        public float travel = 0.045f;
        [Range(0.3f, 0.95f)] public float threshold = 0.72f;

        public float Open01 { get; private set; }
        public bool LockedOpen { get; private set; }
        public GripPoint HandleGrip { get; private set; }
        public Rigidbody HandleBody { get; private set; }
        public Vector3 OpenDirectionWorld => transform.TransformDirection(openAxisLocal).normalized;

        Vector3 closedLocal;
        Vector3 visualRest;
        ConfigurableJoint joint;
        bool armed, wasPast, wasHeld;
        float suppressUntil, blipUntil;

        void Awake()
        {
            if (!marker || !firearm)
            {
                Debug.LogError("Universal Rig firearm: action part is missing its marker or firearm.", this);
                enabled = false;
                return;
            }
            travel = Mathf.Max(0.02f, travel);
            closedLocal = transform.InverseTransformPoint(marker.position);
            if (visual) visualRest = visual.localPosition;
            suppressUntil = Time.time + 0.25f;
            SpawnHandle();
        }

        void SpawnHandle()
        {
            var go = new GameObject(name + " handle");
            go.transform.SetPositionAndRotation(marker.position, marker.rotation);
            HandleBody = go.AddComponent<Rigidbody>();
            HandleBody.mass = 0.22f;
            HandleBody.useGravity = false;
            HandleBody.interpolation = RigidbodyInterpolation.Interpolate;
            HandleBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            HandleBody.maxDepenetrationVelocity = 2f;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.06f, 0.05f, 0.07f);
            HandleGrip = go.AddComponent<GripPoint>();
            HandleGrip.role = GripPoint.Role.Support;
            HandleGrip.fingerCurl = 1f;
            PhysicsGrabbable grab = go.AddComponent<PhysicsGrabbable>();
            grab.maximumHands = 1;
            joint = go.AddComponent<ConfigurableJoint>();
            joint.connectedBody = firearm.Body;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = closedLocal;
            joint.axis = go.transform.InverseTransformDirection(OpenDirectionWorld);
            // Linear motion is scripted (ClampToSegment / SlideToward). The joint axis
            // did not stay aligned with the barrel, so a locked axis blocked the pull.
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.linearLimit = new SoftJointLimit { limit = travel };
            joint.xDrive = Drive(false);
            joint.targetPosition = Vector3.zero;
            joint.enableCollision = false;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
            Collider[] gunColliders = firearm.GetComponentsInChildren<Collider>();
            foreach (Collider gunCollider in gunColliders)
                if (gunCollider && gunCollider != box) Physics.IgnoreCollision(box, gunCollider, true);
            marker.gameObject.SetActive(false);
        }

        public void SetLocked(bool locked)
        {
            LockedOpen = locked;
            if (locked) armed = true;
        }

        public void Suppress(float seconds) { suppressUntil = Mathf.Max(suppressUntil, Time.time + seconds); }

        public void Blip() { if (!LockedOpen) blipUntil = Time.time + 0.06f; }

        void FixedUpdate()
        {
            if (!HandleBody || !joint) return;
            bool held = HandleHeld();
            if (held) FollowHand();
            else SlideToward(LockedOpen ? travel : 0f);
            ClampToSegment();
            Open01 = Measure();
            if (LockedOpen && wasHeld && !held && firearm.profile && firearm.profile.cycle != FirearmCycle.OpenBolt)
                firearm.ReleaseSlideLock();
            wasHeld = held;
            bool quiet = Time.time < suppressUntil || Time.time < blipUntil;
            if (!quiet)
            {
                bool past = Open01 >= threshold;
                if (past && !wasPast) firearm.NotifyOpened();
                if (past) armed = true;
                if (armed && Open01 <= 0.2f && !LockedOpen)
                {
                    armed = false;
                    firearm.OnActionClosed();
                }
                wasPast = past;
            }
            // While a hand is on the handle the drive follows it, so the return spring
            // does not cancel the pull. Released, it shuts (or stays open on slide lock).
            float target = LockedOpen ? travel : Time.time < blipUntil ? travel * 0.35f : 0f;
            if (held && !LockedOpen) target = Open01 * travel;
            joint.xDrive = Drive(held && !LockedOpen);
            joint.targetPosition = new Vector3(target, 0f, 0f);
        }

        void LateUpdate()
        {
            if (!visual) return;
            Vector3 axis = visual.parent
                ? visual.parent.InverseTransformVector(OpenDirectionWorld)
                : OpenDirectionWorld;
            visual.localPosition = visualRest + axis * (Open01 * travel);
        }

        static JointDrive Drive(bool held) => new JointDrive
        {
            positionSpring = held ? 30f : 500f,
            positionDamper = held ? 8f : 35f,
            maximumForce = held ? 6f : 35f
        };

        bool HandleHeld()
        {
            PhysicsGrabbable grab = HandleBody.GetComponent<PhysicsGrabbable>();
            return grab && grab.HoldingHands > 0;
        }

        float Measure()
        {
            Vector3 rest = transform.TransformPoint(closedLocal);
            return Mathf.Clamp01(Vector3.Dot(HandleBody.position - rest, OpenDirectionWorld) / travel);
        }

        // Move the handle to the holding hand's target, clamped to the slide axis.
        void FollowHand()
        {
            PhysicsGrabbable grab = HandleBody.GetComponent<PhysicsGrabbable>();
            PhysicsHand hand = grab ? grab.PrimaryHand : null;
            if (!hand) return;
            Vector3 rest = transform.TransformPoint(closedLocal);
            Vector3 axis = OpenDirectionWorld;
            float along = Mathf.Clamp(Vector3.Dot(hand.TargetPosition - rest, axis), 0f, travel);
            HandleBody.position = rest + axis * along;
            HandleBody.linearVelocity = Vector3.ProjectOnPlane(HandleBody.linearVelocity, axis);
        }

        // The joint drive's axis does not reliably match the slide. Shut (or lock) it directly.
        void SlideToward(float along)
        {
            Vector3 rest = transform.TransformPoint(closedLocal);
            Vector3 axis = OpenDirectionWorld;
            float current = Vector3.Dot(HandleBody.position - rest, axis);
            float next = Mathf.MoveTowards(current, along, 0.55f * Time.fixedDeltaTime);
            Vector3 target = rest + axis * Mathf.Clamp(next, 0f, travel);
            if ((HandleBody.position - target).sqrMagnitude < 0.000004f) return;
            HandleBody.position = target;
            HandleBody.linearVelocity = Vector3.zero;
        }

        // ponytail: ConfigurableJoint linear limits are symmetric. Clamp the handle to the
        // rearward segment. A one-sided limit joint replaces this if the slide needs to stack.
        void ClampToSegment()
        {
            Vector3 rest = transform.TransformPoint(closedLocal);
            Vector3 axis = OpenDirectionWorld;
            float along = Mathf.Clamp(Vector3.Dot(HandleBody.position - rest, axis), 0f, travel);
            Vector3 target = rest + axis * along;
            if ((HandleBody.position - target).sqrMagnitude < 0.000004f) return;
            HandleBody.position = target;
            HandleBody.linearVelocity = Vector3.zero;
        }

        void OnDestroy() { if (HandleBody) Destroy(HandleBody.gameObject); }
    }
}
