using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Short, centred text drawn over one half of the split screen. A Swap victim
    /// (or the player who just got mined) would otherwise see themselves shoved
    /// sideways with no explanation and read it as a bug rather than as their
    /// opponent's move - see design doc §4.1.
    /// Placement follows the split-screen convention: P1's camera occupies the
    /// screen's top half, P2's the bottom half.
    /// </summary>
    public class ItemBanner : MonoBehaviour
    {
        [SerializeField] float displayDuration = 0.8f;
        [SerializeField] int fontSize = 34;

        class Message
        {
            public string Text;
            public float Remaining;
        }

        readonly Message _top = new Message();
        readonly Message _bottom = new Message();

        GUIStyle _style;

        public void Show(PlayerSlot slot, string text)
        {
            Message message = slot == PlayerSlot.One ? _top : _bottom;
            message.Text = text;
            message.Remaining = displayDuration;
        }

        void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            float half = Screen.height * 0.5f;
            Draw(_top, new Rect(0f, 0f, Screen.width, half));
            Draw(_bottom, new Rect(0f, half, Screen.width, half));
        }

        void Draw(Message message, Rect area)
        {
            if (message.Remaining <= 0f) return;

            message.Remaining -= Time.unscaledDeltaTime;

            float alpha = Mathf.Clamp01(message.Remaining / (displayDuration * 0.4f));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(area, message.Text, _style);
            GUI.color = previous;
        }
    }
}
