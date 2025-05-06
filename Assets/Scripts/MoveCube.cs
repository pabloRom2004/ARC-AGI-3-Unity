using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CubeMovement : MonoBehaviour
{
    [SerializeField] private float smoothSpeed = 10f;

    private Camera mainCamera;
    private bool isDragging = false;
    private Vector3 offset;
    private Vector3 targetPosition;

    private Vector2Int currentGridPos;
    private List<Transform> childCubes = new List<Transform>();
    private List<Vector2Int> childRelativePositions = new List<Vector2Int>();
    private List<int> childBlockIDs = new List<int>();

    private void Start()
    {
        mainCamera = Camera.main;
        targetPosition = transform.position;
        currentGridPos = GridManager.Instance.WorldToGrid(transform.position);

        // Add this cube if it has a renderer
        Renderer ownRenderer = GetComponent<Renderer>();
        if (ownRenderer != null)
        {
            childCubes.Add(transform);
            childRelativePositions.Add(new Vector2Int(0, 0)); // Relative to itself is 0,0
            childBlockIDs.Add(GridManager.Instance.GetColorID(ownRenderer.material));
        }

        // Find all child cubes
        foreach (Transform child in transform)
        {
            Renderer childRenderer = child.GetComponent<Renderer>();
            if (childRenderer != null && child.GetComponent<Collider>() != null)
            {
                childCubes.Add(child);

                // Calculate relative grid position
                Vector3 relativePos = child.position - transform.position;
                Vector2Int relativeGridPos = new Vector2Int(
                    Mathf.RoundToInt(relativePos.x),
                    Mathf.RoundToInt(relativePos.z) // Map world Z to grid Y
                );
                childRelativePositions.Add(relativeGridPos);

                // Get block ID
                int blockID = GridManager.Instance.GetColorID(childRenderer.material);
                childBlockIDs.Add(blockID);
            }
        }
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

    private void Update()
    {
        // Get mouse position from Input System
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        // Handle selection
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            RaycastHit hit;
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                // Check if we hit this object or any of its children
                Transform hitTransform = hit.transform;
                bool isOurCube = (hitTransform == transform);

                foreach (Transform child in childCubes)
                {
                    if (hitTransform == child)
                    {
                        isOurCube = true;
                        break;
                    }
                }

                if (isOurCube)
                {
                    isDragging = true;

                    Vector3 hitPointOnPlane = new Vector3(hit.point.x, 0, hit.point.z);
                    Vector3 objectPositionOnPlane = new Vector3(transform.position.x, 0, transform.position.z);
                    offset = objectPositionOnPlane - hitPointOnPlane;
                }
            }
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

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
}