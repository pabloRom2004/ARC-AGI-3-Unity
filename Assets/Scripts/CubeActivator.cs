using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeSequentialActivator : MonoBehaviour
{
    [Tooltip("The parent object containing all the cubes (e.g., Grid or Move Parent)")]
    [SerializeField] private Transform cubeParent;
    
    [Tooltip("The delay between each cube activation in seconds")]
    [SerializeField] private float activationDelay = 0.025f;
    
    [Tooltip("Tag of the cube objects to find")]
    [SerializeField] private string cubeTag = "Cube";
    
    private List<GameObject> orderedCubes = new List<GameObject>();
    
    // private void Awake()
    // {
    //     // If no parent is assigned, use this object
    //     if (cubeParent == null)
    //         cubeParent = transform;
        
    //     // Find and gather all cubes in hierarchy order
    //     FindCubesInOrder(cubeParent);
        
    //     // Disable all cubes initially
    //     foreach (GameObject cube in orderedCubes)
    //     {
    //         cube.SetActive(false);
    //     }
    // }
    
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
        
        Debug.Log($"Activating {orderedCubes.Count} cubes with {activationDelay}s delay");
        
        // Activate each cube with the specified delay
        for (int i = 0; i < orderedCubes.Count; i++)
        {
            if (orderedCubes[i] != null)
            {
                orderedCubes[i].SetActive(true);
                // Debug.Log($"Activated cube {i+1}/{orderedCubes.Count}: {orderedCubes[i].name}");
                
                // Wait for the specified delay
                yield return new WaitForSeconds(activationDelay);
            }
        }
    }
}