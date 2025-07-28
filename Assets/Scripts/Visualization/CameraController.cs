using UnityEngine;
using System.Collections;

namespace Archetype.Visualization
{
    /// <summary>
    /// Camera controller for 3D neural network visualization - MUST inherit from MonoBehaviour
    /// Provides smooth navigation, focus controls, and cinematic transitions
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float smoothTime = 0.3f;

        [Header("Limits")]
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 100f;
        [SerializeField] private float minVerticalAngle = -80f;
        [SerializeField] private float maxVerticalAngle = 80f;

        [Header("Input Settings")]
        [SerializeField] private bool invertY = false;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float scrollSensitivity = 2f;
        [SerializeField] private KeyCode focusKey = KeyCode.F;
        [SerializeField] private KeyCode resetKey = KeyCode.R;

        [Header("Auto Focus")]
        [SerializeField] private bool enableAutoFocus = true;
        [SerializeField] private float autoFocusDistance = 15f;
        [SerializeField] private float focusTransitionTime = 1f;

        [Header("Cinematic")]
        [SerializeField] private bool enableCinematicMode = false;
        [SerializeField] private float cinematicSpeed = 0.5f;
        [SerializeField] private AnimationCurve cinematicCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Internal state
        private Camera controlledCamera;
        private Transform cameraTransform;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float targetDistance;
        private Vector3 currentVelocity;
        private Vector3 rotationVelocity;

        // Orbit controls
        private Vector3 orbitCenter = Vector3.zero;
        private float currentDistance = 10f;
        private float horizontalAngle = 0f;
        private float verticalAngle = 0f;

        // Input state
        private bool isMouseDragging = false;
        private bool isRightMouseDragging = false;
        private Vector3 lastMousePosition;
        private bool isTransitioning = false;

