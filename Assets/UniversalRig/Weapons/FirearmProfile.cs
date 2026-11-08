using UnityEngine;

namespace UniversalRig.Weapons
{
    public enum FirearmCycle { SelfLoading, OpenBolt, Pump }
    public enum FireMode { Semi, Auto }

    [CreateAssetMenu(menuName = "Universal Rig/Firearm Profile")]
    public sealed class FirearmProfile : ScriptableObject
    {
        public string displayName;
        public string ammoId = "9mm";
        public FirearmCycle cycle = FirearmCycle.SelfLoading;
        public bool allowSemi = true;
        public bool allowAuto;
        public bool lockOpenOnEmpty = true;
        [Min(30f)] public float roundsPerMinute = 400f;
        [Min(0f)] public float semiDelay = 0.12f;
        [Min(0)] public int tubeCapacity;
        [Min(0f)] public float recoilImpulse = 0.45f;
        [Min(0f)] public float recoilImpulseMax = 0.8f;
        [Min(0f)] public float recoilTorque = 0.05f;
        [Min(0f)] public float recoilTorqueMax = 0.12f;
        [Min(0f)] public float maxRecoilSpeed = 0.4f;
        [Min(1)] public int pellets = 1;
        [Min(0f)] public float spreadDegrees;
        [Min(1f)] public float range = 80f;
        [Min(0.01f)] public float muzzleBackoff = 0.08f;
        [Min(0f)] public float obstructionSlack = 0.025f;
        [Range(0.3f, 0.95f)] public float actionThreshold = 0.72f;
        [Range(0f, 1f)] public float hapticAmplitude = 0.35f;
        [Range(0f, 1f)] public float fireHaptic = 0.75f;
        [Min(0.01f)] public float hapticDuration = 0.04f;
    }
}
