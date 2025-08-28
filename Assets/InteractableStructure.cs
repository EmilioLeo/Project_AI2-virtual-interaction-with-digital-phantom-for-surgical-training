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

    // ----------------------------
    // Metodi pubblici per il MouseInteractionManager
    // ----------------------------
    public void StartDrag(Vector3 mouseWorld)
    {
        isDragging = true;
        dragStartWorld = mouseWorld;

        if (rend != null)
            rend.material.color = highlightColor;

        if (uiText != null)
            uiText.text = structureName;
    }

    public void Drag(Vector3 mouseWorld)
    {
        if (!isDragging || mesh == null) return;

        float delta = (mouseWorld - dragStartWorld).x * dragSensitivity;
        delta = Mathf.Clamp(delta, -maxOffset, maxOffset);

        for (int i = 0; i < vertices.Length; i++)
        {
            float t = Mathf.InverseLerp(mesh.bounds.min.y, mesh.bounds.max.y, originalVertices[i].y);
            float weight = Mathf.Sin(t * Mathf.PI);
            Vector3 newVertex = originalVertices[i] + dragAxis.normalized * delta * Mathf.Pow(weight, falloff);

            if (float.IsNaN(newVertex.x) || float.IsNaN(newVertex.y) || float.IsNaN(newVertex.z))
                continue;

            vertices[i] = newVertex;
        }

        mesh.vertices = vertices;
    }

    public void EndDrag()
    {
        isDragging = false;

        if (rend != null)
            rend.material.color = originalColor;

        if (mesh != null)
        {
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        if (uiText != null)
            uiText.text = "";
    }

    // ----------------------------
    // Metodi opzionali per evidenziare all'hover
    // ----------------------------
    void OnMouseEnter()
    {
        if (rend != null && !isDragging)
            rend.material.color = highlightColor;

        if (uiText != null && !isDragging)
            uiText.text = structureName;
    }

    void OnMouseExit()
    {
        if (!isDragging && rend != null)
            rend.material.color = originalColor;

        if (uiText != null && !isDragging)
            uiText.text = "";
    }
}