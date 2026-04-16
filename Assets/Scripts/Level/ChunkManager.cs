using UnityEngine;

namespace Level
{
    public class ChunkManager : MonoBehaviour
    {
        public Transform player;
        public Transform parent;
        public GameObject[] chunkPrefabs; 
    
        public float chunkLength = 20f; 
        public float spawnDistanceThreshold = 10f; // Spawns when you are 10 meters away from the edge

        private float _nextRightSpawnX = 20f; // First chunk spawns 20m to the right
        private float _nextLeftSpawnX = -20f; // First chunk spawns 20m to the left

        private void Update()
        {
            // Check right side
            if (player.position.x + spawnDistanceThreshold > _nextRightSpawnX)
            {
                SpawnChunk(_nextRightSpawnX);
                _nextRightSpawnX += chunkLength; // Move the target 20m further right
            }
        
            // Check left side
            if (player.position.x - spawnDistanceThreshold < _nextLeftSpawnX)
            {
                SpawnChunk(_nextLeftSpawnX);
                _nextLeftSpawnX -= chunkLength; // Move the target 20m further left
            }
        }

        private void SpawnChunk(float xPos)
        {
            if (chunkPrefabs.Length == 0) return;

            // Pick random prefab and drop it exactly at xPos
            int randomIndex = Random.Range(0, chunkPrefabs.Length);
            Instantiate(chunkPrefabs[randomIndex], new Vector3(xPos, 0f, 0f), Quaternion.identity).transform.SetParent(parent);
        }
    }
}