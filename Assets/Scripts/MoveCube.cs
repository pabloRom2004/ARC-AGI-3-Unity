using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CubeMovement : MonoBehaviour
{
    private float smoothSpeed = 20f;
    private float animationDelayBetweenCubes = 0.025f; // Delay between cube animations

    private Camera mainCamera;
    private bool isDragging = false;
    private Vector3 offset;
    private Vector3 targetPosition;

    private Vector2Int currentGridPos;
    private List<Transform> childCubes = new List<Transform>();
    private List<Vector2Int> childRelativePositions = new List<Vector2Int>();
    private List<int> childBlockIDs = new List<int>();
    private List<Animator> cubeAnimators = new List<Animator>(); // List of all cube animators
    private Dictionary<Collider, Animator> colliderToAnimator = new Dictionary<Collider, Animator>(); // Map colliders to animators

    // Animation trigger names
    private const string HOVER_ENTER_TRIGGER = "MoveHoverEnter";
    private const string HOVER_EXIT_TRIGGER = "MoveHoverExit";
    private const string CLICK_TRIGGER = "MoveClick";
    private const string RELEASE_TRIGGER = "MoveRelease";

    private bool isHovering = false; // Track if we're currently hovering over any part of the shape
    private bool hoverEnterAnimationPlayed = false; // Track if hover enter animation has been played for this hover session

    private void Start()
    {
        mainCamera = Camera.main;
        targetPosition = transform.position;
        currentGridPos = GridManager.Instance.WorldToGrid(transform.position);

        // Find all child cube holders with their colliders and animators
        CollectCubeComponents();
    }

    private void CollectCubeComponents()
    {
        // Process all direct children of the parent (these are the individual cubes)
        foreach (Transform cube in transform)
        {
            // Find the cube holder in each cube that has the collider and animator
            Collider holderCollider = null;
            Animator holderAnimator = null;

            // First check if the cube itself has what we need
            holderCollider = cube.GetComponent<Collider>();
            holderAnimator = cube.GetComponent<Animator>();

            // If not, look for a "Cube Holder" child
            if (holderCollider == null || holderAnimator == null)
            {
                // Find the holder among the children
                foreach (Transform holder in cube)
                {
                    // Try to find the holder with both collider and animator
                    Collider collider = holder.GetComponent<Collider>();
                    Animator animator = holder.GetComponent<Animator>();

                    if (collider != null)
                        holderCollider = collider;

                    if (animator != null)
                        holderAnimator = animator;

                    // If both found, we can stop looking
                    if (holderCollider != null && holderAnimator != null)
                        break;
                }
            }

            // If we found both components, add them to our tracking lists
            if (holderCollider != null && holderAnimator != null)
            {
                childCubes.Add(cube);

                // Calculate relative grid position
                Vector3 relativePos = cube.position - transform.position;
                Vector2Int relativeGridPos = new Vector2Int(
                    Mathf.RoundToInt(relativePos.x),
                    Mathf.RoundToInt(relativePos.z) // Map world Z to grid Y
                );
                childRelativePositions.Add(relativeGridPos);

                // Get block ID from renderer - we need to find the visual child
                Renderer cubeRenderer = cube.GetComponentInChildren<Renderer>();
                if (cubeRenderer != null)
                {
                    int blockID = GridManager.Instance.GetColorID(cubeRenderer.material);
                    childBlockIDs.Add(blockID);
                }
                else
                {
                    // If no renderer found, use default ID
                    childBlockIDs.Add(0);
                    Debug.LogWarning("No renderer found for cube: " + cube.name);
                }

                // Add the animator to our list
                cubeAnimators.Add(holderAnimator);

                // Map the collider to its animator
                colliderToAnimator.Add(holderCollider, holderAnimator);
            }
            else
            {
                Debug.LogWarning("Could not find collider or animator for cube: " + cube.name);
            }
        }

        // Debug.Log("Found " + childCubes.Count + " cubes with " + cubeAnimators.Count + " animators");
    }

    // Trigger animation with delay between cubes
    private IEnumerator TriggerAnimationWithDelay(string triggerName)
    {
        for (int i = 0; i < cubeAnimators.Count; i++)
        {
            if (cubeAnimators[i] != null)
            {
                cubeAnimators[i].Play(triggerName, 0, 0f);
                yield return new WaitForSeconds(animationDelayBetweenCubes);
            }
        }
    }

    private void Update()
    {
        // Get mouse position from Input System
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        // Handle selection and hover
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        bool isHoveringThisFrame = false;

        if (Physics.Raycast(ray, out hit))
        {
            // Check if we hit one of our cube holders' colliders
            if (colliderToAnimator.ContainsKey(hit.collider))
            {
                isHoveringThisFrame = true;

                // We're hovering over one of our cubes
                if (!isHovering)
                {
                    // First time hovering over this shape
                    isHovering = true;

                    // Only play enter animation if we haven't played it already in this hover session
                    // and we're not dragging
                    if (!hoverEnterAnimationPlayed && !isDragging)
                    {
                        hoverEnterAnimationPlayed = true;
                        StartCoroutine(TriggerAnimationWithDelay(HOVER_ENTER_TRIGGER));
                    }
                }

                // Check for mouse press on our cubes
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    isDragging = true;
                    StartCoroutine(TriggerAnimationWithDelay(CLICK_TRIGGER));

                    Vector3 hitPointOnPlane = new Vector3(hit.point.x, 1, hit.point.z);
                    Vector3 objectPositionOnPlane = new Vector3(transform.position.x, 1, transform.position.z);
                    offset = objectPositionOnPlane - hitPointOnPlane;
                }
            }
        }

        // Only consider the mouse to have left if it's not hovering over any part of the shape
        // AND we're not dragging
        if (!isHoveringThisFrame && isHovering && !isDragging)
        {
            isHovering = false;
            hoverEnterAnimationPlayed = false; // Reset so we can play it again next time
            //StartCoroutine(TriggerAnimationWithDelay(HOVER_EXIT_TRIGGER));
        }

        // Check for mouse release
        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;
            StartCoroutine(TriggerAnimationWithDelay(RELEASE_TRIGGER));
            
            // We're still hovering, so make sure we don't play enter animation again
            if (isHoveringThisFrame)
            {
                isHovering = true;
                hoverEnterAnimationPlayed = true;
            }
        }

        // Movement handling
        HandleMovement(mousePosition);
    }

    private void HandleMovement(Vector2 mousePosition)
    {
        // Movement
        if (isDragging)
        {
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            float distance;
            Plane plane = new Plane(Vector3.up, Vector3.zero);

            if (plane.Raycast(ray, out distance))
            {
                Vector3 pointOnPlane = ray.GetPoint(distance);

                Vector3 rawPosition = new Vector3(
                    pointOnPlane.x + offset.x,
                    transform.position.y,
                    pointOnPlane.z + offset.z
                );

                Vector3 snappedPosition = SnapToGrid(rawPosition);
                Vector2Int targetGridPos = GridManager.Instance.WorldToGrid(snappedPosition);

                // Check if move is valid
                if (IsMoveValid(currentGridPos, targetGridPos))
                {
                    // Update grid state
                    UpdateGridState(targetGridPos);

                    // Update tracking variables
                    currentGridPos = targetGridPos;
                    targetPosition = snappedPosition;
                }
                // Try alternative moves if direct move is invalid
                else
                {
                    // Determine which axis has the larger difference
                    bool horizontalPriority = Mathf.Abs(targetGridPos.x - currentGridPos.x) >=
                                             Mathf.Abs(targetGridPos.y - currentGridPos.y);

                    // Create arrays for the primary and secondary directions to try
                    Vector2Int[] directionsToTry = new Vector2Int[2];

                    // Set up the directions to try based on priority
                    if (horizontalPriority)
                    {
                        // Try horizontal first
                        directionsToTry[0] = new Vector2Int(
                            (targetGridPos.x > currentGridPos.x) ? currentGridPos.x + 1 :
                            (targetGridPos.x < currentGridPos.x) ? currentGridPos.x - 1 :
                            currentGridPos.x,
                            currentGridPos.y
                        );

                        // Then try vertical
                        directionsToTry[1] = new Vector2Int(
                            currentGridPos.x,
                            (targetGridPos.y > currentGridPos.y) ? currentGridPos.y + 1 :
                            (targetGridPos.y < currentGridPos.y) ? currentGridPos.y - 1 :
                            currentGridPos.y
                        );
                    }
                    else
                    {
                        // Try vertical first
                        directionsToTry[0] = new Vector2Int(
                            currentGridPos.x,
                            (targetGridPos.y > currentGridPos.y) ? currentGridPos.y + 1 :
                            (targetGridPos.y < currentGridPos.y) ? currentGridPos.y - 1 :
                            currentGridPos.y
                        );

                        // Then try horizontal
                        directionsToTry[1] = new Vector2Int(
                            (targetGridPos.x > currentGridPos.x) ? currentGridPos.x + 1 :
                            (targetGridPos.x < currentGridPos.x) ? currentGridPos.x - 1 :
                            currentGridPos.x,
                            currentGridPos.y
                        );
                    }

                    // Try each direction in order
                    for (int i = 0; i < directionsToTry.Length; i++)
                    {
                        Vector2Int moveTarget = directionsToTry[i];

                        // Skip if move doesn't actually change position
                        if (moveTarget == currentGridPos)
                            continue;

                        // Check if move is valid
                        if (IsMoveValid(currentGridPos, moveTarget))
                        {
                            // Update grid state
                            UpdateGridState(moveTarget);

                            // Update tracking variables
                            currentGridPos = moveTarget;
                            targetPosition = new Vector3(moveTarget.x, transform.position.y, moveTarget.y);

                            // Break the loop since we found a valid move
                            break;
                        }
                    }
                }
            }
        }

        // Smooth movement
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x),
            position.y,
            Mathf.Round(position.z)
        );
    }

    private bool IsMoveValid(Vector2Int from, Vector2Int to)
    {
        // Only allow movement of 1 grid cell at a time (Manhattan distance)
        int distance = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);

        // Only allow orthogonal movement (not diagonal)
        bool isOrthogonal = (to.x == from.x || to.y == from.y);

        // Return true if move is only 1 cell away and orthogonal
        return distance <= 1 && isOrthogonal && CanMoveTo(to);
    }

    private bool CanMoveTo(Vector2Int targetGridPos)
    {
        // Check each child position
        for (int i = 0; i < childCubes.Count; i++)
        {
            Vector2Int childTargetPos = targetGridPos + childRelativePositions[i];

            // Check if position is valid
            if (!GridManager.Instance.IsInBounds(childTargetPos))
                return false;

            // Check if position is empty or occupied by this group
            if (!GridManager.Instance.IsPositionEmpty(childTargetPos))
            {
                // Check if the position is currently occupied by one of our own cubes
                bool isOccupiedByUs = false;
                for (int j = 0; j < childCubes.Count; j++)
                {
                    Vector2Int currentChildPos = currentGridPos + childRelativePositions[j];
                    if (currentChildPos == childTargetPos)
                    {
                        isOccupiedByUs = true;
                        break;
                    }
                }

                if (!isOccupiedByUs)
                    return false;
            }
        }

        return true;
    }

    private void UpdateGridState(Vector2Int targetGridPos)
    {
        // Clear current positions
        for (int i = 0; i < childCubes.Count; i++)
        {
            Vector2Int currentChildPos = currentGridPos + childRelativePositions[i];
            GridManager.Instance.UpdateGridPosition(currentChildPos, new Vector2Int(-1, -1), 0); // Clear old position
        }

        // Set new positions
        for (int i = 0; i < childCubes.Count; i++)
        {
            Vector2Int targetChildPos = targetGridPos + childRelativePositions[i];
            GridManager.Instance.UpdateGridPosition(new Vector2Int(-1, -1), targetChildPos, childBlockIDs[i]);
        }
    }
}