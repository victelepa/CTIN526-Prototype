using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Obstacles are triggers, not walls. An auto-running player pressed against
    /// a solid wall would never get past it, so a hit costs speed for a few
    /// seconds and the player passes through.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Obstacle : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float speedMultiplier = 0.4f;
        [SerializeField] float duration = 1.5f;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        public void Configure(float multiplier, float slowDuration)
        {
            speedMultiplier = multiplier;
            duration = slowDuration;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.attachedRigidbody == null) return;

            PlayerMotor motor = other.attachedRigidbody.GetComponent<PlayerMotor>();
            if (motor == null) return;

            motor.ApplySpeedPenalty(speedMultiplier, duration);
        }
    }
}
