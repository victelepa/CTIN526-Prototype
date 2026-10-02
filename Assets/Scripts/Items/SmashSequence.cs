using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// A short, telegraphed three-hit lane attack. P1 produces falling rocks on the
    /// lower track; P2 produces ground spikes on the upper track. Presentation differs,
    /// but timing and hit boxes are identical so neither lane gets a mechanical advantage.
    /// </summary>
    public class SmashSequence : MonoBehaviour
    {
        sealed class Strike
        {
            public float X;
            public float Time;
            public SpriteRenderer Warning;
            public SpriteRenderer Hazard;
            public bool Started;
            public bool Finished;
        }

        static readonly Color WarningColor = new Color(1f, 0.42f, 0.12f, 0.75f);
        static readonly Color RockColor = new Color(0.46f, 0.38f, 0.32f);
        static readonly Color SpikeColor = new Color(0.86f, 0.34f, 0.16f);

        PlayerMotor _target;
        PlayerSlot _targetSlot;
        ItemBanner _banner;
        Collider2D _targetCollider;
        Strike[] _strikes;
        Vector2 _hazardSize;
        float _baselineY;
        float _warningSeconds;
        float _activeSeconds;
        float _speedMultiplier;
        float _slowDuration;
        float _elapsed;
        bool _fallingRocks;
        bool _hit;

        public bool HitTarget => _hit;

        public void Configure(PlayerSlot attackerSlot, PlayerMotor target, PlayerSlot targetSlot,
            ItemBanner banner, float baselineY, float firstStrikeX, float strikeSpacing,
            int strikeCount, float warningSeconds, float strikeInterval, float activeSeconds,
            Vector2 hazardSize, float speedMultiplier, float slowDuration)
        {
            _target = target;
            _targetSlot = targetSlot;
            _banner = banner;
            _targetCollider = target != null ? target.GetComponent<Collider2D>() : null;
            _baselineY = baselineY;
            _warningSeconds = Mathf.Max(0.1f, warningSeconds);
            _activeSeconds = Mathf.Max(0.05f, activeSeconds);
            _hazardSize = new Vector2(Mathf.Max(0.2f, hazardSize.x), Mathf.Max(0.2f, hazardSize.y));
            _speedMultiplier = Mathf.Clamp01(speedMultiplier);
            _slowDuration = Mathf.Max(0.1f, slowDuration);
            _fallingRocks = attackerSlot == PlayerSlot.One;

            int count = Mathf.Max(1, strikeCount);
            _strikes = new Strike[count];
            for (int i = 0; i < count; i++)
            {
                float x = firstStrikeX + strikeSpacing * i;
                float strikeTime = _warningSeconds + Mathf.Max(0.05f, strikeInterval) * i;

                SpriteRenderer warning = PrimitiveSprite.CreateBox($"Warning_{i + 1}", transform,
                    new Vector2(x, _baselineY + 0.045f), new Vector2(_hazardSize.x * 1.25f, 0.09f),
                    WarningColor, PrimitiveSprite.ColliderKind.None, 15);

                Vector2 startPosition = _fallingRocks
                    ? new Vector2(x, _baselineY + 4.8f)
                    : new Vector2(x, _baselineY + _hazardSize.y * 0.5f);
                SpriteRenderer hazard = PrimitiveSprite.CreateBox(
                    _fallingRocks ? $"FallingRock_{i + 1}" : $"GroundSpike_{i + 1}",
                    transform, startPosition,
                    _fallingRocks ? new Vector2(_hazardSize.x * 0.8f, _hazardSize.x * 0.8f) : _hazardSize,
                    _fallingRocks ? RockColor : SpikeColor,
                    PrimitiveSprite.ColliderKind.None, 16);
                hazard.enabled = _fallingRocks;

                _strikes[i] = new Strike
                {
                    X = x,
                    Time = strikeTime,
                    Warning = warning,
                    Hazard = hazard
                };
            }
        }

        void Update()
        {
            if (_strikes == null || _strikes.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            _elapsed += Time.deltaTime;
            bool allFinished = true;

            foreach (Strike strike in _strikes)
            {
                if (strike.Finished) continue;
                allFinished = false;

                if (!strike.Started)
                {
                    UpdateWarning(strike);
                    if (_elapsed >= strike.Time) StartStrike(strike);
                    continue;
                }

                if (!_hit && TargetInside(strike)) ApplyHit();
                if (_elapsed >= strike.Time + _activeSeconds) FinishStrike(strike);
            }

            if (allFinished) Destroy(gameObject);
        }

        void UpdateWarning(Strike strike)
        {
            float pulse = 0.45f + Mathf.PingPong(_elapsed * 3.5f, 0.45f);
            Color warningColor = WarningColor;
            warningColor.a = pulse;
            strike.Warning.color = warningColor;

            if (!_fallingRocks) return;

            float progress = Mathf.Clamp01((_elapsed - (strike.Time - _warningSeconds)) / _warningSeconds);
            float y = Mathf.Lerp(_baselineY + 4.8f, _baselineY + _hazardSize.y * 0.5f, progress);
            strike.Hazard.transform.localPosition = new Vector3(strike.X, y, 0f);
        }

        void StartStrike(Strike strike)
        {
            strike.Started = true;
            strike.Warning.enabled = false;
            strike.Hazard.enabled = true;
            strike.Hazard.transform.localPosition = new Vector3(
                strike.X, _baselineY + _hazardSize.y * 0.5f, 0f);
            strike.Hazard.transform.localScale = new Vector3(_hazardSize.x, _hazardSize.y, 1f);
        }

        void FinishStrike(Strike strike)
        {
            strike.Finished = true;
            if (strike.Hazard != null) strike.Hazard.enabled = false;
        }

        bool TargetInside(Strike strike)
        {
            if (_target == null || _targetCollider == null || _target.Frozen) return false;

            var hazardBounds = new Bounds(
                new Vector3(strike.X, _baselineY + _hazardSize.y * 0.5f, 0f),
                new Vector3(_hazardSize.x, _hazardSize.y, 2f));
            return hazardBounds.Intersects(_targetCollider.bounds);
        }

        void ApplyHit()
        {
            _hit = true;
            _target.ApplySpeedPenalty(_speedMultiplier, _slowDuration);
            _banner?.Show(_targetSlot, "SMASHED!");
        }
    }
}
