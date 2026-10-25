using System;
using UnityEngine;

namespace gishadev.tools.Effects
{
    /// <summary>
    /// Sounds a physics prop makes (a cart, a gate, a crate): a bump on impact, a loop while it moves, random
    /// squeaks while it rolls, a clack when a hinged part swings shut. Passive data: it only reports collisions;
    /// <see cref="PhysicsSoundService"/> decides what plays. All sounds come from the SFX pool, saved by name.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsSoundSource : MonoBehaviour
    {
        [Header("Impact")]
        [Tooltip("Played where it hits something. Unset = only the bump of the surface it hit.")]
        [SerializeField] private SFXPoolRef impactSound;
        [Tooltip("Speed into the contact (m/s) needed for a bump. Sliding along a wall doesn't count.")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 1.2f;
        [Tooltip("Speed into the contact (m/s) at which the bump is at full volume.")]
        [SerializeField, Min(0.1f)] private float fullImpactSpeed = 4f;
        [SerializeField, Min(0f)] private float impactCooldown = 0.25f;

        [Header("Motion loop")]
        [Tooltip("Loops while it moves; its 3D range comes from the pool entry's prefab. Unset = none.")]
        [SerializeField] private SFXPoolRef motionLoop;
        [Tooltip("Speed at which the loop starts: m/s, or degrees/s for a body on a HingeJoint.")]
        [SerializeField, Min(0f)] private float minMotionSpeed = 0.15f;
        [Tooltip("Speed at which the loop is at full volume: m/s, or degrees/s for a body on a HingeJoint.")]
        [SerializeField, Min(0.01f)] private float fullMotionSpeed = 2.5f;
        [Tooltip("Pitch from slowest to fastest.")]
        [SerializeField] private Vector2 motionPitch = new(0.85f, 1.15f);

        [Header("Squeaks while moving")]
        [Tooltip("Played now and then while it moves. Unset = none.")]
        [SerializeField] private SFXPoolRef motionSqueak;
        [Tooltip("Average squeaks per meter travelled.")]
        [SerializeField, Min(0.01f)] private float squeaksPerMeter = 0.2f;

        [Header("Hinge shut")]
        [Tooltip("Needs a HingeJoint: played when the part swings back to its rest angle fast enough. Unset = none.")]
        [SerializeField] private SFXPoolRef shutSound;
        [Tooltip("Within this many degrees of the rest angle counts as shut.")]
        [SerializeField, Min(0.1f)] private float shutAngle = 4f;
        [Tooltip("Degrees per second needed for an audible clack.")]
        [SerializeField, Min(0f)] private float minShutSpeed = 45f;

        /// <summary>Raised on every collision; the service filters by speed.</summary>
        public event Action<PhysicsSoundSource, Collision> Collided;

        public Rigidbody Body { get; private set; }
        /// <summary>Null when the body isn't hinged.</summary>
        public HingeJoint Hinge { get; private set; }

        public SFXPoolRef ImpactSound => impactSound;
        public float MinImpactSpeed => minImpactSpeed;
        public float FullImpactSpeed => fullImpactSpeed;
        public float ImpactCooldown => impactCooldown;

        public SFXPoolRef MotionLoop => motionLoop;
        public float MinMotionSpeed => minMotionSpeed;
        public float FullMotionSpeed => fullMotionSpeed;
        public Vector2 MotionPitch => motionPitch;

        public SFXPoolRef MotionSqueak => motionSqueak;
        public float SqueaksPerMeter => squeaksPerMeter;

        public SFXPoolRef ShutSound => shutSound;
        public bool HasShutSound => shutSound.IsSet && Hinge != null;
        public float ShutAngle => shutAngle;
        public float MinShutSpeed => minShutSpeed;

        /// <summary>m/s along the ground, or degrees/s around the hinge.</summary>
        public float MotionSpeed
        {
            get
            {
                if (Hinge != null)
                    return Mathf.Abs(Hinge.velocity);

                var velocity = Body.linearVelocity;
                velocity.y = 0f;
                return velocity.magnitude;
            }
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Hinge = GetComponent<HingeJoint>();
        }

        private void OnCollisionEnter(Collision collision) => Collided?.Invoke(this, collision);
    }
}
