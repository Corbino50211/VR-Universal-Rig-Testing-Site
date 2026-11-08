using System.Collections.Generic;
using UnityEngine;
using UniversalRig;

namespace UniversalRig.Weapons
{
    // Shared gun. Hitscan from the muzzle (see Cast). A round leaves the barrel only when
    // ChamberLoaded is true inside Shoot. Targets implement IHitReceiver; colliders without
    // that interface take an impact spark and nothing else.
    //
    // Muzzle obstruction: the ray starts muzzleBackoff behind the muzzle. A hit at or behind
    // the muzzle, or a foreign collider overlapping the muzzle, stops the shot. The local
    // rig is excluded with PhysicsRig.IgnoresBody and by skipping the rig hierarchy.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(PhysicsGrabbable))]
    [DefaultExecutionOrder(30)]
    public sealed class Firearm : MonoBehaviour
    {
        public FirearmProfile profile;
        public Transform muzzle;
        public MagazineSocket socket;
        public ActionPart action;
        public Transform selector;
        public Transform triggerVisual;
        public Transform hammerVisual;
        public TextMesh modeLabel;
        public Light muzzleLight;

        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();
        public PhysicsGrabbable Grabbable => grabbable ? grabbable : grabbable = GetComponent<PhysicsGrabbable>();
        public bool ChamberLoaded { get; private set; }
        public bool NeedsCycle { get; private set; }
        public bool Cocked { get; private set; }
        public bool LastBlocked { get; private set; }
        public int TubeRounds { get; private set; }
        public int ShotsFired { get; private set; }
        public int DryFires { get; private set; }
        public float LastImpulse { get; private set; }
        public FireMode Mode { get; private set; }
        public Magazine Magazine => socket ? socket.Seated : null;
        public bool LockedOpen => action && action.LockedOpen;

        Rigidbody body;
        PhysicsGrabbable grabbable;
        Quaternion triggerRest, hammerRest;
        float nextShot, nextDry, hammerUntil, lightUntil;
        bool triggerHeld, selectorHeld;
        bool audioNoted;
        readonly List<Collider> ignoredExtras = new List<Collider>(12);
        readonly List<Collider> scratch = new List<Collider>(12);
        Collider[] handleColliders = System.Array.Empty<Collider>();
        Collider[] magazineColliders = System.Array.Empty<Collider>();
        Magazine cachedMagazine;
        readonly RaycastHit[] hitBuffer = new RaycastHit[24];
        readonly Collider[] overlapBuffer = new Collider[12];
        readonly float[] distances = new float[24];
        readonly bool[] ignored = new bool[24];
        static AudioClip fireClip, dryClip, mechClip;

        void Awake()
        {
            if (!profile || !muzzle)
            {
                Debug.LogError("Universal Rig firearm: assign a profile and a muzzle.", this);
                enabled = false;
                return;
            }
            Mode = profile.allowSemi ? FireMode.Semi : FireMode.Auto;
            if (triggerVisual) triggerRest = triggerVisual.localRotation;
            if (hammerVisual) hammerRest = hammerVisual.localRotation;
            if (muzzleLight) muzzleLight.enabled = false;
        }

        void FixedUpdate()
        {
            SyncExtras();
            PhysicsHand hand = FiringHand();
            float trigger = hand ? hand.Trigger : 0f;
            bool down = trigger >= 0.65f;
            bool edge = down && !triggerHeld;
            triggerHeld = down;
            if (edge || (Mode == FireMode.Auto && profile.allowAuto && down)) TryFire(edge);
            CheckSelector(hand);
        }

        void LateUpdate()
        {
            PhysicsHand hand = FiringHand();
            if (triggerVisual)
                triggerVisual.localRotation = triggerRest * Quaternion.Euler((hand ? hand.Trigger : 0f) * 18f, 0f, 0f);
            if (hammerVisual)
                hammerVisual.localRotation = hammerRest * Quaternion.Euler(Time.time < hammerUntil ? -28f : 0f, 0f, 0f);
            if (muzzleLight && Time.time > lightUntil) muzzleLight.enabled = false;
            if (modeLabel) modeLabel.text = Mode == FireMode.Auto ? "AUTO" : profile.cycle == FirearmCycle.Pump ? "PUMP" : "SEMI";
        }

        public void CycleMode()
        {
            if (!profile.allowSemi || !profile.allowAuto) return;
            Mode = Mode == FireMode.Semi ? FireMode.Auto : FireMode.Semi;
        }

