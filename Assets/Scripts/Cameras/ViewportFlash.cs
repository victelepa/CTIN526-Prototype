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

        Camera _camera;
        SpriteRenderer _renderer;
        Color _tint = Color.white;
        float _strength;
        float _remaining;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _camera = GetComponentInParent<Camera>();
            SetAlpha(0f);
        }

        public void Flash(Color color, float strength)
        {
            _tint = color;
            _strength = strength;
            _remaining = fadeDuration;
        }

        void LateUpdate()
        {
            if (_camera != null)
            {
                float height = _camera.orthographicSize * 2f;
                transform.localScale = new Vector3(height * _camera.aspect, height, 1f);
            }

            if (_remaining <= 0f) return;

            _remaining -= Time.unscaledDeltaTime;
            SetAlpha(Mathf.Max(0f, _remaining / fadeDuration) * _strength);
        }

        void SetAlpha(float alpha)
        {
            if (_renderer == null) return;
            _renderer.color = new Color(_tint.r, _tint.g, _tint.b, alpha);
        }
    }
}
