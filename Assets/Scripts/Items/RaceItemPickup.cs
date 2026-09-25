using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// One option in a mid-race item choice. The options share a root so collecting
    /// any one of them removes the whole choice for that player.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class RaceItemPickup : MonoBehaviour
    {
        PlayerSlot _slot;
        PlayerMotor _motor;
        PlayerLoadout _loadout;
        ItemBanner _banner;
        Camera _camera;
        ItemKind _item;
        GameObject _choiceRoot;
        GUIStyle _labelStyle;

        public void Configure(PlayerSlot slot, PlayerMotor motor, PlayerLoadout loadout,
            ItemBanner banner, Camera camera, ItemKind item, GameObject choiceRoot)
        {
            _slot = slot;
            _motor = motor;
            _loadout = loadout;
            _banner = banner;
            _camera = camera;
            _item = item;
            _choiceRoot = choiceRoot;

            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_motor == null || _loadout == null || other.attachedRigidbody == null) return;
            if (other.attachedRigidbody.GetComponent<PlayerMotor>() != _motor) return;

            if (!_loadout.Grant(_item))
            {
                _banner?.Show(_slot, "INVENTORY FULL");
                return;
            }

            _banner?.Show(_slot, $"{_item.ToString().ToUpperInvariant()} COLLECTED");
            if (_choiceRoot != null) _choiceRoot.SetActive(false);
        }

        void OnGUI()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) return;

            Vector3 point = _camera.WorldToScreenPoint(transform.position);
            Rect viewport = _camera.pixelRect;
            if (point.z <= 0f || !viewport.Contains(new Vector2(point.x, point.y))) return;

            _labelStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            const float width = 126f;
            const float height = 42f;
            var area = new Rect(point.x - width * 0.5f, Screen.height - point.y - height * 0.5f, width, height);
            GUI.Box(area, _item.ToString().ToUpperInvariant(), _labelStyle);
        }
    }
}
