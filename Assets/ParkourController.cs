using TMPro;
using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.InputSystem;

public class ParkourController : MonoBehaviour
{
    [Header("Visuals & States")]
    public SpriteRenderer cubeSprite;
    public Color normalColor = Color.blue;
    public Color stumbleColor = Color.red;
    public Color slideColor = Color.yellow; 
    public Color crouchColor = new Color(1f, 0.5f, 0f); 
    
    public bool isVaulting = false;
    public bool isGrounded = false;
    public bool isStumbling = false;
    public bool isSliding = false;
    public bool isCrouching = false;
    
    private float stumbleTimer = 1f;
    private float lockedFacingDirection = 1f;

    [Header("Physics & Momentum")]
    public Rigidbody2D rb;
    public float topSpeed = 12f;
    public float accelerationRate = 12f; 
    public float decelerationRate = 15f; 
    public float turnaroundMultiplier = 3f; 
    public float crouchSpeedMultiplier = 0.5f; 
    public float jumpForce = 20f;
    public float springboardForce = 25f;
    public float facingDirection = 1f;
    public float playerWidth = 1f;
    public float slideDecelerationRate = 6f; 

    [Header("Shift Mechanic")]
    public float maxShiftMeter = 100f;
    public float currentShiftMeter = 100f; 
    public float passiveRegenRate = 15f; 
    public float burstSpeedBonus = 8f; 
    public float burstCost = 30f;
    public float maxBurstActivationSpeed = 8f; 
    public InputActionReference shiftAction; 

    [Header("Raycast Data")]
    public Transform footPosition;
    public Transform headPosition; 
    public float rayDistance = 2f;
    public float groundCheckDistance = 0.2f;
    public LayerMask obstacleLayer;
    public LayerMask groundLayer;

    [Header("Slide & Crouch Mechanics")]
    public BoxCollider2D playerCollider;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference parkourAction;
    public InputActionReference trickAction;
    
    [Header("UI & Feedback")]
    public TMP_Text actionText;
    public TMP_Text speedText; 
    public Image shiftMeterFill; 
    private Coroutine currentTextCoroutine;

    // ==========================================
    // 1. UNITY LIFECYCLE
    // ==========================================
    void Start()
    {
        if (cubeSprite == null) cubeSprite = GetComponent<SpriteRenderer>(); 
        if (cubeSprite != null) cubeSprite.color = normalColor;
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        parkourAction.action.Enable();
        trickAction.action.Enable();
        shiftAction.action.Enable(); 

        jumpAction.action.performed += OnJump;
        trickAction.action.performed += OnTrickTap;
        shiftAction.action.performed += OnShiftBurst; 
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
        parkourAction.action.Disable();
        trickAction.action.Disable();
        shiftAction.action.Disable();

        jumpAction.action.performed -= OnJump;
        trickAction.action.performed -= OnTrickTap;
        shiftAction.action.performed -= OnShiftBurst;
    }

    void Update()
    {
        CheckGrounded();
            
        // Force stand up if airborne
        if (!isGrounded && (isSliding || isCrouching)) StandUp();
            
        HandleShiftRegenAndUI(); 

        if (isStumbling && !isVaulting)
        {
            HandleStumbleDeceleration();
            return;
        }

        HandleMovement();
        HandleCrouchAndSlide();
        HandlePassiveStumble();
        HandleParkourHold();
    }

    // ==========================================
    // 2. CORE STATE LOGIC & MOMENTUM
    // ==========================================
    void CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(footPosition.position, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = hit.collider != null;
    }

    void HandleShiftRegenAndUI()
    {

        if (shiftMeterFill != null)
        {
            shiftMeterFill.fillAmount = currentShiftMeter / maxShiftMeter;
        }

        if (speedText != null)
        {
            float kph = Mathf.Abs(rb.linearVelocity.x) * 3.6f;
            speedText.text = kph.ToString("F0") + " KM/H";
        }
    }

