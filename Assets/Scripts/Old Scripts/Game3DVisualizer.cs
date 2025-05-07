using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game3DVisualizer : MonoBehaviour
{
    [Header("Prefabs and Materials")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject groundPrefab;
    [SerializeField] private GameObject lowerGroundPrefab;
    [SerializeField] private Material[] materials;  // Array of 9 materials, index 0 = blue (for ID 1)

    [Header("Grid Settings")]
    [SerializeField] private float cubeSize = 1f;
    [SerializeField] private float spacing = 0f;
    [SerializeField] private float maxPlayAreaSize = 18f; // 9 units in each direction from center

    [Header("Floor Settings")]
    [SerializeField] private float floorRimSize = 1.5f; // Rim size for top floor
    [SerializeField] private float lowerFloorExtraSize = 0.5f; // Extra size for lower floor
    [SerializeField] private float floorHeight = -0.4f; // Y position for floor
    [SerializeField] private float lowerFloorHeight = -0.5f; // Y position for lower floor

    private GameObject levelParent;
    private GameObject cubesParent;
    private GameObject floorParent;
    private GameObject groundParent;

    private Dictionary<string, GameObject> cubeObjects = new Dictionary<string, GameObject>();
    private GameStateManager gameStateManager;
    private bool isVisualizerActive = true;

    // Add dictionary to track interaction parent objects
    private Dictionary<int, GameObject> interactionParents = new Dictionary<int, GameObject>();

    // Scale and position info for grid calculations
    private Vector3 levelScale = Vector3.one;
    private Vector3 levelPosition = Vector3.zero;
    private int gridRows = 0;

    private void OnEnable()
    {
        // Only initialize if we haven't already
        if (!isVisualizerActive)
        {
            StartCoroutine(InitializeWhenReady());
            isVisualizerActive = true;
        }
    }

    private IEnumerator InitializeWhenReady()
    {
        // Wait for one frame to let other objects initialize
        yield return null;

        // Find the GameStateManager
        gameStateManager = FindFirstObjectByType<GameStateManager>();
        if (gameStateManager != null)
        {
            // No longer subscribe to grid updates - only visualize once at startup

            // Wait for the grid to be initialized
            int attempts = 0;
            while (gameStateManager.GetCurrentGrid() == null && attempts < 10)
            {
                yield return new WaitForSeconds(0.1f);
                attempts++;
            }

            // Try to visualize the current grid if it exists
            List<List<int>> currentGrid = gameStateManager.GetCurrentGrid();
            if (currentGrid != null && currentGrid.Count > 0)
            {
                VisualizeGrid(currentGrid);
            }
        }
    }

    private void OnDisable()
    {
        // Clear the visualization when disabled
        ClearVisualization();

        isVisualizerActive = false;
    }

    // Called at startup to visualize the initial grid
    public void VisualizeGrid(List<List<int>> grid)
    {
        if (!isVisualizerActive) return;

        // Clear any existing visualization
        ClearVisualization();

        // Create visualization based on new grid data
        CreateLevel(grid);
    }

    // NEW METHODS: Grid position conversion utilities

    // Convert world position to grid position
    public Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        if (levelParent == null) return Vector2Int.zero;

        // Adjust for level's scale and position
        Vector3 localPos = worldPosition;

        // Apply inverse transform to get local position in level space
        if (levelParent != null)
        {
            // Convert to local space
            localPos -= levelPosition;
            localPos.x /= levelScale.x;
            localPos.z /= levelScale.z;
        }

        // Calculate grid position
        int col = Mathf.RoundToInt(localPos.x / (cubeSize + spacing));
        int row = gridRows - 1 - Mathf.RoundToInt(localPos.z / (cubeSize + spacing));

        return new Vector2Int(col, row);
    }

    // Convert grid position to world position
    public Vector3 GridToWorldPosition(Vector2Int gridPosition)
    {
        float x = gridPosition.x * (cubeSize + spacing);
        float z = (gridRows - 1 - gridPosition.y) * (cubeSize + spacing);
        float y = cubeSize / 2f; // Position it above the floor

        Vector3 worldPos = new Vector3(x, y, z);

        // Apply level parent's transform
        if (levelParent != null)
        {
            worldPos.x *= levelScale.x;
            worldPos.z *= levelScale.z;
            worldPos += levelPosition;
        }

        return worldPos;
    }

    // Method to update a specific cell in the visualization
    // NOTE: This won't be called by GameStateManager anymore, only directly if needed
    public void UpdateCell(int row, int col, int newValue)
    {
        if (!isVisualizerActive) return;

        string cubeKey = $"Cube_{row}_{col}";

        // Check if we have this cube in our dictionary
        if (cubeObjects.TryGetValue(cubeKey, out GameObject existingCube))
        {
            // If new value is 0, destroy the cube
            if (newValue == 0)
            {
                Destroy(existingCube);
                cubeObjects.Remove(cubeKey);
            }
            else
            {
                // Update the material of the existing cube
                MeshRenderer renderer = existingCube.GetComponentInChildren<MeshRenderer>();
                if (renderer != null && newValue >= 1 && newValue <= 9 && newValue - 1 < materials.Length)
                {
                    renderer.material = materials[newValue - 1];
                }
            }
        }
        else if (newValue > 0)
        {
            // We need to create a new cube
            CreateCubeAt(row, col, newValue);
        }
    }

    // Clear all visualized elements
    private void ClearVisualization()
    {
        // Clear any existing level
        if (levelParent != null)
        {
            Destroy(levelParent);
            levelParent = null;
        }

        // Clear our dictionaries
        cubeObjects.Clear();
        interactionParents.Clear();
    }

    private void CreateParentObjects()
    {
        // Create a parent object for the entire level
        levelParent = new GameObject("Level");

        // Create separate parent objects for organization
        cubesParent = new GameObject("Cubes");
        floorParent = new GameObject("Floor");
        groundParent = new GameObject("Ground");
        levelParent.transform.parent = transform;
        cubesParent.transform.parent = levelParent.transform;
        floorParent.transform.parent = levelParent.transform;
        groundParent.transform.parent = levelParent.transform;
    }

    private void CreateLevel(List<List<int>> grid)
    {
        gridRows = grid.Count;
        if (gridRows == 0) return;

        int cols = grid[0].Count;

        // Create separate parent objects
        CreateParentObjects();

        // First create cubes (since those should be created first)
        CreateCubeGrid(grid);

        // Then create floor grid
        CreateFloorGrid(gridRows, cols, floorParent);

        // Add this line to create interaction parents after creating cubes
        CreateInteractionParents();

        // Scale and position the entire level
        ScaleAndPositionLevel(levelParent, gridRows, cols);

        // Create ground last (after scaling) to ensure proper size
        CreateGround(gridRows, cols, groundParent);
    }

    // Add method to create parent objects for interactions
    private void CreateInteractionParents()
    {
        if (gameStateManager == null) return;

        List<InteractionData> interactions = gameStateManager.GetInteractionData();
        if (interactions == null || interactions.Count == 0) return;

        // Process each interaction group
        for (int i = 0; i < interactions.Count; i++)
        {
            InteractionData interaction = interactions[i];

            // Create a parent object for this interaction group
            GameObject parentObject = new GameObject($"Interaction_{i}_{interaction.scriptType}");
            parentObject.transform.parent = cubesParent.transform;

            // List to collect all child cubes for this interaction
            List<GameObject> childCubes = new List<GameObject>();
            Vector3 centerPosition = Vector3.zero;

            // Find all the cubes for this interaction
            foreach (Vector2Int blockPos in interaction.blocks)
            {
                int row = blockPos.y; // Note: Vector2Int stores (x,y) but we want (row,col)
                int col = blockPos.x;

                string cubeKey = $"Cube_{row}_{col}";
                if (cubeObjects.TryGetValue(cubeKey, out GameObject cube))
                {
                    childCubes.Add(cube);
                    centerPosition += cube.transform.position;
                }
                else
                {
                    Debug.LogWarning($"Cube not found for interaction at position ({row}, {col})");
                }
            }

            // If we found any cubes, create the parent
            if (childCubes.Count > 0)
            {
                // Calculate center position
                centerPosition /= childCubes.Count;
                parentObject.transform.position = centerPosition;

                // Set cubes as children of the parent object
                foreach (GameObject cube in childCubes)
                {
                    // Create an additional child GameObject for the cube visuals
                    GameObject visualCube = Instantiate(cube, cube.transform.position, cube.transform.rotation);
                    visualCube.name = "Coloured Cube";

                    // Move the components from the original cube to the visual cube
                    MeshRenderer meshRenderer = cube.GetComponentInChildren<MeshRenderer>();
                    Material cubeMaterial = null;
                    if (meshRenderer != null)
                    {
                        cubeMaterial = meshRenderer.material;
                    }

                    // Set the material on the visual cube
                    MeshRenderer visualRenderer = visualCube.GetComponentInChildren<MeshRenderer>();
                    if (visualRenderer != null && cubeMaterial != null)
                    {
                        visualRenderer.material = cubeMaterial;
                    }

                    // Remove the mesh renderer from the original cube (it's just a position holder now)
                    if (meshRenderer != null)
                    {
                        Destroy(meshRenderer);
                    }

                    // Make the visual cube a child of the original cube
                    visualCube.transform.SetParent(cube.transform);
                    visualCube.transform.localPosition = Vector3.zero;

                    // Set the original cube's parent to the interaction parent
                    cube.transform.SetParent(parentObject.transform);
                }

                // Add the script component based on the interaction type name
                System.Type scriptType = System.Type.GetType(interaction.scriptType);
                if (scriptType != null && scriptType.IsSubclassOf(typeof(MonoBehaviour)))
                {
                    // Add the component using the script type name
                    parentObject.AddComponent(scriptType);

                    // For Move types, still add the direction info to the name for debugging
                    if (interaction.scriptType == "Move")
                    {
                        parentObject.name += $"_Up{interaction.canMoveUp}_Down{interaction.canMoveDown}_Left{interaction.canMoveLeft}_Right{interaction.canMoveRight}";
                    }
                }
                else
                {
                    Debug.LogWarning($"Script type '{interaction.scriptType}' not found or not a MonoBehaviour. Make sure the script class name matches exactly.");
                }

                // Store the parent in our dictionary
                interactionParents[i] = parentObject;

                Debug.Log($"Created interaction parent '{parentObject.name}' with {childCubes.Count} cubes");
            }
            else
            {
                // If no cubes were found, destroy the parent
                Destroy(parentObject);
                Debug.LogWarning($"No cubes found for interaction {i}, parent not created");
            }
        }
    }

    private void CreateFloorGrid(int rows, int cols, GameObject floorParent)
    {
        // Create floor tiles for every position in the grid
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                // Calculate position for floor tile
                float x = col * (cubeSize + spacing);

                // Invert row position to start from top instead of bottom
                float z = (rows - 1 - row) * (cubeSize + spacing);

                Vector3 position = new Vector3(x, 0, z);

                // Instantiate floor tile
                GameObject floor = Instantiate(floorPrefab, position, Quaternion.identity, floorParent.transform);
                floor.name = $"Floor_{row}_{col}";

                // Ensure floor is properly sized
                floor.transform.localScale = new Vector3(cubeSize, 2f, cubeSize);
            }
        }
    }

    private void CreateGround(int rows, int cols, GameObject groundParent)
    {
        // Calculate grid dimensions
        float gridWidth = cols * (cubeSize + spacing) - spacing;
        float gridHeight = rows * (cubeSize + spacing) - spacing;

        // Create top floor (with consistent rim size)
        Vector3 topFloorScale = new Vector3(
            gridWidth,
            0.2f,
            gridHeight
        );

        // Create bottom floor (slightly larger)
        Vector3 bottomFloorScale = new Vector3(
            gridWidth,
            0.2f,
            gridHeight
        );

        // Calculate center position of the grid after scaling
        Vector3 gridCenter = levelParent.transform.position + new Vector3(0, 0, 0);

        // Instantiate the top floor (independent of the level's transform)
        GameObject topFloor = Instantiate(groundPrefab, Vector3.zero, Quaternion.identity, groundParent.transform);
        topFloor.name = "TopFloor";
        topFloor.transform.position = new Vector3(0, floorHeight, 0);
        topFloor.transform.localScale = topFloorScale;

        // Instantiate the bottom floor (independent of the level's transform)
        GameObject bottomFloor = Instantiate(lowerGroundPrefab, Vector3.zero, Quaternion.identity, groundParent.transform);
        bottomFloor.name = "BottomFloor";
        bottomFloor.transform.position = new Vector3(0, lowerFloorHeight, 0);
        bottomFloor.transform.localScale = bottomFloorScale;
        // Make the ground objects ignore the level parent's scale
        topFloor.transform.SetParent(null);
        bottomFloor.transform.SetParent(null);
        topFloor.transform.localScale += new Vector3(floorRimSize, 0, floorRimSize);
        bottomFloor.transform.localScale += new Vector3(floorRimSize + lowerFloorExtraSize, 0, floorRimSize + lowerFloorExtraSize);

        // Reparent to maintain hierarchy, but now they have their own transforms
        topFloor.transform.SetParent(groundParent.transform, true);
        bottomFloor.transform.SetParent(groundParent.transform, true);
    }

    private void CreateCubeGrid(List<List<int>> grid)
    {
        int rows = grid.Count;
        int cols = grid[0].Count;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int id = grid[row][col];

                // Skip empty spaces (ID 0)
                if (id == 0) continue;

                CreateCubeAt(row, col, id);
            }
        }
    }

    private void CreateCubeAt(int row, int col, int id)
    {
        if (cubesParent == null)
        {
            Debug.LogError("Cubes parent is null!");
            return;
        }

        // Calculate position (cubes are one unit above the floor)
        float x = col * (cubeSize + spacing);
        float y = cubeSize / 2f; // Position it above the floor

        // Invert row position to start from top instead of bottom
        int rows = 0;
        if (gameStateManager != null)
        {
            rows = gameStateManager.GetGridDimensions().y;
        }
        float z = (rows - 1 - row) * (cubeSize + spacing);

        Vector3 position = new Vector3(x, y, z);

        // Instantiate cube
        GameObject cube = Instantiate(cubePrefab, position, Quaternion.identity, cubesParent.transform);
        string cubeName = $"Cube_{row}_{col}_ID{id}";
        cube.name = cubeName;

        // Add to our dictionary for easy lookup
        cubeObjects[$"Cube_{row}_{col}"] = cube;

        // Ensure cube is properly sized
        cube.transform.localScale = new Vector3(cubeSize, cubeSize, cubeSize);

        // Assign material based on ID (ID 1 uses materials[0], etc.)
        if (id >= 1 && id <= 9 && id - 1 < materials.Length)
        {
            MeshRenderer renderer = cube.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = materials[id - 1];
            }
        }
    }

    private void ScaleAndPositionLevel(GameObject levelParent, int rows, int cols)
    {
        // Calculate current grid dimensions
        float gridWidth = cols * (cubeSize + spacing) - spacing;
        float gridHeight = rows * (cubeSize + spacing) - spacing;

        // Calculate true center position in local space
        Vector3 gridCenter = new Vector3(
            (cols - 1) / 2f * (cubeSize + spacing),
            0f,
            (rows - 1) / 2f * (cubeSize + spacing)
        );

        // Determine which dimension is longer
        float longestDimension = Mathf.Max(gridWidth, gridHeight);

        // Calculate scale factor to fit within play area
        float scaleFactor = maxPlayAreaSize / longestDimension;

        // Apply scale to the level parent
        levelParent.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        levelScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

        // Calculate offset to center the grid at origin
        Vector3 centerOffset = new Vector3(
            -gridCenter.x * scaleFactor,
            0f,
            -gridCenter.z * scaleFactor
        );

        // Apply position offset
        levelParent.transform.position = centerOffset;
        levelPosition = centerOffset;
    }
}