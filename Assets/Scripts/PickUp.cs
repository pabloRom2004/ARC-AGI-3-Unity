using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickUp : MonoBehaviour
{
    private float smoothSpeed = 20f;
    private float elevationHeight = 2f;
    private float animationDelayBetweenCubes = 0.025f; // Delay between cube animations

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

    private void Start()
    {
        mainCamera = Camera.main;
        startPosition = transform.position;
        targetPosition = transform.position;
        currentGridPos = GridManager.Instance.WorldToGrid(transform.position);

        // Find all child cube holders with their colliders, animators, and outlines
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

        // Handle selection
        if (!isPickedUp && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                // Check if we hit one of our cube holders' colliders
                if (colliderToAnimator.ContainsKey(hit.collider))
                {
                    PickUpObject(hit.point);
                }
            }
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

        // Elevate the object
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
            // Place at current position and update grid
            targetPosition = new Vector3(targetPosition.x, 0f, targetPosition.z);
            transform.position = targetPosition;
            UpdateGridState(currentGridPos);
            StartCoroutine(TriggerAnimationWithDelay(PLACE_TRIGGER));
        }
        else
        {
            // Return to start position
            targetPosition = startPosition;
            transform.position = targetPosition;
            currentGridPos = GridManager.Instance.WorldToGrid(startPosition);
            UpdateGridState(currentGridPos);
            StartCoroutine(TriggerAnimationWithDelay(PLACE_TRIGGER));
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

    private void CheckPlacement()
    {
        print(canPlace);
        canPlace = true;

        // Check each child cube for valid placement
        for (int i = 0; i < childCubes.Count; i++)
        {
            Vector2Int childGridPos = currentGridPos + childRelativePositions[i];
            bool isValidPosition = IsValidPlacement(childGridPos);

            // Update outline material based on placement validity
            if (i < cubeOutlines.Count && cubeOutlines[i] != null)
            {
                cubeOutlines[i].material = isValidPosition ? validPlacementMaterial : invalidPlacementMaterial;
            }

            // If any position is invalid, the entire shape can't be placed
            if (!isValidPosition)
            {
                canPlace = false;
            }
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
    }
}