    void HandleMovement()
    {
        if (isVaulting || !isGrounded) return; 

        if (isSliding)
        {
            float slideVelocity = Mathf.MoveTowards(rb.linearVelocity.x, 0f, slideDecelerationRate * Time.deltaTime);
            rb.linearVelocity = new Vector2(slideVelocity, rb.linearVelocity.y);
            return; 
        }

        Vector2 moveVector = moveAction.action.ReadValue<Vector2>();
        float targetSpeed = 0f;

        if (moveVector.x != 0)
        {
            targetSpeed = moveVector.x * (isCrouching ? topSpeed * crouchSpeedMultiplier : topSpeed);
            facingDirection = moveVector.x > 0 ? 1f : -1f;
        }

        float currentVelocityX = rb.linearVelocity.x;
        
        if (Mathf.Abs(moveVector.x) > 0.1f)
        {
            float accelToUse = accelerationRate;
            if (Mathf.Sign(moveVector.x) != Mathf.Sign(currentVelocityX) && Mathf.Abs(currentVelocityX) > 0.5f)
            {
                accelToUse *= turnaroundMultiplier; 
            }
            
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, accelToUse * Time.deltaTime);
        }
        else
        {
            currentVelocityX = Mathf.MoveTowards(currentVelocityX, 0f, decelerationRate * Time.deltaTime);
        }

