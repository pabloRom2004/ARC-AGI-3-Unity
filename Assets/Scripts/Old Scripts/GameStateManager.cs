using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Define interaction data class above the GameStateManager class
[System.Serializable]
public class InteractionData
{
    public string scriptType; // "PickUp" or "Move"
    public List<Vector2Int> blocks = new List<Vector2Int>(); // Block coordinates
    public bool canMoveUp;
    public bool canMoveDown;
    public bool canMoveLeft;
    public bool canMoveRight;
}

public class GameStateManager : MonoBehaviour
{
    private List<List<int>> currentGrid;
    private List<List<int>> targetGrid; // For storing the winning configuration
    private bool isGameInitialized = false;
    private Game3DVisualizer visualizer;
    
    // Add a list to store interaction data
    private List<InteractionData> interactionData = new List<InteractionData>();

    // Event that can be subscribed to when the grid changes
    public delegate void GridUpdatedHandler();
    public event GridUpdatedHandler OnGridUpdated;

    private void Awake()
    {
        // Initialize empty grid to prevent null references
        currentGrid = new List<List<int>>();
        targetGrid = new List<List<int>>();
        
        // Make sure this object persists across scene loads if needed
        // DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        // Find the 3D visualizer with a slight delay
        StartCoroutine(FindVisualizerDelayed());
    }

    private IEnumerator FindVisualizerDelayed()
    {
        // Wait for one frame to allow all objects to initialize
        yield return null;
        
        // Find visualizer
        FindVisualizer();
    }

    private void FindVisualizer()
    {
        visualizer = FindFirstObjectByType<Game3DVisualizer>();
        if (visualizer == null)
        {
            Debug.LogWarning("Game3DVisualizer not found. The grid will not be visualized.");
        }
        else if (isGameInitialized && currentGrid != null && currentGrid.Count > 0)
        {
            // If we're already initialized, update the visualizer once
            visualizer.VisualizeGrid(currentGrid);
        }
    }

    // Add method to set interaction data
    public void SetInteractionData(List<InteractionData> interactions)
    {
        interactionData = interactions;
        Debug.Log($"Set {interactions.Count} interaction groups in GameStateManager");
    }

    // Add method to get interaction data
    public List<InteractionData> GetInteractionData()
    {
        return interactionData;
    }

    // Initialize the grid with the loaded data
    public void InitializeGrid(List<List<int>> gridData)
    {
        if (gridData == null || gridData.Count == 0)
        {
            Debug.LogError("Attempted to initialize with null or empty grid data");
            return;
        }
        
        currentGrid = new List<List<int>>(gridData.Count);
        
        // Deep copy the grid to avoid reference issues
        for (int i = 0; i < gridData.Count; i++)
        {
            currentGrid.Add(new List<int>(gridData[i]));
        }

        // For now, we'll set the target grid to be the same as the current grid
        // In a real game, you'd load or generate a target grid separately
        targetGrid = new List<List<int>>(gridData.Count);
        for (int i = 0; i < gridData.Count; i++)
        {
            targetGrid.Add(new List<int>(gridData[i]));
        }

        isGameInitialized = true;
        Debug.Log("Game state initialized with grid size: " + currentGrid.Count + "x" + currentGrid[0].Count);

        // If we don't have a visualizer yet, try to find it again
        if (visualizer == null)
        {
            FindVisualizer();
        }
        else
        {
            // Initial visualization only
            visualizer.VisualizeGrid(currentGrid);
        }

        // Trigger the grid updated event
        OnGridUpdated?.Invoke();
    }

    // Get the current grid (could be used by UI or game logic)
    public List<List<int>> GetCurrentGrid()
    {
        if (!isGameInitialized)
        {
            // Return empty grid instead of null to prevent errors
            return new List<List<int>>();
        }
        return currentGrid;
    }

    // Get the target grid (the winning configuration)
    public List<List<int>> GetTargetGrid()
    {
        if (!isGameInitialized)
        {
            // Return empty grid instead of null to prevent errors
            return new List<List<int>>();
        }
        return targetGrid;
    }

    // Method to update a specific cell in the grid
    public void UpdateCell(int row, int col, int newValue)
    {
        if (!isGameInitialized || currentGrid == null || currentGrid.Count == 0)
        {
            Debug.LogWarning("Trying to update grid before it's initialized!");
            return;
        }

        if (row >= 0 && row < currentGrid.Count && col >= 0 && col < currentGrid[row].Count)
        {
            // Update the logical grid state
            currentGrid[row][col] = newValue;
            
            // No longer notify visualizer for updates - the visualizer is static
            // The physical cubes will be moved directly by the interaction scripts

            // Check if the puzzle is solved after the update
            CheckForWinCondition();
            
            // Trigger the grid updated event (for other systems like UI)
            OnGridUpdated?.Invoke();
        }
        else
        {
            Debug.LogError($"Invalid grid position: {row}, {col}");
        }
    }

    // Method to check if the current grid matches the target grid
    public bool CheckForWinCondition()
    {
        if (!isGameInitialized || currentGrid == null || targetGrid == null)
        {
            return false;
        }

        // Compare the current grid with the target grid
        for (int row = 0; row < currentGrid.Count; row++)
        {
            for (int col = 0; col < currentGrid[row].Count; col++)
            {
                if (currentGrid[row][col] != targetGrid[row][col])
                {
                    return false; // Grids don't match
                }
            }
        }

        // If we got here, the puzzle is solved!
        Debug.Log("Puzzle Solved!");
        return true;
    }

    // Method to get the dimensions of the grid
    public Vector2Int GetGridDimensions()
    {
        if (!isGameInitialized || currentGrid == null || currentGrid.Count == 0)
        {
            return Vector2Int.zero;
        }
        
        return new Vector2Int(currentGrid[0].Count, currentGrid.Count);
    }

    // Method to reset the grid to its initial state
    public void ResetGrid()
    {
        if (!isGameInitialized || currentGrid == null)
        {
            Debug.LogWarning("Cannot reset grid before it's initialized!");
            return;
        }

        // For a simple reset, we could reinitialize with the same initial grid
        // In a more complex implementation, you'd store the initial grid separately
        
        // Recreate visualization only on full resets
        if (visualizer == null)
        {
            FindVisualizer();
        }
        else
        {
            visualizer.VisualizeGrid(currentGrid);
        }
        
        OnGridUpdated?.Invoke();
    }
}