using UnityEngine;

public class MouseInteractionManager : MonoBehaviour
{
    private InteractableStructure currentDrag;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryStartDrag();
        }
        else if (Input.GetMouseButton(0) && currentDrag != null)
        {
            DragCurrent();
        }
        else if (Input.GetMouseButtonUp(0) && currentDrag != null)
        {
            EndCurrentDrag();
        }
    }

    void TryStartDrag()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray);

        if (hits.Length == 0) return;

        // Ordina per distanza (vicino -> lontano)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            InteractableStructure interactable = hit.collider.GetComponent<InteractableStructure>();
            if (interactable != null)
            {
                currentDrag = interactable;
                currentDrag.StartDrag(GetMouseWorld());
                break; // prende solo il primo oggetto più vicino
            }
        }
    }

    void DragCurrent()
    {
        currentDrag.Drag(GetMouseWorld());
    }

    void EndCurrentDrag()
    {
        currentDrag.EndDrag();
        currentDrag = null;
    }

    Vector3 GetMouseWorld()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.WorldToScreenPoint(Vector3.zero).z;
        return Camera.main.ScreenToWorldPoint(mousePos);
    }
}