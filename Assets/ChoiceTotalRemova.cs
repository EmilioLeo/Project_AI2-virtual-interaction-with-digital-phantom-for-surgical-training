using UnityEngine;

public class ChoiceTotalRemova : MonoBehaviour
{
    private bool isDragging = false;
    private Vector3 offset;
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void OnMouseDown()
    {
        if (!enabled) return;

        isDragging = true;
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = cam.WorldToScreenPoint(transform.position).z;
        Vector3 worldPos = cam.ScreenToWorldPoint(mousePos);
        offset = transform.position - worldPos;
    }

    void OnMouseDrag()
    {
        if (!isDragging) return;

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = cam.WorldToScreenPoint(transform.position).z;
        Vector3 worldPos = cam.ScreenToWorldPoint(mousePos);
        transform.position = worldPos + offset;
    }

    void OnMouseUp()
    {
        isDragging = false;
        enabled = false;  // disabilita lo script dopo il drag
    }
}
