using System.Collections;
using UnityEngine;

namespace gishadev.tools.Effects
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
            // Real time and pitch-aware: audio plays on while timeScale is 0, and a pitched-down clip lasts longer.
            if (_audioSource.clip != null)
                yield return new WaitForSecondsRealtime(_audioSource.clip.length /
                                                        Mathf.Max(Mathf.Abs(_audioSource.pitch), 0.01f));

            gameObject.SetActive(false);
        }
    }
}