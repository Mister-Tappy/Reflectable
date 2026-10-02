using UnityEngine;

namespace Reflectable
{
    /// <summary>Unscaled, bounded camera follow used only by the floating-island menu.</summary>
    public sealed class MenuCameraFollow2D : MonoBehaviour
    {
        [SerializeField, Min(.01f)] float followDamping = .12f;
        [SerializeField, Min(.01f)] float zoomDamping = .18f;
        [SerializeField] Vector2 minimumPosition = new Vector2(-10.5f, -7.5f);
        [SerializeField] Vector2 maximumPosition = new Vector2(10.5f, 7.5f);

        Camera controlledCamera;
        Transform followTarget;
        Vector3 targetPosition;
        Vector3 followOffset;
        Vector3 positionVelocity;
        float targetZoom;
        float zoomVelocity;
        float cameraZ;

        public void Initialize(Camera camera, Vector3 initialPosition, float initialZoom)
        {
            controlledCamera = camera;
            cameraZ = initialPosition.z;
            targetPosition = initialPosition;
            targetZoom = initialZoom;
            if (controlledCamera) controlledCamera.transform.position = initialPosition;
        }

        public void Follow(Transform target, float zoom, Vector3 offset = default)
        {
            followTarget = target;
            followOffset = offset;
            targetZoom = zoom;
        }

        public void MoveTo(Vector3 position, float zoom)
        {
            followTarget = null;
            followOffset = Vector3.zero;
            targetPosition = position;
            targetZoom = zoom;
        }

        public void SnapTo(Vector3 position, float zoom)
        {
            followTarget = null;
            targetPosition = position;
            targetZoom = zoom;
            positionVelocity = Vector3.zero;
            zoomVelocity = 0f;
            if (!controlledCamera) return;
            controlledCamera.transform.position = position;
            if (controlledCamera.orthographic) controlledCamera.orthographicSize = zoom;
        }

        void LateUpdate()
        {
            if (!controlledCamera) return;
            if (followTarget) targetPosition = followTarget.position + followOffset;
            float marginY = controlledCamera.orthographic ? controlledCamera.orthographicSize : 0f;
            float marginX = marginY * controlledCamera.aspect;
            Vector3 bounded = targetPosition;
            bounded.x = Mathf.Clamp(bounded.x, minimumPosition.x + marginX * .08f, maximumPosition.x - marginX * .08f);
            bounded.y = Mathf.Clamp(bounded.y, minimumPosition.y + marginY * .08f, maximumPosition.y - marginY * .08f);
            bounded.z = cameraZ;
            float sensitivity = MenuSettingsAudioMockup.CameraSensitivity;
            float damping = followTarget ? Mathf.Lerp(followDamping * 2.1f, followDamping * .42f, sensitivity) : followDamping;
            controlledCamera.transform.position = Vector3.SmoothDamp(controlledCamera.transform.position, bounded, ref positionVelocity, damping, Mathf.Infinity, Time.unscaledDeltaTime);
            if (controlledCamera.orthographic)
                controlledCamera.orthographicSize = Mathf.SmoothDamp(controlledCamera.orthographicSize, targetZoom, ref zoomVelocity, zoomDamping, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }
}
