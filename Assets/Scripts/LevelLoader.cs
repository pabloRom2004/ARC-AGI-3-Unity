using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

public class LevelLoader : MonoBehaviour
{
    [Header("Prefabs and Materials")]
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject groundPrefab;
    [SerializeField] private Material[] materials;  // Array of 9 materials, index 0 = blue (for ID 1)

    [Header("Grid Settings")]
    [SerializeField] private float cubeSize = 1f;
    [SerializeField] private float spacing = 0f;
    [SerializeField] private float maxPlayAreaSize = 18f; // 9 units in each direction from center

    [Header("Debug Settings")]
    [SerializeField] private bool useDebugGrid = false;
    [SerializeField] private int debugRows = 8;
    [SerializeField] private int debugCols = 8;

    private string levelDataPath;
    private List<List<int>> gridData;

    void Start()
    {
        levelDataPath = Path.Combine(Application.streamingAssetsPath, "levelTest.json");
        LoadLevel();
    }

    private void LoadLevel()
    {
        if (useDebugGrid)
        {
            // Create a debug grid with custom dimensions
            CreateDebugGrid(debugRows, debugCols);
        }
        else
        {
            // Load from JSON as before
            LoadFromJson();
        }
    }

    private void LoadFromJson()
    {
        // Check if the file exists
        if (!File.Exists(levelDataPath))
        {
            Debug.LogError("Level file not found: " + levelDataPath);
            return;
        }

        // Read the JSON file
        string jsonText = File.ReadAllText(levelDataPath);
        Debug.Log("Successfully read JSON file, parsing...");

        // Parse the grid data manually
        gridData = ExtractInputGrid(jsonText);

        if (gridData == null || gridData.Count == 0)
        {
            Debug.LogError("Failed to parse level grid data");
            return;
        }

        Debug.Log($"Successfully parsed grid with dimensions: {gridData.Count}x{gridData[0].Count}");

        // Create the complete level
        CreateLevel(gridData);
    }

    private void CreateDebugGrid(int rows, int cols)
    {
        Debug.Log($"Creating debug grid with dimensions: {rows}x{cols}");

        // Create a test pattern grid
        List<List<int>> debugGrid = new List<List<int>>();

        for (int r = 0; r < rows; r++)
        {
            List<int> row = new List<int>();
            for (int c = 0; c < cols; c++)
            {
                // Create a simple pattern:
                // - Borders have ID 1 (blue)
                // - Checkerboard pattern with IDs 2-4 inside
                // - Some empty spaces (ID 0) scattered around

                if (r == 0 || r == rows - 1 || c == 0 || c == cols - 1)
                {
                    // Border
                    row.Add(1);
                }
                else if ((r + c) % 3 == 0)
                {
                    // Empty space
                    row.Add(0);
                }
                else
                {
                    // Checkerboard with IDs 2, 3, and 4
                    int id = ((r + c) % 3) + 2;
                    row.Add(id);
                }
            }
            debugGrid.Add(row);
        }

        // Create the complete level with the debug grid
        CreateLevel(debugGrid);
    }

    private List<List<int>> ExtractInputGrid(string jsonText)
    {
        try
        {
            // Extract the "input" array from JSON
            int inputStartIndex = jsonText.IndexOf("\"input\":");
            if (inputStartIndex == -1) return null;

            // Find the start of the array
            int arrayStartIndex = jsonText.IndexOf("[", inputStartIndex);
            if (arrayStartIndex == -1) return null;

            // Find the matching end bracket for the outer array
            int bracketCount = 1;
            int arrayEndIndex = arrayStartIndex + 1;

            while (bracketCount > 0 && arrayEndIndex < jsonText.Length)
            {
                if (jsonText[arrayEndIndex] == '[') bracketCount++;
                else if (jsonText[arrayEndIndex] == ']') bracketCount--;
                arrayEndIndex++;
            }

            // Extract the array portion
            string arrayText = jsonText.Substring(arrayStartIndex, arrayEndIndex - arrayStartIndex);

            // Parse the 2D array
            return Parse2DArray(arrayText);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error parsing input grid: " + e.Message);
            return null;
        }
    }

    private List<List<int>> Parse2DArray(string arrayText)
    {
        List<List<int>> result = new List<List<int>>();

        // Use regex to find all inner arrays
        Regex innerArrayPattern = new Regex(@"\[(.*?)\]");
        MatchCollection matches = innerArrayPattern.Matches(arrayText);

        foreach (Match match in matches)
        {
            // Skip the outer array match (the first match will be the entire array)
            if (match.Value == arrayText) continue;

            string innerContent = match.Groups[1].Value;
            List<int> row = new List<int>();

            // Split by commas and parse each number
            string[] numbers = innerContent.Split(',');
            foreach (string numStr in numbers)
            {
                string trimmed = numStr.Trim();
                int value;
                if (int.TryParse(trimmed, out value))
                {
                    row.Add(value);
                }
            }

            if (row.Count > 0)
            {
                result.Add(row);
            }
        }

        return result;
    }

