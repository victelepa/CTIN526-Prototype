using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// A tinted quad parented to one camera, so it covers exactly that player's half
    /// of a split screen and nothing else. Resizes itself from the camera every frame
    /// so it survives window resizes and orthographic size changes.
    /// </summary>
    public class ViewportFlash : MonoBehaviour
    {
        [SerializeField] float fadeDuration = 0.35f;
        [SerializeField] float blindFadeDuration = 0.25f;

        Camera _camera;
        SpriteRenderer _renderer;
        Color _flashTint = Color.white;
        float _flashStrength;
        float _flashRemaining;

        Color _blindTint = Color.black;
        float _blindStrength;
        float _blindRemaining;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _camera = GetComponentInParent<Camera>();
            SetAlpha(0f);
        }

        public void Flash(Color color, float strength)
        {
            _flashTint = color;
            _flashStrength = strength;
            _flashRemaining = fadeDuration;
        }

        /// <summary>
        /// Obscures this camera's viewport without affecting the opponent's half of
        /// the split screen. A hit flash may briefly take visual priority, after which
        /// the remaining blind resumes instead of being cancelled.
        /// </summary>
        public void Blind(Color color, float strength, float duration)
        {
            _blindTint = color;
            _blindStrength = Mathf.Clamp01(strength);
            _blindRemaining = Mathf.Max(_blindRemaining, duration);
        }

        void LateUpdate()
        {
            if (_camera != null)
            {
                float height = _camera.orthographicSize * 2f;
                transform.localScale = new Vector3(height * _camera.aspect, height, 1f);
            }

            _flashRemaining -= Time.unscaledDeltaTime;
            _blindRemaining -= Time.unscaledDeltaTime;

            if (_flashRemaining > 0f)
            {
                SetTint(_flashTint, Mathf.Clamp01(_flashRemaining / fadeDuration) * _flashStrength);
                return;
            }

            if (_blindRemaining > 0f)
            {
                float fade = blindFadeDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(_blindRemaining / blindFadeDuration);
                SetTint(_blindTint, fade * _blindStrength);
                return;
            }

            SetAlpha(0f);
        }

        void SetTint(Color tint, float alpha)
        {
            if (_renderer == null) return;
            _renderer.color = new Color(tint.r, tint.g, tint.b, alpha);
        }

        void SetAlpha(float alpha)
        {
            if (_renderer == null) return;
            Color color = _renderer.color;
            color.a = alpha;
            _renderer.color = color;
        }
    }
}
