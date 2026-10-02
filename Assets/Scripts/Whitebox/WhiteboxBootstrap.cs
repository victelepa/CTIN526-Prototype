using System.Collections.Generic;
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

        [Header("Shops")]
        [Tooltip("Both tracks use the same offers at matching shop indices.")]
        [SerializeField] float[] shopPositions = { 47f, 119f, 191f };
        [SerializeField] Vector2 shopTriggerSize = new Vector2(5f, 4f);

        [Header("Racing Item Choice")]
        [Tooltip("X position of the first free item. The other two follow at the configured spacing.")]
        [SerializeField] float itemChoiceStartX = 80f;
        [SerializeField] float itemChoiceSpacing = 3.2f;
        [SerializeField] float itemChoiceHeight = 2f;
        [SerializeField] Vector2 itemChoiceTriggerSize = new Vector2(1.7f, 0.65f);

        [Header("Player")]
        [SerializeField] Vector2 playerSize = new Vector2(1f, 1f);
        [SerializeField] float startX = 4f;

        [Header("Preparation Room")]
        [SerializeField] float preparationDuration = 12f;
        [SerializeField] Vector2 roomCenter = new Vector2(-12f, 12f);
        [SerializeField] Vector2 roomSize = new Vector2(18f, 10f);
        [SerializeField] float sharedCameraOrthographicSize = 6f;

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
        static readonly Color ShopColor = new Color(0.22f, 0.82f, 0.72f, 0.22f);
        static readonly Color RoomColor = new Color(0.24f, 0.27f, 0.33f);
        static readonly Color CoinColor = new Color(1f, 0.78f, 0.15f);
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
            public PlayerWallet Wallet;
            public PlayerLoadout Loadout;
            public Camera Camera;
            public FollowCamera Follow;
            public CameraShake Shake;
            public ViewportFlash Flash;
            public RaceProgressBar Bar;
            public readonly List<TrackShop> Shops = new List<TrackShop>();
        }

        Side _one;
        Side _two;
        WhiteboxHud _hud;
        ItemKind[][] _shopOffers;
        ItemKind[] _racingItemOffers;
        readonly List<CoinPickup> _coins = new List<CoinPickup>();
        Vector2 _oneRoomSpawn;
        Vector2 _twoRoomSpawn;
        bool _started;

        void Awake()
        {
            MainMenu menu = gameObject.AddComponent<MainMenu>();
            menu.Configure(BeginGame);
        }

        public void BeginGame()
        {
            if (_started) return;
            _started = true;

            _hud = showDebugHud ? gameObject.AddComponent<WhiteboxHud>() : null;
            BuildShopOffers();
            BuildRacingItemOffers();

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
            BuildRoom();
            BuildCamera(_one);
            BuildCamera(_two);
            Camera sharedCamera = BuildSharedCamera();
            BuildProgressBar(_one, _two);
            BuildProgressBar(_two, _one);

            _one.Feedback.Configure(_one.Shake, _one.Flash, _one.Bar, _two.Bar);
            _two.Feedback.Configure(_two.Shake, _two.Flash, _two.Bar, _one.Bar);

            ItemBanner banner = gameObject.AddComponent<ItemBanner>();
            BuildLoadout(_one, _two, banner);
            BuildLoadout(_two, _one, banner);
            BuildRacingItemChoice(_one, banner);
            BuildRacingItemChoice(_two, banner);

            SplitScreenManager splitScreen = gameObject.AddComponent<SplitScreenManager>();
            splitScreen.SharedCamera = sharedCamera;
            splitScreen.PlayerOneCamera = _one.Camera;
            splitScreen.PlayerTwoCamera = _two.Camera;
            splitScreen.Apply();

            GameFlow flow = gameObject.AddComponent<GameFlow>();
            flow.Configure(_one.Motor, _one.Wallet, _oneRoomSpawn, new Vector2(startX, _one.RestY),
                _two.Motor, _two.Wallet, _twoRoomSpawn, new Vector2(startX, _two.RestY),
                splitScreen, _coins, preparationDuration);

            if (_hud != null)
            {
                _hud.Register("P1", _one.Motor, _one.Loadout, _one.Wallet);
                _hud.Register("P2", _two.Motor, _two.Loadout, _two.Wallet);
                _hud.Configure(startX, trackLength, _one.Camera);
            }

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
            BuildShops(side, root.transform, trackY);
            BuildFinishLine(root.transform, trackY);
            BuildPlayer(side, root.transform);
        }

        void BuildShopOffers()
        {
            int count = shopPositions?.Length ?? 0;
            _shopOffers = new ItemKind[count][];

            for (int shopIndex = 0; shopIndex < count; shopIndex++)
            {
                bool finalShop = shopIndex == count - 1;
                var pool = finalShop
                    ? new List<ItemKind>
                        { ItemKind.Nitro, ItemKind.Mine, ItemKind.Blind, ItemKind.Laser, ItemKind.Smash }
                    : new List<ItemKind>
                        { ItemKind.Swap, ItemKind.Nitro, ItemKind.Mine, ItemKind.Blind, ItemKind.Laser, ItemKind.Smash };

                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int swapIndex = Random.Range(0, i + 1);
                    (pool[i], pool[swapIndex]) = (pool[swapIndex], pool[i]);
                }

                _shopOffers[shopIndex] = pool.GetRange(0, 3).ToArray();
            }
        }

        void BuildRacingItemOffers()
        {
            var pool = new List<ItemKind>
                { ItemKind.Swap, ItemKind.Nitro, ItemKind.Mine, ItemKind.Blind, ItemKind.Laser, ItemKind.Smash };
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (pool[i], pool[swapIndex]) = (pool[swapIndex], pool[i]);
            }

            _racingItemOffers = pool.GetRange(0, 3).ToArray();
        }

        void BuildRacingItemChoice(Side side, ItemBanner banner)
        {
            if (_racingItemOffers == null || _racingItemOffers.Length < 3) return;

            float lastX = itemChoiceStartX + itemChoiceSpacing * (_racingItemOffers.Length - 1);
            if (itemChoiceStartX <= startX || lastX >= trackLength) return;

            var choiceRoot = new GameObject("RacingItemChoice");
            choiceRoot.transform.SetParent(side.TrackRoot, false);

            for (int i = 0; i < _racingItemOffers.Length; i++)
            {
                ItemKind item = _racingItemOffers[i];
                var pickupObject = new GameObject($"FreeItem_{i + 1}_{item}");
                pickupObject.transform.SetParent(choiceRoot.transform, false);
                pickupObject.transform.localPosition = new Vector3(
                    itemChoiceStartX + i * itemChoiceSpacing,
                    side.TrackY + itemChoiceHeight,
                    0f);

                BoxCollider2D trigger = pickupObject.AddComponent<BoxCollider2D>();
                trigger.size = itemChoiceTriggerSize;
                trigger.isTrigger = true;

                RaceItemPickup pickup = pickupObject.AddComponent<RaceItemPickup>();
                pickup.Configure(side.Slot, side.Motor, side.Loadout, banner, side.Camera, item, choiceRoot);
            }
        }

        void BuildShops(Side side, Transform root, float trackY)
        {
            if (shopPositions == null) return;

            for (int i = 0; i < shopPositions.Length; i++)
            {
                float x = shopPositions[i];
                if (x <= startX || x >= trackLength) continue;

                SpriteRenderer box = PrimitiveSprite.CreateBox($"Shop_{i + 1}", root,
                    new Vector2(x, trackY + shopTriggerSize.y * 0.5f), shopTriggerSize,
                    ShopColor, PrimitiveSprite.ColliderKind.Trigger, 2);

                TrackShop shop = box.gameObject.AddComponent<TrackShop>();
                shop.OfferIndex = i;
                side.Shops.Add(shop);
            }
        }

        void BuildRoom()
        {
            var root = new GameObject("Room");
            float floorY = roomCenter.y - roomSize.y * 0.5f;
            float left = roomCenter.x - roomSize.x * 0.5f;
            float right = roomCenter.x + roomSize.x * 0.5f;

            PrimitiveSprite.CreateBox("Floor", root.transform,
                new Vector2(roomCenter.x, floorY - 0.75f), new Vector2(roomSize.x, 1.5f),
                RoomColor, PrimitiveSprite.ColliderKind.Solid, -2);
            PrimitiveSprite.CreateBox("LeftWall", root.transform,
                new Vector2(left - 0.5f, roomCenter.y), new Vector2(1f, roomSize.y),
                RoomColor, PrimitiveSprite.ColliderKind.Solid, -2);
            PrimitiveSprite.CreateBox("RightWall", root.transform,
                new Vector2(right + 0.5f, roomCenter.y), new Vector2(1f, roomSize.y),
                RoomColor, PrimitiveSprite.ColliderKind.Solid, -2);

            float leftPlatformY = floorY + 1.35f;
            float rightPlatformY = floorY + 1.75f;
            PrimitiveSprite.CreateBox("PlatformLeft", root.transform,
                new Vector2(roomCenter.x - 4.2f, leftPlatformY), new Vector2(4.2f, 0.4f),
                RoomColor, PrimitiveSprite.ColliderKind.Solid, -1);
            PrimitiveSprite.CreateBox("PlatformRight", root.transform,
                new Vector2(roomCenter.x + 4.2f, rightPlatformY), new Vector2(4.2f, 0.4f),
                RoomColor, PrimitiveSprite.ColliderKind.Solid, -1);

            _oneRoomSpawn = new Vector2(roomCenter.x - 3f, floorY + playerSize.y * 0.5f + 0.05f);
            _twoRoomSpawn = new Vector2(roomCenter.x + 3f, floorY + playerSize.y * 0.5f + 0.05f);

            float[] floorOffsets = { -7f, -4.5f, 0f, 4.5f, 7f };
            foreach (float offset in floorOffsets)
                BuildCoin(root.transform, new Vector2(roomCenter.x + offset, floorY + 0.45f));

            for (int i = -1; i <= 1; i++)
            {
                BuildCoin(root.transform,
                    new Vector2(roomCenter.x - 4.2f + i * 1.2f, leftPlatformY + 0.65f));
                BuildCoin(root.transform,
                    new Vector2(roomCenter.x + 4.2f + i * 1.2f, rightPlatformY + 0.65f));
            }

            BuildCoin(root.transform, new Vector2(roomCenter.x, floorY + 2f));
        }

        void BuildCoin(Transform root, Vector2 position)
        {
            SpriteRenderer box = PrimitiveSprite.CreateBox($"Coin_{_coins.Count + 1}", root,
                position, new Vector2(0.45f, 0.45f), CoinColor,
                PrimitiveSprite.ColliderKind.Trigger, 3);
            _coins.Add(box.gameObject.AddComponent<CoinPickup>());
        }

        Camera BuildSharedCamera()
        {
            var cameraObject = new GameObject("Cam_Shared");
            cameraObject.transform.position = new Vector3(roomCenter.x, roomCenter.y, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = sharedCameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.13f, 0.18f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.depth = 2f;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            return camera;
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
            side.Wallet = player.AddComponent<PlayerWallet>();
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
                opponent.Motor, opponent.TrackRoot, opponent.Shake, opponent.Flash,
                side.RestY, opponent.RestY, opponent.TrackY, trackLength);
            side.Loadout = loadout;

            foreach (TrackShop shop in side.Shops)
            {
                shop.Configure(side.Slot, side.Motor, side.Input, loadout, side.Wallet, banner, side.Camera,
                    _shopOffers[shop.OfferIndex]);
            }
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
