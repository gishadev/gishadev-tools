using System.Collections;
using UnityEngine;

namespace Gisha.Effects.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class DisableSFXOnComplete : MonoBehaviour
    {
        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        public void StartTimer()
        {
            StopAllCoroutines();
            StartCoroutine(DisableOnAudioComplete());
        }

        private IEnumerator DisableOnAudioComplete()
        {
            yield return new WaitForSeconds(_audioSource.clip.length);
            gameObject.SetActive(false);
        }
    }
}