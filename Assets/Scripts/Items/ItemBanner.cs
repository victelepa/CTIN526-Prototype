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
            public string PersistentText;
        }

        readonly Message _top = new Message();
        readonly Message _bottom = new Message();

        GUIStyle _style;

        public void Show(PlayerSlot slot, string text)
        {
            Message message = For(slot);
            message.Text = text;
            message.Remaining = displayDuration;
        }

        /// <summary>
        /// Sets a fallback message that remains until explicitly cleared. Timed
        /// messages can temporarily appear over it, then the persistent message
        /// resumes for as long as the owning interaction remains available.
        /// </summary>
        public void ShowPersistent(PlayerSlot slot, string text)
        {
            For(slot).PersistentText = text;
        }

        public void ClearPersistent(PlayerSlot slot, string text)
        {
            Message message = For(slot);
            if (message.PersistentText == text) message.PersistentText = null;
        }

        Message For(PlayerSlot slot) => slot == PlayerSlot.One ? _top : _bottom;

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
            bool timed = message.Remaining > 0f;
            if (!timed && string.IsNullOrEmpty(message.PersistentText)) return;

            if (timed) message.Remaining -= Time.unscaledDeltaTime;

            float alpha = timed
                ? Mathf.Clamp01(message.Remaining / (displayDuration * 0.4f))
                : 1f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(area, timed ? message.Text : message.PersistentText, _style);
            GUI.color = previous;
        }
    }
}
