using System.Collections.Generic;
using UnityEngine;

namespace RaceSabotage.Whitebox
{
    /// <summary>
    /// Throwaway IMGUI readout for the Day 1 test. Each player's inventory is drawn
    /// inside their own split-screen viewport so selection and cooldown state remain
    /// readable during the race.
    /// </summary>
    public class WhiteboxHud : MonoBehaviour
    {
        class Entry
        {
            public string Label;
            public PlayerMotor Motor;
            public PlayerLoadout Loadout;
            public PlayerWallet Wallet;
            public float FinishTime = -1f;
        }

        readonly List<Entry> _entries = new List<Entry>();

        float _startX;
        float _finishX = 1f;
        float _raceStartTime;
        Camera _referenceCamera;

        GUIStyle _style;
        GUIStyle _headerStyle;
        GUIStyle _slotStyle;

        public void Configure(float startX, float finishX, Camera referenceCamera)
        {
            _startX = startX;
            _finishX = finishX;
            _referenceCamera = referenceCamera;
            _raceStartTime = Time.time;
        }

        public void Register(string label, PlayerMotor motor, PlayerLoadout loadout = null, PlayerWallet wallet = null)
        {
            _entries.Add(new Entry { Label = label, Motor = motor, Loadout = loadout, Wallet = wallet });
        }

        public void ReportFinish(PlayerMotor motor)
        {
            foreach (Entry entry in _entries)
            {
                if (entry.Motor != motor || entry.FinishTime >= 0f) continue;
                entry.FinishTime = Time.time - _raceStartTime;
                return;
            }
        }

        void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            _headerStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _slotStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            float viewportHeight = Screen.height * 0.5f;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Motor == null) continue;
                DrawPlayerPanel(entry, i, viewportHeight);
            }
        }

        void DrawPlayerPanel(Entry entry, int index, float viewportHeight)
        {
            const float margin = 12f;
            const float panelHeight = 104f;
            float panelWidth = Mathf.Min(560f, Screen.width - margin * 2f);
            float viewportTop = index * viewportHeight;
            float panelY = viewportTop + viewportHeight - panelHeight - 8f;
            var panel = new Rect(margin, panelY, panelWidth, panelHeight);
            GUI.Box(panel, GUIContent.none);

            float span = Mathf.Max(0.001f, _finishX - _startX);
            float progress = Mathf.Clamp01((entry.Motor.transform.position.x - _startX) / span);
            string finish = entry.FinishTime >= 0f ? $"  FINISHED {entry.FinishTime:0.00}s" : string.Empty;
            string coins = entry.Wallet != null ? $"  coins:{entry.Wallet.Coins}" : string.Empty;
            GUI.Label(new Rect(panel.x + 8f, panel.y + 4f, panel.width - 16f, 20f),
                $"{entry.Label}  {progress * 100f:00.0}%  speed x{entry.Motor.SpeedMultiplier:0.00}{coins}{finish}", _style);

            string switchKey = index == 0 ? "A" : "LEFT";
            string useKey = index == 0 ? "S" : "DOWN";
            GUI.Label(new Rect(panel.x + 8f, panel.y + 24f, panel.width - 16f, 22f),
                $"ITEMS   [{switchKey}] SWITCH   [{useKey}] USE", _headerStyle);

            if (entry.Loadout == null) return;

            int slots = entry.Loadout.InventoryCapacity;
            const float gap = 6f;
            float slotWidth = (panel.width - 16f - gap * (slots - 1)) / slots;
            float slotY = panel.y + 48f;
            for (int slot = 0; slot < slots; slot++)
            {
                var slotRect = new Rect(panel.x + 8f + slot * (slotWidth + gap), slotY, slotWidth, 48f);
                bool hasItem = entry.Loadout.TryGetItem(slot, out ItemKind item);
                bool selected = hasItem && slot == entry.Loadout.SelectedIndex;
                string label = hasItem ? ItemLabel(entry.Loadout, item, selected) : "EMPTY";

                Color oldColor = GUI.color;
                GUI.color = selected ? new Color(1f, 0.82f, 0.3f) : Color.white;
                GUI.Box(slotRect, label, _slotStyle);
                GUI.color = oldColor;
            }

            if (_referenceCamera != null && _referenceCamera.orthographic)
            {
                float height = _referenceCamera.orthographicSize * 2f;
                float width = height * _referenceCamera.aspect;
                string viewport = $"viewport {width:0.0} x {height:0.0}  elapsed {Time.time - _raceStartTime:0.00}s";
                Vector2 size = _style.CalcSize(new GUIContent(viewport));
                GUI.Label(new Rect(Screen.width - size.x - 12f, panel.y + 4f, size.x, 20f), viewport, _style);
            }
        }

        static string ItemLabel(PlayerLoadout loadout, ItemKind item, bool selected)
        {
            float remaining = loadout.CooldownRemaining(item);
            string state = remaining > 0f ? $"CD {remaining:0.0}s" : "READY";
            string marker = selected ? "> " : string.Empty;
            return $"{marker}{item.ToString().ToUpperInvariant()}\n{state}";
        }
    }
}
