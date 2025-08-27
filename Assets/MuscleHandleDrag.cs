using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

[RequireComponent(typeof(Collider))]
public class MuscleHandleDrag : MonoBehaviour
{
    [Header("Settings")]
    public string structureName;

    [Header("UI Message")]
    public TextMeshProUGUI messageText;

    [Header("Drag settings")]
    public bool useParentLocalSpace = false; 
    public enum DragAxis { Free, LocalX, LocalY, LocalZ, GlobalX, GlobalY, GlobalZ }
    public DragAxis dragAxis = DragAxis.Free;

    [Header("Retract limits")]
    public float maxRetractDistance = 0.2f;

    [Header("Highlight")]
    public Color highlightColor = Color.green; // nuovo campo per colore

    private Material originalMaterial;
    private Material highlightMatInstance;
    private bool isSelected = false;
    private bool isHovered = false;

    // internals
    bool isDragging = false;
    float zCoord;
    Vector3 offset;
    Vector3 startTargetPos;

    // riferimenti interni
    private Camera cam;
    private GameObject targetMuscle;
    private Renderer rend;

    void Start()
    {
        targetMuscle = this.gameObject;
        cam = Camera.main;

        rend = targetMuscle.GetComponent<Renderer>();
        if (rend != null)
        {
            originalMaterial = rend.material;
            highlightMatInstance = new Material(originalMaterial);
            highlightMatInstance.color = highlightColor;
        }
        else
        {
            Debug.LogWarning("MuscleHandleDrag: nessun Renderer su " + targetMuscle.name);
        }

        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null || targetMuscle == null) return;

        Vector2 mousePos = mouse.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);
        RaycastHit hit;

        // Hover
        if (rend != null && Physics.Raycast(ray, out hit) && hit.collider.gameObject == targetMuscle)
        {
            if (!isHovered && !isSelected)
            {
                rend.material = highlightMatInstance;
                isHovered = true;
            }
        }
        else
        {
            if (isHovered && !isSelected && rend != null)
            {
                rend.material = originalMaterial;
                isHovered = false;
            }
        }

        // Selezione con click sinistro
        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == targetMuscle)
            {
                ToggleSelection();
            }
        }
        // Deselezione con click destro
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == targetMuscle)
            {
                Deselect();
            }
        }

        // Drag del muscolo
        if (!isDragging && mouse.leftButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out hit) && hit.collider.gameObject == targetMuscle)
            {
                isDragging = true;
                zCoord = cam.WorldToScreenPoint(targetMuscle.transform.position).z;

                Vector3 mp = new Vector3(mousePos.x, mousePos.y, zCoord);
                Vector3 worldPoint = cam.ScreenToWorldPoint(mp);
                offset = targetMuscle.transform.position - worldPoint;
                startTargetPos = targetMuscle.transform.position;
            }
        }

        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            if (rend != null)
            rend.material = originalMaterial;
            return;
        }

        if (isDragging)
        {
            Vector3 mp = new Vector3(mousePos.x, mousePos.y, zCoord);
            Vector3 desiredWorld = cam.ScreenToWorldPoint(mp) + offset;
            Vector3 newWorldPos = ApplyConstraint(startTargetPos, desiredWorld);

            if (useParentLocalSpace && targetMuscle.transform.parent != null)
            {
                Transform p = targetMuscle.transform.parent;
                Vector3 desiredLocal = p.InverseTransformPoint(newWorldPos);
                targetMuscle.transform.localPosition = desiredLocal;
            }
            else
            {
                targetMuscle.transform.position = newWorldPos;
            }
        }
    }

    Vector3 ApplyConstraint(Vector3 originWorld, Vector3 desiredWorld)
    {
        Vector3 axisVector;

        switch (dragAxis)
        {
            case DragAxis.LocalX: axisVector = targetMuscle.transform.right; break;
            case DragAxis.LocalY: axisVector = targetMuscle.transform.up; break;
            case DragAxis.LocalZ: axisVector = targetMuscle.transform.forward; break;
            case DragAxis.GlobalX: axisVector = Vector3.right; break;
            case DragAxis.GlobalY: axisVector = Vector3.up; break;
            case DragAxis.GlobalZ: axisVector = Vector3.forward; break;
            case DragAxis.Free: return desiredWorld; 
            default: return desiredWorld;
        }

        float delta = Vector3.Dot(desiredWorld - originWorld, axisVector);
        delta = Mathf.Clamp(delta, -maxRetractDistance, maxRetractDistance);

        return originWorld + axisVector * delta;
    }

    void ToggleSelection()
    {
        isSelected = !isSelected;
        if (rend != null)
            rend.material = isSelected ? highlightMatInstance : originalMaterial;

        if (messageText != null)
        {
            messageText.gameObject.SetActive(isSelected); 
            if (isSelected)
                messageText.text = structureName; // Mostra il nome della struttura
        }
    }

    void Deselect()
    {
        isSelected = false;
        if (rend != null)
            rend.material = originalMaterial;

        if (messageText != null)
        {
            messageText.text = "";
            messageText.gameObject.SetActive(false); 
        }
    }
}
