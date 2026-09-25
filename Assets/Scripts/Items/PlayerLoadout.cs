using System.Collections.Generic;
using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Holds and casts up to three consumable items. Normal play cycles through the
    /// inventory during the race; the unlimited mode remains as an Inspector debug
    /// option for focused item testing.
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        static readonly ItemKind[] Order =
            { ItemKind.Swap, ItemKind.Nitro, ItemKind.Mine, ItemKind.Blind, ItemKind.Laser };
        const int MaxInventorySize = 3;

        [Header("Mode")]
        [SerializeField] bool debugUnlimitedItems;

        [Header("Cooldowns (seconds)")]
        [Tooltip("Swap is the most decisive item on the list (design doc §4.1), so it gets the longest cooldown.")]
        [SerializeField] float swapCooldown = 6f;
        [SerializeField] float nitroCooldown = 4f;
        [SerializeField] float mineCooldown = 5f;
        [SerializeField] float blindCooldown = 7f;

        [Header("Swap")]
        [Tooltip("Landing grace so a swap can't drop you straight into an obstacle's penalty on arrival.")]
        [SerializeField] float swapImmunitySeconds = 0.5f;
        [SerializeField] float swapShakeTrauma = 0.55f;

        [Header("Nitro")]
        [SerializeField] float nitroMultiplier = 1.6f;
        [SerializeField] float nitroDuration = 1.4f;

        [Header("Mine")]
        [Tooltip("How far ahead of the opponent's current position the mine is dropped.")]
        [SerializeField] float mineLeadDistance = 6f;
        [SerializeField, Range(0f, 1f)] float mineSpeedMultiplier = 0.3f;
        [SerializeField] float mineSlowDuration = 1.8f;
        [SerializeField] Vector2 mineSize = new Vector2(0.5f, 1.3f);
        [Tooltip("Keeps a mine from spawning so close to the finish it becomes unavoidable.")]
        [SerializeField] float mineFinishMargin = 6f;

        [Header("Blind")]
        [SerializeField] float blindDuration = 2f;
        [SerializeField, Range(0f, 1f)] float blindStrength = 0.9f;
        [SerializeField] Color blindColor = new Color(0.015f, 0.02f, 0.035f);

        [Header("Laser")]
        [SerializeField] float laserCooldown = 6f;
        [SerializeField] float laserRange = 28f;
        [Tooltip("Logical forward distance used to project the opponent into the shooter's split-screen lane.")]
        [SerializeField] float laserTargetDistance = 14f;
        [SerializeField] float laserSweepAngle = 24f;
        [SerializeField] float laserSweepSeconds = 1.8f;
        [SerializeField] float laserStunDuration = 2.5f;
        [SerializeField] float laserAimWidth = 0.06f;
        [SerializeField] float laserBeamWidth = 0.18f;
        [SerializeField] float laserBeamDuration = 0.18f;
        [SerializeField] float laserOriginHeight = 0.1f;
        [SerializeField] float laserHitShakeTrauma = 0.8f;

        static readonly Color MineColor = new Color(0.64f, 0.27f, 0.86f);
        static readonly Color LaserAimColor = new Color(0.25f, 0.95f, 1f, 0.38f);
        static readonly Color LaserBeamColor = new Color(0.7f, 1f, 1f, 1f);

        PlayerSlot _slot;
        PlayerMotor _motor;
        PlayerInputReader _input;
        CameraShake _ownShake;
        ItemBanner _banner;

        PlayerMotor _opponentMotor;
        Transform _opponentTransform;
        CameraShake _opponentShake;
        ViewportFlash _opponentFlash;
        Transform _opponentTrackRoot;

        float _ownRestY;
        float _opponentRestY;
        float _opponentTrackBaselineY;
        float _trackFinishX;

        readonly float[] _cooldownRemaining = new float[Order.Length];
        readonly List<ItemKind> _inventory = new List<ItemKind>(MaxInventorySize);
        int _selected;
        bool _shopOpen;
        int _resumeInputAfterFrame = -1;
        SpriteRenderer _laserLine;
        SpriteRenderer _opponentLaserLine;
        Vector2 _laserDirection = Vector2.right;
        float _laserAimElapsed;
        float _laserBeamRemaining;
        bool _laserAiming;

        public bool HasItem => debugUnlimitedItems || _inventory.Count > 0;
        public bool CanAddItem => debugUnlimitedItems || _inventory.Count < MaxInventorySize;
        public int InventoryCapacity => debugUnlimitedItems ? Order.Length : MaxInventorySize;
        public int ItemCount => debugUnlimitedItems ? Order.Length : _inventory.Count;
        public int SelectedIndex => _selected;
        public bool IsAimingLaser => _laserAiming;
        public ItemKind Selected => debugUnlimitedItems ? Order[_selected] : _inventory[_selected];
        public float CooldownRemaining(ItemKind kind) => Mathf.Max(0f, _cooldownRemaining[(int)kind]);

        public bool Purchase(ItemKind kind)
        {
            return Grant(kind);
        }

        public bool Grant(ItemKind kind)
        {
            if (!CanAddItem) return false;
            if (debugUnlimitedItems) return true;

            _inventory.Add(kind);
            if (_inventory.Count == 1) _selected = 0;
            return true;
        }

        public bool TryGetItem(int index, out ItemKind item)
        {
            if (debugUnlimitedItems)
            {
                if (index >= 0 && index < Order.Length)
                {
                    item = Order[index];
                    return true;
                }
            }
            else if (index >= 0 && index < _inventory.Count)
            {
                item = _inventory[index];
                return true;
            }

            item = default;
            return false;
        }

        public void SetShopping(bool shopping)
        {
            _shopOpen = shopping;
            if (shopping) CancelLaserAim();
            if (!shopping) _resumeInputAfterFrame = Time.frameCount + 1;
        }

        public void Configure(PlayerSlot slot, PlayerMotor motor, PlayerInputReader input, CameraShake ownShake,
            ItemBanner banner, PlayerMotor opponentMotor, Transform opponentTrackRoot, CameraShake opponentShake,
            ViewportFlash opponentFlash, float ownRestY, float opponentRestY,
            float opponentTrackBaselineY, float trackFinishX)
        {
            _slot = slot;
            _motor = motor;
            _input = input;
            _ownShake = ownShake;
            _banner = banner;
            _opponentMotor = opponentMotor;
            _opponentTransform = opponentMotor.transform;
            _opponentTrackRoot = opponentTrackRoot;
            _opponentShake = opponentShake;
            _opponentFlash = opponentFlash;
            _ownRestY = ownRestY;
            _opponentRestY = opponentRestY;
            _opponentTrackBaselineY = opponentTrackBaselineY;
            _trackFinishX = trackFinishX;
        }

        void Update()
        {
            UpdateLaserVisual();

            for (int i = 0; i < _cooldownRemaining.Length; i++)
            {
                _cooldownRemaining[i] -= Time.deltaTime;
            }

            if (_input == null) return;
            if (_shopOpen || Time.frameCount <= _resumeInputAfterFrame) return;

            if (_motor != null && _motor.Mode == MotorMode.AutoRun && _input.SwitchItemPressed) SelectNextItem();
            if (_input.UseItemPressed) TryCast();
        }

        void SelectNextItem()
        {
            if (_laserAiming) CancelLaserAim();

            int count = ItemCount;
            if (count < 2) return;

            _selected = (_selected + 1) % count;
            _banner?.Show(_slot, $"{Selected.ToString().ToUpperInvariant()} SELECTED");
        }

        bool TryCast()
        {
            if (!HasItem) return false;

            ItemKind kind = Selected;
            if (CooldownRemaining(kind) > 0f) return false;

            if (kind == ItemKind.Laser && !_laserAiming)
            {
                BeginLaserAim();
                return false;
            }

            bool cast = kind switch
            {
                ItemKind.Swap => CastSwap(),
                ItemKind.Nitro => CastNitro(),
                ItemKind.Mine => CastMine(),
                ItemKind.Blind => CastBlind(),
                ItemKind.Laser => FireLaser(),
                _ => false
            };

            if (!cast) return false;

            _cooldownRemaining[(int)kind] = CooldownFor(kind);
            if (!debugUnlimitedItems)
            {
                _inventory.RemoveAt(_selected);
                _selected = _inventory.Count > 0 ? _selected % _inventory.Count : 0;
            }

            return true;
        }

        float CooldownFor(ItemKind kind) => kind switch
        {
            ItemKind.Swap => swapCooldown,
            ItemKind.Nitro => nitroCooldown,
            ItemKind.Mine => mineCooldown,
            ItemKind.Blind => blindCooldown,
            ItemKind.Laser => laserCooldown,
            _ => 1f
        };

        bool CastSwap()
        {
            float myX = _motor.transform.position.x;
            float theirX = _opponentTransform.position.x;

            _motor.Teleport(new Vector2(theirX, _ownRestY));
            _opponentMotor.Teleport(new Vector2(myX, _opponentRestY));

            _motor.GrantImmunity(swapImmunitySeconds);
            _opponentMotor.GrantImmunity(swapImmunitySeconds);

            _ownShake?.AddTrauma(swapShakeTrauma);
            _opponentShake?.AddTrauma(swapShakeTrauma);

            _banner?.Show(_slot, "SWAPPED!");
            _banner?.Show(Opposite(_slot), "SWAPPED!");
            return true;
        }

        bool CastNitro()
        {
            _motor.ApplyBoost(nitroMultiplier, nitroDuration);
            _banner?.Show(_slot, "NITRO!");
            return true;
        }

        bool CastMine()
        {
            float spawnX = Mathf.Min(
                _opponentTransform.position.x + mineLeadDistance,
                _trackFinishX - mineFinishMargin);
            if (spawnX <= _opponentTransform.position.x) return false; // too close to the finish to be fair

            SpriteRenderer box = PrimitiveSprite.CreateBox($"Mine_{_slot}_{Time.frameCount}", _opponentTrackRoot,
                new Vector2(spawnX, _opponentTrackBaselineY + mineSize.y * 0.5f), mineSize,
                MineColor, PrimitiveSprite.ColliderKind.Trigger, 1);
            box.gameObject.AddComponent<Mine>().Configure(mineSpeedMultiplier, mineSlowDuration);

            _banner?.Show(_slot, "MINE PLACED");
            return true;
        }

        bool CastBlind()
        {
            if (_opponentFlash == null) return false;

            _opponentFlash?.Blind(blindColor, blindStrength, blindDuration);
            _banner?.Show(_slot, "BLIND SENT");
            _banner?.Show(Opposite(_slot), "BLINDED!");
            return true;
        }

        void BeginLaserAim()
        {
            _laserAiming = true;
            _laserAimElapsed = 0f;
            EnsureLaserLine();
            UpdateLaserLine(Vector2.right, laserAimWidth, LaserAimColor);
            _banner?.Show(_slot, "LASER AIMING");
        }

        void UpdateLaserVisual()
        {
            if (_laserAiming)
            {
                _laserAimElapsed += Time.deltaTime;
                float cycle = Mathf.Max(0.1f, laserSweepSeconds);
                float angle = Mathf.Sin(_laserAimElapsed * Mathf.PI * 2f / cycle) * laserSweepAngle;
                _laserDirection = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                UpdateLaserLine(_laserDirection, laserAimWidth, LaserAimColor);
                return;
            }

            if (_laserBeamRemaining <= 0f) return;

            _laserBeamRemaining -= Time.deltaTime;
            if (_laserBeamRemaining <= 0f) DestroyLaserLine();
        }

        bool FireLaser()
        {
            _laserAiming = false;
            _laserBeamRemaining = laserBeamDuration;
            UpdateLaserLine(_laserDirection, laserBeamWidth, LaserBeamColor);

            bool hit = LaserHitsOpponent(_laserDirection);
            if (hit)
            {
                _opponentMotor.ApplyStun(laserStunDuration);
                _opponentShake?.AddTrauma(laserHitShakeTrauma);
                _opponentFlash?.Flash(LaserBeamColor, 0.12f);
                _banner?.Show(_slot, "LASER HIT!");
                _banner?.Show(Opposite(_slot), "STUNNED!");
            }
            else
            {
                _banner?.Show(_slot, "LASER MISSED");
            }

            return true;
        }

        bool LaserHitsOpponent(Vector2 direction)
        {
            if (_opponentMotor == null || _opponentTransform == null) return false;

            Vector2 origin = LaserOrigin();
            Vector2 opponentProxy = LaserTargetProxy(origin);

            Collider2D opponentCollider = _opponentTransform.GetComponent<Collider2D>();
            Vector3 targetSize = opponentCollider != null
                ? opponentCollider.bounds.size
                : new Vector3(1f, 1f, 1f);
            targetSize.z = 1f;

            var targetBounds = new Bounds(opponentProxy, targetSize);
            var ray = new Ray(origin, direction);
            return targetBounds.IntersectRay(ray, out float distance) && distance <= laserRange;
        }

        Vector2 LaserTargetProxy(Vector2 origin)
        {
            float raceLead = _opponentTransform.position.x - _motor.transform.position.x;
            float projectedDistance = Mathf.Clamp(laserTargetDistance + raceLead, 4f, laserRange - 1f);
            return new Vector2(
                origin.x + projectedDistance,
                _ownRestY + (_opponentTransform.position.y - _opponentRestY));
        }

        void UpdateLaserLine(Vector2 direction, float width, Color color)
        {
            EnsureLaserLine();
            Vector2 origin = LaserOrigin();
            PositionLaserLine(_laserLine, origin, direction, width, color);

            Vector2 opponentProxy = LaserTargetProxy(origin);
            Vector2 opponentTrackOrigin = origin + (Vector2)_opponentTransform.position - opponentProxy;
            PositionLaserLine(_opponentLaserLine, opponentTrackOrigin, direction, width, color);
        }

        void PositionLaserLine(SpriteRenderer renderer, Vector2 origin, Vector2 direction, float width, Color color)
        {
            Transform line = renderer.transform;
            line.position = origin + direction * (laserRange * 0.5f);
            line.localScale = new Vector3(laserRange, width, 1f);
            line.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            renderer.color = color;
        }

        Vector2 LaserOrigin() => (Vector2)_motor.transform.position + Vector2.up * laserOriginHeight;

        void EnsureLaserLine()
        {
            if (_laserLine == null)
            {
                _laserLine = PrimitiveSprite.CreateBox($"Laser_{_slot}", null, Vector2.zero, Vector2.one,
                    LaserAimColor, PrimitiveSprite.ColliderKind.None, 40);
            }

            if (_opponentLaserLine == null)
            {
                _opponentLaserLine = PrimitiveSprite.CreateBox($"OpponentLaser_{_slot}", null,
                    Vector2.zero, Vector2.one, LaserAimColor, PrimitiveSprite.ColliderKind.None, 40);
            }
        }

        void CancelLaserAim()
        {
            if (!_laserAiming) return;
            _laserAiming = false;
            DestroyLaserLine();
        }

        void DestroyLaserLine()
        {
            if (_laserLine != null) Destroy(_laserLine.gameObject);
            if (_opponentLaserLine != null) Destroy(_opponentLaserLine.gameObject);
            _laserLine = null;
            _opponentLaserLine = null;
            _laserBeamRemaining = 0f;
        }

        void OnDisable() => DestroyLaserLine();

        static PlayerSlot Opposite(PlayerSlot slot) => slot == PlayerSlot.One ? PlayerSlot.Two : PlayerSlot.One;
    }
}
