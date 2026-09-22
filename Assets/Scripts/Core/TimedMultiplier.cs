using System.Collections.Generic;

namespace RaceSabotage
{
    /// <summary>
    /// Stacks multiplicative effects that expire on their own.
    /// Durations are meant to be ticked with real time, never with the affected
    /// player's scaled time, or a slow effect would stretch its own countdown.
    /// </summary>
    public class TimedMultiplier
    {
        struct Effect
        {
            public float Value;
            public float Remaining;
        }

        readonly List<Effect> _effects = new List<Effect>();

        public float Value { get; private set; } = 1f;

        public void Add(float value, float duration)
        {
            if (duration <= 0f) return;
            _effects.Add(new Effect { Value = value, Remaining = duration });
            Recalculate();
        }

        public void Clear()
        {
            _effects.Clear();
            Value = 1f;
        }

        public void Tick(float realDeltaTime)
        {
            if (_effects.Count == 0) return;

            bool anyExpired = false;
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                Effect effect = _effects[i];
                effect.Remaining -= realDeltaTime;
                if (effect.Remaining <= 0f)
                {
                    _effects.RemoveAt(i);
                    anyExpired = true;
                }
                else
                {
                    _effects[i] = effect;
                }
            }

            if (anyExpired) Recalculate();
        }

        void Recalculate()
        {
            float value = 1f;
            foreach (Effect effect in _effects) value *= effect.Value;
            Value = value;
        }
    }
}