        // Focus targets
        private Transform currentFocusTarget;
        private Bounds currentFocusBounds;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeCamera();
        }

        private void Start()
        {
            SetupDefaultPosition();
        }

        private void Update()
        {
            if (!isTransitioning)
            {
                HandleInput();
                HandleKeyboardMovement();
            }

            UpdateCameraPosition();
            HandleCinematicMode();
        }

        private void LateUpdate()
        {
            // Ensure camera constraints are applied
            ApplyConstraints();
        }

        #endregion

        #region Initialization

        private void InitializeCamera()
        {
            controlledCamera = GetComponent<Camera>();
            if (controlledCamera == null)
            {
                controlledCamera = Camera.main;
            }

            if (controlledCamera == null)
            {
                Debug.LogError("❌ CameraController: No camera found!");
                return;
            }

            cameraTransform = controlledCamera.transform;
            targetPosition = cameraTransform.position;
            targetRotation = cameraTransform.rotation;
            currentDistance = Vector3.Distance(cameraTransform.position, orbitCenter);
            targetDistance = currentDistance;

            Debug.Log("📹 Camera Controller initialized");
        }

        private void SetupDefaultPosition()
        {
            // Position camera for optimal neural network viewing
            orbitCenter = Vector3.zero;
            currentDistance = 15f;
            targetDistance = currentDistance;
            horizontalAngle = 45f;
            verticalAngle = 30f;

            UpdateOrbitPosition();
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            HandleMouseInput();
            HandleScrollInput();
            HandleKeyInput();
        }

        private void HandleMouseInput()
        {
            // Left mouse button - orbit around target
            if (Input.GetMouseButtonDown(0))
            {
                isMouseDragging = true;
                lastMousePosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isMouseDragging = false;
            }

            // Right mouse button - pan camera
            if (Input.GetMouseButtonDown(1))
            {
                isRightMouseDragging = true;
                lastMousePosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                isRightMouseDragging = false;
            }

            // Mouse drag handling
            if (isMouseDragging)
            {
                HandleOrbitDrag();
            }
            else if (isRightMouseDragging)
            {
                HandlePanDrag();
            }
        }

        private void HandleOrbitDrag()
        {
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            
            horizontalAngle += mouseDelta.x * mouseSensitivity * Time.deltaTime * rotationSpeed;
            verticalAngle -= mouseDelta.y * mouseSensitivity * Time.deltaTime * rotationSpeed * (invertY ? -1 : 1);
            
            // Clamp vertical angle
            verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
            
            lastMousePosition = Input.mousePosition;
            UpdateOrbitPosition();
        }

        private void HandlePanDrag()
        {
            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            
            // Convert screen space mouse movement to world space
            Vector3 worldDelta = cameraTransform.right * (-mouseDelta.x * 0.01f) + 
                                cameraTransform.up * (-mouseDelta.y * 0.01f);
            
            orbitCenter += worldDelta * currentDistance * 0.001f;
            
            lastMousePosition = Input.mousePosition;
            UpdateOrbitPosition();
        }

        private void HandleScrollInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float zoomDelta = scroll * zoomSpeed * scrollSensitivity;
                targetDistance = Mathf.Clamp(targetDistance - zoomDelta, minDistance, maxDistance);
            }
        }

        private void HandleKeyInput()
        {
            // Focus on selected object
            if (Input.GetKeyDown(focusKey))
            {
                FocusOnSelection();
            }

            // Reset camera
            if (Input.GetKeyDown(resetKey))
            {
                ResetCamera();
            }

            // Toggle cinematic mode
            if (Input.GetKeyDown(KeyCode.C))
            {
                ToggleCinematicMode();
            }
        }

        private void HandleKeyboardMovement()
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                // Fast movement when holding shift
                float currentMoveSpeed = moveSpeed * 2f;
                
                if (Input.GetKey(KeyCode.W))
                    orbitCenter += cameraTransform.forward * currentMoveSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.S))
                    orbitCenter -= cameraTransform.forward * currentMoveSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.A))
                    orbitCenter -= cameraTransform.right * currentMoveSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.D))
                    orbitCenter += cameraTransform.right * currentMoveSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.Q))
                    orbitCenter -= Vector3.up * currentMoveSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.E))
                    orbitCenter += Vector3.up * currentMoveSpeed * Time.deltaTime;
                
                UpdateOrbitPosition();
            }
        }

        #endregion

        #region Camera Updates

        private void UpdateCameraPosition()
        {
            // Smooth distance transition
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime / smoothTime);
            
            // Update orbit position
            UpdateOrbitPosition();
            
            // Smooth position and rotation transitions
            cameraTransform.position = Vector3.SmoothDamp(cameraTransform.position, targetPosition, 
                ref currentVelocity, smoothTime);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, 
                Time.deltaTime / smoothTime);
        }

        private void UpdateOrbitPosition()
        {
            // Calculate position based on spherical coordinates
            float radianHorizontal = horizontalAngle * Mathf.Deg2Rad;
            float radianVertical = verticalAngle * Mathf.Deg2Rad;
            
            Vector3 offset = new Vector3(
                Mathf.Sin(radianHorizontal) * Mathf.Cos(radianVertical),
                Mathf.Sin(radianVertical),
                Mathf.Cos(radianHorizontal) * Mathf.Cos(radianVertical)
            ) * currentDistance;
            
            targetPosition = orbitCenter + offset;
            targetRotation = Quaternion.LookRotation(orbitCenter - targetPosition, Vector3.up);
        }

        private void ApplyConstraints()
        {
            // Ensure distance limits
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            
            // Ensure vertical angle limits
            verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        #endregion

        #region Focus and Navigation

        public void FocusOnObject(Transform target, bool immediate = false)
        {
            if (target == null) return;

            currentFocusTarget = target;
            
            // Calculate optimal viewing distance based on object bounds
            Bounds bounds = CalculateObjectBounds(target);
            currentFocusBounds = bounds;
            
            float optimalDistance = CalculateOptimalDistance(bounds);
            
            if (immediate)
            {
                orbitCenter = bounds.center;
                targetDistance = optimalDistance;
                currentDistance = optimalDistance;
                UpdateOrbitPosition();
            }
            else
            {
                StartCoroutine(SmoothFocusTransition(bounds.center, optimalDistance));
            }
        }

        public void FocusOnSelection()
        {
            // Try to find selected neural network component
            var selectedNode = FindSelectedNode();
            if (selectedNode != null)
            {
                FocusOnObject(selectedNode);
                return;
            }

            // Focus on active neural network
            var visualizer = FindObjectOfType<NeuralNetworkVisualizer>();
            if (visualizer != null)
            {
                FocusOnObject(visualizer.transform);
            }
        }

        public void ResetCamera()
        {
            StartCoroutine(SmoothResetTransition());
        }

        private Transform FindSelectedNode()
        {
            // Raycast from mouse position to find selected node
            Ray ray = controlledCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var nodeViz = hit.collider.GetComponent<NodeVisualization>();
                if (nodeViz != null)
                {
                    return nodeViz.transform;
                }
            }
            return null;
        }

        private Bounds CalculateObjectBounds(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(target.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            
            return bounds;
        }

        private float CalculateOptimalDistance(Bounds bounds)
        {
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float fov = controlledCamera.fieldOfView * Mathf.Deg2Rad;
            float distance = (size / 2f) / Mathf.Tan(fov / 2f);
            
            return Mathf.Clamp(distance * 1.5f, minDistance, maxDistance);
        }

        #endregion

        #region Smooth Transitions

        private IEnumerator SmoothFocusTransition(Vector3 targetCenter, float targetDist)
        {
            isTransitioning = true;
            
            Vector3 startCenter = orbitCenter;
            float startDistance = targetDistance;
            float elapsed = 0f;
            
            while (elapsed < focusTransitionTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / focusTransitionTime;
                
                // Use smooth curve for transition
                t = Mathf.SmoothStep(0f, 1f, t);
                
                orbitCenter = Vector3.Lerp(startCenter, targetCenter, t);
                targetDistance = Mathf.Lerp(startDistance, targetDist, t);
                
                UpdateOrbitPosition();
                yield return null;
            }
            
            orbitCenter = targetCenter;
            targetDistance = targetDist;
            isTransitioning = false;
        }

        private IEnumerator SmoothResetTransition()
        {
            isTransitioning = true;
            
            Vector3 startCenter = orbitCenter;
            float startDistance = targetDistance;
            float startHorizontal = horizontalAngle;
            float startVertical = verticalAngle;
            
            Vector3 resetCenter = Vector3.zero;
            float resetDistance = 15f;
            float resetHorizontal = 45f;
            float resetVertical = 30f;
            
            float elapsed = 0f;
            float transitionTime = focusTransitionTime;
            
            while (elapsed < transitionTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionTime;
                t = Mathf.SmoothStep(0f, 1f, t);
                
                orbitCenter = Vector3.Lerp(startCenter, resetCenter, t);
                targetDistance = Mathf.Lerp(startDistance, resetDistance, t);
                horizontalAngle = Mathf.LerpAngle(startHorizontal, resetHorizontal, t);
                verticalAngle = Mathf.LerpAngle(startVertical, resetVertical, t);
                
                UpdateOrbitPosition();
                yield return null;
            }
            
            orbitCenter = resetCenter;
            targetDistance = resetDistance;
            horizontalAngle = resetHorizontal;
            verticalAngle = resetVertical;
            isTransitioning = false;
        }

        #endregion

        #region Cinematic Mode

        private void HandleCinematicMode()
        {
            if (!enableCinematicMode) return;
            
            // Automatic camera rotation for cinematic effect
            horizontalAngle += cinematicSpeed * Time.deltaTime * 10f;
            
            // Optional vertical oscillation
            float verticalOscillation = Mathf.Sin(Time.time * cinematicSpeed) * 5f;
            verticalAngle = 20f + verticalOscillation;
            
            UpdateOrbitPosition();
        }

        public void ToggleCinematicMode()
        {
            enableCinematicMode = !enableCinematicMode;
            Debug.Log($"🎬 Cinematic mode: {(enableCinematicMode ? "ON" : "OFF")}");
        }

        public void SetCinematicSpeed(float speed)
        {
            cinematicSpeed = speed;
        }

        #endregion

        #region Auto Focus

        private void UpdateAutoFocus()
        {
            if (!enableAutoFocus) return;
            
            // Auto-focus on the center of visible neural networks
            var visualizers = FindObjectsOfType<NeuralNetworkVisualizer>();
            if (visualizers.Length > 0)
            {
                Vector3 center = Vector3.zero;
                foreach (var visualizer in visualizers)
                {
                    center += visualizer.transform.position;
                }
                center /= visualizers.Length;
                
                // Gradually move orbit center towards the calculated center
                orbitCenter = Vector3.Lerp(orbitCenter, center, Time.deltaTime * 0.5f);
            }
        }

        #endregion

        #region Public Interface

        public void SetMoveSpeed(float speed)
        {
            moveSpeed = speed;
        }

        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = speed;
        }

        public void SetZoomSpeed(float speed)
        {
            zoomSpeed = speed;
        }

        public void SetMouseSensitivity(float sensitivity)
        {
            mouseSensitivity = sensitivity;
        }

        public void SetOrbitCenter(Vector3 center)
        {
            orbitCenter = center;
            UpdateOrbitPosition();
        }

        public void SetDistance(float distance)
        {
            targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        public void SetAngles(float horizontal, float vertical)
        {
            horizontalAngle = horizontal;
            verticalAngle = Mathf.Clamp(vertical, minVerticalAngle, maxVerticalAngle);
            UpdateOrbitPosition();
        }

        public Vector3 GetOrbitCenter()
        {
            return orbitCenter;
        }

        public float GetDistance()
        {
            return currentDistance;
        }

        public Vector2 GetAngles()
        {
            return new Vector2(horizontalAngle, verticalAngle);
        }

        public bool IsTransitioning()
        {
            return isTransitioning;
        }

        public void EnableControls(bool enabled)
        {
            this.enabled = enabled;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            
            // Draw orbit center
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(orbitCenter, 0.5f);
            
            // Draw camera target position
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPosition, 0.3f);
            
            // Draw focus bounds if available
            if (currentFocusTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(currentFocusBounds.center, currentFocusBounds.size);
            }
            
            // Draw distance constraints
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(orbitCenter, minDistance);
            Gizmos.DrawWireSphere(orbitCenter, maxDistance);
        }

        #endregion
    }
}
                