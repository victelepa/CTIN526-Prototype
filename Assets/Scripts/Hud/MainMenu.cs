using System;
using UnityEngine;

namespace RaceSabotage
{
    /// <summary>
    /// Minimal prototype front end. WebGL cannot reliably close its browser tab, so
    /// the quit action is intentionally omitted from WebGL players.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        const float ReferenceWidth = 1920f;
        const float ReferenceHeight = 1080f;

        Action _startGame;
        GUIStyle _panelStyle;
        GUIStyle _titleStyle;
        GUIStyle _subtitleStyle;
        GUIStyle _buttonStyle;

        public void Configure(Action startGame) => _startGame = startGame;

        void OnGUI()
        {
            float scale = Mathf.Clamp(
                Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight),
                0.55f, 1f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = Screen.width / scale;
            float height = Screen.height / scale;
            EnsureStyles();

            Color previousColor = GUI.color;
            GUI.color = new Color(0.035f, 0.045f, 0.07f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            const float panelWidth = 560f;
#if UNITY_WEBGL && !UNITY_EDITOR
            const float panelHeight = 300f;
#else
            const float panelHeight = 390f;
#endif
            var panel = new Rect(
                (width - panelWidth) * 0.5f,
                (height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            GUI.Label(new Rect(panel.x + 20f, panel.y + 34f, panel.width - 40f, 70f),
                "Game Title", _titleStyle);

            var startRect = new Rect(panel.x + 120f, panel.y + 172f, 320f, 64f);
            if (GUI.Button(startRect, "START GAME", _buttonStyle))
            {
                Action startGame = _startGame;
                enabled = false;
                startGame?.Invoke();
                Destroy(this);
                GUI.matrix = previousMatrix;
                return;
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            var quitRect = new Rect(panel.x + 120f, panel.y + 252f, 320f, 64f);
            if (GUI.Button(quitRect, "QUIT", _buttonStyle)) Quit();
#endif

            GUI.matrix = previousMatrix;
        }

        void EnsureStyles()
        {
            _panelStyle ??= new GUIStyle(GUI.skin.box)
            {
                normal = { background = Texture2D.grayTexture }
            };
            _titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _subtitleStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.65f, 0.76f, 0.9f) }
            };
            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
