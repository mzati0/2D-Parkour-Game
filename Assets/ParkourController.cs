        using System.Collections;
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
            public Color crouchColor = new(1f, 0.5f, 0f); 
            
            public bool isVaulting;
            public bool isGrounded;
            public bool isStumbling;
            public bool isSliding;
            public bool isCrouching;
            
            private float _stumbleTimer = 1f;
            private float _lockedFacingDirection = 1f;

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

            [Header("Flow Mechanic")]
            public float maxFlowMeter = 100f;
            public float currentFlowMeter = 100f; 
            public float burstSpeedBonus = 8f; 
            public float burstCost = 30f;
            public float maxBurstActivationSpeed = 8f; 
            public InputActionReference flowAction; 
            
            [Header("Flow Economy")]
            public float minTopSpeed = 36f; // The sluggish speed at 0 Flow
            public float maxTopSpeed = 45f; // The blazing speed at 100 Flow
            public float minAcceleration = 8f;
            public float maxAcceleration = 14f;
            public float minFlowRegen = 1f; // Guaranteed gain just for moving
            public float maxFlowRegen = 5f; // Max gain for hitting absolute max speed
            public float flowDecayRate = 50f; // Punishing decay when stopped
            public float flowDecaySpeedThreshold = 5f;
            private float _currentFlowRegenRate;

            [Header("Raycast Data")]
            public Transform footPosition;
            public Transform headPosition; 
            public float rayDistance = 2f;
            public float groundCheckDistance = 0.2f;
            public LayerMask obstacleLayer;
            public LayerMask groundLayer;

            [Header("Slide & Crouch Mechanics")]
            public Collider2D playerCollider;

            [Header("Input Actions")]
            public InputActionReference moveAction;
            public InputActionReference jumpAction;
            public InputActionReference parkourAction;
            public InputActionReference trickAction;
            
            [Header("UI & Feedback")]
            public TextMeshProUGUI actionText;
            public TextMeshProUGUI speedText;
            public TextMeshProUGUI flowCapacityText;
            public TextMeshProUGUI flowRateText;
            public Image flowMeterFill;
            public float uiLerpSpeed = 10f; // New variable for the smooth UI transition
            private Coroutine _currentTextCoroutine;
            private Collider2D _collider2D;

            // ==========================================
            // 1. UNITY LIFECYCLE
            // ==========================================
            private void Start()
            {
                _collider2D = GetComponent<Collider2D>();
                if (cubeSprite == null) cubeSprite = GetComponent<SpriteRenderer>(); 
                if (cubeSprite != null) cubeSprite.color = normalColor;
            }

            private void OnEnable()
            {
                moveAction.action.Enable();
                jumpAction.action.Enable();
                parkourAction.action.Enable();
                trickAction.action.Enable();
                flowAction.action.Enable(); 

                jumpAction.action.performed += OnJump;
                trickAction.action.performed += OnTrickTap;
                flowAction.action.performed += OnFlowBurst; 
            }

            private void OnDisable()
            {
                moveAction.action.Disable();
                jumpAction.action.Disable();
                parkourAction.action.Disable();
                trickAction.action.Disable();
                flowAction.action.Disable();

                jumpAction.action.performed -= OnJump;
                trickAction.action.performed -= OnTrickTap;
                flowAction.action.performed -= OnFlowBurst;
            }

            private void Update()
            {
                CheckGrounded();
                    
                // Force stand up if airborne
                if (!isGrounded && (isSliding || isCrouching)) StandUp();
                    

                if (isStumbling && !isVaulting)
                {
                    HandleStumbleDeceleration();
                    return;
                }
                
                HandleFlowEconomy();

                HandleMovement();
                HandleCrouchAndSlide();
                HandlePassiveStumble();
                HandleParkourHold();
                HandleUI(); 

            }

            // ==========================================
            // 2. CORE STATE LOGIC & MOMENTUM
            // ==========================================
            private void CheckGrounded()
            {
                RaycastHit2D hit = Physics2D.Raycast(footPosition.position, Vector2.down, groundCheckDistance, groundLayer);
                isGrounded = hit.collider ;
            }

            private void HandleUI()
            {
                if (flowMeterFill)
                {
                    float targetFill = currentFlowMeter / maxFlowMeter;
                    flowMeterFill.fillAmount = Mathf.Lerp(flowMeterFill.fillAmount, targetFill, Time.deltaTime * uiLerpSpeed);
                }

                if (speedText)
                {
                    float trueSpeed = Mathf.Abs(rb.linearVelocity.x);
                    speedText.text = (trueSpeed*3).ToString("F0") + " Km/h"; 
                    
                    // FIX: Uses InverseLerp. 
                    // If speed is 9 or below, ratio is 0 (White). 
                    // If speed is 12, ratio is 1 (Cyan). 
                    float colorRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, trueSpeed);
                    speedText.color = Color.Lerp(Color.white, Color.cyan, colorRatio);
                }
                
                if (flowCapacityText) flowCapacityText.text = currentFlowMeter.ToString("F0") + " / " + maxFlowMeter.ToString("F0");
                

                if (flowRateText)
                {
                    // FIX: Now it ONLY shows if the rate is strictly positive.
                    if (_currentFlowRegenRate > 0.01f) 
                    {
                        flowRateText.gameObject.SetActive(true);
                        flowRateText.text = "+" + _currentFlowRegenRate.ToString("F1") + " / sec";
                    }
                    else
                    {
                        // Instantly hides itself if it is 0 or negative (decaying).
                        flowRateText.gameObject.SetActive(false);
                    }
                }
            }

            private void HandleMovement()
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

                // Use Abs to create a dead zone, ignoring tiny stick drifts
                if (Mathf.Abs(moveVector.x) > 0.1f) 
                {
                    // FIX: Force the input to be exactly 1 or -1. No more diagonal 0.7 slowdowns.
                    float cleanDirection = Mathf.Sign(moveVector.x); 
                    
                    targetSpeed = cleanDirection * (isCrouching ? topSpeed * crouchSpeedMultiplier : topSpeed);
                    facingDirection = cleanDirection;
                }

                float currentVelocityX = rb.linearVelocity.x;
                
                if (Mathf.Abs(moveVector.x) > 0.1f)
                {
                    float accelToUse = accelerationRate;
                    if (!Mathf.Approximately(Mathf.Sign(moveVector.x), Mathf.Sign(currentVelocityX)) && Mathf.Abs(currentVelocityX) > 0.5f)
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

            private void HandleCrouchAndSlide()
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

            private void HandleStumbleDeceleration()
            {
                _stumbleTimer -= Time.deltaTime;
                
                float speedMultiplier = Mathf.Clamp01(_stumbleTimer / 3f); 
                rb.linearVelocity = new Vector2(_lockedFacingDirection * topSpeed * speedMultiplier, rb.linearVelocity.y);

                if (_stumbleTimer <= 0f)
                {
                    isStumbling = false;
                    if (cubeSprite) cubeSprite.color = normalColor;
                    DisplayAction("RECOVERED", Color.white);
                }
            }

            private void HandlePassiveStumble()
            {
                if (isVaulting || !isGrounded || isStumbling || parkourAction.action.IsPressed()) return;

                Vector2 fireDirection = new Vector2(facingDirection, 0f);
                Vector2 originPos = new Vector2(footPosition.position.x, footPosition.position.y + 0.02f);
                
                RaycastHit2D hit = Physics2D.Raycast(originPos, fireDirection, 1f, obstacleLayer);
                
                if (hit.collider && hit.collider.bounds.size.y < 0.6f)
                {
                    float armorCost = 50f; 
                    
                    if (currentFlowMeter >= armorCost)
                    {
                        // PROTECTED: Eat the meter, flash UI, and do a normal Speed Step
                        currentFlowMeter -= armorCost;
                        DisplayAction("STUMBLE PROTECTED!", Color.cyan);
                        if (cubeSprite) cubeSprite.color = Color.cyan;
                        
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
            // 3. INPUT TRIGGERS & FLOW
            // ==========================================
            private void OnFlowBurst(InputAction.CallbackContext context)
            {
                if (!isGrounded || isVaulting || isStumbling || isSliding || isCrouching) return;

                if (Mathf.Abs(rb.linearVelocity.x) > maxBurstActivationSpeed)
                {
                    DisplayAction("MAX SPEED!", Color.gray); 
                    return; 
                }

                if (currentFlowMeter >= burstCost)
                {
                    currentFlowMeter -= burstCost;
                    DisplayAction("FLOW BURST!", Color.white);
                    
                    float newSpeed = Mathf.Abs(rb.linearVelocity.x) + burstSpeedBonus;
                    rb.linearVelocity = new Vector2(facingDirection * newSpeed, rb.linearVelocity.y);
                }
            }

            private void OnJump(InputAction.CallbackContext context)
            {
                if (!isGrounded || isVaulting || isStumbling || isSliding || isCrouching) return;

                float moveInputX = moveAction.action.ReadValue<Vector2>().x;

                // If holding a direction, try to use the existing parkour raycast first
                if (Mathf.Abs(moveInputX) > 0.1f)
                {
                    // If FireRaycast returns true, it found a wall and is handling it. Cancel the jump.
                    if (FireRaycast(false)) return; 
                }

                // STANDARD JUMP: Triggers if standing still, or if moving but no wall is present.
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }

            private void HandleParkourHold()
            {
                if (isVaulting || !isGrounded || isStumbling) return; 
                if (parkourAction.action.IsPressed()) FireRaycast(false);
            }

            private void OnTrickTap(InputAction.CallbackContext context)
            {
                if (isVaulting || !isGrounded || isStumbling) return; 
                
                // Add 10% to the meter
                currentFlowMeter = Mathf.Clamp(currentFlowMeter + 10f, 0, maxFlowMeter);
                
                FireRaycast(true);
            }

            private bool FireRaycast(bool isTricking)
            {
                if (!isGrounded) return false; 

                Vector2 fireDirection = new Vector2(facingDirection, 0f);
                Vector2 originPos = new Vector2(footPosition.position.x, footPosition.position.y + 0.1f);
                
                RaycastHit2D hit = Physics2D.Raycast(originPos, fireDirection, rayDistance, obstacleLayer);

                if (hit.collider) 
                {
                    Calculate(hit.collider, isTricking);
                    return true; // We hit something and started a parkour move
                }
                
                return false; // Thin air
            }

            private void HandleFlowEconomy()
            {
                float currentAbsSpeed = Mathf.Abs(rb.linearVelocity.x);
                float currentFlowRatio = currentFlowMeter / maxFlowMeter;

                // 1. DYNAMIC SPEED & ACCELERATION
                topSpeed = Mathf.Lerp(minTopSpeed, maxTopSpeed, currentFlowRatio);
                accelerationRate = Mathf.Lerp(minAcceleration, maxAcceleration, currentFlowRatio);

                // 2. DECAY VS GROWTH
                if (currentAbsSpeed < flowDecaySpeedThreshold && isGrounded && !isVaulting)
                {
                    _currentFlowRegenRate = -flowDecayRate;
                    currentFlowMeter += _currentFlowRegenRate * Time.deltaTime;
                }
                else if (currentAbsSpeed >= flowDecaySpeedThreshold)
                {
                    // GROWING
                    // FIX: The scale now officially starts at minTopSpeed (8), not 0.
                    // If speed is 8 or lower, ratio is 0. If max speed, ratio is 1.
                    float speedRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, currentAbsSpeed);
                    
                    // This perfectly maps 0 ratio to minRegen (1), and 1 ratio to maxRegen (5).
                    _currentFlowRegenRate = Mathf.Lerp(minFlowRegen, maxFlowRegen, speedRatio);
                    
                    currentFlowMeter += _currentFlowRegenRate * Time.deltaTime;
                }
                else 
                {
                    _currentFlowRegenRate = 0f;
                }

                currentFlowMeter = Mathf.Clamp(currentFlowMeter, 0f, maxFlowMeter);
            }

            // ==========================================
            // 4. ACTION EXECUTION
            // ==========================================
            private void Calculate(Collider2D obstacle, bool isTricking)
        {
            float obstacleHeight = obstacle.bounds.size.y;
            float obstacleClearance = obstacle.bounds.size.x;
            float inputY = moveAction.action.ReadValue<Vector2>().y;

            // 1. ABSOLUTE HEIGHT LIMIT: Prevents climbing the Eiffel Tower
            
            if (obstacleHeight > 4.0f)
            {
                return; // Too tall. Treat it like a solid wall.
            }

            // 2. ZERO-SPEED CLAMBER OVERRIDE
            // Because of the check above, we know it is guaranteed to be <= 4.0f
            if (Mathf.Abs(rb.linearVelocity.x) < 1.5f && !isTricking)
            {
                DisplayAction("Climb!", Color.white);
                StartCoroutine(MantleRoutine(obstacle));
                return;
            }
            
            // 3. SPRINGBOARD
            if (inputY > 0.5f && !isTricking && obstacleHeight <= 1.0f)
            {
                Springboard();
                return;
            }

            switch (obstacleHeight)
            {
                // 4. THE PARKOUR DECISION TREE (Moving > 1.5f Speed)
                case < 0.6f:
                {
                    // Tiny objects: Step / Hop
                    float duration = isTricking ? 0.4f : 0.2f;
                    DisplayAction(isTricking ? "TRICK HOP!" : "SPEED STEP!", isTricking ? Color.cyan : Color.white);
                    StartCoroutine(VaultRoutine(obstacle, duration, 0.1f));
                    break;
                }
                // Medium objects (up to chest height): Vault or Kong
                case <= 1.5f when obstacleClearance <= 1.5f:
                {
                    float duration = isTricking ? 0.7f : 0.3f;
                    DisplayAction(isTricking ? "TRICK VAULT!" : "SPEED VAULT!", isTricking ? Color.cyan : Color.white);
                    StartCoroutine(VaultRoutine(obstacle, duration, 1.0f));
                    break;
                }
                case <= 1.5f when obstacleClearance <= 3.0f:
                {
                    float duration = isTricking ? 0.8f : 0.4f;
                    DisplayAction(isTricking ? "TRICK KONG!" : "SPEED KONG!", isTricking ? Color.cyan : Color.white);
                    StartCoroutine(VaultRoutine(obstacle, duration, 1.5f));
                    break;
                }
                case <= 1.5f:
                    // Short enough to vault, but too long to clear. Climb on top.
                    DisplayAction("Climb!", Color.white);
                    StartCoroutine(MantleRoutine(obstacle));
                    break;
                default:
                    // Tall objects (Height is > 1.5f and <= 4.0f): Forced Climb
                    // Prevents flying speed-vaults over tall fences/pillars
                    DisplayAction("Climb!", Color.white);
                    StartCoroutine(MantleRoutine(obstacle));
                    break;
            }
        }

            private void Springboard()
            {
                DisplayAction("SPRINGBOARD!", Color.white);
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, springboardForce);
            }

            private void StartSlide()
            {
                isSliding = true;
                DisplayAction("SLIDE!", slideColor);
                ApplyDownVisuals(slideColor);
            }

            private void StartCrouch()
            {
                isCrouching = true;
                DisplayAction("CROUCH!", crouchColor);
                ApplyDownVisuals(crouchColor);
            }

            private void TransitionToCrouch()
            {
                isSliding = false;
                isCrouching = true;
                DisplayAction("CROUCH WALK", crouchColor);
                ApplyDownVisuals(crouchColor);
            }

            private void StandUp()
            {
                isSliding = false;
                isCrouching = false;
                transform.localScale = new Vector3(1f, 2f, 1f); 
                if (cubeSprite) cubeSprite.color = normalColor;
            }

            void ApplyDownVisuals(Color stateColor)
            {
                transform.localScale = new Vector3(1f, 1f, 1f); 
                if (cubeSprite) cubeSprite.color = stateColor;
            }

            // ==========================================
            // 5. COROUTINES
            // ==========================================
            private IEnumerator VaultRoutine(Collider2D obstacle, float duration, float extraHeight)
            {
                isVaulting = true;
            
                float entrySpeedX = rb.linearVelocity.x;
            
                rb.bodyType = RigidbodyType2D.Kinematic; 
                rb.linearVelocity = Vector2.zero;
                _collider2D.enabled = false; 

                Vector2 startPos = transform.position;
            
                // FIX: Added 0.25f clearance padding to ensure the player fully clears the obstacle's raycast zone
                float clearancePadding = 0.25f;
                float landX = Mathf.Approximately(facingDirection, 1) 
                    ? obstacle.bounds.max.x + (playerWidth / 2f) + clearancePadding 
                    : obstacle.bounds.min.x - (playerWidth / 2f) - clearancePadding;
                
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

                _collider2D.enabled = true; 
                rb.bodyType = RigidbodyType2D.Dynamic; 
                isVaulting = false;
            
                rb.linearVelocity = new Vector2(entrySpeedX, rb.linearVelocity.y);
            
                if (cubeSprite) cubeSprite.color = normalColor;
            }

            private IEnumerator MantleRoutine(Collider2D obstacle)
            {
                isVaulting = true;
                rb.bodyType = RigidbodyType2D.Kinematic; 
                rb.linearVelocity = Vector2.zero;
                _collider2D.enabled = false;

                // --- 1. DYNAMIC DURATION CALCULATION ---
                float obstacleHeight = obstacle.bounds.size.y;
                
                // InverseLerp finds where the height falls between 1 and 3 (returns a 0.0 to 1.0 ratio)
                float heightRatio = Mathf.InverseLerp(1f, 4f, obstacleHeight);
                
                // We use that ratio to scale the duration perfectly between 0.4s and 0.75s
                float totalDuration = Mathf.Lerp(0.3f, 01f, heightRatio);

                // --- 2. TRAJECTORY SETUP ---
                Vector2 startPos = transform.position;
                float edgeX = Mathf.Approximately(facingDirection, 1) ? obstacle.bounds.min.x + (playerWidth / 2f) : obstacle.bounds.max.x - (playerWidth / 2f);
                float topY = obstacle.bounds.max.y + (playerWidth / 2f);

                // Split the animation: 70% of the time is spent pulling UP, 30% is spent pushing FORWARD over the ledge.
                float climbDuration = totalDuration * 0.7f;
                float pullDuration = totalDuration * 0.3f;

                // --- PHASE 1: CLIMB UP ---
                float timePassed = 0f;
                while (timePassed < climbDuration)
                {
                    timePassed += Time.deltaTime;
                    float t = timePassed / climbDuration;
                    
                    // Using a Sine Ease-Out makes the initial pull fast, slowing down as they reach the top of the wall
                    float easeT = Mathf.Sin(t * Mathf.PI / 2f); 
                    
                    // Notice X stays exactly the same, only Y moves
                    rb.MovePosition(new Vector2(startPos.x, Mathf.Lerp(startPos.y, topY, easeT)));
                    yield return new WaitForFixedUpdate();
                }

                // --- PHASE 2: PULL FORWARD ---
                timePassed = 0f;
                Vector2 topPos = new Vector2(startPos.x, topY); // Locked at the top of the wall
                
                while (timePassed < pullDuration)
                {
                    timePassed += Time.deltaTime;
                    float t = timePassed / pullDuration;
                    
                    // Notice Y stays exactly the same, only X moves over the ledge
                    rb.MovePosition(new Vector2(Mathf.Lerp(topPos.x, edgeX, t), topY));
                    yield return new WaitForFixedUpdate();
                }

                // --- FINISH ---
                _collider2D.enabled = true;
                rb.bodyType = RigidbodyType2D.Dynamic; 
                isVaulting = false;
            }

            private IEnumerator StumbleRoutine(Collider2D obstacle)
            {
                DisplayAction("STUMBLE!", stumbleColor);
                isVaulting = true;
                isStumbling = true;
                
                if (cubeSprite) cubeSprite.color = stumbleColor;
                
                rb.bodyType = RigidbodyType2D.Kinematic; 
                
                rb.linearVelocity = Vector2.zero;
                _collider2D.enabled = false;

                Vector2 startPos = transform.position;
                float landX = Mathf.Approximately(facingDirection, 1) ? obstacle.bounds.max.x + (playerWidth / 2f) : obstacle.bounds.min.x - (playerWidth / 2f);
                Vector2 endPos = new Vector2(landX, startPos.y); 

                float timePassed = 0f;
                const float duration = 0.4f; 

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

                _collider2D.enabled = true;
                rb.bodyType = RigidbodyType2D.Dynamic; 
                isVaulting = false;
                _lockedFacingDirection = facingDirection;

                float speedRatio = Mathf.Abs(rb.linearVelocity.x) / topSpeed;
                _stumbleTimer = Mathf.Clamp(5f * speedRatio, 1f, 5f);
            }

            // ==========================================
            // 6. UI FEEDBACK
            // ==========================================
            private void DisplayAction(string text, Color color)
            {
                if (!actionText) return;
                
                if (_currentTextCoroutine != null) StopCoroutine(_currentTextCoroutine);
                
                _currentTextCoroutine = StartCoroutine(ClearTextAfterDelay(text, color));
            }

            private IEnumerator ClearTextAfterDelay(string text, Color color)
            {
                actionText.text = text;
                actionText.color = color;
                
                yield return new WaitForSeconds(1.5f); 
                
                actionText.text = "";
            }
        }