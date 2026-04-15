using UnityEngine;
using UnityEngine.SceneManagement; 
    
public class RedLaserDeathWall : MonoBehaviour
{
    [Header("Settings")]
    public float startingSpeed = 1f; 
    public float acceleration = 0.5f; // How much speed it gains per second
    public float maxSpeed = 15f; // The absolute cap
    
    private float _currentSpeed;

    private void Awake()
    {
        _currentSpeed = startingSpeed;
    }

    private void Update()
    {
            // 1. Increase the speed every frame
            _currentSpeed += acceleration * Time.deltaTime;
            
            // 2. Clamp it so it doesn't get infinitely fast
            _currentSpeed = Mathf.Clamp(_currentSpeed, 0, maxSpeed);

            // 3. Move the wall at the new current speed
            transform.Translate(Vector3.right * (_currentSpeed * Time.deltaTime));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}