        public bool TryLoadTube(int rounds)
        {
            if (rounds <= 0 || profile.tubeCapacity <= 0 || TubeRounds >= profile.tubeCapacity) return false;
            TubeRounds = Mathf.Min(profile.tubeCapacity, TubeRounds + rounds);
            Tone(mechClip ? mechClip : Mech(), 0.7f);
            return true;
        }

        public void NotifyOpened()
        {
            if (profile.cycle != FirearmCycle.OpenBolt) return;
            Cocked = true;
            if (action) action.SetLocked(true);
        }

        public void ReleaseSlideLock() { if (action) action.SetLocked(false); }

        public void OnActionClosed()
        {
            if (profile.cycle == FirearmCycle.OpenBolt) return;
            if (profile.cycle == FirearmCycle.Pump)
            {
                ChamberLoaded = false;
                if (TubeRounds > 0) { TubeRounds--; ChamberLoaded = true; }
                NeedsCycle = false;
                Tone(Mech(), 0.8f);
                return;
            }
            ChamberLoaded = false; // ponytail: ejected rounds are not spawned; there is no casing mesh.
            if (TakeRound())
            {
                ChamberLoaded = true;
                if (action) action.SetLocked(false);
            }
            else if (profile.lockOpenOnEmpty && action) action.SetLocked(true);
            Tone(Mech(), 0.8f);
        }

        void TryFire(bool edge)
        {
            if (Time.time < nextShot) return;
            if (!edge && Mode != FireMode.Auto) return;
            if (profile.cycle == FirearmCycle.OpenBolt) { FireOpen(); return; }
            if (NeedsCycle || (action && action.Open01 > 0.4f) || !ChamberLoaded) { if (edge) Dry(); return; }
            Shoot();
            if (profile.cycle == FirearmCycle.Pump) NeedsCycle = true;
            else if (TakeRound())
            {
                ChamberLoaded = true;
                if (action) { action.Suppress(0.2f); action.Blip(); }
            }
            else if (profile.lockOpenOnEmpty && action) action.SetLocked(true);
            nextShot = Time.time + (Mode == FireMode.Auto && profile.allowAuto
                ? 60f / Mathf.Max(60f, profile.roundsPerMinute) : profile.semiDelay);
        }

        void FireOpen()
        {
            if (!Cocked) { Dry(); return; }
            if (!TakeRound())
            {
                Cocked = false;
                if (action) action.SetLocked(false);
                Dry();
                return;
            }
            ChamberLoaded = true;
            Shoot();
            // ponytail: open-bolt auto stays latched and meters the magazine. A reciprocating
            // bolt is the upgrade if the handle should slam closed on every shot.
            bool more = Magazine && Magazine.rounds > 0;
            Cocked = more;
            if (action) action.SetLocked(more);
            nextShot = Time.time + (Mode == FireMode.Auto && profile.allowAuto
                ? 60f / Mathf.Max(60f, profile.roundsPerMinute) : profile.semiDelay);
        }

        void Shoot()
        {
            if (!ChamberLoaded) { Dry(); return; }
            ChamberLoaded = false;
            ShotsFired++;
            Recoil();
            bool clear = false;
            int pellets = Mathf.Max(1, profile.pellets);
            for (int i = 0; i < pellets; ++i)
            {
                float along = pellets == 1 ? 0f : (i + 0.5f) / pellets;
                float around = (i * 0.618034f) % 1f;
                if (Cast(RigMath.ConeDirection(muzzle.forward, profile.spreadDegrees, along, around))) clear = true;
            }
            LastBlocked = !clear;
            hammerUntil = Time.time + 0.08f;
            Flash();
            Tone(FireClip(), 1f);
            Buzz(profile.fireHaptic);
        }

        void Dry()
        {
            if (Time.time < nextDry) return;
            nextDry = Time.time + 0.18f;
            DryFires++;
            LastBlocked = false;
            Tone(DryClip(), 0.6f);
            Buzz(profile.hapticAmplitude);
        }

