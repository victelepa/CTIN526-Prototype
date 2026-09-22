using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// A one-shot obstacle dropped onto the opponent's track by the Mine item.
    /// Unlike a level-authored <see cref="Obstacle"/>, it consumes itself on the
    /// first hit - a mine that kept punishing everyone who passed would stop being
    /// a targeted item and start being level geometry.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Mine : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float speedMultiplier = 0.3f;
        [SerializeField] float duration = 1.8f;

        bool _consumed;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        public void Configure(float multiplier, float slowDuration)
        {
            speedMultiplier = multiplier;
            duration = slowDuration;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed || other.attachedRigidbody == null) return;

            PlayerMotor motor = other.attachedRigidbody.GetComponent<PlayerMotor>();
            if (motor == null) return;

            _consumed = true;
            motor.ApplySpeedPenalty(speedMultiplier, duration);
            Destroy(gameObject);
        }
    }
}
