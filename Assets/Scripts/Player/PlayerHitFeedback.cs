using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Turns one hit into feedback on both screens. The victim's own half shakes,
    /// tints and blinks; on the other half the victim's marker flashes, so the
    /// opponent sees that it landed. That second half is the part the whole sabotage
    /// loop rests on - an item nobody notices connecting reads as "no effect" in a
    /// playtest.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerHitFeedback : MonoBehaviour
    {
        [Tooltip("Trauma added for a full-strength hit; scaled by how hard the hit slowed you.")]
        [SerializeField] float traumaPerHit = 0.85f;
        [SerializeField] Color hitTint = new Color(1f, 0.25f, 0.2f);
        [Tooltip("Keep this tiny. URP composites in linear space, so a bright overlay on a dark " +
                 "background reads far stronger than the number suggests: 0.06 alpha red over the " +
                 "sky colour already lands near sRGB 0.29.")]
        [SerializeField, Range(0f, 0.2f)] float tintStrength = 0.03f;

        [Header("Sprite Blink")]
        [Tooltip("The clearest 'I got hit' signal, because it costs no visibility.")]
        [SerializeField] float blinkDuration = 0.3f;
        [SerializeField] int blinkCount = 3;

        PlayerMotor _motor;
        SpriteRenderer _sprite;
        Color _baseColor = Color.white;
        float _blinkRemaining;

        CameraShake _shake;
        ViewportFlash _flash;
        RaceProgressBar _ownBar;
        RaceProgressBar _opponentBar;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _sprite = GetComponent<SpriteRenderer>();
            if (_sprite != null) _baseColor = _sprite.color;
        }

        void OnEnable() => _motor.SpeedPenaltyApplied += OnSpeedPenalty;

        void OnDisable() => _motor.SpeedPenaltyApplied -= OnSpeedPenalty;

        public void Configure(CameraShake shake, ViewportFlash flash, RaceProgressBar ownBar,
            RaceProgressBar opponentBar)
        {
            _shake = shake;
            _flash = flash;
            _ownBar = ownBar;
            _opponentBar = opponentBar;
        }

        void OnSpeedPenalty(float multiplier, float duration)
        {
            // A 0.4x hit should read harder than a 0.9x graze.
            float severity = Mathf.Clamp01(1f - multiplier);

            if (_shake != null) _shake.AddTrauma(traumaPerHit * severity);
            if (_flash != null) _flash.Flash(hitTint, tintStrength * severity);
            if (_ownBar != null) _ownBar.FlashSelf();
            if (_opponentBar != null) _opponentBar.FlashOpponent();

            _blinkRemaining = blinkDuration;
        }

        void Update()
        {
            if (_sprite == null || _blinkRemaining <= 0f) return;

            _blinkRemaining -= Time.unscaledDeltaTime;
            if (_blinkRemaining <= 0f)
            {
                _sprite.color = _baseColor;
                return;
            }

            float fade = _blinkRemaining / blinkDuration;
            bool lit = Mathf.Repeat(fade * blinkCount, 1f) > 0.5f;
            _sprite.color = lit ? Color.Lerp(_baseColor, Color.white, fade) : _baseColor;
        }
    }
}
