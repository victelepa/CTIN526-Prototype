using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Horizontal-only follow. The split-screen viewport is roughly 16:4.5, so
    /// vertical camera motion in that little strip reads as nausea rather than
    /// information; height stays locked to the track.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [Tooltip("How far ahead of the target the camera sits, in world units.")]
        [SerializeField] float lookAhead = 4f;
        [SerializeField] float smoothTime = 0.15f;
        [SerializeField] float lockedY;
        [SerializeField] Vector2 xLimits = new Vector2(float.NegativeInfinity, float.PositiveInfinity);

        CameraShake _shake;
        // Tracked separately from transform.position so shake offsets never feed
        // back into the smoothing.
        float _followX;
        float _velocity;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public float LockedY
        {
            get => lockedY;
            set => lockedY = value;
        }

        void Awake()
        {
            _shake = GetComponent<CameraShake>();
            _followX = transform.position.x;
        }

        public void SetXLimits(float min, float max) => xLimits = new Vector2(min, max);

        public void SetLookAhead(float value) => lookAhead = value;

        public void SnapToTarget()
        {
            if (target == null) return;
            _velocity = 0f;
            _followX = DesiredX();
            ApplyPosition();
        }

        void LateUpdate()
        {
            if (target == null) return;

            _followX = Mathf.SmoothDamp(_followX, DesiredX(), ref _velocity, smoothTime);
            ApplyPosition();
        }

        void ApplyPosition()
        {
            Vector2 offset = _shake != null ? _shake.Offset : Vector2.zero;
            transform.position = new Vector3(_followX + offset.x, lockedY + offset.y, transform.position.z);
        }

        float DesiredX() => Mathf.Clamp(target.position.x + lookAhead, xLimits.x, xLimits.y);
    }
}