        rb.linearVelocity = new Vector2(currentVelocityX, rb.linearVelocity.y);
    }

    void HandleCrouchAndSlide()
    {
        if (!isGrounded || isVaulting || isStumbling) return;

        float inputY = moveAction.action.ReadValue<Vector2>().y;
        bool wantsToGoDown = inputY < -0.5f; 
        bool momentumDead = Mathf.Abs(rb.linearVelocity.x) < 1f; 

        if (wantsToGoDown)
        {
            if (!isSliding && !isCrouching)
            {
                if (!momentumDead) StartSlide();
                else StartCrouch();
            }
            else if (isSliding && momentumDead)
            {
                TransitionToCrouch();
            }
        }
        else
        {
            if (isSliding || isCrouching)
            {
                RaycastHit2D ceilingCheck = Physics2D.Raycast(footPosition.position, Vector2.up, 1.9f, obstacleLayer);
                
                if (ceilingCheck.collider == null) StandUp(); 
            }
        }
    }

    void HandleStumbleDeceleration()
    {
        stumbleTimer -= Time.deltaTime;
        
        float speedMultiplier = Mathf.Clamp01(stumbleTimer / 3f); 
        rb.linearVelocity = new Vector2(lockedFacingDirection * topSpeed * speedMultiplier, rb.linearVelocity.y);

        if (stumbleTimer <= 0f)
        {
            isStumbling = false;
            if (cubeSprite != null) cubeSprite.color = normalColor;
            DisplayAction("RECOVERED", Color.white);
        }
    }

    void HandlePassiveStumble()
    {
        if (isVaulting || !isGrounded || isStumbling || parkourAction.action.IsPressed()) return;

        Vector2 fireDirection = new Vector2(facingDirection, 0f);
        Vector2 originPos = new Vector2(footPosition.position.x, footPosition.position.y + 0.02f);
        
        RaycastHit2D hit = Physics2D.Raycast(originPos, fireDirection, 1f, obstacleLayer);
        
        if (hit.collider != null && hit.collider.bounds.size.y < 0.6f)
        {
            float armorCost = 50f; 
            
            if (currentShiftMeter >= armorCost)
            {
                // PROTECTED: Eat the meter, flash UI, and do a normal Speed Step
                currentShiftMeter -= armorCost;
                DisplayAction("STUMBLE PROTECTED!", Color.cyan);
                if (cubeSprite != null) cubeSprite.color = Color.cyan;
                
                // Route directly to the VaultRoutine (0.2s duration, 0.1f height)
                StartCoroutine(VaultRoutine(hit.collider, 0.2f, 0.1f)); 
            }
            else
            {
                // UNPROTECTED: Suffer the real stumble
                StartCoroutine(StumbleRoutine(hit.collider));
            }
        }
    }

    // ==========================================
    // 3. INPUT TRIGGERS & SHIFT
    // ==========================================
    void OnShiftBurst(InputAction.CallbackContext context)
    {
        if (!isGrounded || isVaulting || isStumbling || isSliding || isCrouching) return;

        if (Mathf.Abs(rb.linearVelocity.x) > maxBurstActivationSpeed)
        {
            DisplayAction("MAX SPEED!", Color.gray); 
            return; 
        }

        if (currentShiftMeter >= burstCost)
        {
            currentShiftMeter -= burstCost;
            DisplayAction("SHIFT BURST!", Color.white);
            
            float newSpeed = Mathf.Abs(rb.linearVelocity.x) + burstSpeedBonus;
            rb.linearVelocity = new Vector2(facingDirection * newSpeed, rb.linearVelocity.y);
        }
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded && !isVaulting && !isStumbling && !isSliding && !isCrouching)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    void HandleParkourHold()
    {
        if (isVaulting || !isGrounded || isStumbling) return; 
        if (parkourAction.action.IsPressed()) FireRaycast(false);
    }

    void OnTrickTap(InputAction.CallbackContext context)
    {
        if (isVaulting || !isGrounded || isStumbling) return; 
        
        // Add 10% to the meter (assuming max is 100)
        currentShiftMeter = Mathf.Clamp(currentShiftMeter + 10f, 0, maxShiftMeter);
        
        FireRaycast(true);
    }

    void FireRaycast(bool isTricking)
    {
        if (!isGrounded) return; 

        Vector2 fireDirection = new Vector2(facingDirection, 0f);
        Vector2 originPos = new Vector2(footPosition.position.x, footPosition.position.y + 0.1f);
        
        RaycastHit2D hit = Physics2D.Raycast(originPos, fireDirection, rayDistance, obstacleLayer);

        if (hit.collider != null) Calculate(hit.collider, isTricking);
    }

    // ==========================================
    // 4. ACTION EXECUTION
    // ==========================================
    void Calculate(Collider2D obstacle, bool isTricking)
    {
        float obstacleHeight = obstacle.bounds.size.y;
        float obstacleClearance = obstacle.bounds.size.x;
        float inputY = moveAction.action.ReadValue<Vector2>().y;

        // Only allow springboard if the obstacle is waist-high (1.0f) or lower
        if (inputY > 0.5f && !isTricking && obstacleHeight <= 1.0f)
        {
            Springboard();
            return;
        }

        if (obstacleHeight < 0.6f)
        {
            float duration = isTricking ? 0.4f : 0.2f;
            DisplayAction(isTricking ? "TRICK HOP!" : "SPEED STEP!", isTricking ? Color.cyan : Color.white);
            StartCoroutine(VaultRoutine(obstacle, duration, 0.1f)); 
        }
        else if (obstacleHeight <= 2.0f)
        {
            if (obstacleClearance <= 1.5f)
            {
                float duration = isTricking ? 0.7f : 0.3f;
                DisplayAction(isTricking ? "TRICK VAULT!" : "SPEED VAULT!", isTricking ? Color.cyan : Color.white);
                StartCoroutine(VaultRoutine(obstacle, duration, 1.0f));
            }
            else if (obstacleClearance <= 4.0f)
            {
                float duration = isTricking ? 0.8f : 0.4f;
                DisplayAction(isTricking ? "TRICK KONG!" : "SPEED KONG!", isTricking ? Color.cyan : Color.white);
                StartCoroutine(VaultRoutine(obstacle, duration, 1.5f)); 
            }
            else
            {
                DisplayAction("MANTLE!", Color.white);
                StartCoroutine(MantleRoutine(obstacle));
            }
        }
    }

    void Springboard()
    {
        DisplayAction("SPRINGBOARD!", Color.white);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, springboardForce);
    }

    void StartSlide()
    {
        isSliding = true;
        DisplayAction("SLIDE!", slideColor);
        ApplyDownVisuals(slideColor);
    }

    void StartCrouch()
    {
        isCrouching = true;
        DisplayAction("CROUCH!", crouchColor);
        ApplyDownVisuals(crouchColor);
    }

    void TransitionToCrouch()
    {
        isSliding = false;
        isCrouching = true;
        DisplayAction("CROUCH WALK", crouchColor);
        ApplyDownVisuals(crouchColor);
    }

    void StandUp()
    {
        isSliding = false;
        isCrouching = false;
        transform.localScale = new Vector3(1f, 2f, 1f); 
        if (cubeSprite != null) cubeSprite.color = normalColor;
    }

    void ApplyDownVisuals(Color stateColor)
    {
        transform.localScale = new Vector3(1f, 1f, 1f); 
        if (cubeSprite != null) cubeSprite.color = stateColor;
    }

    // ==========================================
    // 5. COROUTINES
    // ==========================================
    System.Collections.IEnumerator VaultRoutine(Collider2D obstacle, float duration, float extraHeight)
    {
        isVaulting = true;
        
        float entrySpeedX = rb.linearVelocity.x;
        
        rb.bodyType = RigidbodyType2D.Kinematic; // FIX: Replaced obsolete isKinematic
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false; 

        Vector2 startPos = transform.position;
        float landX = facingDirection == 1 ? obstacle.bounds.max.x + (playerWidth / 2f) : obstacle.bounds.min.x - (playerWidth / 2f);
        Vector2 endPos = new Vector2(landX, startPos.y); 

        float heightToClear = (obstacle.bounds.max.y - startPos.y) + extraHeight;
        float timePassed = 0f;

        while (timePassed < duration)
        {
            timePassed += Time.deltaTime;
            float linearT = timePassed / duration;
            float heightModifier = Mathf.Sin(linearT * Mathf.PI) * heightToClear;
            Vector2 currentPos = Vector2.Lerp(startPos, endPos, linearT);
            currentPos.y += heightModifier;
            rb.MovePosition(currentPos);
            yield return new WaitForFixedUpdate();
        }

        GetComponent<Collider2D>().enabled = true; 
        rb.bodyType = RigidbodyType2D.Dynamic; // FIX: Replaced obsolete isKinematic
        isVaulting = false;
        
        rb.linearVelocity = new Vector2(entrySpeedX, rb.linearVelocity.y);
        
        // FIX: Ensure the player returns to normal color after a protected stumble (or any vault)
        if (cubeSprite != null) cubeSprite.color = normalColor;
    }

    System.Collections.IEnumerator MantleRoutine(Collider2D obstacle)
    {
        isVaulting = true;
        rb.bodyType = RigidbodyType2D.Kinematic; // FIX
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;

        Vector2 startPos = transform.position;
        float edgeX = facingDirection == 1 ? obstacle.bounds.min.x + (playerWidth / 2f) : obstacle.bounds.max.x - (playerWidth / 2f);
        Vector2 endPos = new Vector2(edgeX, obstacle.bounds.max.y + (playerWidth / 2f));

        float timePassed = 0f;
        float duration = 0.25f; 

        while (timePassed < duration)
        {
            timePassed += Time.deltaTime;
            float linearT = timePassed / duration;
            float heightModifier = Mathf.Sin(linearT * Mathf.PI) * 0.5f; 
            Vector2 currentPos = Vector2.Lerp(startPos, endPos, linearT);
            currentPos.y += heightModifier;
            rb.MovePosition(currentPos);
            yield return new WaitForFixedUpdate();
        }

        GetComponent<Collider2D>().enabled = true;
        rb.bodyType = RigidbodyType2D.Dynamic; // FIX
        isVaulting = false;
    }

    System.Collections.IEnumerator StumbleRoutine(Collider2D obstacle)
    {
        DisplayAction("STUMBLE!", stumbleColor);
        isVaulting = true;
        isStumbling = true;
        
        if (cubeSprite != null) cubeSprite.color = stumbleColor;
        
        rb.bodyType = RigidbodyType2D.Kinematic; 
        
        // Unprotected Stumble = Dead Stop
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;

        Vector2 startPos = transform.position;
        float landX = facingDirection == 1 ? obstacle.bounds.max.x + (playerWidth / 2f) : obstacle.bounds.min.x - (playerWidth / 2f);
        Vector2 endPos = new Vector2(landX, startPos.y); 

        float timePassed = 0f;
        float duration = 0.4f; 

        while (timePassed < duration)
        {
            timePassed += Time.deltaTime;
            float linearT = timePassed / duration;
            
            float sagModifier = Mathf.Sin(linearT * Mathf.PI) * 0.2f; 
            Vector2 currentPos = Vector2.Lerp(startPos, endPos, linearT);
            currentPos.y += sagModifier;
            rb.MovePosition(currentPos);
            yield return new WaitForFixedUpdate();
        }

        GetComponent<Collider2D>().enabled = true;
        rb.bodyType = RigidbodyType2D.Dynamic; 
        isVaulting = false;
        lockedFacingDirection = facingDirection;

        // Punishing recovery timer based on how fast you crashed
        float speedRatio = Mathf.Abs(rb.linearVelocity.x) / topSpeed;
        stumbleTimer = Mathf.Clamp(5f * speedRatio, 1f, 5f);
    }

    // ==========================================
    // 6. UI FEEDBACK
    // ==========================================
    public void DisplayAction(string text, Color color)
    {
        if (actionText == null) return;
        
        if (currentTextCoroutine != null) StopCoroutine(currentTextCoroutine);
        
        currentTextCoroutine = StartCoroutine(ClearTextAfterDelay(text, color));
    }

    System.Collections.IEnumerator ClearTextAfterDelay(string text, Color color)
    {
        actionText.text = text;
        actionText.color = color;
        
        yield return new WaitForSeconds(1.5f); 
        
        actionText.text = "";
    }
}