using UnityEngine;

public class LaserDirector : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject laserObject; // Drag your actual Laser here

    [Header("Settings")]
    public float startThresholdX = 10f; // The invisible line the player must cross

    private bool _laserActivated;

    void Start()
    {
        // Ensure the laser is off when the game starts
        if (laserObject != null)
        {
            laserObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!_laserActivated && player != null)
        {
            // If player runs to the right and crosses the threshold
            if (player.position.x > startThresholdX)
            {
                _laserActivated = true;
                
                if (laserObject != null)
                {
                    laserObject.SetActive(true);
                }
                
                // Optional: Print a debug log so you know it triggered
                Debug.Log("Player crossed threshold. Laser Activated!");
            }
        }
    }
}