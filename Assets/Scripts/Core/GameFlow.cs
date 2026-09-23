using System.Collections.Generic;
using UnityEngine;

namespace RaceSabotage
{
    public enum FlowPhase
    {
        Preparation,
        Racing
    }

    /// <summary>
    /// Runs the preparation countdown and the forced transition into the two tracks.
    /// Movement mode still belongs to each player's physical location rather than a
    /// large global state machine.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        PlayerMotor _one;
        PlayerMotor _two;
        PlayerWallet _oneWallet;
        PlayerWallet _twoWallet;
        SplitScreenManager _screens;
        IReadOnlyList<CoinPickup> _coins;
        Vector2 _oneTrackSpawn;
        Vector2 _twoTrackSpawn;
        float _remaining;

        GUIStyle _phaseStyle;
        GUIStyle _walletStyle;

        public FlowPhase Phase { get; private set; } = FlowPhase.Preparation;
        public float Remaining => Mathf.Max(0f, _remaining);

        public void Configure(PlayerMotor one, PlayerWallet oneWallet, Vector2 oneRoomSpawn, Vector2 oneTrackSpawn,
            PlayerMotor two, PlayerWallet twoWallet, Vector2 twoRoomSpawn, Vector2 twoTrackSpawn,
            SplitScreenManager screens, IReadOnlyList<CoinPickup> coins, float preparationDuration)
        {
            _one = one;
            _two = two;
            _oneWallet = oneWallet;
            _twoWallet = twoWallet;
            _screens = screens;
            _coins = coins;
            _oneTrackSpawn = oneTrackSpawn;
            _twoTrackSpawn = twoTrackSpawn;
            _remaining = Mathf.Max(1f, preparationDuration);

            PreparePlayer(_one, oneRoomSpawn);
            PreparePlayer(_two, twoRoomSpawn);

            Collider2D oneCollider = _one.GetComponent<Collider2D>();
            Collider2D twoCollider = _two.GetComponent<Collider2D>();
            if (oneCollider != null && twoCollider != null)
                Physics2D.IgnoreCollision(oneCollider, twoCollider, true);

            _screens.SetMode(ScreenMode.Shared);
        }

        static void PreparePlayer(PlayerMotor motor, Vector2 spawn)
        {
            motor.ClearSpeedPenalties();
            motor.Mode = MotorMode.Manual;
            motor.Frozen = false;
            motor.Teleport(spawn);
        }

        void Update()
        {
            if (Phase != FlowPhase.Preparation) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) BeginRace();
        }

        void BeginRace()
        {
            Phase = FlowPhase.Racing;

            if (_coins != null)
            {
                foreach (CoinPickup coin in _coins)
                {
                    if (coin != null) coin.gameObject.SetActive(false);
                }
            }

            StartRacer(_one, _oneTrackSpawn);
            StartRacer(_two, _twoTrackSpawn);
            _screens.SetMode(ScreenMode.Split);
        }

        static void StartRacer(PlayerMotor motor, Vector2 spawn)
        {
            motor.ClearSpeedPenalties();
            motor.Mode = MotorMode.AutoRun;
            motor.Frozen = false;
            motor.Teleport(spawn);
        }

        void OnGUI()
        {
            _phaseStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _walletStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            if (Phase == FlowPhase.Preparation)
            {
                GUI.Label(new Rect(0f, 16f, Screen.width, 46f),
                    $"COLLECT COINS — {Mathf.CeilToInt(Remaining)}", _phaseStyle);
                GUI.Label(new Rect(24f, 58f, 240f, 32f), $"P1 COINS  {_oneWallet.Coins}", _walletStyle);

                GUIStyle right = new GUIStyle(_walletStyle) { alignment = TextAnchor.UpperRight };
                GUI.Label(new Rect(Screen.width - 264f, 58f, 240f, 32f),
                    $"P2 COINS  {_twoWallet.Coins}", right);
                return;
            }

            float half = Screen.height * 0.5f;
            GUI.Label(new Rect(16f, 54f, 220f, 30f), $"COINS  {_oneWallet.Coins}", _walletStyle);
            GUI.Label(new Rect(16f, half + 54f, 220f, 30f), $"COINS  {_twoWallet.Coins}", _walletStyle);
        }
    }
}
