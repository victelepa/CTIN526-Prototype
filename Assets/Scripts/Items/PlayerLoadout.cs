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
        static readonly ItemKind[] Order = { ItemKind.Swap, ItemKind.Nitro, ItemKind.Mine, ItemKind.Blind };
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

        static readonly Color MineColor = new Color(0.64f, 0.27f, 0.86f);

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

        public bool HasItem => debugUnlimitedItems || _inventory.Count > 0;
        public bool CanAddItem => debugUnlimitedItems || _inventory.Count < MaxInventorySize;
        public int InventoryCapacity => debugUnlimitedItems ? Order.Length : MaxInventorySize;
        public int ItemCount => debugUnlimitedItems ? Order.Length : _inventory.Count;
        public int SelectedIndex => _selected;
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

            bool cast = kind switch
            {
                ItemKind.Swap => CastSwap(),
                ItemKind.Nitro => CastNitro(),
                ItemKind.Mine => CastMine(),
                ItemKind.Blind => CastBlind(),
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

        static PlayerSlot Opposite(PlayerSlot slot) => slot == PlayerSlot.One ? PlayerSlot.Two : PlayerSlot.One;
    }
}
