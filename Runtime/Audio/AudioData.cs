using System;
using gishadev.tools.Core;
using UnityEngine;

namespace gishadev.tools.Audio
{
    [Serializable]
    public abstract class AudioData : EnumEntryTarget, IDropdownTargetData
    {
        [field: Header("General")] [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField, Tooltip("Variations of Audio")] public AudioClip[] AudioClips { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float InitialVolume { get; private set; }
        [field: SerializeField, Range(0.3f, 3f)] public float Pitch { get; private set; }

        public AudioSource AudioSource { get; private set; }
        public virtual BaseAudioPlayer AudioPlayer { get; private set; }

        protected AudioData(AudioClip audioClip)
        {
            if (audioClip == null)
            {
                Debug.LogError("NULL audio clips provided.");
                return;
            }
            
            AudioClips = new[]{audioClip};
            
            Name = audioClip.name;
            InitialVolume = 1f;
            Pitch = 1f;
        }

        protected AudioData()
        {
        }

        public virtual void InitAudioSource(AudioSource audioSource)
        {
            AudioSource = audioSource;

            if (AudioClips == null || AudioClips.Length == 0)
                Debug.LogWarning($"Audio entry '{Name}' has no AudioClips assigned - it will play nothing.");
            else
                AudioSource.clip = AudioClips[0];

            AudioSource.volume = InitialVolume;
            AudioSource.pitch = Pitch;
        }

        public virtual void InitAudioPlayer(BaseAudioPlayer audioPlayer)
        {
            AudioPlayer = audioPlayer;
        }


        public abstract void Play();
        public abstract void Pause();
        public abstract void Stop();
    }

    [Serializable]
    public class MusicData : AudioData
    {
        [field: Header("Music")]
        [field: SerializeField]
        public bool IsLooping { get; private set; }

        [field: SerializeField] public bool IsFade { get; private set; }

        public override void InitAudioSource(AudioSource audioSource)
        {
            base.InitAudioSource(audioSource);
            AudioSource.loop = IsLooping;
        }

        public override void Play() => ((MusicPlayer) AudioPlayer).Play(this);
        public override void Pause() => ((MusicPlayer) AudioPlayer).Pause(this);
        public override void Stop() => ((MusicPlayer) AudioPlayer).Stop(this);

        public MusicData(AudioClip audioClip) : base(audioClip)
        {
            IsFade = true;
            IsLooping = false;
        }

        public MusicData()
        {
        }
    }

    [Serializable]
    public class SFXData : AudioData
    {
        public override void Play() => ((SFXPlayer) AudioPlayer).Play(this);
        public override void Pause() => ((SFXPlayer) AudioPlayer).Pause(this);
        public override void Stop() => ((SFXPlayer) AudioPlayer).Stop(this);
        
        public SFXData(AudioClip audioClip) : base(audioClip)
        {
        }

        public SFXData()
        {
        }
    }
}