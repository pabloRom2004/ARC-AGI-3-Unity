using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeSequentialActivator : MonoBehaviour
{
    [Tooltip("The total time to activate all cubes in seconds")]
    [SerializeField] private float totalActivationTime = 1.0f;
    
    [Tooltip("Tag of the cube objects to find")]
    [SerializeField] private string cubeTag = "Cube";
    
    private Transform cubeParent;
    private List<GameObject> orderedCubes = new List<GameObject>();
    private Coroutine activationCoroutine;
    
    public void SetCubeParent(Transform newParent)
    {
        // Stop any ongoing activation
        if (activationCoroutine != null)
        {
            StopCoroutine(activationCoroutine);
            activationCoroutine = null;
        }
        
        cubeParent = newParent;
        orderedCubes.Clear();
        
        // Find and gather all cubes in hierarchy order
        if (cubeParent != null)
        {
            FindCubesInOrder(cubeParent);
            
            // Disable all cubes and their colliders initially
            foreach (GameObject cube in orderedCubes)
            {
                cube.SetActive(false);
                
                // Disable box collider if it exists
                BoxCollider boxCollider = cube.GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    boxCollider.enabled = false;
                }
            }
        }
    }
    
    public void StartActivationSequence()
    {
        if (cubeParent == null || orderedCubes.Count == 0)
            return;
            
        // Start the sequence to enable cubes one by one
        activationCoroutine = StartCoroutine(ActivateCubesSequentially());
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
        
        // Activate each cube with the calculated delay (but keep colliders disabled)
        for (int i = 0; i < cubeCount; i++)
        {
            if (orderedCubes[i] != null)
            {
                orderedCubes[i].SetActive(true);
                
                // Get the animator if it exists
                Animator animator = orderedCubes[i].GetComponent<Animator>();
                if (animator != null && orderedCubes[i].gameObject.activeSelf)
                {
                    animator.Play("CubeEnter");
                }
                
                // Wait for the calculated delay (skip delay after the last cube)
                if (i < cubeCount - 1)
                {
                    yield return new WaitForSeconds(delay);
                }
            }
        }
        
        // Add a small delay to ensure all animations have time to complete
        yield return new WaitForSeconds(0.5f);
        
        // Enable all colliders after all cubes are activated and animations are complete
        foreach (GameObject cube in orderedCubes)
        {
            if (cube != null)
            {
                BoxCollider boxCollider = cube.GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    boxCollider.enabled = true;
                }
            }
        }
        
        activationCoroutine = null;
    }
}