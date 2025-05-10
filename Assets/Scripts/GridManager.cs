using UnityEngine;
using TMPro;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }
    
    [SerializeField] private Material[] colorMaterials; // Array of materials for the 9 colors
    
    private int[,] gridState; // 0 = empty, 1-9 = block ID based on color
    private const int GridSize = 16; // 16x16 grid
    
    [SerializeField] private TextMeshProUGUI textGrid;
    private string gridVisuals;
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
        InitializeGrid();
    }
    
    private void InitializeGrid()
    {
        // Set all cells to 0 (empty) initially
        for (int x = 0; x < GridSize; x++)
        {
            for (int z = 0; z < GridSize; z++)
            {
                gridState[x, z] = 0;
            }
        }
        
        // Find all cubes in the scene
        GameObject[] cubes = GameObject.FindGameObjectsWithTag("Cube");
        
        // Update grid with the positions of existing cubes
        foreach (GameObject cube in cubes)
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
    
    public int GetColorID(Material material)
    {
        // Find the matching material in the array
        for (int i = 0; i < colorMaterials.Length; i++)
        {
            if (material.name[7] == colorMaterials[i].name[7])
                return i + 1; // IDs are 1-based (0 is empty)
        }
        
        // Default to 1 if no match found
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

        gridVisuals = "";
        for (int i = 0; i < 16; i++)
        {
            for (int y = 0; y < 16; y++)
            {
                gridVisuals +=  gridState[y, 15 - i] + " ";
            }
            gridVisuals += "\n";
        }
        textGrid.text = gridVisuals;
    }
}