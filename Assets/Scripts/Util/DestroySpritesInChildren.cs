using UnityEngine;

namespace Util
{
    public class DestroySpritesInChildren : MonoBehaviour
    {
        [SerializeField] private bool on;
        void Start()
        {
            if(!on) return;
            foreach (var sprite in GetComponentsInChildren<SpriteRenderer>())
            {
                Destroy(sprite);
                sprite.enabled = false;
            }
        }

    }
}