        // True when the pellet is not stopped by a muzzle buried in geometry.
        bool Cast(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.000001f) direction = muzzle.forward;
            direction.Normalize();
            float backoff = profile.muzzleBackoff;
            Vector3 origin = muzzle.position - direction * backoff;
            bool buried = MuzzleBuried();
            int count = Physics.RaycastNonAlloc(origin, direction, hitBuffer, profile.range + backoff,
                Mask(), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; ++i)
            {
                distances[i] = hitBuffer[i].distance;
                ignored[i] = IsIgnored(hitBuffer[i].collider);
            }
            int index = RigMath.SelectShotHit(distances, ignored, count, backoff, profile.obstructionSlack, out bool obstructed);
            if (buried && (index < 0 || !obstructed)) return false;
            if (index < 0) return true;
            RaycastHit hit = hitBuffer[index];
            Deliver(hit, direction);
            return !obstructed;
        }

        void Deliver(RaycastHit hit, Vector3 direction)
        {
            Spark(hit.point);
            IHitReceiver receiver = hit.collider.GetComponentInParent<IHitReceiver>();
            if (receiver == null) return;
            receiver.ReceiveHit(new FirearmHit
            {
                point = hit.point, normal = hit.normal, direction = direction,
                distance = hit.distance, collider = hit.collider, source = this
            });
        }

        bool MuzzleBuried()
        {
            int count = Physics.OverlapSphereNonAlloc(muzzle.position, 0.02f, overlapBuffer, Mask(), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; ++i)
                if (overlapBuffer[i] && !overlapBuffer[i].isTrigger && !IsIgnored(overlapBuffer[i])) return true;
            return false;
        }

        bool IsIgnored(Collider collider)
        {
            if (!collider) return true;
            Rigidbody hitBody = collider.attachedRigidbody;
            if (hitBody && (hitBody == Body || (action && hitBody == action.HandleBody) || (Magazine && hitBody == Magazine.Body)))
                return true;
            PhysicsHand hand = Grabbable.PrimaryHand ? Grabbable.PrimaryHand : Grabbable.SecondaryHand;
            if (!hand || !hand.rig) return false;
            if (hand.rig.IgnoresBody(collider)) return true;
            Transform root = hand.rig.transform.parent ? hand.rig.transform.parent : hand.rig.transform;
            return collider.transform == root || collider.transform.IsChildOf(root);
        }

        int Mask()
        {
            PhysicsHand hand = Grabbable.PrimaryHand ? Grabbable.PrimaryHand : Grabbable.SecondaryHand;
            return hand && hand.rig && hand.rig.settings ? hand.rig.settings.environmentMask.value : Physics.DefaultRaycastLayers;
        }

        bool TakeRound()
        {
            if (!Magazine || Magazine.rounds <= 0) return false;
            Magazine.rounds--;
            return true;
        }

        bool Braced()
        {
            if (Grabbable.HoldingHands >= 2) return true;
            if (!action || !action.HandleBody || Grabbable.HoldingHands == 0) return false;
            PhysicsGrabbable handle = action.HandleBody.GetComponent<PhysicsGrabbable>();
            return handle && handle.HoldingHands > 0;
        }

        void Recoil()
        {
            // Second hand on the gun or on the slide/bolt/pump cuts the impulse. The cap stays
            // under maxRecoilSpeed either way, so a shot cannot be used as a thruster.
            float scale = Braced() ? 0.45f : 1f;
            Vector3 impulse = RigMath.BoundedImpulse(-muzzle.forward, profile.recoilImpulse * scale, profile.recoilImpulseMax);
            LastImpulse = impulse.magnitude;
            if (impulse.sqrMagnitude < 0.000001f) return;
            Vector3 before = Body.linearVelocity;
            Body.AddForce(impulse, ForceMode.Impulse);
            float cap = profile.maxRecoilSpeed * scale;
            Vector3 added = Vector3.Project(Body.linearVelocity - before, impulse);
            if (added.magnitude > cap)
                Body.linearVelocity -= added.normalized * (added.magnitude - cap);
            Vector3 torque = RigMath.BoundedImpulse(muzzle.up, profile.recoilTorque * scale, profile.recoilTorqueMax);
            Body.AddTorque(torque, ForceMode.Impulse);
            if (Body.angularVelocity.magnitude > 2f)
                Body.angularVelocity = Body.angularVelocity.normalized * 2f;
        }

        PhysicsHand FiringHand()
        {
            PhysicsHand primary = Grabbable.PrimaryHand;
            PhysicsHand secondary = Grabbable.SecondaryHand;
            if (primary && primary.ActiveGrip && primary.ActiveGrip.role == GripPoint.Role.Support && secondary) return secondary;
            return primary ? primary : secondary;
        }

