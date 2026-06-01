using Player;
using UnityEngine;

namespace Util
{
    public class AnimationEventBridge : MonoBehaviour
    {
        [Header("Link to Audio Script")]
        public PlayerSound playerSound; // Drag your root Player here!

        public void PlayFootstepSFX()
        {
            if (playerSound) playerSound.PlayFootstepSFX();
        }

        public void PlaySpecificSound(AudioClip clipToPlay)
        {
            if (playerSound) playerSound.PlaySpecificSound(clipToPlay);
        }
    }
}