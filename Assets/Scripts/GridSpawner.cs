using UnityEngine;

public class GridSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private int gridWidth = 16;
    [SerializeField] private int gridHeight = 16;
    [SerializeField] private float spacing = 1.0f;
    
    private void Awake()
    {
        SpawnGrid();
    }
    
    private void SpawnGrid()
    {
        // Create parent grid object
        GameObject gridParent = new GameObject("Grid");
        
        // Calculate offset to center the grid at (0,0)
        float offsetX = (gridWidth - 1) * spacing / 2;
        float offsetY = (gridHeight - 1) * spacing / 2;
        
        // Spawn prefabs in a grid pattern
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // Calculate position with offset to center the grid
                Vector3 position = new Vector3(
                    x * spacing - offsetX,
                    0,
                    y * spacing - offsetY
                );
                
                // Instantiate the prefab
                GameObject spawnedObject = Instantiate(prefabToSpawn, position, Quaternion.identity);
                
                // Make it a child of the grid parent
                spawnedObject.transform.SetParent(gridParent.transform);
                
                // Rename the object for clarity
                spawnedObject.name = $"GridCell_{x}_{y}";
            }
        }
    }
}