using System.Collections.Generic;
using UnityEngine;

namespace UniversalRig.Weapons
{
    // Seats a magazine with a ConfigurableJoint. It is never parented to the gun.
    // A tube port uses the same alignment test, then stores the round inside the gun:
    // the shell model has no internal tube to simulate.
    [DefaultExecutionOrder(20)]
    public sealed class MagazineSocket : MonoBehaviour
    {
        public Firearm firearm;
        public Transform seat;
        public string ammoId = "9mm";
        public bool loadsTube;
        [Min(0.005f)] public float distance = 0.035f;
        [Range(1f, 60f)] public float angle = 28f;
        public Magazine Seated { get; private set; }

        readonly Collider[] nearby = new Collider[16];
        readonly List<Collider> pairsA = new List<Collider>(8);
        readonly List<Collider> pairsB = new List<Collider>(8);
        ConfigurableJoint joint;
        Magazine guided;

        void FixedUpdate()
        {
            if (!seat || !firearm) return;
            if (Seated)
            {
                PhysicsGrabbable grab = Seated.GetComponent<PhysicsGrabbable>();
                if (!joint || !Seated.gameObject.activeInHierarchy || (grab && grab.HoldingHands > 0)) Unseat();
                return;
            }
            int count = Physics.OverlapSphereNonAlloc(seat.position, 0.22f, nearby, ~0, QueryTriggerInteraction.Ignore);
            Magazine best = null;
            float bestDistance = 0.18f;
            for (int i = 0; i < count; ++i)
            {
                if (!nearby[i] || !nearby[i].attachedRigidbody) continue;
                Magazine mag = nearby[i].attachedRigidbody.GetComponent<Magazine>();
                if (!mag || !mag.isActiveAndEnabled || mag.Socket || Time.time < mag.InsertionBlockedUntil || mag.ammoId != ammoId) continue;
                PhysicsGrabbable grab = mag.GetComponent<PhysicsGrabbable>();
                if (!grab || grab.Body.isKinematic) continue;
                float gap = Vector3.Distance(mag.transform.position, seat.position);
                if (gap > bestDistance) continue;
                if (!loadsTube && Quaternion.Angle(mag.transform.rotation, seat.rotation) > Mathf.Max(angle, 60f)) continue;
                best = mag;
                bestDistance = gap;
            }
            if (guided != best)
            {
                if (guided) IgnorePair(guided, false);
                guided = best;
                if (guided) IgnorePair(guided, true);
            }
            if (best && bestDistance <= Mathf.Max(distance, 0.065f) && ClearInsertion(best)) Seat(best);
        }

        bool ClearInsertion(Magazine mag)
        {
            Vector3 path = seat.position - mag.Body.position;
            if (path.sqrMagnitude < 0.000001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(mag.Body.position, path.normalized, path.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody == mag.Body || hit.rigidbody == firearm.Body ||
                    (firearm.action && hit.rigidbody == firearm.action.HandleBody)) continue;
                if (hit.collider.GetComponentInParent<PhysicsHand>() || hit.collider.GetComponentInParent<PhysicsRig>()) continue;
                return false;
            }
            return true;
        }

        static void ReleaseHands(Magazine mag)
        {
            PhysicsGrabbable grab = mag.GetComponent<PhysicsGrabbable>();
            while (grab && grab.PrimaryHand) grab.PrimaryHand.ReleaseToSocket();
        }

        void Seat(Magazine mag)
        {
            if (loadsTube)
            {
                if (!firearm.TryLoadTube(mag.rounds)) return;
                ReleaseHands(mag);
                if (guided) IgnorePair(guided, false);
                guided = null;
                mag.rounds = 0;
                mag.gameObject.SetActive(false);
                return;
            }
            if (!mag.TryClaim(this)) return;
            ReleaseHands(mag);
            mag.transform.SetPositionAndRotation(seat.position, seat.rotation);
            mag.Body.linearVelocity = Vector3.zero;
            mag.Body.angularVelocity = Vector3.zero;
            joint = mag.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = firearm.Body;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = firearm.transform.InverseTransformPoint(seat.position);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.breakForce = 700f;
            joint.breakTorque = 40f;
            joint.enableCollision = false;
            if (guided != mag) IgnorePair(mag, true);
            guided = null;
            Seated = mag;
        }

        public void Unseat()
        {
            if (joint)
            {
                joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Free;
                joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
                Destroy(joint);
            }
            joint = null;
            if (Seated)
            {
                Seated.InsertionBlockedUntil = Time.time + 0.35f;
                Seated.ReleaseSocket(this);
                IgnorePair(Seated, false);
            }
            Seated = null;
        }

        void IgnorePair(Magazine mag, bool ignore)
        {
            if (!ignore) 
            {
                for (int i = 0; i < pairsA.Count; ++i)
                    if (pairsA[i] && pairsB[i]) Physics.IgnoreCollision(pairsA[i], pairsB[i], false);
                pairsA.Clear();
                pairsB.Clear();
                return;
            }
            Collider[] magColliders = mag.GetComponentsInChildren<Collider>();
            var gunColliders = new List<Collider>(firearm.GetComponentsInChildren<Collider>());
            if (firearm.action && firearm.action.HandleBody)
                gunColliders.AddRange(firearm.action.HandleBody.GetComponentsInChildren<Collider>());
            foreach (Collider a in magColliders)
                foreach (Collider b in gunColliders)
                {
                    if (!a || !b) continue;
                    Physics.IgnoreCollision(a, b, true);
                    pairsA.Add(a);
                    pairsB.Add(b);
                }
        }

        void OnDisable()
        {
            if (Seated) Unseat();
            if (guided) IgnorePair(guided, false);
            guided = null;
        }
    }
}
