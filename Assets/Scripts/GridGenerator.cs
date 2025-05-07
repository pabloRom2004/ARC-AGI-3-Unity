using UnityEngine;

public class SimpleGridGenerator : MonoBehaviour
{
    public GameObject prefab;
    public float spacing = 1.0f;
    
    void Start()
    {
        GenerateGrid();
    }
    
    void GenerateGrid()
    {
        if (prefab == null)
        {
            Debug.LogError("No prefab assigned!");
            return;
        }
        
        // Generate a 16x16 grid
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                // Calculate position with offset to center the grid
                Vector3 position = new Vector3(
                    x * spacing - 7.5f * spacing, 
                    z * spacing - 7.5f * spacing,
                    0f
                );
                
                // Create tile and set parent
                GameObject tile = Instantiate(prefab, position, Quaternion.identity);
                tile.name = $"Tile_{x}_{z}";
                tile.transform.parent = this.transform;
            }
        }
    }
}