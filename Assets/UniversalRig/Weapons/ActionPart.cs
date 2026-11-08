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
            HandleGrip.isActionHandle = true;
            HandleGrip.fingerCurl = 1f;
            PhysicsGrabbable grab = go.AddComponent<PhysicsGrabbable>();
            grab.maximumHands = 1;
            joint = go.AddComponent<ConfigurableJoint>();
            joint.connectedBody = firearm.Body;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            // Center the symmetric joint limit halfway along the one-sided rail.
            joint.connectedAnchor = firearm.transform.InverseTransformPoint(
                marker.position + OpenDirectionWorld * (travel * 0.5f));
            joint.axis = go.transform.InverseTransformDirection(OpenDirectionWorld);
            Vector3 perpendicular = Mathf.Abs(Vector3.Dot(joint.axis, Vector3.up)) < 0.9f
                ? Vector3.up : Vector3.forward;
            joint.secondaryAxis = Vector3.ProjectOnPlane(perpendicular, joint.axis).normalized;
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.linearLimit = new SoftJointLimit { limit = travel * 0.5f, contactDistance = 0.001f };
            joint.projectionMode = JointProjectionMode.None;
            HandleBody.solverIterations = 12;
            HandleBody.solverVelocityIterations = 4;
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
            // The hand pulls through its grab joint. Only the bounded return spring
            // drives a released handle; neither tracked targets nor transforms move it.
            float target = LockedOpen ? travel : Time.time < blipUntil ? travel * 0.35f : 0f;
            ApplySpring(target, held && !LockedOpen);

        }

        void LateUpdate()
        {
            if (!visual) return;
            Vector3 axis = visual.parent
                ? visual.parent.InverseTransformVector(OpenDirectionWorld)
                : OpenDirectionWorld;
            visual.localPosition = visualRest + axis * (Open01 * travel);
        }

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

        void ApplySpring(float target, bool held)
        {
            Vector3 axis = OpenDirectionWorld;
            Vector3 rest = transform.TransformPoint(closedLocal);
            float position = Vector3.Dot(HandleBody.position - rest, axis);
            float speed = Vector3.Dot(HandleBody.linearVelocity - firearm.Body.GetPointVelocity(HandleBody.position), axis);
            float spring = held ? 30f : 500f;
            float damping = held ? 2f : 12f;
            float inverseMass = 1f / HandleBody.mass + (firearm.Body.isKinematic ? 0f : 1f / firearm.Body.mass);
            float dt = Time.fixedDeltaTime;
            // Implicit damping keeps the return spring stable at lower physics rates.
            float denominator = 1f + (damping * dt + spring * dt * dt) * inverseMass;
            float force = Mathf.Clamp(((target - position) * spring - speed * damping) / denominator,
                held ? -6f : -35f, held ? 6f : 35f);
            Vector3 applied = axis * force;
            HandleBody.AddForce(applied, ForceMode.Force);
            firearm.Body.AddForceAtPosition(-applied, HandleBody.position, ForceMode.Force);
        }

        void OnDisable()
        {
            if (HandleBody) HandleBody.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (HandleBody) HandleBody.gameObject.SetActive(true);
        }

        void OnDestroy() { if (HandleBody) Destroy(HandleBody.gameObject); }
    }
}
