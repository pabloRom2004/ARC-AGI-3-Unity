using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attach this script to UI buttons to handle animations on mouse interactions.
/// </summary>
public class ButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Tooltip("Animator component to control animations")]
    public Animator animator;

    [Tooltip("Animation name to play when pointer enters")]
    public string pointerEnterAnim = "MouseEnter";

    [Tooltip("Animation name to play when pointer exits")]
    public string pointerExitAnim = "MouseExit";

    [Tooltip("Animation name to play when pointer clicks")]
    public string pointerClickAnim = "MouseClick";

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (animator != null && !string.IsNullOrEmpty(pointerEnterAnim))
        {
            animator.Play(pointerEnterAnim, 0, 0);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (animator != null && !string.IsNullOrEmpty(pointerExitAnim))
        {
            animator.Play(pointerExitAnim, 0, 0);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (animator != null && !string.IsNullOrEmpty(pointerClickAnim))
        {
            animator.Play(pointerClickAnim, 0, 0);
        }
        // Note: The actual click functionality is handled by the Button component
    }
}