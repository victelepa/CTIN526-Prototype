using System;
using UnityEngine;

namespace RaceSabotage
{
    public enum MotorMode
    {
        /// <summary>Prep and fighting phases: the player drives horizontal movement.</summary>
        Manual,

        /// <summary>Racing phase: constant forward run, jump is the only movement input.</summary>
        AutoRun
    }

    /// <summary>
    /// Gravity and jumping are integrated by hand so every step can be scaled by
    /// <see cref="PlayerTime"/>. Unity's own gravity runs on one global clock and
    /// cannot be slowed for a single player in local multiplayer.
    /// Collision resolution is still left to the physics engine: the body is
    /// dynamic with gravityScale 0 and its velocity is written every step.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerTime))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] MotorMode mode = MotorMode.AutoRun;

        [Header("Speed (world units per second)")]
        [SerializeField] float runSpeed = 8f;
        [SerializeField] float manualSpeed = 8f;
        [Tooltip("Floor on the combined speed multiplier. Slows stack multiplicatively, so two " +
                 "overlapping 0.4x hits would otherwise leave you at 0.16x and unable to catch up.")]
        [SerializeField, Range(0.05f, 1f)] float minSpeedMultiplier = 0.35f;

        [Header("Jump")]
        [Tooltip("Peak height of a full jump, in world units.")]
        [SerializeField] float jumpHeight = 2.2f;
        [Tooltip("Seconds from takeoff to the peak. Lower is snappier.")]
        [SerializeField] float timeToApex = 0.35f;
        [Tooltip("Rising velocity is multiplied by this when jump is released early.")]
        [SerializeField, Range(0f, 1f)] float jumpCutMultiplier = 0.45f;
        [SerializeField] float maxFallSpeed = 20f;

        [Header("Forgiveness")]
        [SerializeField] float coyoteTime = 0.1f;
        [SerializeField] float jumpBufferTime = 0.1f;

        [Header("Ground Check")]
        [Tooltip("Constant downward push while grounded. Keeps the body in real contact with " +
                 "the surface instead of resting wherever the check box first reported ground.")]
        [SerializeField] float groundStickSpeed = 1f;
        [SerializeField] Vector2 groundCheckSize = new Vector2(0.8f, 0.14f);
        [Tooltip("Distance below the body centre. Keep the box clear of the player's own collider.")]
        [SerializeField] float groundCheckOffset = 0.58f;

        Rigidbody2D _body;
        PlayerTime _time;
        PlayerInputReader _input;

        readonly TimedMultiplier _speed = new TimedMultiplier();
        Vector2 _velocity;
        float _moveInput;
        float _coyoteLeft;
        float _jumpBufferLeft;
        float _immunityLeft;
        bool _grounded;
        bool _jumpHeld;
        bool _jumpCutArmed;

        public MotorMode Mode
        {
            get => mode;
            set => mode = value;
        }

        /// <summary>Used by the shop stop, the finish line and stuns.</summary>
        public bool Frozen { get; set; }

        public bool IsGrounded => _grounded;
        public float SpeedMultiplier => Mathf.Max(_speed.Value, minSpeedMultiplier);
        public Vector2 Velocity => _velocity;

        /// <summary>True right after a Swap's landing grace, so the item can't double-punish someone
        /// it just dropped onto (or next to) an obstacle.</summary>
        public bool IsImmune => _immunityLeft > 0f;

        float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
        float JumpVelocity => 2f * jumpHeight / timeToApex;

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _time = GetComponent<PlayerTime>();
            _input = GetComponent<PlayerInputReader>();

