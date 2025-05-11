using UnityEngine;
using TMPro;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }
    
    [SerializeField] private Material[] colorMaterials; // Array of materials for the 9 colors
    [SerializeField] private Transform levelParent; // Parent containing all level objects
    [SerializeField] private TextMeshProUGUI textGrid;
    [SerializeField] private TextMeshProUGUI levelNumberText; // Text for displaying level number
    
    private int[,] gridState; // 0 = empty, 1-9 = block ID based on color
    private const int GridSize = 16; // 16x16 grid
    
    private string gridVisuals;
    private int currentLevelIndex = 0;
    private CubeSequentialActivator cubeActivator;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        // Initialize the grid
        gridState = new int[GridSize, GridSize];
        
        // Get reference to the CubeSequentialActivator on the same GameObject
        cubeActivator = GetComponent<CubeSequentialActivator>();
        
        // Initialize first level if available
        if (levelParent != null && levelParent.childCount > 0)
        {
            SetActiveLevel(0);
        }
        else
        {
            Debug.LogWarning("No levels found in levelParent!");
            InitializeGrid(null);
            UpdateLevelNumberText(1); // Default to level 1
        }
    }
    
    public void NextLevel()
    {
        if (levelParent == null || levelParent.childCount == 0)
            return;
            
        currentLevelIndex++;
        if (currentLevelIndex >= levelParent.childCount)
            currentLevelIndex = 0; // Loop back to first level
            
        SetActiveLevel(currentLevelIndex);
    }
    
    public void PreviousLevel()
    {
        if (levelParent == null || levelParent.childCount == 0)
            return;
            
        currentLevelIndex--;
        if (currentLevelIndex < 0)
            currentLevelIndex = levelParent.childCount - 1; // Loop to last level
            
        SetActiveLevel(currentLevelIndex);
    }
    
    private void SetActiveLevel(int levelIndex)
    {
        if (levelParent == null || levelIndex < 0 || levelIndex >= levelParent.childCount)
            return;
            
        // Get the level GameObject
        Transform levelTransform = levelParent.GetChild(levelIndex);
        
        // First deactivate all levels
        for (int i = 0; i < levelParent.childCount; i++)
        {
            levelParent.GetChild(i).gameObject.SetActive(i == levelIndex);
        }
        
        // Initialize grid with the new level
        InitializeGrid(levelTransform);
        
        // Pass the level to the activator and start the sequence
        if (cubeActivator != null)
        {
            cubeActivator.SetCubeParent(levelTransform);
            cubeActivator.StartActivationSequence();
        }
        
        // Update level number text (add 1 because levels are displayed starting from 1, not 0)
        UpdateLevelNumberText(levelIndex + 1);
    }
    
    private void UpdateLevelNumberText(int levelNumber)
    {
        if (levelNumberText != null)
        {
            // Format with leading zeros based on magnitude
            if (levelNumber < 10)
                levelNumberText.text = "00" + levelNumber.ToString(); // Three digits, two leading zeros
            else if (levelNumber < 100)
                levelNumberText.text = "0" + levelNumber.ToString();  // Three digits, one leading zero
            else
                levelNumberText.text = levelNumber.ToString();        // Three or more digits, no leading zeros
        }
    }
    
    private void InitializeGrid(Transform currentLevel)
    {
        // Clear the grid
        for (int x = 0; x < GridSize; x++)
        {
            for (int z = 0; z < GridSize; z++)
            {
                gridState[x, z] = 0;
            }
        }
        
        // If no level is passed, just leave the grid empty
        if (currentLevel == null)
            return;
            
        // Find all cubes in the current level
        GameObject[] cubes = GameObject.FindGameObjectsWithTag("Cube");
        
        // Update grid with the positions of existing cubes
        foreach (GameObject cube in cubes)
        {
            // Only process cubes that are part of the current level
            if (cube.transform.IsChildOf(currentLevel))
            {
                Vector2Int gridPos = WorldToGrid(cube.transform.position);
                
                // Skip if outside grid boundaries
                if (!IsInBounds(gridPos))
                    continue;
                
                // Get the color ID based on material
                Renderer renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    int colorID = GetColorID(renderer.material);
                    
                    // Update the grid
                    gridState[gridPos.x, gridPos.y] = colorID;
                }
            }
        }
        
        // Update grid visuals
        UpdateGridVisuals();
    }
    
    private void UpdateGridVisuals()
    {
        gridVisuals = "";
        for (int i = 0; i < 16; i++)
        {
            for (int y = 0; y < 16; y++)
            {
                gridVisuals +=  gridState[y, 15 - i] + "   ";
            }
            gridVisuals += "\n";
        }
        textGrid.text = gridVisuals;
    }
    
    public int GetColorID(Material material)
    {
        // Find the matching material in the array
        for (int i = 0; i < colorMaterials.Length; i++)
        {
            if (System.Text.RegularExpressions.Regex.Match(material.name, @"\d+").Value == 
                System.Text.RegularExpressions.Regex.Match(colorMaterials[i].name, @"\d+").Value)
                return i + 1; // IDs are 1-based (0 is empty)
        }
        
        // Default to 1 if no match found
        Debug.Log(material.name);
        Debug.LogWarning("No matching material found! Defaulting to ID 1.");
        return 1;
    }
    
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        // Convert from world position to grid coordinates (world Z maps to grid Y)
        int x = Mathf.RoundToInt(worldPosition.x);
        int y = Mathf.RoundToInt(worldPosition.z); // Map world Z to grid Y
        
        return new Vector2Int(x, y);
    }
    
    public bool IsInBounds(Vector2Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < GridSize && 
               gridPos.y >= 0 && gridPos.y < GridSize;
    }
    
    public bool IsPositionEmpty(Vector2Int gridPos)
    {
        if (!IsInBounds(gridPos))
            return false;
            
        return gridState[gridPos.x, gridPos.y] == 0;
    }
    
    public void UpdateGridPosition(Vector2Int oldPos, Vector2Int newPos, int blockID)
    {
        // Clear old position
        if (IsInBounds(oldPos))
            gridState[oldPos.x, oldPos.y] = 0;
        
        // Set new position
        if (IsInBounds(newPos))
            gridState[newPos.x, newPos.y] = blockID;

        UpdateGridVisuals();
    }
}