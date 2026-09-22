using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Follow cameras on separate parallel tracks make it impossible to tell who is
    /// ahead by looking at the world, and "I am behind" is the only thing that makes
    /// a player reach for an item. So every viewport carries its own copy of this.
    /// Built as world-space sprites parented to one camera, which keeps it inside that
    /// camera's half of the split screen without any canvas setup.
    /// </summary>
    public class RaceProgressBar : MonoBehaviour
    {
        [Tooltip("Bar width as a fraction of the camera's visible half-width.")]
        [SerializeField, Range(0.2f, 1f)] float widthFraction = 0.82f;
        [Tooltip("Height up the viewport, as a fraction of the camera's visible half-height.")]
        [SerializeField, Range(0f, 1f)] float verticalAnchor = 0.74f;
        [SerializeField] float barThickness = 0.12f;
        [SerializeField] float markerSize = 0.42f;
        [SerializeField] float flashDuration = 0.5f;

        Camera _camera;
        Transform _self;
        Transform _opponent;
        float _startX;
        float _finishX = 1f;

        SpriteRenderer _bar;
        SpriteRenderer _selfMarker;
        SpriteRenderer _opponentMarker;
        Color _selfColor = Color.white;
        Color _opponentColor = Color.white;
        float _selfFlash;
        float _opponentFlash;

        public void Configure(Camera owner, Transform self, Color selfColor, Transform opponent, Color opponentColor,
            float startX, float finishX)
        {
            _camera = owner;
            _self = self;
            _opponent = opponent;
            _selfColor = selfColor;
            _opponentColor = opponentColor;
            _startX = startX;
            _finishX = finishX;

            _bar = PrimitiveSprite.CreateBox("Bar", transform, Vector2.zero, Vector2.one,
                new Color(1f, 1f, 1f, 0.16f), PrimitiveSprite.ColliderKind.None, 900);
            _opponentMarker = PrimitiveSprite.CreateBox("OpponentMarker", transform, Vector2.zero, Vector2.one,
                opponentColor, PrimitiveSprite.ColliderKind.None, 901);
            _selfMarker = PrimitiveSprite.CreateBox("SelfMarker", transform, Vector2.zero, Vector2.one,
                selfColor, PrimitiveSprite.ColliderKind.None, 902);
        }

        public void FlashSelf() => _selfFlash = flashDuration;

        public void FlashOpponent() => _opponentFlash = flashDuration;

        void LateUpdate()
        {
            if (_camera == null || _bar == null) return;

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect * widthFraction;

            transform.localPosition = new Vector3(0f, halfHeight * verticalAnchor, 1f);

            _bar.transform.localPosition = Vector3.zero;
            _bar.transform.localScale = new Vector3(halfWidth * 2f, barThickness, 1f);

            PlaceMarker(_selfMarker, _self, halfWidth, -markerSize * 0.78f, markerSize);
            PlaceMarker(_opponentMarker, _opponent, halfWidth, markerSize * 0.78f, markerSize * 0.82f);

            _selfFlash = Mathf.Max(0f, _selfFlash - Time.unscaledDeltaTime);
            _opponentFlash = Mathf.Max(0f, _opponentFlash - Time.unscaledDeltaTime);
            ApplyFlash(_selfMarker, _selfColor, _selfFlash);
            ApplyFlash(_opponentMarker, _opponentColor, _opponentFlash);
        }

        void PlaceMarker(SpriteRenderer marker, Transform tracked, float halfWidth, float localY, float size)
        {
            if (marker == null || tracked == null) return;

            float span = Mathf.Max(0.001f, _finishX - _startX);
            float progress = Mathf.Clamp01((tracked.position.x - _startX) / span);

            marker.transform.localPosition = new Vector3(Mathf.Lerp(-halfWidth, halfWidth, progress), localY, 0f);
            marker.transform.localScale = new Vector3(size, size, 1f);
        }

        void ApplyFlash(SpriteRenderer marker, Color baseColor, float remaining)
        {
            if (marker == null) return;
            marker.color = Color.Lerp(baseColor, Color.white, remaining / flashDuration);
        }
    }
}
