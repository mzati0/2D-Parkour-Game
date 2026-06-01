using UnityEngine;

namespace Player
{
    public class PlayerSound : MonoBehaviour
    {
        [Header("Footsteps")]
        public AudioClip[] footstepSounds;
        private int lastFootstepIndex = -1;

        public void PlayFootstepSFX()
        {
            if (footstepSounds != null && footstepSounds.Length > 0)
            {
                int randomIndex = Random.Range(0, footstepSounds.Length);
                
                // Prevent duplicate sounds from playing twice in a row
                if (footstepSounds.Length > 1 && randomIndex == lastFootstepIndex)
                {
                    randomIndex = (randomIndex + 1) % footstepSounds.Length;
                }
                lastFootstepIndex = randomIndex;
                
                float randomPitch = Random.Range(0.85f, 1.15f);
                SpawnAudioSource(footstepSounds[randomIndex], randomPitch, 0.5f);
            }
        }

        // Used for Taunts, Jumps, Landings, etc. via Animation Events
        public void PlaySpecificSound(AudioClip clipToPlay)
        {
            SpawnAudioSource(clipToPlay, 1f, 0.7f);
        }

        private void SpawnAudioSource(AudioClip clip, float pitch, float volume)
        {
            if (clip == null) return;

            // Create a temporary GameObject to hold the sound
            GameObject tempAudio = new GameObject("TempSFX_" + clip.name);
            tempAudio.transform.position = transform.position;
            
            // Add and configure the AudioSource
            AudioSource source = tempAudio.AddComponent<AudioSource>();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = volume;
            source.spatialBlend = 0f; // 2D sound (ignores distance)

            source.Play();

            // Self-destruct exactly when the clip finishes playing!
            // Divided by pitch because higher pitch = shorter playback time
            Destroy(tempAudio, clip.length / pitch);
        }
    }
}