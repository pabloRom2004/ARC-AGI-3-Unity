using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeSequentialActivator : MonoBehaviour
{
    [Tooltip("The parent object containing all the cubes (e.g., Grid or Move Parent)")]
    [SerializeField] private Transform cubeParent;
    
    [Tooltip("The total time to activate all cubes in seconds")]
    [SerializeField] private float totalActivationTime = 1.0f;
    
    [Tooltip("Tag of the cube objects to find")]
    [SerializeField] private string cubeTag = "Cube";
    
    private List<GameObject> orderedCubes = new List<GameObject>();
    
    private void Start()
    {
        // If no parent is assigned, use this object
        if (cubeParent == null)
            cubeParent = transform;
        
        // Find and gather all cubes in hierarchy order
        FindCubesInOrder(cubeParent);
        
        // Disable all cubes initially
        foreach (GameObject cube in orderedCubes)
        {
            cube.SetActive(false);
        }
        
        // Start the sequence to enable cubes one by one
        StartCoroutine(ActivateCubesSequentially());
    }
    
    // Recursively find cubes in hierarchy order
    private void FindCubesInOrder(Transform parent)
    {
        // First, process direct children in inspector order
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            
            // If this child has the Cube tag, add it
            if (child.CompareTag(cubeTag))
            {
                orderedCubes.Add(child.gameObject);
            }
            
            // Recursively search for cubes in this child's hierarchy
            FindCubesInOrder(child);
        }
    }
    
    private IEnumerator ActivateCubesSequentially()
    {
        // Wait a frame to ensure everything is initialized
        yield return null;
        
        int cubeCount = orderedCubes.Count;
        float delay = 0f;
        
        // Calculate the delay between cubes based on total time
        if (cubeCount > 1)
        {
            delay = totalActivationTime / (cubeCount - 1);
        }
        
        // Activate each cube with the calculated delay
        for (int i = 0; i < cubeCount; i++)
        {
            if (orderedCubes[i] != null)
            {
                orderedCubes[i].SetActive(true);
                orderedCubes[i].GetComponent<Animator>().Play("CubeEnter");
                
                // Wait for the calculated delay (skip delay after the last cube)
                if (i < cubeCount - 1)
                {
                    yield return new WaitForSeconds(delay);
                }
            }
        }
    }
}