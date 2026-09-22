using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// One clock per player. Local multiplayer shares Time.timeScale, so a
    /// slow-motion item must never touch it: every gameplay system reads its
    /// delta from here so a single player can be slowed in isolation.
    /// </summary>
    public class PlayerTime : MonoBehaviour
    {
        [SerializeField, Range(0f, 3f)] float baseScale = 1f;

        readonly TimedMultiplier _effects = new TimedMultiplier();

        public float Scale => baseScale * _effects.Value;
        public float DeltaTime => Time.deltaTime * Scale;
        public float FixedDeltaTime => Time.fixedDeltaTime * Scale;

        public void ApplyScale(float scale, float duration) => _effects.Add(scale, duration);

        public void ClearEffects() => _effects.Clear();

        void Update() => _effects.Tick(Time.deltaTime);
    }
}