        void CheckSelector(PhysicsHand firing)
        {
            if (!selector) return;
            PhysicsHand other = OtherHand(firing);
            bool down = other && other.Trigger >= 0.65f && (other.Body.position - selector.position).sqrMagnitude < 0.0144f;
            if (down && !selectorHeld) CycleMode();
            selectorHeld = down;
        }

        PhysicsHand OtherHand(PhysicsHand firing)
        {
            if (!firing || !firing.rig) return null;
            PhysicsHand other = firing.isLeft ? firing.rig.rightHand : firing.rig.leftHand;
            return other == firing ? null : other;
        }

        void Buzz(float amplitude)
        {
            PhysicsHand hand = FiringHand();
            if (hand) hand.Pulse(amplitude, profile.hapticDuration);
            PhysicsHand other = OtherHand(hand);
            if (other && Grabbable.SecondaryHand) other.Pulse(amplitude * 0.4f, profile.hapticDuration);
        }

        void Flash()
        {
            if (!muzzleLight) return;
            muzzleLight.enabled = true;
            lightUntil = Time.time + 0.04f;
        }

        void Spark(Vector3 point)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "Shot impact";
            spark.transform.SetPositionAndRotation(point, Quaternion.identity);
            spark.transform.localScale = Vector3.one * 0.025f;
            Destroy(spark.GetComponent<Collider>());
            Destroy(spark, 0.15f);
        }

        void Tone(AudioClip clip, float volume)
        {
            if (!clip) return;
            AudioSource source = GetComponent<AudioSource>();
            if (!source) source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.playOnAwake = false;
            source.PlayOneShot(clip, volume);
        }

        void SyncExtras()
        {
            PhysicsHand hand = Grabbable.PrimaryHand ? Grabbable.PrimaryHand : Grabbable.SecondaryHand;
            if (!hand || !hand.rig) return;
            PhysicsRig rig = hand.rig;
            if (handleColliders.Length == 0 && action && action.HandleBody)
                handleColliders = action.HandleBody.GetComponents<Collider>();
            Magazine seated = Magazine;
            if (seated != cachedMagazine)
            {
                cachedMagazine = seated;
                magazineColliders = seated ? seated.GetComponentsInChildren<Collider>() : System.Array.Empty<Collider>();
            }
            scratch.Clear();
            if (Grabbable.HoldingHands > 0)
            {
                scratch.AddRange(handleColliders);
                scratch.AddRange(magazineColliders);
            }
            for (int i = ignoredExtras.Count - 1; i >= 0; --i)
            {
                Collider collider = ignoredExtras[i];
                if (scratch.Contains(collider)) continue;
                PhysicsGrabbable owner = collider && collider.attachedRigidbody
                    ? collider.attachedRigidbody.GetComponent<PhysicsGrabbable>() : null;
                if (owner && owner.HoldingHands > 0) continue;
                rig.QueueRestoreCollider(collider);
                ignoredExtras.RemoveAt(i);
            }
            for (int i = 0; i < scratch.Count; ++i)
            {
                if (!scratch[i] || ignoredExtras.Contains(scratch[i])) continue;
                rig.IgnoreCollider(scratch[i]);
                ignoredExtras.Add(scratch[i]);
            }
        }

        void NoteAudio()
        {
            if (audioNoted) return;
            audioNoted = true;
            Debug.Log("Universal Rig firearms: temporary synthesized clips. This project has no firearm audio.");
        }

        AudioClip FireClip() { NoteAudio(); return fireClip ? fireClip : fireClip = MakeClip("TEMPORARY firearm shot", 0.08f, 120f); }
        AudioClip DryClip() { NoteAudio(); return dryClip ? dryClip : dryClip = MakeClip("TEMPORARY firearm dry fire", 0.03f, 900f); }
        AudioClip Mech() { NoteAudio(); return mechClip ? mechClip : mechClip = MakeClip("TEMPORARY firearm mechanism", 0.04f, 420f); }

        static AudioClip MakeClip(string clipName, float seconds, float frequency)
        {
            const int rate = 22050;
            int samples = Mathf.Max(8, (int)(rate * seconds));
            float[] data = new float[samples];
            for (int i = 0; i < samples; ++i)
            {
                float env = 1f - i / (float)samples;
                env *= env;
                float noise = Mathf.Repeat(i * 0.013f, 1f) * 2f - 1f;
                data[i] = (Mathf.Sin(i * frequency * Mathf.PI * 2f / rate) * 0.3f + noise * 0.7f) * env * 0.3f;
            }
            AudioClip clip = AudioClip.Create(clipName, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
