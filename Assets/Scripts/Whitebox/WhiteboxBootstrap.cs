using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RaceSabotage.Whitebox
{
    /// <summary>
    /// Builds the whitebox race at runtime: two identical parallel tracks, two follow
    /// cameras in a split viewport, a progress bar per viewport and hit feedback wired
    /// across both halves. Everything here is meant to be replaced by authored scenes;
    /// the components it assembles are not.
    /// </summary>
    public class WhiteboxBootstrap : MonoBehaviour
    {
        [Header("Track")]
        [SerializeField] float trackLength = 240f;
        [Tooltip("Vertical distance between the two identical tracks.")]
        [SerializeField] float trackSeparation = 30f;
        [SerializeField] float groundThickness = 2f;
        [SerializeField] float groundOverhang = 30f;

        [Header("Obstacles")]
        [SerializeField] float firstObstacleX = 20f;
        [SerializeField] float obstacleSpacing = 18f;
        [SerializeField] float obstacleWidth = 0.5f;
        [Tooltip("Must stay below PlayerMotor.jumpHeight, which is how far a full jump lifts the body.")]
        [SerializeField] float[] obstacleHeights = { 1f, 1.4f, 1.7f, 1.2f, 1.5f };
        [SerializeField, Range(0f, 1f)] float obstacleSpeedMultiplier = 0.4f;
        [SerializeField] float obstacleSlowDuration = 1.5f;

        [Header("Player")]
        [SerializeField] Vector2 playerSize = new Vector2(1f, 1f);
        [SerializeField] float startX = 4f;

        [Header("Camera")]
        [SerializeField] float orthographicSize = 3f;
        [SerializeField] float cameraLookAhead = 4f;
        [Tooltip("Higher values spend less of the short viewport on solid ground.")]
        [SerializeField] float cameraHeightOffset = 2.4f;

        [Header("Readability")]
        [Tooltip("Faint horizontal lines above the ground, to judge jump height against.")]
        [SerializeField] bool showHeightGuides = true;
        [SerializeField] int heightGuideCount = 4;
        [SerializeField] bool showDebugHud = true;

        static readonly Color GroundColor = new Color(0.22f, 0.24f, 0.28f);
        static readonly Color ObstacleColor = new Color(0.85f, 0.31f, 0.28f);
        static readonly Color FinishColor = new Color(0.95f, 0.85f, 0.35f);
        static readonly Color GuideColor = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color PlayerOneColor = new Color(0.36f, 0.68f, 0.96f);
        static readonly Color PlayerTwoColor = new Color(0.98f, 0.62f, 0.31f);
        static readonly Color PlayerOneSky = new Color(0.09f, 0.11f, 0.16f);
        static readonly Color PlayerTwoSky = new Color(0.16f, 0.12f, 0.10f);

        class Side
        {
            public PlayerSlot Slot;
            public Color Tint;
            public Color Sky;
            public float TrackY;
            public float CameraDepth;

            public Transform TrackRoot;
            public Transform Player;
            public float RestY;
            public PlayerMotor Motor;
            public PlayerInputReader Input;
            public PlayerHitFeedback Feedback;
            public PlayerLoadout Loadout;
            public Camera Camera;
            public FollowCamera Follow;
            public CameraShake Shake;
            public ViewportFlash Flash;
            public RaceProgressBar Bar;
        }

        Side _one;
        Side _two;
        WhiteboxHud _hud;

        void Awake()
        {
            _hud = showDebugHud ? gameObject.AddComponent<WhiteboxHud>() : null;

            _one = new Side
            {
                Slot = PlayerSlot.One, Tint = PlayerOneColor, Sky = PlayerOneSky, TrackY = 0f, CameraDepth = 0f
            };
            _two = new Side
            {
                Slot = PlayerSlot.Two, Tint = PlayerTwoColor, Sky = PlayerTwoSky, TrackY = -trackSeparation,
                CameraDepth = 1f
            };

            BuildTrack(_one);
            BuildTrack(_two);
            BuildCamera(_one);
            BuildCamera(_two);
            BuildProgressBar(_one, _two);
            BuildProgressBar(_two, _one);

            _one.Feedback.Configure(_one.Shake, _one.Flash, _one.Bar, _two.Bar);
            _two.Feedback.Configure(_two.Shake, _two.Flash, _two.Bar, _one.Bar);

            ItemBanner banner = gameObject.AddComponent<ItemBanner>();
            BuildLoadout(_one, _two, banner);
            BuildLoadout(_two, _one, banner);

            SplitScreenManager splitScreen = gameObject.AddComponent<SplitScreenManager>();
            splitScreen.PlayerOneCamera = _one.Camera;
            splitScreen.PlayerTwoCamera = _two.Camera;
            splitScreen.Apply();

            if (_hud != null)
            {
                _hud.Register("P1", _one.Motor, _one.Loadout);
                _hud.Register("P2", _two.Motor, _two.Loadout);
                _hud.Configure(startX, trackLength, _one.Camera);
            }
        }

        void Start()
        {
            ClampCamera(_one);
            ClampCamera(_two);
        }

        void BuildTrack(Side side)
        {
            var root = new GameObject($"Track_{side.Slot}");
            side.TrackRoot = root.transform;
            float trackY = side.TrackY;

            PrimitiveSprite.CreateBox("Ground", root.transform,
                new Vector2(trackLength * 0.5f, trackY - groundThickness * 0.5f),
                new Vector2(trackLength + groundOverhang * 2f, groundThickness),
                GroundColor, PrimitiveSprite.ColliderKind.Solid, -2);

            if (showHeightGuides)
            {
                for (int i = 1; i <= heightGuideCount; i++)
                {
                    PrimitiveSprite.CreateBox($"HeightGuide_{i}m", root.transform,
                        new Vector2(trackLength * 0.5f, trackY + i),
                        new Vector2(trackLength + groundOverhang * 2f, 0.04f),
                        GuideColor, PrimitiveSprite.ColliderKind.None, -1);
                }
            }

            BuildObstacles(root.transform, trackY);
            BuildFinishLine(root.transform, trackY);
            BuildPlayer(side, root.transform);
        }

        void BuildObstacles(Transform root, float trackY)
        {
            if (obstacleHeights == null || obstacleHeights.Length == 0) return;

            int index = 0;
            for (float x = firstObstacleX; x <= trackLength - obstacleSpacing * 0.5f; x += obstacleSpacing)
            {
                float height = Mathf.Max(0.2f, obstacleHeights[index % obstacleHeights.Length]);
                SpriteRenderer box = PrimitiveSprite.CreateBox($"Obstacle_{index}", root,
                    new Vector2(x, trackY + height * 0.5f),
                    new Vector2(obstacleWidth, height),
                    ObstacleColor, PrimitiveSprite.ColliderKind.Trigger, 1);

                box.gameObject.AddComponent<Obstacle>().Configure(obstacleSpeedMultiplier, obstacleSlowDuration);
                index++;
            }
        }

        void BuildFinishLine(Transform root, float trackY)
        {
            SpriteRenderer box = PrimitiveSprite.CreateBox("FinishLine", root,
                new Vector2(trackLength, trackY + 4f), new Vector2(0.4f, 8f),
                FinishColor, PrimitiveSprite.ColliderKind.Trigger, 1);

            FinishLine finish = box.gameObject.AddComponent<FinishLine>();
            finish.Finished += motor =>
            {
                if (_hud != null) _hud.ReportFinish(motor);
            };
        }

        void BuildPlayer(Side side, Transform root)
        {
            float restY = side.TrackY + playerSize.y * 0.5f + 0.05f;
            SpriteRenderer box = PrimitiveSprite.CreateBox($"Player_{side.Slot}", root,
                new Vector2(startX, restY), playerSize,
                side.Tint, PrimitiveSprite.ColliderKind.Solid, 10);

            GameObject player = box.gameObject;

            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            player.AddComponent<PlayerTime>();
            PlayerInputReader input = player.AddComponent<PlayerInputReader>();
            input.Slot = side.Slot;

            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            motor.Mode = MotorMode.AutoRun;
            motor.ConfigureGroundCheck(playerSize);

            side.Player = player.transform;
            side.RestY = restY;
            side.Motor = motor;
            side.Input = input;
            side.Feedback = player.AddComponent<PlayerHitFeedback>();
        }

        void BuildLoadout(Side side, Side opponent, ItemBanner banner)
        {
            PlayerLoadout loadout = side.Player.gameObject.AddComponent<PlayerLoadout>();
            loadout.Configure(side.Slot, side.Motor, side.Input, side.Shake, banner,
                opponent.Motor, opponent.TrackRoot, opponent.Shake,
                side.RestY, opponent.RestY, opponent.TrackY, trackLength);
            side.Loadout = loadout;
        }

        void BuildCamera(Side side)
        {
            var cameraObject = new GameObject($"Cam_{side.Slot}");
            float cameraY = side.TrackY + cameraHeightOffset;
            cameraObject.transform.position = new Vector3(side.Player.position.x, cameraY, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = side.Sky;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            // Explicit order: two base cameras at the same depth render in an
            // undefined order, which matters once they share a render target.
            camera.depth = side.CameraDepth;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();

            side.Shake = cameraObject.AddComponent<CameraShake>();

            FollowCamera follow = cameraObject.AddComponent<FollowCamera>();
            follow.Target = side.Player;
            follow.LockedY = cameraY;
            follow.SetLookAhead(cameraLookAhead);

            SpriteRenderer flashQuad = PrimitiveSprite.CreateBox("ViewportFlash", cameraObject.transform,
                new Vector2(0f, 0f), Vector2.one, new Color(1f, 1f, 1f, 0f),
                PrimitiveSprite.ColliderKind.None, 500);
            flashQuad.transform.localPosition = new Vector3(0f, 0f, 1f);

            side.Camera = camera;
            side.Follow = follow;
            side.Flash = flashQuad.gameObject.AddComponent<ViewportFlash>();
        }

        void BuildProgressBar(Side side, Side opponent)
        {
            var barRoot = new GameObject("ProgressBar");
            barRoot.transform.SetParent(side.Camera.transform, false);

            RaceProgressBar bar = barRoot.AddComponent<RaceProgressBar>();
            bar.Configure(side.Camera, side.Player, side.Tint, opponent.Player, opponent.Tint, startX, trackLength);
            side.Bar = bar;
        }

        void ClampCamera(Side side)
        {
            float halfWidth = side.Camera.orthographicSize * side.Camera.aspect;
            side.Follow.SetXLimits(-groundOverhang + halfWidth, trackLength + groundOverhang - halfWidth);
            side.Follow.SnapToTarget();
        }
    }
}