    private void CreateLevel(List<List<int>> grid)
    {
        int rows = grid.Count;
        if (rows == 0) return;

        int cols = grid[0].Count;

        // Clear any existing level
        GameObject existingLevel = GameObject.Find("Level");
        if (existingLevel != null)
        {
            DestroyImmediate(existingLevel);
        }

        // Create a parent object for the entire level
        GameObject levelParent = new GameObject("Level");

        // Create separate parent objects for organization
        GameObject cubesParent = new GameObject("Cubes");
        GameObject floorParent = new GameObject("Floor");
        GameObject groundParent = new GameObject("Ground");

        cubesParent.transform.parent = levelParent.transform;
        floorParent.transform.parent = levelParent.transform;
        groundParent.transform.parent = levelParent.transform;

        // Create all level elements
        CreateFloorGrid(rows, cols, floorParent);
        CreateGround(rows, cols, groundParent);
        CreateCubeGrid(grid, cubesParent);

        // Scale and position the entire level
        ScaleAndPositionLevel(levelParent, rows, cols);

        Debug.Log($"Successfully created level with {rows} rows and {cols} columns");
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
                float z = row * (cubeSize + spacing);
                Vector3 position = new Vector3(x, 0, z);

                // Instantiate floor tile
                GameObject floor = Instantiate(floorPrefab, position, Quaternion.identity, floorParent.transform);
                floor.name = $"Floor_{row}_{col}";

                // Ensure floor is properly sized
                floor.transform.localScale = new Vector3(cubeSize, 0.1f, cubeSize);
            }
        }
    }

    private void CreateGround(int rows, int cols, GameObject groundParent)
    {
        // Calculate grid dimensions
        float gridWidth = cols * (cubeSize + spacing) - spacing;
        float gridHeight = rows * (cubeSize + spacing) - spacing;

        // Position ground at the center of the grid, below the floor
        Vector3 groundPosition = new Vector3(
            gridWidth / 2f - (cubeSize / 2f),
            -0.5f, // Position it below the floor
            gridHeight / 2f - (cubeSize / 2f)
        );

        // Scale the ground to match the grid size
        Vector3 groundScale = new Vector3(
            gridWidth,
            1f,
            gridHeight
        );

        // Instantiate the ground
        GameObject ground = Instantiate(groundPrefab, groundPosition, Quaternion.identity, groundParent.transform);
        ground.name = "Ground";
        ground.transform.localScale = groundScale;
    }

    private void CreateCubeGrid(List<List<int>> grid, GameObject cubesParent)
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

                // Calculate position (cubes are one unit above the floor)
                float x = col * (cubeSize + spacing);
                float y = cubeSize / 2f; // Position it above the floor
                float z = row * (cubeSize + spacing);
                Vector3 position = new Vector3(x, y, z);

                // Instantiate cube
                GameObject cube = Instantiate(cubePrefab, position, Quaternion.identity, cubesParent.transform);
                cube.name = $"Cube_{row}_{col}_ID{id}";

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
        }
    }

    private void ScaleAndPositionLevel(GameObject levelParent, int rows, int cols)
    {
        // Calculate current grid dimensions
        float gridWidth = cols * (cubeSize + spacing) - spacing;
        float gridHeight = rows * (cubeSize + spacing) - spacing;

        // Determine which dimension is longer
        float longestDimension = Mathf.Max(gridWidth, gridHeight);

        // Calculate scale factor to fit within play area
        float scaleFactor = maxPlayAreaSize / longestDimension;

        // Apply scale to the level parent
        levelParent.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

        // Calculate scaled dimensions
        float scaledWidth = gridWidth * scaleFactor;
        float scaledHeight = gridHeight * scaleFactor;

        // Calculate offset to center the grid at origin
        Vector3 centerOffset = new Vector3(
            -scaledWidth / 2f,
            0f,
            -scaledHeight / 2f
        );

        // Apply position offset
        levelParent.transform.position = centerOffset + new Vector3(1.2f, 0, 1.2f);

        Debug.Log($"Scaled level by factor {scaleFactor} and centered at {centerOffset}");
    }

    // Add this for runtime debugging
    public void ReloadWithDebugSize()
    {
        useDebugGrid = true;
        LoadLevel();
    }

    // Optional: Unity Editor extension to allow reloading from the Inspector
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(LevelLoader))]
    public class LevelLoaderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LevelLoader levelLoader = (LevelLoader)target;

            if (GUILayout.Button("Reload Level"))
            {
                levelLoader.LoadLevel();
            }
        }
    }
#endif
}