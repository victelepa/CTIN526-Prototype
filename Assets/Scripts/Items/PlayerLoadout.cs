using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Day 3 debug loadout: every item is unlimited, gated only by its own cooldown,
    /// so a first playtest can test whether sabotage itself is fun before a shop or
    /// coin economy exists to gate it. The eventual shop model only ever hands a
    /// player one held item at a time, so "cycle with one key, cast with another"
    /// is not a placeholder input scheme - it is the real one, just with every item
    /// unlocked instead of one bought item.
    /// Reuses the two keys already reserved for the racing phase (design doc §7.2):
    /// Interact cycles the selected item (idle otherwise, since there is no shop
    /// yet), Use Item casts it.
    /// </summary>
    public class PlayerLoadout : MonoBehaviour
    {
        static readonly ItemKind[] Order = { ItemKind.Swap, ItemKind.Nitro, ItemKind.Mine, ItemKind.Blind };

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
        int _selected;

        public ItemKind Selected => Order[_selected];
        public float CooldownRemaining(ItemKind kind) => Mathf.Max(0f, _cooldownRemaining[(int)kind]);

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

            if (_input.InteractPressed) _selected = (_selected + 1) % Order.Length;
            if (_input.UseItemPressed) TryCast();
        }

        void TryCast()
        {
            ItemKind kind = Order[_selected];
            if (CooldownRemaining(kind) > 0f) return;

            switch (kind)
            {
                case ItemKind.Swap: CastSwap(); break;
                case ItemKind.Nitro: CastNitro(); break;
                case ItemKind.Mine: CastMine(); break;
                case ItemKind.Blind: CastBlind(); break;
            }

            _cooldownRemaining[(int)kind] = CooldownFor(kind);
        }

        float CooldownFor(ItemKind kind) => kind switch
        {
            ItemKind.Swap => swapCooldown,
            ItemKind.Nitro => nitroCooldown,
            ItemKind.Mine => mineCooldown,
            ItemKind.Blind => blindCooldown,
            _ => 1f
        };

        void CastSwap()
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
        }

        void CastNitro()
        {
            _motor.ApplyBoost(nitroMultiplier, nitroDuration);
            _banner?.Show(_slot, "NITRO!");
        }

        void CastMine()
        {
            float spawnX = Mathf.Min(
                _opponentTransform.position.x + mineLeadDistance,
                _trackFinishX - mineFinishMargin);
            if (spawnX <= _opponentTransform.position.x) return; // too close to the finish to be fair

            SpriteRenderer box = PrimitiveSprite.CreateBox($"Mine_{_slot}_{Time.frameCount}", _opponentTrackRoot,
                new Vector2(spawnX, _opponentTrackBaselineY + mineSize.y * 0.5f), mineSize,
                MineColor, PrimitiveSprite.ColliderKind.Trigger, 1);
            box.gameObject.AddComponent<Mine>().Configure(mineSpeedMultiplier, mineSlowDuration);

            _banner?.Show(_slot, "MINE PLACED");
        }

        void CastBlind()
        {
            _opponentFlash?.Blind(blindColor, blindStrength, blindDuration);
            _banner?.Show(_slot, "BLIND SENT");
            _banner?.Show(Opposite(_slot), "BLINDED!");
        }

        static PlayerSlot Opposite(PlayerSlot slot) => slot == PlayerSlot.One ? PlayerSlot.Two : PlayerSlot.One;
    }
}
