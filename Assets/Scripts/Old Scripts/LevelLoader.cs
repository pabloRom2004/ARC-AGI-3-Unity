using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

public class LevelLoader : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool useDebugGrid = false;
    [SerializeField] private int debugRows = 8;
    [SerializeField] private int debugCols = 8;

    private string levelDataPath;
    private GameStateManager gameStateManager;

    void Start()
    {
        // Find or create the GameStateManager
        gameStateManager = FindFirstObjectByType<GameStateManager>();
        if (gameStateManager == null)
        {
            Debug.LogError("GameStateManager not found in the scene!");
            return;
        }

        levelDataPath = Path.Combine(Application.streamingAssetsPath, "levelTest.json");
        LoadLevel();
    }

    public void LoadLevel()
    {
        if (useDebugGrid)
        {
            // Create a debug grid with custom dimensions
            CreateDebugGrid(debugRows, debugCols);
        }
        else
        {
            // Load from JSON
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

        // Parse the grid data manually
        List<List<int>> gridData = ExtractInputGrid(jsonText);

        if (gridData == null || gridData.Count == 0)
        {
            Debug.LogError("Failed to parse level grid data");
            return;
        }

        // Extract interaction data
        List<InteractionData> interactionData = ExtractInteractionData(jsonText);

        // Pass the grid and interaction data to the Game State Manager
        gameStateManager.InitializeGrid(gridData);
        gameStateManager.SetInteractionData(interactionData);
    }

    private void CreateDebugGrid(int rows, int cols)
    {
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

        // Create some debug interactions
        List<InteractionData> debugInteractions = new List<InteractionData>();
        
        // Add a "PickUp" interaction
        InteractionData pickUpInteraction = new InteractionData();
        pickUpInteraction.scriptType = "PickUp";
        pickUpInteraction.blocks = new List<Vector2Int>
        {
            new Vector2Int(1, 1),
            new Vector2Int(2, 1),
            new Vector2Int(1, 2),
            new Vector2Int(2, 2)
        };
        debugInteractions.Add(pickUpInteraction);
        
        // Add a "Move" interaction
        InteractionData moveInteraction = new InteractionData();
        moveInteraction.scriptType = "Move";
        moveInteraction.canMoveUp = true;
        moveInteraction.canMoveDown = true;
        moveInteraction.canMoveLeft = true;
        moveInteraction.canMoveRight = true;
        moveInteraction.blocks = new List<Vector2Int>
        {
            new Vector2Int(cols - 2, 1),
            new Vector2Int(cols - 2, 2),
            new Vector2Int(cols - 3, 2)
        };
        debugInteractions.Add(moveInteraction);

        // Pass the debug grid and interactions to the Game State Manager
        gameStateManager.InitializeGrid(debugGrid);
        gameStateManager.SetInteractionData(debugInteractions);
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

    // Add method to extract interaction data from JSON
    private List<InteractionData> ExtractInteractionData(string jsonText)
    {
        List<InteractionData> interactions = new List<InteractionData>();
        
        try
        {
            // Extract the "interactions" array from JSON
            int interactionsStartIndex = jsonText.IndexOf("\"interactions\":");
            if (interactionsStartIndex == -1) return interactions;

            // Find the start of the array
            int arrayStartIndex = jsonText.IndexOf("[", interactionsStartIndex);
            if (arrayStartIndex == -1) return interactions;

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

            // Parse the interactions array
            return ParseInteractionsArray(arrayText);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error parsing interactions: " + e.Message);
            return interactions;
        }
    }

    private List<InteractionData> ParseInteractionsArray(string arrayText)
    {
        List<InteractionData> interactions = new List<InteractionData>();
        
        // Use regex to find all interaction objects
        Regex interactionPattern = new Regex(@"\{(.*?)\}", RegexOptions.Singleline);
        MatchCollection matches = interactionPattern.Matches(arrayText);

        foreach (Match match in matches)
        {
            string interactionText = match.Value;
            InteractionData interaction = new InteractionData();
            
            // Extract script type
            Regex scriptPattern = new Regex(@"""script"":\s*""(\w+)""");
            Match scriptMatch = scriptPattern.Match(interactionText);
            if (scriptMatch.Success)
            {
                interaction.scriptType = scriptMatch.Groups[1].Value;
            }
            
            // Extract movement directions if present
            if (interaction.scriptType == "Move")
            {
                // Up
                Regex upPattern = new Regex(@"""up"":\s*(true|false)");
                Match upMatch = upPattern.Match(interactionText);
                if (upMatch.Success)
                {
                    interaction.canMoveUp = upMatch.Groups[1].Value.ToLower() == "true";
                }
                
                // Down
                Regex downPattern = new Regex(@"""down"":\s*(true|false)");
                Match downMatch = downPattern.Match(interactionText);
                if (downMatch.Success)
                {
                    interaction.canMoveDown = downMatch.Groups[1].Value.ToLower() == "true";
                }
                
                // Left
                Regex leftPattern = new Regex(@"""left"":\s*(true|false)");
                Match leftMatch = leftPattern.Match(interactionText);
                if (leftMatch.Success)
                {
                    interaction.canMoveLeft = leftMatch.Groups[1].Value.ToLower() == "true";
                }
                
                // Right
                Regex rightPattern = new Regex(@"""right"":\s*(true|false)");
                Match rightMatch = rightPattern.Match(interactionText);
                if (rightMatch.Success)
                {
                    interaction.canMoveRight = rightMatch.Groups[1].Value.ToLower() == "true";
                }
            }
            
            // Extract blocks array
            Regex blocksPattern = new Regex(@"""blocks"":\s*(\[\[.*?\]\])", RegexOptions.Singleline);
            Match blocksMatch = blocksPattern.Match(interactionText);
            if (blocksMatch.Success)
            {
                string blocksArrayText = blocksMatch.Groups[1].Value;
                
                // Parse individual block coordinates
                Regex blockCoordPattern = new Regex(@"\[(\d+),\s*(\d+)\]");
                MatchCollection blockMatches = blockCoordPattern.Matches(blocksArrayText);
                
                foreach (Match blockMatch in blockMatches)
                {
                    int row = int.Parse(blockMatch.Groups[1].Value);
                    int col = int.Parse(blockMatch.Groups[2].Value);
                    interaction.blocks.Add(new Vector2Int(col, row)); // Note: Using col,row for Unity coordinates
                }
            }
            
            interactions.Add(interaction);
        }
        
        Debug.Log($"Parsed {interactions.Count} interaction groups from JSON");
        return interactions;
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

    // Method to reload level (for debugging or UI calls)
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