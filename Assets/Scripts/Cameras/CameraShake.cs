using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Trauma-based shake. Callers add trauma and it decays on its own, so several
    /// hits in quick succession build instead of cutting each other off.
    /// The offset is computed in Update and consumed by <see cref="FollowCamera"/>
    /// in LateUpdate, which fixes the order the two would otherwise fight over.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Tooltip("World units of shake at full trauma.")]
        [SerializeField] float maxOffset = 0.32f;
        [SerializeField] float decayPerSecond = 3.2f;
        [SerializeField] float frequency = 26f;

        float _trauma;

        public Vector2 Offset { get; private set; }

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        void Update()
        {
            if (_trauma <= 0f)
            {
                Offset = Vector2.zero;
                return;
            }

            // Squaring makes small hits subtle and big ones read as impacts.
            float magnitude = _trauma * _trauma * maxOffset;
            float seed = Time.unscaledTime * frequency;
            Offset = new Vector2(
                Mathf.PerlinNoise(seed, 0.37f) - 0.5f,
                Mathf.PerlinNoise(0.71f, seed) - 0.5f) * (2f * magnitude);

            _trauma = Mathf.Max(0f, _trauma - decayPerSecond * Time.unscaledDeltaTime);
        }
    }
}
