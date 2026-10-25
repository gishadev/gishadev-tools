using System;
using System.Collections.Generic;
using gishadev.tools.Audio;
using UnityEngine;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace gishadev.tools.Effects
{
    /// <summary>
    /// Plays the sounds of every <see cref="PhysicsSoundSource"/> it is given: bumps (its own and the bump of the
    /// <see cref="SurfaceAudio"/> it ran into), motion loops, squeaks and hinge clacks. Register it in the scope that
    /// owns the props, with the list of sources:
    /// <c>builder.RegisterEntryPoint&lt;PhysicsSoundService&gt;().WithParameter&lt;IReadOnlyList&lt;PhysicsSoundSource&gt;&gt;(sources);</c>
    /// </summary>
    public class PhysicsSoundService : IStartable, ITickable, IDisposable
    {
        private class SourceState
        {
            public SFXLoop Loop;
            public float NextImpactTime;
            public float Travelled;
            public float NextSqueakAt;
            public float LastAngle;
        }

        private readonly IReadOnlyList<PhysicsSoundSource> _sources;
        private readonly ISFXEmitter _emitter;
        private readonly Dictionary<PhysicsSoundSource, SourceState> _states = new();

        public PhysicsSoundService(IReadOnlyList<PhysicsSoundSource> sources, ISFXEmitter emitter)
        {
            _sources = sources;
            _emitter = emitter;
        }

        public void Start()
        {
            foreach (var source in _sources)
            {
                if (source == null || _states.ContainsKey(source))
                    continue;

                var state = new SourceState
                {
                    NextSqueakAt = NextSqueakDistance(source),
                    LastAngle = source.Hinge != null ? source.Hinge.angle : 0f
                };
                if (source.MotionLoop.IsSet)
                    state.Loop = _emitter.CreateLoop(source.MotionLoop, source.transform);

                _states.Add(source, state);
                source.Collided += OnCollided;
            }
        }

        public void Dispose()
        {
            foreach (var (source, state) in _states)
            {
                if (source != null)
                    source.Collided -= OnCollided;
                if (state.Loop != null)
                    state.Loop.Dispose();
            }

            _states.Clear();
        }

        public void Tick()
        {
            foreach (var (source, state) in _states)
            {
                if (source == null || !source.isActiveAndEnabled)
                    continue;

                float speed = source.MotionSpeed;
                UpdateMotionLoop(source, state, speed);
                UpdateSqueaks(source, state, speed);
                UpdateShut(source, state, speed);
            }
        }

        private void OnCollided(PhysicsSoundSource source, Collision collision)
        {
            if (!_states.TryGetValue(source, out var state) || Time.time < state.NextImpactTime ||
                collision.contactCount == 0)
                return;

            var contact = collision.GetContact(0);
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (speed < source.MinImpactSpeed)
                return;

            state.NextImpactTime = Time.time + source.ImpactCooldown;
            float volume = Mathf.Lerp(0.35f, 1f,
                Mathf.InverseLerp(source.MinImpactSpeed, source.FullImpactSpeed, speed));

            if (source.ImpactSound.IsSet)
                _emitter.EmitAt(source.ImpactSound, contact.point, null, volume, PitchJitter);

            var surface = collision.collider.GetComponentInParent<SurfaceAudio>();
            if (surface != null && surface.BumpSound.IsSet)
                _emitter.EmitAt(surface.BumpSound, contact.point, null, volume, PitchJitter);
        }

        // One-shots get a slightly different pitch each time so repeated bumps don't sound copy-pasted.
        private const float PitchJitter = 0.05f;

        private static void UpdateMotionLoop(PhysicsSoundSource source, SourceState state, float speed)
        {
            if (state.Loop == null)
                return;

            float t = Mathf.InverseLerp(source.MinMotionSpeed, source.FullMotionSpeed, speed);
            state.Loop.SetPlaying(speed > source.MinMotionSpeed, t);
            state.Loop.SetPitch(Mathf.Lerp(source.MotionPitch.x, source.MotionPitch.y, t));
        }

        private void UpdateSqueaks(PhysicsSoundSource source, SourceState state, float speed)
        {
            if (!source.MotionSqueak.IsSet || speed <= source.MinMotionSpeed)
                return;

            state.Travelled += speed * Time.deltaTime;
            if (state.Travelled < state.NextSqueakAt)
                return;

            state.Travelled = 0f;
            state.NextSqueakAt = NextSqueakDistance(source);
            _emitter.EmitAt(source.MotionSqueak, source.transform.position, null,
                Mathf.InverseLerp(0f, source.FullMotionSpeed, speed), PitchJitter);
        }

        private void UpdateShut(PhysicsSoundSource source, SourceState state, float speed)
        {
            if (!source.HasShutSound)
                return;

            float angle = source.Hinge.angle;
            bool wasOpen = Mathf.Abs(state.LastAngle) > source.ShutAngle;
            state.LastAngle = angle;

            if (wasOpen && Mathf.Abs(angle) <= source.ShutAngle && speed >= source.MinShutSpeed &&
                Time.time >= state.NextImpactTime)
            {
                state.NextImpactTime = Time.time + source.ImpactCooldown;
                _emitter.EmitAt(source.ShutSound, source.transform.position, null, 1f, PitchJitter);
            }
        }

        // Random spacing around the average, so squeaks don't tick like a metronome.
        private static float NextSqueakDistance(PhysicsSoundSource source) =>
            Random.Range(0.4f, 1.6f) / source.SqueaksPerMeter;
    }
}
