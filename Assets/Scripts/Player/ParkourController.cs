using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Player
{
    public class ParkourController : MonoBehaviour
    {
        #region VARIABLES: Core & Components
        [Header("Core Components")]
        public Rigidbody2D rb;
        public SpriteRenderer cubeSprite;
        private CapsuleCollider2D _capsuleCollider2D;
        [SerializeField] private GameObject sprites;
        #endregion

        #region VARIABLES: Input Actions
        [Header("Input Actions")]
        public InputActionReference moveAction;
        public InputActionReference jumpAction;
        public InputActionReference parkourAction;
        public InputActionReference trickAction;
        public InputActionReference flowAction;
        #endregion

        #region VARIABLES: State Flags & Visuals
        [Header("State Flags")]
        public bool isVaulting;
        public bool isGrounded;
        public bool isStumbling;
        public bool isSliding;
        public bool isCrouching;
        public bool isHanging;
        private Collider2D _currentLedge;

        [Header("State Colors")]
        public Color normalColor = Color.blue;
        public Color stumbleColor = Color.red;
        public Color slideColor = Color.yellow; 
        public Color crouchColor = new(1f, 0.5f, 0f); 
    
        private float _stumbleTimer = 1f;
        private float _lockedFacingDirection = 1f;
        #endregion

        #region VARIABLES: Physics & Movement
        [Header("Movement Mechanics")]
        public float topSpeed = 12f;
        public float accelerationRate = 12f; 
        public float decelerationRate = 15f; 
        public float turnaroundMultiplier = 3f; 
        public float crouchSpeedMultiplier = 0.5f; 
        public float slideDecelerationRate = 6f; 
        public float jumpForce = 20f;
        public float springboardForce = 25f;
    
        [Header("Spatial Data")]
        public float facingDirection = 1f;
        public float playerWidth = 1f;
        #endregion

        #region VARIABLES: Flow System
        [Header("Flow System")]
        public float maxFlowMeter = 100f;
        public float currentFlowMeter = 100f; 
        public float burstSpeedBonus = 8f; 
        public float burstCost = 30f;
        public float maxBurstActivationSpeed = 8f; 
        public float minTopSpeed = 36f; 
        public float maxTopSpeed = 45f; 
        public float minAcceleration = 8f;
        public float maxAcceleration = 14f;
        public float minFlowRegen = 1f; 
        public float maxFlowRegen = 5f; 
        public float flowDecayRate = 50f; 
        public float flowDecaySpeedThreshold = 5f;
        private float _currentFlowRegenRate;
        #endregion

        #region VARIABLES: Multi-Sensor Array (NEW)
        [Header("Multi-Sensor Array")]
        public Transform headPosition; 
        public Transform chestPosition;
        public Transform waistPosition; 
        public Transform shinPosition;  
        public Transform footPosition;  

        [Header("Sensor Dimensions")]
        public Vector2 headBoxSize = new(0.2f, 0.2f);
        public Vector2 chestBoxSize = new(0.2f, 0.3f);
        public Vector2 waistBoxSize = new(0.2f, 0.3f);
        public Vector2 shinBoxSize = new(0.2f, 0.3f);
        public Vector2 toeBoxSize = new(0.2f, 0.1f);
        public float sensorCastDistance = 2f;
        public float groundCheckDistance = 0.2f;
        
        public LayerMask obstacleLayer;
        public LayerMask groundLayer;

        // Internal Sensor Memory
        private bool _isCollidingFront;
        private RaycastHit2D _headHit;
        private RaycastHit2D _chestHit;
        private RaycastHit2D _waistHit;
        private RaycastHit2D _shinHit;
        private RaycastHit2D _toeHit;
        #endregion
    
        #region VARIABLES: UI & Feedback
        [Header("UI & Feedback")]
        public TextMeshProUGUI actionText;
        public TextMeshProUGUI speedKmhText;
        public TextMeshProUGUI speedUsText;
        public TextMeshProUGUI flowCapacityText;
        public TextMeshProUGUI flowRateText;
        public Image flowMeterFill;
        public float uiLerpSpeed = 10f; 
        private Coroutine _currentTextCoroutine;
        #endregion

        #region  VARIABLES: Debug
        [Header("Debug")]
        [SerializeField] private bool showUnits;
        #endregion

        #region 1. UNITY LIFECYCLE
        private void Start()
        {
            _capsuleCollider2D = GetComponent<CapsuleCollider2D>();
            if (cubeSprite == null) cubeSprite = GetComponent<SpriteRenderer>(); 
            if (cubeSprite) cubeSprite.color = normalColor;
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
            ScanFrontObstacles(); // Always keep our nervous system updated
            
            if (isHanging)
            {
                HandleHangingState();
                return; // Skips all movement and gravity logic while on the wall
            }

            CheckAirborneLedgeGrab();
            
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
        #endregion

        #region 2. INPUT HANDLERS
        private void OnJump(InputAction.CallbackContext context)
        {
            if (!isGrounded || isVaulting || isStumbling || isSliding || isCrouching) return;

            float moveInputX = moveAction.action.ReadValue<Vector2>().x;

            if (Mathf.Abs(moveInputX) > 0.1f)
            {
                if (FireParkourAction(false)) return; 
            }

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        private void OnTrickTap(InputAction.CallbackContext context)
        {
            if (isVaulting || !isGrounded || isStumbling) return; 
            currentFlowMeter = Mathf.Clamp(currentFlowMeter + 10f, 0, maxFlowMeter);
            FireParkourAction(true);
        }

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
        #endregion

        #region 3. CORE UPDATE LOGIC
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

            if (Mathf.Abs(moveVector.x) > 0.1f) 
            {
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
                    if (!ceilingCheck.collider) StandUp(); 
                }
            }
        }

        private void HandleFlowEconomy()
        {
            float currentAbsSpeed = Mathf.Abs(rb.linearVelocity.x);
            float currentFlowRatio = currentFlowMeter / maxFlowMeter;

            topSpeed = Mathf.Lerp(minTopSpeed, maxTopSpeed, currentFlowRatio);
            accelerationRate = Mathf.Lerp(minAcceleration, maxAcceleration, currentFlowRatio);

            if (currentAbsSpeed < flowDecaySpeedThreshold && isGrounded && !isVaulting)
            {
                _currentFlowRegenRate = -flowDecayRate;
                currentFlowMeter += _currentFlowRegenRate * Time.deltaTime;
            }
            else if (currentAbsSpeed >= flowDecaySpeedThreshold)
            {
                float speedRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, currentAbsSpeed);
                _currentFlowRegenRate = Mathf.Lerp(minFlowRegen, maxFlowRegen, speedRatio);
                currentFlowMeter += _currentFlowRegenRate * Time.deltaTime;
            }
            else 
            {
                _currentFlowRegenRate = 0f;
            }

            currentFlowMeter = Mathf.Clamp(currentFlowMeter, 0f, maxFlowMeter);
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

            // Using the new Toe Hit logic instead of the old raycast
            if (_toeHit.collider && !_waistHit.collider)
            {
                float armorCost = 50f; 
                if (currentFlowMeter >= armorCost)
                {
                    currentFlowMeter -= armorCost;
                    DisplayAction("STUMBLE PROTECTED!", Color.cyan);
                    if (cubeSprite) cubeSprite.color = Color.cyan;
                    StartCoroutine(VaultRoutine(_toeHit.collider, 0.2f, 0.1f)); 
                }
                else
                {
                    StartCoroutine(StumbleRoutine(_toeHit.collider));
                }
            }
        }

        private void HandleParkourHold()
        {
            if (isVaulting || !isGrounded || isStumbling) return; 
            if (parkourAction.action.IsPressed()) FireParkourAction(false);
        }
        #endregion

       #region 4. MULTI-SENSOR SYSTEM & LOGIC (NEW)
        private void CheckGrounded()
        {
            RaycastHit2D hit = Physics2D.Raycast(footPosition.position, Vector2.down, groundCheckDistance, groundLayer);
            isGrounded = hit.collider;
        }

        private void ScanFrontObstacles()
        {
            if (!headPosition || !chestPosition || !waistPosition || !shinPosition || !footPosition) return;

            Vector2 fireDirection = new Vector2(facingDirection, 0f);

            _headHit = Physics2D.BoxCast(headPosition.position, headBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);
            _chestHit = Physics2D.BoxCast(chestPosition.position, chestBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);
            _waistHit = Physics2D.BoxCast(waistPosition.position, waistBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);
            _shinHit = Physics2D.BoxCast(shinPosition.position, shinBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);
            _toeHit = Physics2D.BoxCast(footPosition.position, toeBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);

            _isCollidingFront = _headHit.collider || _chestHit.collider || _waistHit.collider || _shinHit.collider || _toeHit.collider;
        }
        
        private void CheckAirborneLedgeGrab()
        {
            // Only attempt a grab if we are falling, airborne, and not already busy
            if (isGrounded || isVaulting || rb.linearVelocity.y > 0f) return; 

            // If Chest or Waist hits a wall, but the Head doesn't, we found the lip.
            if ((_chestHit.collider || _waistHit.collider) && !_headHit.collider)
            {
                Collider2D wallCollider = _chestHit.collider ? _chestHit.collider : _waistHit.collider;
                Vector2 hitPoint = _chestHit.collider ? _chestHit.point : _waistHit.point;
                
                // Cast down from comfortably above the wall hit to find the exact top corner
                Vector2 rayOrigin = new Vector2(hitPoint.x + (facingDirection * 0.1f), hitPoint.y + 1.5f);
                RaycastHit2D downHit = Physics2D.Raycast(rayOrigin, Vector2.down, 2.5f, obstacleLayer);

                if (downHit.collider)
                {
                    // No waiting. The moment we detect this state while falling, snap!
                    SnapToLedge(hitPoint, downHit.point.y, wallCollider);
                }
            }
        }

        private void SnapToLedge(Vector2 wallHitPoint, float trueTopY, Collider2D ledgeCollider)
        {
            isHanging = true;
            _currentLedge = ledgeCollider;
            
            // Freeze physics
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;

            // Calculate snap coordinates
            float targetX = Mathf.Approximately(facingDirection, 1f) 
                ? wallHitPoint.x - (playerWidth / 2f) 
                : wallHitPoint.x + (playerWidth / 2f);
            
            // Align the top of the head exactly to the ledge
            float targetY = trueTopY - 2f;

            transform.position = new Vector2(targetX, targetY);
            
            DisplayAction("LEDGE GRAB!", Color.white);
            if (cubeSprite) cubeSprite.color = normalColor;
        }

        private void HandleHangingState()
        {
            float inputY = moveAction.action.ReadValue<Vector2>().y;

            if (inputY > 0.5f) // Press UP to Mantle
            {
                isHanging = false;
                DisplayAction("MANTLE!", Color.white);
                if (_currentLedge) StartCoroutine(MantleRoutine(_currentLedge));
            }
            else if (inputY < -0.5f) // Press DOWN to Drop
            {
                isHanging = false;
                rb.bodyType = RigidbodyType2D.Dynamic;
                _currentLedge = null;
                DisplayAction("DROP", Color.gray);
            }
        }

        private bool FireParkourAction(bool isTricking)
        {
            if (!_isCollidingFront) return false; 
            CalculateParkourMatrix(isTricking);
            return true; 
        }

        private void CalculateParkourMatrix(bool isTricking)
        {
            float inputY = moveAction.action.ReadValue<Vector2>().y;

            // 1. TALL WALL: Chest/Head hits.
            if (_chestHit.collider || _headHit.collider)
            {
                DisplayAction("Climb!", Color.white);
                StartCoroutine(MantleRoutine(_chestHit.collider ? _chestHit.collider : _headHit.collider));
                return;
            }

            // 2. VAULTABLE / TABLE: Waist hits, Chest is clear.
            if (_waistHit.collider && !_chestHit.collider)
            {
                float trueTopY;
                float obstacleDepth = CalculateObstacleDepth(_waistHit, out trueTopY);

                // Calculate exactly how high the player needs to lift their center to clear the lip
                float exactHeightToClear = (trueTopY - transform.position.y);

                if (obstacleDepth < 1.0f)
                {
                    float duration = isTricking ? 0.4f : 0.2f;
                    DisplayAction(isTricking ? "TRICK VAULT!" : "SPEED VAULT!", isTricking ? Color.cyan : Color.white);
                    StartCoroutine(VaultRoutine(_waistHit.collider, duration, exactHeightToClear));
                }
                else if (obstacleDepth < 3.0f)
                {
                    float duration = isTricking ? 0.7f : 0.4f;
                    DisplayAction(isTricking ? "TRICK KONG!" : "SPEED KONG!", isTricking ? Color.cyan : Color.white);
                    StartCoroutine(VaultRoutine(_waistHit.collider, duration, exactHeightToClear));
                }
                else
                {
                    DisplayAction("Mantle!", Color.white);
                    StartCoroutine(MantleRoutine(_waistHit.collider));
                }
                return;
            }

            // 3. LOW OBSTACLE: Shin or Toe hits, Waist is clear.
            if ((_shinHit.collider || _toeHit.collider) && !_waistHit.collider)
            {
                float duration = isTricking ? 0.4f : 0.2f;
                DisplayAction(isTricking ? "TRICK HOP!" : "SPEED STEP!", isTricking ? Color.cyan : Color.white);
                StartCoroutine(VaultRoutine(_shinHit.collider ? _shinHit.collider : _toeHit.collider, duration, 0.1f));
                return;
            }
        }
        private float CalculateObstacleDepth(RaycastHit2D forwardHit, out float trueTopY)
        {
            float maxSearchDistance = 4f; 
            float maxClimbHeight = 2.5f; // Maximum Y distance we are willing to scan upwards
            float scanStep = 0.1f;
            
            trueTopY = forwardHit.point.y; 
            bool foundTop = false;

            // --- 1. THE VERTICAL SCAN (Find the lip) ---
            // Start slightly pulled back from the wall so our forward casts don't spawn inside it
            Vector2 verticalScanOrigin = new Vector2(forwardHit.point.x - (facingDirection * 0.05f), forwardHit.point.y);

            for (float yOffset = scanStep; yOffset <= maxClimbHeight; yOffset += scanStep)
            {
                Vector2 stepOrigin = new Vector2(verticalScanOrigin.x, verticalScanOrigin.y + yOffset);
                
                // Cast forward a tiny bit to see if the wall is still there
                RaycastHit2D hit = Physics2D.Raycast(stepOrigin, new Vector2(facingDirection, 0f), 0.2f, obstacleLayer);

                if (!hit.collider)
                {
                    // The ray missed! We found the top edge.
                    // We set trueTopY slightly below the miss-line to ensure our downward casts hit the surface.
                    trueTopY = stepOrigin.y - (scanStep / 2f); 
                    foundTop = true;
                    break;
                }
            }

            if (!foundTop)
            {
                // The wall goes up infinitely (or higher than maxClimbHeight). 
                // Return massive depth so the system treats it as an unclimbable wall.
                return maxSearchDistance + 1f; 
            }

            // --- 2. THE HORIZONTAL SCAN (Find the depth) ---
            float depthStep = 0.25f;
            float startX = forwardHit.point.x + (facingDirection * 0.1f);

            for (float depth = 0f; depth <= maxSearchDistance; depth += depthStep)
            {
                // Position probe slightly above our newly found trueTopY
                Vector2 rayOrigin = new Vector2(startX + (facingDirection * depth), trueTopY + 0.2f);
                
                // Cast straight down
                RaycastHit2D downHit = Physics2D.Raycast(rayOrigin, Vector2.down, 0.5f, obstacleLayer);

                // If we hit nothing, or the floor drops out, we found the back edge
                if (!downHit.collider || downHit.point.y < trueTopY - 0.2f)
                {
                    return depth; 
                }
            }
            
            return maxSearchDistance; // Massive platform (Mantle)
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
            _capsuleCollider2D.offset = new Vector2(0, 1f);
            _capsuleCollider2D.size = new Vector2(1f, 2f);
            sprites.transform.localPosition = Vector3.zero;
            sprites.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            if (cubeSprite) cubeSprite.color = normalColor;
        }
        #endregion

        #region 5. COROUTINES
        private IEnumerator VaultRoutine(Collider2D obstacle, float duration, float extraHeight)
        {
            isVaulting = true;
            float entrySpeedX = rb.linearVelocity.x;
            rb.bodyType = RigidbodyType2D.Kinematic; 
            rb.linearVelocity = Vector2.zero;
            _capsuleCollider2D.enabled = false; 

            Vector2 startPos = transform.position;
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

            _capsuleCollider2D.enabled = true; 
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
            _capsuleCollider2D.enabled = false;

            float obstacleHeight = obstacle.bounds.size.y;
            float heightRatio = Mathf.InverseLerp(1f, 4f, obstacleHeight);
            float totalDuration = Mathf.Lerp(0.3f, 1f, heightRatio);

            Vector2 startPos = transform.position;
            float edgeX = Mathf.Approximately(facingDirection, 1) ? obstacle.bounds.min.x + (playerWidth / 2f) : obstacle.bounds.max.x - (playerWidth / 2f);
            float topY = obstacle.bounds.max.y + (playerWidth / 2f);

            float climbDuration = totalDuration * 0.7f;
            float pullDuration = totalDuration * 0.3f;

            float timePassed = 0f;
            while (timePassed < climbDuration)
            {
                timePassed += Time.deltaTime;
                float t = timePassed / climbDuration;
                float easeT = Mathf.Sin(t * Mathf.PI / 2f); 
                rb.MovePosition(new Vector2(startPos.x, Mathf.Lerp(startPos.y, topY, easeT)));
                yield return new WaitForFixedUpdate();
            }

            timePassed = 0f;
            Vector2 topPos = new Vector2(startPos.x, topY); 
        
            while (timePassed < pullDuration)
            {
                timePassed += Time.deltaTime;
                float t = timePassed / pullDuration;
                rb.MovePosition(new Vector2(Mathf.Lerp(topPos.x, edgeX, t), topY));
                yield return new WaitForFixedUpdate();
            }

            _capsuleCollider2D.enabled = true;
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
            _capsuleCollider2D.enabled = false;

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

            _capsuleCollider2D.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic; 
            isVaulting = false;
            _lockedFacingDirection = facingDirection;

            float speedRatio = Mathf.Abs(rb.linearVelocity.x) / topSpeed;
            _stumbleTimer = Mathf.Clamp(5f * speedRatio, 1f, 5f);
        }
        #endregion

        #region 6. UI, VISUALS & GIZMOS
        private void HandleUI()
        {
            if (flowMeterFill)
            {
                float targetFill = currentFlowMeter / maxFlowMeter;
                flowMeterFill.fillAmount = Mathf.Lerp(flowMeterFill.fillAmount, targetFill, Time.deltaTime * uiLerpSpeed);
            }

            if (speedKmhText)
            {
                float trueSpeed = Mathf.Abs(rb.linearVelocity.x);
                speedKmhText.text = (trueSpeed*3).ToString("F0") + " Km/h"; 
                float colorRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, trueSpeed);
                speedKmhText.color = Color.Lerp(Color.white, Color.cyan, colorRatio);
            }

            if (speedUsText && showUnits)
            {
                float trueSpeed = Mathf.Abs(rb.linearVelocity.x);
                speedUsText.text = (trueSpeed).ToString("F0") + " U/s";
                float colorRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, trueSpeed);
                speedKmhText.color = Color.Lerp(Color.white, Color.cyan, colorRatio);
            }
        
            if (flowCapacityText) flowCapacityText.text = currentFlowMeter.ToString("F0") + " / " + maxFlowMeter.ToString("F0");
        
            if (flowRateText)
            {
                if (_currentFlowRegenRate > 0.01f) 
                {
                    flowRateText.gameObject.SetActive(true);
                    flowRateText.text = "+" + _currentFlowRegenRate.ToString("F1") + " / sec";
                }
                else
                {
                    flowRateText.gameObject.SetActive(false);
                }
            }
        }

        private void ApplyDownVisuals(Color stateColor)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
            _capsuleCollider2D.offset = new Vector2(0f, 0.5f);
            _capsuleCollider2D.size = new Vector2(0.5f, 1f);
            sprites.transform.localPosition = new Vector3(1f, 0f, 0f);
            sprites.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (cubeSprite) cubeSprite.color = stateColor;
        }

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

        // --- CORRECTED: IN-GAME RAYCAST VISUALIZER ---
        private void OnDrawGizmos()
        {
            if (!headPosition || !chestPosition || !waistPosition || !shinPosition || !footPosition) return;

            // BoxCast sweeps a box over a distance. To draw the full area being checked, 
            // we stretch the Gizmo cube's X size by the cast distance, and push its center forward by half the distance.
            Vector3 offset = new Vector3(facingDirection * (sensorCastDistance / 2f), 0f, 0f);

            // Helper function to draw the sweep
            void DrawSweep(Transform t, Vector2 size, RaycastHit2D hit)
            {
                Gizmos.color = hit.collider ? Color.red : Color.green;
                Vector3 sweepSize = new Vector3(size.x + sensorCastDistance, size.y, 0f);
                Gizmos.DrawWireCube(t.position + offset, sweepSize);
            }

            DrawSweep(headPosition, headBoxSize, _headHit);
            DrawSweep(chestPosition, chestBoxSize, _chestHit);
            DrawSweep(waistPosition, waistBoxSize, _waistHit);
            DrawSweep(shinPosition, shinBoxSize, _shinHit);
            DrawSweep(footPosition, toeBoxSize, _toeHit);
        }
        #endregion
    }
}