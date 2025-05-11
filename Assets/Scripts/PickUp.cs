using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickUp : MonoBehaviour
{
    private float smoothSpeed = 20f;
    private float elevationHeight = 2f;
    private float animationDelayBetweenCubes = 0.025f; // Delay between cube animations

    [SerializeField] private GameObject pickupSymbolPrefab; // Assign the pickup symbol prefab in inspector

    private Camera mainCamera;
    private bool isPickedUp = false;
    private Vector3 offset;
    private Vector3 targetPosition;
    private Vector3 startPosition;
    private bool canPlace = false;

    private Vector2Int currentGridPos;
    private List<Transform> childCubes = new List<Transform>();
    private List<Vector2Int> childRelativePositions = new List<Vector2Int>();
    private List<int> childBlockIDs = new List<int>();
    private List<Animator> cubeAnimators = new List<Animator>();
    private List<Renderer> cubeOutlines = new List<Renderer>(); // Outline renderers for material changes
    private Dictionary<Collider, Animator> colliderToAnimator = new Dictionary<Collider, Animator>();

    // Materials for valid/invalid placement
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;
    private List<Material> originalMaterials = new List<Material>();

    // Animation trigger names
    private const string PICKUP_TRIGGER = "PickUpClick";
    private const string PLACE_TRIGGER = "PickUpRelease";
    private const string NOT_PLACE_TRIGGER = "PickUpCantRelease";
    private const string HOVER_ENTER_TRIGGER = "MoveHoverEnter";
    private const string CANT_PLACE_TRIGGER = "PickUpCantPlace";

    // Hover tracking
    private bool isHovering = false;
    private bool hoverEnterAnimationPlayed = false;
    private List<bool> cubeCanPlaceStatus = new List<bool>();

    private void Start()
    {
        mainCamera = Camera.main;
        startPosition = transform.position;
        targetPosition = transform.position;
        currentGridPos = GridManager.Instance.WorldToGrid(transform.position);

        // Find all child cube holders with their colliders, animators, and outlines
        CollectCubeComponents();

        // Initialize placement status list
        for (int i = 0; i < childCubes.Count; i++)
        {
            cubeCanPlaceStatus.Add(true);
        }

        // Add the pickup symbol to the top left cube
        AddMovementSymbol();
    }

    private void AddMovementSymbol()
    {
        if (pickupSymbolPrefab == null || childCubes.Count == 0)
            return;

        // Find the "top left" cube (minimum x, maximum z)
        Transform topLeftCube = FindTopLeftCube();

        if (topLeftCube != null)
        {
            // Find the visual child of the cube to parent our symbol to
            Transform cubeVisual = FindCubeVisual(topLeftCube);

            if (cubeVisual != null)
            {
                // Instantiate the symbol as a child of the cube visual
                GameObject symbol = Instantiate(pickupSymbolPrefab, cubeVisual);
                symbol.transform.localPosition = Vector3.zero;
                symbol.transform.localRotation = Quaternion.identity;
                symbol.transform.localScale = Vector3.one;
            }
        }
    }

    private Transform FindTopLeftCube()
    {
        if (childCubes.Count == 0)
            return null;

        // Start with the first cube as the candidate
        Transform topLeftCube = childCubes[0];
        Vector2Int topLeftPos = childRelativePositions[0];

        // Find the cube with the minimum x and maximum z (top left in grid coordinates)
        for (int i = 1; i < childCubes.Count; i++)
        {
            Vector2Int currentPos = childRelativePositions[i];

            // If this cube has a lower x value, or the same x but higher z, it's more "top left"
            if (currentPos.y > topLeftPos.y ||
                (currentPos.y == topLeftPos.y && currentPos.x < topLeftPos.x))
            {
                topLeftCube = childCubes[i];
                topLeftPos = currentPos;
            }
        }

        return topLeftCube;
    }

    private Transform FindCubeVisual(Transform cube)
    {
        // First check if the cube has a child that could be the visual
        if (cube.childCount > 0)
        {
            // Usually the first child is the visual container
            return cube.GetChild(0);
        }

        // If we can't find a visual child, return the cube itself
        return cube;
    }

    private void CollectCubeComponents()
    {
        // Process all direct children of the parent (these are the individual cubes)
        foreach (Transform cube in transform)
        {
            // Find the cube holder in each cube that has the collider and animator
            Collider holderCollider = null;
            Animator holderAnimator = null;
            Renderer outlineRenderer = null;

            // First check if the cube itself has what we need
            holderCollider = cube.GetComponent<Collider>();
            holderAnimator = cube.GetComponent<Animator>();

            // If we found the necessary components, add them to our tracking lists
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

                outlineRenderer = cube.GetChild(0).GetChild(0).GetComponent<Renderer>();
                // Add outline renderer and store original material
                if (outlineRenderer != null)
                {
                    cubeOutlines.Add(outlineRenderer);
                    originalMaterials.Add(outlineRenderer.material);
                }
                else
                {
                    Debug.LogWarning("Could not find outline renderer for cube: " + cube.name);
                    // Add null to maintain indexing with other lists
                    cubeOutlines.Add(null);
                    originalMaterials.Add(null);
                }
            }
            else
            {
                Debug.LogWarning("Could not find collider or animator for cube: " + cube.name);
            }
        }

        // Debug.Log("Found " + childCubes.Count + " cubes with " + cubeAnimators.Count + " animators and " + cubeOutlines.Count + " outlines");
    }

    // Add this as a new field at the class level
    [Tooltip("Total time for all cube animations, regardless of cube count")]
    [SerializeField] private float totalAnimationTime = 0.1f;

    // Replace the TriggerAnimationWithDelay method with this:
    private IEnumerator TriggerAnimationWithDelay(string triggerName)
    {
        int cubeCount = cubeAnimators.Count;
        float calculatedDelay = 0f;

        // Calculate the delay between cubes based on total time
        if (cubeCount > 1)
        {
            calculatedDelay = totalAnimationTime / (cubeCount - 1);
        }

        // First, initialize all animations at frame 0 and pause them
        for (int i = 0; i < cubeCount; i++)
        {
            if (cubeAnimators[i] != null)
            {
                cubeAnimators[i].Play(triggerName, 0, 0f);
                cubeAnimators[i].speed = 0; // Pause animation at first frame
            }
        }

        // Then unpause each animation with calculated delay
        for (int i = 0; i < cubeCount; i++)
        {
            if (cubeAnimators[i] != null)
            {
                cubeAnimators[i].speed = 1; // Resume animation

                // Skip delay after the last cube
                if (i < cubeCount - 1)
                {
                    yield return new WaitForSeconds(calculatedDelay);
                }
            }
        }
    }

    // Trigger animation for specific cubes
    private IEnumerator TriggerAnimationForSpecificCubes(string triggerName, List<int> indices)
    {
        foreach (int i in indices)
        {
            if (i < cubeAnimators.Count && cubeAnimators[i] != null)
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

        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit hit;
        bool isHoveringThisFrame = false;

        // Handle hover animation
        if (Physics.Raycast(ray, out hit))
        {
            // Check if we hit one of our cube holders' colliders
            if (colliderToAnimator.ContainsKey(hit.collider))
            {
                isHoveringThisFrame = true;

                // We're hovering over one of our cubes
                if (!isHovering && !isPickedUp)
                {
                    // First time hovering over this shape
                    isHovering = true;

                    // Only play enter animation if we haven't played it already in this hover session
                    if (!hoverEnterAnimationPlayed)
                    {
                        hoverEnterAnimationPlayed = true;
                        StartCoroutine(TriggerAnimationWithDelay(HOVER_ENTER_TRIGGER));
                    }
                }

                // Handle selection
                if (!isPickedUp && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    PickUpObject(hit.point);
                }
            }
        }

        // Only consider the mouse to have left if it's not hovering over any part of the shape
        // AND we're not dragging
        if (!isHoveringThisFrame && isHovering && !isPickedUp)
        {
            isHovering = false;
            hoverEnterAnimationPlayed = false; // Reset so we can play it again next time
        }

        // Handle releasing the object
        if (isPickedUp && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            PlaceObject();
        }

        // Movement and placement check
        if (isPickedUp)
        {
            HandleMovement(mousePosition);
            CheckPlacement();
        }
    }

    private void PickUpObject(Vector3 hitPoint)
    {
        isPickedUp = true;
        startPosition = transform.position;

        // Calculate offset for dragging
        Vector3 hitPointOnPlane = new Vector3(hitPoint.x, transform.position.y, hitPoint.z);
        Vector3 objectPositionOnPlane = new Vector3(transform.position.x, transform.position.y, transform.position.z);
        offset = objectPositionOnPlane - hitPointOnPlane;

        // Remove from grid while picked up
        RemoveFromGrid();

        // Elevate the object INSTANTLY
        Vector3 newPosition = transform.position;
        newPosition.y = elevationHeight;
        transform.position = newPosition;
        targetPosition = new Vector3(transform.position.x, elevationHeight, transform.position.z);

        // Play pickup animation
        StartCoroutine(TriggerAnimationWithDelay(PICKUP_TRIGGER));
    }

    private void PlaceObject()
    {
        isPickedUp = false;

        // Check if placement is valid
        if (canPlace)
        {
            targetPosition = new Vector3(targetPosition.x, 0f, targetPosition.z);
            transform.position = targetPosition;
            UpdateGridState(currentGridPos);
            StartCoroutine(TriggerAnimationWithDelay(PLACE_TRIGGER));
        }
        else
        {
            // Return to start position
            targetPosition = startPosition;
            transform.position = new Vector3(startPosition.x, 0f, startPosition.z);
            currentGridPos = GridManager.Instance.WorldToGrid(startPosition);
            UpdateGridState(currentGridPos);
            StartCoroutine(TriggerAnimationWithDelay(NOT_PLACE_TRIGGER));
        }

        // Reset outline materials
        ResetOutlineMaterials();
    }

    private void HandleMovement(Vector2 mousePosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        float distance;
        Plane plane = new Plane(Vector3.up, new Vector3(0, elevationHeight, 0));

        if (plane.Raycast(ray, out distance))
        {
            Vector3 pointOnPlane = ray.GetPoint(distance);

            Vector3 rawPosition = new Vector3(
                pointOnPlane.x + offset.x,
                elevationHeight,  // Keep elevated
                pointOnPlane.z + offset.z
            );

            Vector3 snappedPosition = SnapToGrid(rawPosition);
            currentGridPos = GridManager.Instance.WorldToGrid(snappedPosition);

            // Update target position (keeping elevation)
            targetPosition = new Vector3(snappedPosition.x, elevationHeight, snappedPosition.z);
        }

        // Smooth movement (X and Z only, Y is already set instantly)
        Vector3 newPosition = transform.position;
        newPosition.x = Mathf.Lerp(newPosition.x, targetPosition.x, smoothSpeed * Time.deltaTime);
        newPosition.z = Mathf.Lerp(newPosition.z, targetPosition.z, smoothSpeed * Time.deltaTime);
        transform.position = newPosition;
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x),
            position.y,
            Mathf.Round(position.z)
        );
    }

    private void CheckPlacement()
    {
        canPlace = true;
        List<int> invalidCubeIndices = new List<int>();

        // Check each child cube for valid placement
        for (int i = 0; i < childCubes.Count; i++)
        {
            Vector2Int childGridPos = currentGridPos + childRelativePositions[i];
            bool isValidPosition = IsValidPlacement(childGridPos);

            // Track if the placement status changed
            bool statusChanged = (cubeCanPlaceStatus[i] != isValidPosition);
            cubeCanPlaceStatus[i] = isValidPosition;

            // Update outline material based on placement validity
            if (i < cubeOutlines.Count && cubeOutlines[i] != null)
            {
                cubeOutlines[i].material = isValidPosition ? validPlacementMaterial : invalidPlacementMaterial;
            }

            // If any position is invalid, the entire shape can't be placed
            if (!isValidPosition)
            {
                canPlace = false;
                invalidCubeIndices.Add(i);

                // Play PickUpCantPlace animation when status changes to invalid
                if (statusChanged)
                {
                    cubeAnimators[i].Play(CANT_PLACE_TRIGGER, 0, 0f);
                }
            }
        }

        // If we have invalid cubes but haven't played the animation yet, play it
        if (invalidCubeIndices.Count > 0)
        {
            // Animation is triggered per cube when status changes
        }
    }

    private bool IsValidPlacement(Vector2Int gridPos)
    {
        // Check if position is within grid bounds
        if (!GridManager.Instance.IsInBounds(gridPos))
            return false;

        // Check if position is empty
        if (!GridManager.Instance.IsPositionEmpty(gridPos))
            return false;

        return true;
    }

    private void RemoveFromGrid()
    {
        // Clear current positions from grid
        for (int i = 0; i < childCubes.Count; i++)
        {
            Vector2Int currentChildPos = GridManager.Instance.WorldToGrid(startPosition) + childRelativePositions[i];
            GridManager.Instance.UpdateGridPosition(currentChildPos, new Vector2Int(-1, -1), 0); // Clear old position
        }
    }

    private void UpdateGridState(Vector2Int targetGridPos)
    {
        // Set new positions in grid
        for (int i = 0; i < childCubes.Count; i++)
        {
            Vector2Int targetChildPos = targetGridPos + childRelativePositions[i];
            GridManager.Instance.UpdateGridPosition(new Vector2Int(-1, -1), targetChildPos, childBlockIDs[i]);
        }
    }

    private void ResetOutlineMaterials()
    {
        // Reset all outline materials to their originals
        for (int i = 0; i < cubeOutlines.Count; i++)
        {
            if (cubeOutlines[i] != null && i < originalMaterials.Count && originalMaterials[i] != null)
            {
                cubeOutlines[i].material = originalMaterials[i];
            }
        }

        // Reset can place status
        for (int i = 0; i < cubeCanPlaceStatus.Count; i++)
        {
            cubeCanPlaceStatus[i] = true;
        }
    }
}