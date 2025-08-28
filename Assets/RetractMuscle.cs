using UnityEngine;
using TMPro;

[RequireComponent(typeof(MeshFilter), typeof(Renderer), typeof(Collider))]
public class RetractMuscle : MonoBehaviour
{
    [Header("Settings")]
    public string structureName;
    public Color highlightColor = Color.green;

    [Header("UI Reference")]
    public TMP_Text uiText;

    [Header("Elastic Deformation")]
    public Vector3 dragAxis = Vector3.right; // Asse di deformazione
    public float dragSensitivity = 0.01f;    // Sensibilità del mouse
    public float maxOffset = 0.02f;          // Massimo spostamento dei vertici
    public float falloff = 1.5f;             // Decadimento dell’effetto verso estremi

    private MeshFilter mf;
    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] vertices;

    private Renderer rend;
    private Color originalColor;

    private bool isDragging = false;
    private Vector3 dragStartWorld;

    void Start()
    {
        rend = GetComponent<Renderer>();
        originalColor = rend.material.color;

        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            // Copia della mesh runtime per poterla deformare
            mesh = Instantiate(mf.mesh);
            mf.mesh = mesh;

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

    }

    void OnMouseExit()
    {
        if (!isDragging && rend != null)
            rend.material.color = originalColor;

    }

    void OnMouseDown()
    {
        isDragging = true;
        dragStartWorld = GetMouseWorldPosition();

        if (uiText != null)
        {
            uiText.gameObject.SetActive(true);
            uiText.text = structureName; // Mostra il nome solo al click
        }
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
            vertices[i] = originalVertices[i] + dragAxis.normalized * delta * Mathf.Pow(weight, falloff);
        }

        mesh.vertices = vertices;
    }

    void OnMouseUp()
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
        {
            uiText.text = "";
            uiText.gameObject.SetActive(false); // Nascondi il testo al rilascio del click
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePos);
    }
    
}
