using System.Collections.Generic;
using UnityEngine;

namespace UniversalRig
{
    // Visual only: poses the fixed Humanoid avatar from the physical rig every frame. It is the
    // sole writer of avatar bones (the Animator stays disabled) and never moves physics bodies.
    // Head/shoulder/wrist placement uses the same RigSettings measurements as the physical
    // reach joint, so the visible arms end where the physical hands are.
    [DefaultExecutionOrder(100)]
    public sealed class AvatarPoseDriver : MonoBehaviour
    {
        public Animator avatar;
        public PhysicsRig rig;
        public RigInput input;
        public PhysicsHand leftHand;
        public PhysicsHand rightHand;
        [Tooltip("Finger curl at full grip (degrees): proximal, intermediate, distal.")]
        public Vector3 fingerCurl = new Vector3(65f, 85f, 55f);
        public Vector3 thumbCurl = new Vector3(15f, 30f, 30f);
        [Range(0f, 1f)] public float neckShare = 0.4f;
        [Tooltip("Largest horizontal hip shift used to keep the avatar's eyes on the camera.")]
        public float maximumLean = 0.35f;

        private Transform hips, neck, head;
        private Quaternion restHeadRotation;
        private float restHipHeight;
        private readonly List<Transform> bones = new List<Transform>();
        private readonly List<Quaternion> restRotations = new List<Quaternion>();
        private readonly List<Vector3> restPositions = new List<Vector3>();
        private readonly List<Finger> leftFingers = new List<Finger>();
        private readonly List<Finger> rightFingers = new List<Finger>();
        private Limb leftArm, rightArm, leftLeg, rightLeg;

        private sealed class Limb
        {
            public Transform root, mid, tip;
            public float upper, lower;
            public Vector3 restTip;
            public Quaternion restRotation;
        }

        private struct Finger
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 axis;
            public float angle;
            public bool index;
        }

        private void Start()
        {
            if (!avatar || !avatar.isHuman || !avatar.avatar || !avatar.avatar.isValid || !rig || !input || !rig.settings)
            {
                Debug.LogError("Universal Rig: visual driver requires a valid Humanoid avatar and rig references.", this);
                enabled = false;
                return;
            }
            avatar.enabled = false;
            avatar.applyRootMotion = false;
            for (int i = 0; i < (int)HumanBodyBones.LastBone; ++i)
            {
                Transform bone = avatar.GetBoneTransform((HumanBodyBones)i);
                if (!bone) continue;
                bones.Add(bone);
                restRotations.Add(bone.localRotation);
                restPositions.Add(bone.localPosition);
            }
            hips = avatar.GetBoneTransform(HumanBodyBones.Hips);
            neck = avatar.GetBoneTransform(HumanBodyBones.Neck);
            head = avatar.GetBoneTransform(HumanBodyBones.Head);
            if (!hips || !head) { enabled = false; return; }
            restHipHeight = transform.InverseTransformPoint(hips.position).y;
            restHeadRotation = Quaternion.Inverse(transform.rotation) * head.rotation;
            leftArm = MakeLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            rightArm = MakeLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            leftLeg = MakeLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            rightLeg = MakeLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            MakeFingers(true, leftFingers);
            MakeFingers(false, rightFingers);
        }

        private Limb MakeLimb(HumanBodyBones root, HumanBodyBones mid, HumanBodyBones tip)
        {
            var limb = new Limb { root = avatar.GetBoneTransform(root), mid = avatar.GetBoneTransform(mid), tip = avatar.GetBoneTransform(tip) };
            if (!limb.root || !limb.mid || !limb.tip) return null;
            limb.upper = Vector3.Distance(limb.root.position, limb.mid.position);
            limb.lower = Vector3.Distance(limb.mid.position, limb.tip.position);
            limb.restTip = transform.InverseTransformPoint(limb.tip.position);
            limb.restRotation = Quaternion.Inverse(transform.rotation) * limb.tip.rotation;
            return limb;
        }

