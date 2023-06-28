using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updates the attached LayoutElement component based on the GameObject's aspect ratio.
/// https://forum.unity.com/threads/solution-layoutelement-fit-parent-with-aspect-ratio.542212/
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(LayoutElement))]
[ExecuteInEditMode]
public class LayoutElementFitParent : MonoBehaviour
{
    [SerializeField]
    private float aspectRatio = 1;

    [SerializeField]
    private bool updateMin = false;

    [SerializeField]
    private bool updatePreferred = false;

    private bool isDirty = false;
    private Vector2 lastParentSize;

    private new RectTransform transform;
    private LayoutElement layoutElement;

    public float AspectRatio
    {
        get { return aspectRatio; }
        set
        {
            aspectRatio = value;
            isDirty = true;
        }
    }

    public bool UpdateMin
    {
        get { return updateMin; }
        set
        {
            updateMin = value;
            isDirty = true;
        }
    }

    public bool UpdatePreferred
    {
        get { return updatePreferred; }
        set
        {
            updatePreferred = value;
            isDirty = true;
        }
    }

    private void OnEnable()
    {
        transform = GetComponent<RectTransform>();
        layoutElement = GetComponent<LayoutElement>();

        isDirty = true;
    }

    private void Update()
    {
        Vector2 parentSize = GetParentSize();

        // Mark as dirty if parent's size changes
        if (lastParentSize != parentSize)
        {
            lastParentSize = parentSize;
            isDirty = true;
        }

        // Only recalculate layout size if something has changed
        if (!isDirty) return;
        isDirty = false;

        float neededWidth = parentSize.y * aspectRatio;
        float neededHeight = parentSize.x / aspectRatio;

        // Is height the limiting factor?
        if (neededWidth <= parentSize.x)
        {
            // Scale to match parent's height
            SetSizes(neededWidth, parentSize.y);
        }
        else
        {
            // Scale to match parent's width
            SetSizes(parentSize.x, neededHeight);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector fields have changed, mark as dirty
        isDirty = true;
    }
#endif

    private void SetSizes(float x, float y)
    {
        if (updateMin)
        {
            layoutElement.minWidth = x;
            layoutElement.minHeight = y;
        }

        if (updatePreferred)
        {
            layoutElement.preferredWidth = x;
            layoutElement.preferredHeight = y;
        }
    }

    private Vector2 GetParentSize()
    {
        var parent = transform.parent as RectTransform;
        return parent == null ? Vector2.zero : parent.rect.size;
    }
}