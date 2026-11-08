using System;
using UnityEditor;
using UnityEngine;

namespace UniversalRig.Editor
{
    public static class RigValidator
    {
        [MenuItem("Tools/Universal Rig/Validate Active Rig")]
        public static void ValidateActiveRig()
        {
            PhysicsRig[] rigs = UnityEngine.Object.FindObjectsByType<PhysicsRig>(FindObjectsSortMode.None);
            Require(rigs.Length == 1, "The loaded scenes must contain exactly one active local PhysicsRig.");
            PhysicsRig rig = rigs[0];
            Require(rig.settings && rig.input && rig.leftHand && rig.rightHand, "Assign all PhysicsRig references.");
            RigInput input = rig.input;
            Require(input.trackingSpace && input.head && input.leftTarget && input.rightTarget, "Assign all tracking transforms.");
            Require(input.actionsAsset, "Assign the serialized input action asset.");
            Require(input.head.parent == input.trackingSpace && input.leftTarget.parent == input.trackingSpace &&
                input.rightTarget.parent == input.trackingSpace, "Tracked nodes must be direct children of Tracking Space.");
            Require(rig.transform.lossyScale == Vector3.one && input.trackingSpace.lossyScale == Vector3.one,
                "Rig transforms must have unit scale. Use calibration/settings, not transform scaling.");
            Require((rig.settings.environmentMask.value & (1 << rig.gameObject.layer)) == 0,
                "Environment query mask must exclude the rig physics layer.");
            Require(rig.leftHand.gameObject.layer == rig.gameObject.layer && rig.rightHand.gameObject.layer == rig.gameObject.layer,
                "Body and hands must use the rig physics layer.");
            Require(rig.leftHand.settings == rig.settings && rig.rightHand.settings == rig.settings &&
                rig.leftHand.input == input && rig.rightHand.input == input, "Hands must reference this rig's settings and input.");
            Require(rig.leftHand.isLeft && !rig.rightHand.isLeft, "Check hand side assignments.");
            Require(!rig.GetComponent<Rigidbody>().isKinematic, "Body must be dynamic.");
            Require(input.actionsAsset.FindActionMap("UniversalRig", false) != null, "UniversalRig action map is missing.");
            Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1,
                "Loaded scenes need exactly one active AudioListener.");
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Require(cameras.Length == 1 && cameras[0].transform == input.head, "Use one active head camera during initial rig validation.");
            if (Time.fixedDeltaTime > 1f / 72f)
                Debug.LogWarning("Physics timestep is still coarse for PCVR. Run Tools/Universal Rig/Set PCVR Physics to 90 Hz and profile.");
            Debug.Log("Universal Rig reference validation passed. This does not replace compilation, Play Mode, XR binding, or headset physics tests.", rig);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Universal Rig validation: " + message);
        }
    }
}
