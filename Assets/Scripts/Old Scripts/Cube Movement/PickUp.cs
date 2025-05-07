using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PickUp : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float pickUpHeight = 2.0f;
    [SerializeField] private float dropAnimationSpeed = 5.0f;
    [SerializeField] private LayerMask floorMask;

    // References
    private GameStateManager gameStateManager;
    private Camera mainCamera;

    // State tracking
    private bool isDragging = false;
    private Vector3 originalPosition;
    private List<Vector2Int> blockGridPositions = new List<Vector2Int>();
    private int blockID = 0;

    // Animation state
    private bool isAnimating = false;
    private Vector3 targetPosition;

    // Input reference
    private Mouse mouse;
    private bool wasPressed = false;

    private void Awake()
    {
        // Get the mouse device
        mouse = Mouse.current;
        if (mouse == null)
        {
            Debug.LogError("Mouse device not found!");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Find the game state manager
        gameStateManager = FindFirstObjectByType<GameStateManager>();
        if (gameStateManager == null)
        {
            Debug.LogError("PickUp script could not find GameStateManager!");
            enabled = false;
            return;
        }

        // Find main camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("PickUp script could not find main camera!");
            enabled = false;
            return;
        }

        // Extract grid positions from the name of each child cube
        ExtractGridPositions();
    }

    private void ExtractGridPositions()
    {
        blockGridPositions.Clear();

        // Get all child cubes
        foreach (Transform child in transform)
        {
            // Extract grid position from the cube name, format: Cube_Row_Col_ID{n}
            string cubeName = child.name;
            if (cubeName.StartsWith("Cube_"))
            {
                string[] parts = cubeName.Split('_');
                if (parts.Length >= 3)
                {
                    // Try to parse row and column
                    if (int.TryParse(parts[1], out int row) &&
                        int.TryParse(parts[2].Split('I')[0], out int col))
                    {
                        blockGridPositions.Add(new Vector2Int(col, row));

                        // Get the ID from the cube name if not already set
                        if (blockID == 0 && parts.Length >= 4)
                        {
                            string idStr = parts[3];
                            if (idStr.StartsWith("ID"))
                            {
                                int.TryParse(idStr.Substring(2), out blockID);
                            }
                        }
                    }
                }
            }
        }

        if (blockGridPositions.Count == 0)
        {
            Debug.LogWarning("No valid grid positions found in children of " + gameObject.name);
        }
        else
        {
            Debug.Log($"Found {blockGridPositions.Count} blocks with ID {blockID} in pickup group");
        }
    }

    private void Update()
    {
        // Safety check for mouse
        if (mouse == null) return;

        if (isAnimating)
        {
            // Handle drop animation
            float step = dropAnimationSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isAnimating = false;
            }
            return;
        }

        // Check for mouse button state
        bool isPressed = mouse.leftButton.isPressed;
        bool justPressed = isPressed && !wasPressed;
        bool justReleased = !isPressed && wasPressed;
        wasPressed = isPressed;

        print(justPressed);
        if (isDragging)
        {
            // Move object with mouse on the elevated plane
            Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            Plane dragPlane = new Plane(Vector3.up, new Vector3(0, pickUpHeight, 0));

            if (dragPlane.Raycast(ray, out float distance))
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                transform.position = new Vector3(hitPoint.x, pickUpHeight, hitPoint.z);
            }

            // Check for release
            if (justReleased)
            {
                DropBlock();
            }
        }
        else
        {
            // Check for mouse down on any of the cubes
            if (justPressed)
            {
                Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

                // We need to check if the ray hits any of our children
                foreach (Transform child in transform)
                {
                    Collider childCollider = child.GetComponent<Collider>();
                    if (childCollider != null && childCollider.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        PickUpBlock();
                        break;
                    }
                }
            }
        }
    }

    private void PickUpBlock()
    {
        if (isDragging || isAnimating) return;

        isDragging = true;
        originalPosition = transform.position;

        // Clear the original positions in the grid
        if (gameStateManager != null)
        {
            // Cache the original grid positions for validation later
            ClearOriginalPositionsInGrid();
        }

        // Move the block up to show it's being picked up
        transform.position = new Vector3(transform.position.x, pickUpHeight, transform.position.z);
    }

    private void DropBlock()
    {
        if (!isDragging) return;
        isDragging = false;

        // Calculate the new grid positions
        List<Vector2Int> newGridPositions = CalculateNewGridPositions();

        // Validate the drop position
        if (IsValidDropPosition(newGridPositions))
        {
            // Update the grid with the new positions
            UpdateGridWithNewPositions(newGridPositions);

            // Find the target world position for the drop animation
            targetPosition = CalculateTargetWorldPosition(newGridPositions);
            isAnimating = true;
        }
        else
        {
            // Not a valid position, return to original
            targetPosition = originalPosition;
            isAnimating = true;

            // Restore the original grid positions
            UpdateGridWithNewPositions(blockGridPositions);
        }
    }

    private void ClearOriginalPositionsInGrid()
    {
        if (gameStateManager == null) return;

        foreach (Vector2Int pos in blockGridPositions)
        {
            gameStateManager.UpdateCell(pos.y, pos.x, 0);
        }
    }

    private List<Vector2Int> CalculateNewGridPositions()
    {
        List<Vector2Int> newPositions = new List<Vector2Int>();

        // Perform a raycast to find where we're dropping
        Ray ray = new Ray(transform.position, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, floorMask))
        {
            // Convert world position to grid position
            Vector3 hitWorldPos = hit.point;

            // Get the floor's local position relative to the scene
            Game3DVisualizer visualizer = FindFirstObjectByType<Game3DVisualizer>();
            if (visualizer != null)
            {
                // Find the floor we hit
                string floorName = hit.collider.gameObject.name;
                if (floorName.StartsWith("Floor_"))
                {
                    string[] parts = floorName.Split('_');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int row) &&
                        int.TryParse(parts[2], out int col))
                    {
                        // Calculate offsets for all blocks based on this reference floor
                        foreach (Vector2Int originalPos in blockGridPositions)
                        {
                            // Find the offset of this block from the first block in the group
                            Vector2Int firstBlock = blockGridPositions[0];
                            Vector2Int offset = originalPos - firstBlock;

                            // Apply offset to the new position
                            Vector2Int newPos = new Vector2Int(col + offset.x, row + offset.y);
                            newPositions.Add(newPos);
                        }
                    }
                }
                else
                {
                    // We didn't hit a floor tile, use original positions
                    newPositions = new List<Vector2Int>(blockGridPositions);
                }
            }
        }

        // If we couldn't calculate new positions, use original
        if (newPositions.Count == 0)
        {
            newPositions = new List<Vector2Int>(blockGridPositions);
        }

        return newPositions;
    }

    private bool IsValidDropPosition(List<Vector2Int> newPositions)
    {
        if (gameStateManager == null) return false;

        // Get the current grid and its dimensions
        List<List<int>> currentGrid = gameStateManager.GetCurrentGrid();
        Vector2Int gridDimensions = gameStateManager.GetGridDimensions();

        foreach (Vector2Int pos in newPositions)
        {
            // Check if the position is within grid bounds
            if (pos.y < 0 || pos.y >= gridDimensions.y || pos.x < 0 || pos.x >= gridDimensions.x)
            {
                return false;
            }

            // Check if the cell is empty (0) or is part of our original position
            int cellValue = currentGrid[pos.y][pos.x];
            if (cellValue != 0 && !blockGridPositions.Contains(pos))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateGridWithNewPositions(List<Vector2Int> newPositions)
    {
        if (gameStateManager == null) return;

        // Update the grid with the new block positions
        foreach (Vector2Int pos in newPositions)
        {
            gameStateManager.UpdateCell(pos.y, pos.x, blockID);
        }

        // Update our stored positions
        blockGridPositions = newPositions;
    }

    private Vector3 CalculateTargetWorldPosition(List<Vector2Int> gridPositions)
    {
        // Find the center position of the new grid positions
        Vector3 center = Vector3.zero;
        foreach (Transform child in transform)
        {
            center += child.position;
        }
        center /= transform.childCount;

        // Maintain the same Y position as the original
        return new Vector3(center.x, originalPosition.y, center.z);
    }
}