using UnityEngine;

public class SimpleChunkManager : MonoBehaviour
{
    public Transform player;
    public GameObject[] chunkPrefabs; 
    
    public float chunkLength = 20f; 
    public float spawnDistanceThreshold = 10f; // Spawns when you are 10 meters away from the edge

    private float nextRightSpawnX = 20f; // First chunk spawns 20m to the right
    private float nextLeftSpawnX = -20f; // First chunk spawns 20m to the left

    void Update()
    {
        // Check right side
        if (player.position.x + spawnDistanceThreshold > nextRightSpawnX)
        {
            SpawnChunk(nextRightSpawnX);
            nextRightSpawnX += chunkLength; // Move the target 20m further right
        }
        
        // Check left side
        if (player.position.x - spawnDistanceThreshold < nextLeftSpawnX)
        {
            SpawnChunk(nextLeftSpawnX);
            nextLeftSpawnX -= chunkLength; // Move the target 20m further left
        }
    }

    void SpawnChunk(float xPos)
    {
        if (chunkPrefabs.Length == 0) return;

        // Pick random prefab and drop it exactly at xPos
        int randomIndex = Random.Range(0, chunkPrefabs.Length);
        Instantiate(chunkPrefabs[randomIndex], new Vector3(xPos, 0f, 0f), Quaternion.identity);
    }
}