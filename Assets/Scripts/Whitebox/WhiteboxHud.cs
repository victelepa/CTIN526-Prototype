using System.Collections.Generic;
using UnityEngine;

namespace RaceSabotage.Whitebox
{
    /// <summary>
    /// Throwaway IMGUI readout for the Day 1 test: relative progress, the active
    /// speed penalty, finish times, and the world-space size of one split-screen
    /// viewport (the number that decides whether vertical level design survives).
    /// The real progress bar is a separate, non-negotiable feature.
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
                fontSize = 16,
                normal = { textColor = Color.white }
            };

            // Anchored to the bottom of the screen: the top of each viewport now
            // belongs to the progress bar.
            GUILayout.BeginArea(new Rect(12f, Screen.height - 116f, 560f, 108f));
            GUILayout.Label($"Elapsed  {Time.time - _raceStartTime:0.00}s", _style);

            foreach (Entry entry in _entries)
            {
                if (entry.Motor == null) continue;

                float span = Mathf.Max(0.001f, _finishX - _startX);
                float progress = Mathf.Clamp01((entry.Motor.transform.position.x - _startX) / span);
                string finish = entry.FinishTime >= 0f ? $"   FINISHED {entry.FinishTime:0.00}s" : string.Empty;
                string item = entry.Loadout == null
                    ? string.Empty
                    : entry.Loadout.HasItem
                        ? $"   item:{entry.Loadout.Selected}{ReadyTag(entry.Loadout)}"
                        : "   item:Empty";
                string coins = entry.Wallet != null ? $"   coins:{entry.Wallet.Coins}" : string.Empty;

                GUILayout.Label(
                    $"{entry.Label}   {progress * 100f:00.0}%   speed x{entry.Motor.SpeedMultiplier:0.00}{finish}{coins}{item}",
                    _style);
            }

            if (_referenceCamera != null && _referenceCamera.orthographic)
            {
                float height = _referenceCamera.orthographicSize * 2f;
                float width = height * _referenceCamera.aspect;
                GUILayout.Label($"viewport  {width:0.0} x {height:0.0} world units   (aspect {_referenceCamera.aspect:0.00})",
                    _style);
            }

            GUILayout.EndArea();
        }

        static string ReadyTag(PlayerLoadout loadout)
        {
            float remaining = loadout.CooldownRemaining(loadout.Selected);
            return remaining > 0f ? $" (cd {remaining:0.0}s)" : " (ready)";
        }
    }
}
