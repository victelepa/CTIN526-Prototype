using UnityEngine;

namespace RaceSabotage
{
    public enum ScreenMode
    {
        /// <summary>Both players share the Room: one fixed camera framing the whole thing.</summary>
        Shared,

        /// <summary>Anyone on a track: P1 on top, P2 on the bottom.</summary>
        Split
    }

    /// <summary>
    /// One rule covers all three phases: both players in the Room means a shared
    /// full-screen camera, anything else means split screen. Because the per-player
    /// cameras simply follow their own player, the asymmetric case (one player back
    /// in the Room while the other is still racing) needs no special handling.
    /// </summary>
    public class SplitScreenManager : MonoBehaviour
    {
        [SerializeField] Camera sharedCamera;
        [SerializeField] Camera playerOneCamera;
        [SerializeField] Camera playerTwoCamera;
        [Tooltip("Gap between the two halves, in normalised screen height. Anything above zero " +
                 "leaves a strip that no camera clears, so draw a UI divider over it if you use one.")]
        [SerializeField, Range(0f, 0.05f)] float dividerHeight;
        [SerializeField] ScreenMode mode = ScreenMode.Split;

        public Camera SharedCamera
        {
            get => sharedCamera;
            set => sharedCamera = value;
        }

        public Camera PlayerOneCamera
        {
            get => playerOneCamera;
            set => playerOneCamera = value;
        }

        public Camera PlayerTwoCamera
        {
            get => playerTwoCamera;
            set => playerTwoCamera = value;
        }

        public ScreenMode Mode => mode;

        void Start() => Apply();

        public void SetMode(ScreenMode value)
        {
            if (mode == value) return;
            mode = value;
            Apply();
        }

        public void Apply()
        {
            bool split = mode == ScreenMode.Split;
            float half = 0.5f - dividerHeight * 0.5f;

            if (sharedCamera != null)
            {
                sharedCamera.rect = new Rect(0f, 0f, 1f, 1f);
                sharedCamera.enabled = !split;
            }

            if (playerOneCamera != null)
            {
                playerOneCamera.rect = new Rect(0f, 0.5f + dividerHeight * 0.5f, 1f, half);
                playerOneCamera.enabled = split;
            }

            if (playerTwoCamera != null)
            {
                playerTwoCamera.rect = new Rect(0f, 0f, 1f, half);
                playerTwoCamera.enabled = split;
            }
        }
    }
}
