using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// A single-use racing shop. Entering its trigger only shows a prompt; opening it
    /// freezes this track's player while the opponent and world continue. The three
    /// choice keys buy the displayed offers directly, with no confirmation menu.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TrackShop : MonoBehaviour
    {
        PlayerSlot _slot;
        PlayerMotor _motor;
        PlayerInputReader _input;
        PlayerLoadout _loadout;
        PlayerWallet _wallet;
        ItemBanner _banner;
        Camera _camera;
        ItemKind[] _offers;
        Collider2D _trigger;
        SpriteRenderer _renderer;
        Color _baseColor;
        bool _inRange;
        bool _active;
        bool _used;
        int _openedFrame = -1;

        GUIStyle _titleStyle;
        GUIStyle _offerStyle;

        public int OfferIndex { get; set; }

        string OpenPrompt => $"{InteractKeyLabel()}: OPEN SHOP";

        void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true;
            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null) _baseColor = _renderer.color;
        }

        public void Configure(PlayerSlot slot, PlayerMotor motor, PlayerInputReader input,
            PlayerLoadout loadout, PlayerWallet wallet, ItemBanner banner, Camera camera, ItemKind[] offers)
        {
            _slot = slot;
            _motor = motor;
            _input = input;
            _loadout = loadout;
            _wallet = wallet;
            _banner = banner;
            _camera = camera;
            _offers = offers;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_used || _motor == null || other.attachedRigidbody == null) return;
            if (other.attachedRigidbody.GetComponent<PlayerMotor>() != _motor) return;

            _inRange = true;
            _banner?.ShowPersistent(_slot, OpenPrompt);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (_motor == null || other.attachedRigidbody == null) return;
            if (other.attachedRigidbody.GetComponent<PlayerMotor>() != _motor) return;
            _inRange = false;
            _banner?.ClearPersistent(_slot, OpenPrompt);
        }

        void OpenShop()
        {
            _active = true;
            _openedFrame = Time.frameCount;
            _motor.Frozen = true;
            _loadout.SetShopping(true);
            _banner?.ClearPersistent(_slot, OpenPrompt);
            _banner?.Show(_slot, "CHOOSE AN ITEM");
        }

        void Update()
        {
            if (_used || _input == null || _offers == null || _offers.Length < 3) return;

            if (!_active)
            {
                if (_inRange && _input.InteractPressed) OpenShop();
                return;
            }

            // The Shift press that opened the shop must not also buy offer 3.
            if (Time.frameCount <= _openedFrame) return;

            if (_input.InteractPressed)
            {
                CloseWithoutPurchase();
                return;
            }

            if (_input.ShopChoiceOnePressed) Purchase(0);
            else if (_input.ShopChoiceTwoPressed) Purchase(1);
            else if (_input.ShopChoiceThreePressed) Purchase(2);
        }

        void Purchase(int index)
        {
            ItemKind item = _offers[index];
            int price = PriceFor(item);
            if (_loadout == null || !_loadout.CanAddItem)
            {
                _banner?.Show(_slot, "INVENTORY FULL");
                return;
            }

            if (_wallet == null || !_wallet.TrySpend(price))
            {
                _banner?.Show(_slot, $"NEED {price} COINS");
                return;
            }

            _loadout.Purchase(item);
            _loadout.SetShopping(false);
            _motor.Frozen = false;
            _inRange = false;
            _active = false;
            _used = true;
            _banner?.ClearPersistent(_slot, OpenPrompt);

            if (_trigger != null) _trigger.enabled = false;
            if (_renderer != null) _renderer.color = Color.Lerp(_baseColor, Color.black, 0.65f);
            _banner?.Show(_slot, $"{item.ToString().ToUpperInvariant()} BOUGHT");
        }

        void CloseWithoutPurchase()
        {
            _loadout.SetShopping(false);
            _motor.Frozen = false;
            _active = false;
            if (_inRange) _banner?.ShowPersistent(_slot, OpenPrompt);
        }

        static int PriceFor(ItemKind item) => item switch
        {
            ItemKind.Nitro => 1,
            ItemKind.Mine => 2,
            ItemKind.Blind => 2,
            ItemKind.Swap => 3,
            _ => 1
        };

        void OnDisable()
        {
            _banner?.ClearPersistent(_slot, OpenPrompt);
            _inRange = false;
            if (!_active) return;
            _active = false;
            if (_motor != null) _motor.Frozen = false;
            if (_loadout != null) _loadout.SetShopping(false);
        }

        void OnGUI()
        {
            if ((!_active && !_inRange) || _camera == null || _motor == null || _offers == null || _offers.Length < 3)
                return;

            _titleStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _offerStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            Vector3 point = _camera.WorldToScreenPoint(_motor.transform.position + Vector3.up * 1.4f);
            const float width = 480f;
            const float height = 96f;
            float x = Mathf.Clamp(point.x - width * 0.5f, 8f, Screen.width - width - 8f);
            float y = Mathf.Clamp(Screen.height - point.y - height - 20f, 8f, Screen.height - height - 8f);
            var area = new Rect(x, y, width, height);

            if (!_active)
            {
                GUI.Box(new Rect(x + 120f, y + 30f, width - 240f, 42f),
                    $"[{InteractKeyLabel()}] OPEN SHOP", _offerStyle);
                return;
            }

            GUI.Box(area, $"SHOP — {_wallet?.Coins ?? 0} coins — SHIFT cancels", _titleStyle);
            float offerWidth = (width - 20f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var offerRect = new Rect(x + 5f + i * (offerWidth + 5f), y + 27f, offerWidth, height - 32f);
                GUI.Box(offerRect, $"[{KeyLabel(i)}]\n{_offers[i]}  ({PriceFor(_offers[i])})", _offerStyle);
            }
        }

        string KeyLabel(int index)
        {
            if (_slot == PlayerSlot.One)
            {
                return index switch { 0 => "A", 1 => "S", _ => "D" };
            }

            return index switch { 0 => "LEFT", 1 => "DOWN", _ => "RIGHT" };
        }

        string InteractKeyLabel() => _slot == PlayerSlot.One ? "L-SHIFT" : "R-SHIFT";
    }
}
