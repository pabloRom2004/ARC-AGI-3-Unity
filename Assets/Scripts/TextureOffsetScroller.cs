using UnityEngine;

public class SimpleTextureScroller : MonoBehaviour
{
    public float scrollSpeedX = 0.000f;
    public float scrollSpeedY = 0.001f;

    Renderer rendererGrid;

    void Awake()
    {
        rendererGrid = GetComponent<Renderer>();
        
        // // 50% chance to flip X direction
        // if (Random.value > 0.5f)
        // {
        //     scrollSpeedX = -scrollSpeedX;
        // }
        
        // // 50% chance to flip Y direction
        // if (Random.value > 0.5f)
        // {
        //     scrollSpeedY = -scrollSpeedY;
        // }
    }

    void Update()
    {
        rendererGrid.material.mainTextureOffset += new Vector2(scrollSpeedX, scrollSpeedY) * Time.deltaTime;
    }
}