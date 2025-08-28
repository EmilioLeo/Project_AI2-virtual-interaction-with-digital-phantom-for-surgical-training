using UnityEngine;
using TMPro;

public class InteractableStructure : MonoBehaviour
{
    [Header("Settings")]
    public string structureName;
    public Color highlightColor = Color.green;

    [Header("UI Reference")]
    public TMP_Text uiText;

    [Header("Elastic Deformation")]
    public Vector3 dragAxis = Vector3.right; // asse laterale
    public float dragSensitivity = 0.01f;    // sensibilità del mouse
    public float maxOffset = 0.02f;          // massimo spostamento laterale
    public float falloff = 1.5f;             // diminuzione verso estremi

    private Renderer rend;
    private Color originalColor;

    private MeshFilter mf;
    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] vertices;

    private bool isDragging = false;
    private Vector3 dragStartWorld;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalColor = rend.material.color;

        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            mesh = mf.mesh;
            originalVertices = mesh.vertices;
            vertices = new Vector3[originalVertices.Length];
            System.Array.Copy(originalVertices, vertices, originalVertices.Length);
        }
        else
        {
            Debug.LogError("MeshFilter mancante su " + gameObject.name);
        }
    }

    void OnMouseEnter()
    {
        if (rend != null)
            rend.material.color = highlightColor;

        if (uiText != null)
            uiText.text = structureName;
    }

    void OnMouseExit()
    {
        if (!isDragging && rend != null)
            rend.material.color = originalColor;

        if (uiText != null && !isDragging)
            uiText.text = "";
    }

    void OnMouseDown()
    {
        isDragging = true;
        dragStartWorld = GetMouseWorldPosition();

        if (uiText != null)
            uiText.text = structureName;
    }

    void OnMouseDrag()
    {
        if (mesh == null) return;

        Vector3 currentMouseWorld = GetMouseWorldPosition();
        float delta = (currentMouseWorld - dragStartWorld).x * dragSensitivity;
        delta = Mathf.Clamp(delta, -maxOffset, maxOffset);

        for (int i = 0; i < vertices.Length; i++)
        {
            float t = Mathf.InverseLerp(mesh.bounds.min.y, mesh.bounds.max.y, originalVertices[i].y);
            float weight = Mathf.Sin(t * Mathf.PI);
            Vector3 newVertex = originalVertices[i] + dragAxis.normalized * delta * Mathf.Pow(weight, falloff);

            // evita NaN o vertici invalidi
            if (float.IsNaN(newVertex.x) || float.IsNaN(newVertex.y) || float.IsNaN(newVertex.z))
                continue;

            vertices[i] = newVertex;
        }

        mesh.vertices = vertices;
        // Non calcolare bounds ad ogni frame per evitare errori
    }

    void OnMouseUp()
    {
        isDragging = false;

        if (rend != null)
            rend.material.color = originalColor;

        // aggiorna bounds solo una volta alla fine
        if (mesh != null)
        {
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        if (uiText != null)
            uiText.text = "";
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePos);
    }
}