        // Curl axis per phalanx: rotates the bone toward the palm, measured in the rest pose.
        private void MakeFingers(bool left, List<Finger> fingers)
        {
            HumanBodyBones first = left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
            Transform hand = avatar.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform middle = avatar.GetBoneTransform(first + 6);
            Transform index = avatar.GetBoneTransform(first + 3);
            Transform little = avatar.GetBoneTransform(first + 12);
            if (!hand || !middle || !index || !little) return;
            Vector3 palm = (left ? 1f : -1f) * Vector3.Cross(index.position - little.position, middle.position - hand.position).normalized;
            for (int finger = 0; finger < 5; ++finger)
                for (int joint = 0; joint < 3; ++joint)
                {
                    Transform bone = avatar.GetBoneTransform(first + finger * 3 + joint);
                    if (!bone) continue;
                    Transform next = joint < 2 ? avatar.GetBoneTransform(first + finger * 3 + joint + 1) : null;
                    Vector3 direction = next ? next.position - bone.position
                        : (bone.childCount > 0 ? bone.GetChild(0).position - bone.position : bone.position - bone.parent.position);
                    Vector3 axis = Vector3.Cross(direction, palm);
                    if (axis.sqrMagnitude < 0.0000001f) continue;
                    Vector3 curl = finger == 0 ? thumbCurl : fingerCurl;
                    fingers.Add(new Finger
                    {
                        bone = bone, rest = bone.localRotation, index = finger == 1,
                        axis = Quaternion.Inverse(bone.rotation) * axis.normalized, angle = curl[joint]
                    });
                }
        }

        private void LateUpdate()
        {
            if (!hips || !input.HeadTracked) return;
            RigSettings settings = rig.settings;
            Quaternion headRotation = input.head.rotation;
            Quaternion yaw = RigMath.Yaw(headRotation);
            transform.SetPositionAndRotation(rig.transform.position, yaw);
            for (int i = 0; i < bones.Count; ++i)
                bones[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);

            // Share some of the head turn with the neck, then put the eyes exactly on the camera
            // by shifting the upper body (bounded lean; crouching lowers the hips).
            if (neck)
            {
                Quaternion relative = Quaternion.Inverse(yaw) * headRotation;
                neck.rotation = yaw * Quaternion.Slerp(Quaternion.identity, relative, neckShare) * Quaternion.Inverse(yaw) * neck.rotation;
            }
            head.rotation = headRotation * restHeadRotation;
            Vector3 shift = settings.HeadBoneFromEye(input.head.position, headRotation) - head.position;
            float hipY = Mathf.Clamp(restHipHeight + shift.y, 0.25f, restHipHeight + 0.1f) - restHipHeight;
            hips.position += Vector3.ClampMagnitude(RigMath.Planar(shift), maximumLean) + Vector3.up * hipY;

            if (leftHand && input.LeftTracked) SolveArm(leftArm, leftHand, settings.leftHandBoneRotation, -1f, yaw);
            if (rightHand && input.RightTracked) SolveArm(rightArm, rightHand, settings.rightHandBoneRotation, 1f, yaw);
            SolveFoot(leftLeg);
            SolveFoot(rightLeg);
            CurlFingers(leftFingers, leftHand);
            CurlFingers(rightFingers, rightHand);
        }

        private void SolveArm(Limb arm, PhysicsHand hand, Quaternion boneInGrip, float side, Quaternion yaw)
        {
            if (arm == null) return;
            Transform grip = hand.transform;
            Vector3 pole = arm.root.position + yaw * new Vector3(side * 0.4f, -0.6f, -0.4f);
            Solve(arm, grip.TransformPoint(hand.WristOffset), pole, grip.rotation * boneInGrip);
        }

        private void SolveFoot(Limb limb)
        {
            if (limb == null) return;
            Vector3 target = transform.TransformPoint(limb.restTip);
            if (Physics.Raycast(target + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 0.8f,
                rig.settings.environmentMask, QueryTriggerInteraction.Ignore) && !rig.IgnoresBody(hit.collider))
                target.y = hit.point.y + limb.restTip.y;
            Solve(limb, target, limb.root.position + transform.forward, transform.rotation * limb.restRotation);
        }

        private static void CurlFingers(List<Finger> fingers, PhysicsHand hand)
        {
            if (!hand) return;
            GripPoint gripPoint = hand.ActiveGrip;
            float grip = gripPoint ? gripPoint.fingerCurl : hand.Holding ? 1f : hand.Grip;
            // A weapon grip keeps the index on the trigger; a plain grab still closes it.
            float index = gripPoint ? hand.Trigger : hand.Holding ? 1f : hand.Trigger;
            foreach (Finger finger in fingers)
                finger.bone.localRotation = finger.rest *
                    Quaternion.AngleAxis((finger.index ? index : grip) * finger.angle, finger.axis);
        }

        private static void Solve(Limb limb, Vector3 target, Vector3 pole, Quaternion rotation)
        {
            if (limb == null) return;
            Vector3 bend = RigMath.SolveBend(limb.root.position, target, pole, limb.upper, limb.lower, out Vector3 end);
            limb.root.rotation = Quaternion.FromToRotation(limb.mid.position - limb.root.position,
                bend - limb.root.position) * limb.root.rotation;
            limb.mid.rotation = Quaternion.FromToRotation(limb.tip.position - limb.mid.position,
                end - limb.mid.position) * limb.mid.rotation;
            limb.tip.rotation = rotation;
        }
    }
}
