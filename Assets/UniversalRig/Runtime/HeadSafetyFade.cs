using UnityEngine;

namespace UniversalRig
{
    [DefaultExecutionOrder(200)]
    public sealed class HeadSafetyFade : MonoBehaviour
    {
        public PhysicsRig rig;
        public RigInput input;
        public Renderer fadeRenderer;
        private MaterialPropertyBlock properties;
        private float alpha;

        private void Awake() { properties = new MaterialPropertyBlock(); }
        private void OnEnable() { Application.onBeforeRender += Refresh; }
        private void OnDisable()
        {
            Application.onBeforeRender -= Refresh;
            if (fadeRenderer) fadeRenderer.enabled = false;
        }
        private void LateUpdate() { Refresh(); }

        [BeforeRenderOrder(100)]
        private void Refresh()
        {
            if (!rig || !input || !input.head || !fadeRenderer) return;
            bool blocked = !input.HeadTracked || Physics.CheckSphere(input.head.position, 0.09f,
                rig.settings.environmentMask, QueryTriggerInteraction.Ignore) ||
                RigMath.Planar(input.head.position - rig.transform.position).magnitude > 0.6f;
            alpha = blocked ? 1f : Mathf.MoveTowards(alpha, 0f, Time.unscaledDeltaTime * 3f);
            properties.SetColor("_BaseColor", new Color(0f, 0f, 0f, alpha));
            fadeRenderer.SetPropertyBlock(properties);
            fadeRenderer.enabled = alpha > 0.001f;
        }
    }
}