            _body.gravityScale = 0f;
            _body.freezeRotation = true;
        }

        void Update()
        {
            _speed.Tick(Time.deltaTime);
            // Fairness window, not a gameplay slow-down, so it runs on real time
            // regardless of any slow-motion effect currently on this player.
            _immunityLeft -= Time.deltaTime;

            if (_input == null) return;
            _moveInput = _input.MoveX;
            _jumpHeld = _input.JumpHeld;
            if (_input.JumpPressed) _jumpBufferLeft = jumpBufferTime;
        }

        void FixedUpdate()
        {
            if (Frozen)
            {
                _velocity = Vector2.zero;
                _body.linearVelocity = Vector2.zero;
                return;
            }

            float dt = _time.FixedDeltaTime;
            UpdateGrounded();

            bool resting = _grounded && _velocity.y <= 0f;
            if (resting)
            {
                _velocity.y = -groundStickSpeed;
                _coyoteLeft = coyoteTime;
            }
            else
            {
                _coyoteLeft -= dt;
            }

            _jumpBufferLeft -= dt;

            if (_jumpBufferLeft > 0f && _coyoteLeft > 0f)
            {
                _velocity.y = JumpVelocity;
                _jumpBufferLeft = 0f;
                _coyoteLeft = 0f;
                _jumpCutArmed = true;
                resting = false;
            }

            if (_jumpCutArmed && !_jumpHeld)
            {
                if (_velocity.y > 0f) _velocity.y *= jumpCutMultiplier;
                _jumpCutArmed = false;
            }

            // A resting body keeps its constant stick speed; letting gravity build up
            // there would grow the penetration the solver has to undo every step.
            if (!resting)
            {
                _velocity.y = Mathf.Max(_velocity.y - Gravity * dt, -maxFallSpeed);
            }

            _velocity.x = mode == MotorMode.AutoRun
                ? runSpeed * SpeedMultiplier
                : _moveInput * manualSpeed * SpeedMultiplier;

            // The body moves at the player's own rate, so world velocity is the
            // internally integrated velocity scaled by that rate.
            _body.linearVelocity = _velocity * _time.Scale;
        }

        void UpdateGrounded()
        {
            Vector2 origin = _body.position + Vector2.down * groundCheckOffset;
            Collider2D[] hits = Physics2D.OverlapBoxAll(origin, groundCheckSize, 0f);

            _grounded = false;
            foreach (Collider2D hit in hits)
            {
                if (hit == null || hit.isTrigger) continue;
                if (hit.attachedRigidbody == _body) continue;
                _grounded = true;
                break;
            }
        }

        /// <summary>
        /// Feeds the same buffer a key press would, so items, scripted sequences and
        /// tests can jump without pretending to be a device.
        /// A full jump lifts the body by exactly <see cref="jumpHeight"/>, which is
        /// also the ceiling on how tall a jumpable obstacle can be.
        /// </summary>
        public void RequestJump() => _jumpBufferLeft = jumpBufferTime;

        /// <summary>Raised with (multiplier, duration) so presentation can react to every hit.</summary>
        public event Action<float, float> SpeedPenaltyApplied;

        /// <summary>Obstacle hits and debuffs slow forward speed without slowing the player's clock.</summary>
        public void ApplySpeedPenalty(float multiplier, float duration)
        {
            if (IsImmune) return;

            _speed.Add(multiplier, duration);
            SpeedPenaltyApplied?.Invoke(multiplier, duration);
        }

        /// <summary>
        /// Self buffs (Nitro) go through the same multiplier stack as a hostile hit, since
        /// both are just "temporarily scale my speed" - but this deliberately skips
        /// SpeedPenaltyApplied, so boosting yourself never triggers the hit-reaction shake/tint
        /// that PlayerHitFeedback wires up for getting hit.
        /// </summary>
        public void ApplyBoost(float multiplier, float duration) => _speed.Add(multiplier, duration);

        /// <summary>Brief grace window after a Swap lands, see <see cref="IsImmune"/>.</summary>
        public void GrantImmunity(float seconds) => _immunityLeft = Mathf.Max(_immunityLeft, seconds);

        /// <summary>The check box must stay clear of the player's own collider, so it moves with body size.</summary>
        public void ConfigureGroundCheck(Vector2 bodySize)
        {
            groundCheckSize = new Vector2(bodySize.x * 0.8f, 0.14f);
            groundCheckOffset = bodySize.y * 0.5f + 0.1f;
        }

        public void ClearSpeedPenalties() => _speed.Clear();

        public void Teleport(Vector2 position, bool keepVerticalVelocity = false)
        {
            _body.position = position;
            if (!keepVerticalVelocity) _velocity.y = 0f;
            _body.linearVelocity = _velocity * _time.Scale;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 origin = transform.position + Vector3.down * groundCheckOffset;
            Gizmos.DrawWireCube(origin, new Vector3(groundCheckSize.x, groundCheckSize.y, 0f));
        }
    }
}
