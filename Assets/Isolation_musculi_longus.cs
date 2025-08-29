using UnityEngine;
//using System.Collections;
public class Isolation_musculi_longus : MonoBehaviour
{
    //Fixed max distance tra pos del mouse e pos del vertex
    public Color highlightColor = Color.green;
    //max offset
    public float maxOffset = 10.5f; 
    private bool  isDragging=false;

    // La velocità  con cui il mesh ritorna alla posizione originale
    //private float returnSpeed = 1.0f;
    public Vector3 dragAxis = Vector3.right;  // Asse di deformazione
    public float dragSensitivity = 4f;    // Sensibilità del mouse
    
    public float falloff = 5.5f;
    //salva oggetto
    public GameObject flex_spinal;
    private Mesh mesh_flex_spinal;
    
    //salva original vertex della mesh dell'oggetto 
    private Vector3[] init_Vertices;
    private Vector3 dragStartWorld;
    //salva current vertex "modified" della mesh dell'oggetto
    private Vector3[] currentVertices;
    private Color material;

    //check se mouse è sopra all'oggetto :) 
    private bool isMouseOver = false;

    // La posizione del mouse nello spazio 3D
    private Vector3 mouseWorldPosition;
    private MeshFilter meshFilter;
    //camera che osserva se è stato selezionato un oggetto 
    Camera cam;
    
    void Start()
    {
       cam = Camera.main;
      
       //take mesh
       material=flex_spinal.GetComponent<Renderer>().material.color;
       meshFilter= flex_spinal.GetComponent<MeshFilter>();
       
       if (meshFilter!=null)
       {
            mesh_flex_spinal=meshFilter.mesh;

            init_Vertices = mesh_flex_spinal.vertices;
            currentVertices = new Vector3[init_Vertices.Length]; 

            //copy init state della mesh
            init_Vertices.CopyTo(currentVertices, 0);
       }
       else
        {
            Debug.LogError("MeshFilter mancante su " + flex_spinal.name);
        }
       
    }

    private Vector3 UpdateMouseWorldPosition()
    {
        // Crea un raggio dalla posizione della telecamera al punto del mouse sullo schermo
        //Debug.Log("Camera trovata: " + cam);
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // Se il raggio colpisce questo oggetto, aggiorna la posizione del mouse
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if(hit.collider.gameObject ==  flex_spinal)
            { 

                mouseWorldPosition = hit.point;
               
            }
        }
        return mouseWorldPosition;
        
    }

    void DeformMesh()
    {
        float delta =0;
        Vector3 currentMouseWorld = GetMouseWorldPosition();
        delta = (currentMouseWorld - dragStartWorld).x * dragSensitivity;
        delta = Mathf.Clamp(delta, -maxOffset, maxOffset);
    
        // 2) Determina quale metà è attiva in base al punto di inizio drag
        float dragStartLocalX = transform.InverseTransformPoint(dragStartWorld).x;
        int activeSide = (dragStartLocalX < 0f) ? -1 : +1; // -1 = sinistra, +1 = destra
        Vector3 axis=Vector3.right;

        
        // 3) Applica deformazione SOLO ai vertici della metà attiva
        float minY = mesh_flex_spinal.bounds.min.y;
        float maxY = mesh_flex_spinal.bounds.max.y;

        for (int i = 0; i < init_Vertices.Length; i++)
        {
            // di base: nessuna deformazione
            //currentVertices[i] = init_Vertices[i];

            // lato del vertice (sign di x)
            float vx = init_Vertices[i].x;
            int vertSide = (vx < 0f) ? -1 : ((vx > 0f) ? +1 : 0);

            // se il vertice NON appartiene alla metà attiva, salta
            if (vertSide == 0 || vertSide != activeSide) continue;

            // peso verticale (più forte a metà altezza)
            float t = Mathf.InverseLerp(minY, maxY, init_Vertices[i].y);
            float weight=0f;
            weight= Mathf.Sin(t * Mathf.PI);
            float w = Mathf.Pow(weight, falloff);
           
           
           
            // deformazione lungo l'asse della metà attiva
            currentVertices[i] = init_Vertices[i] + axis * delta * w;
        }
        
        mesh_flex_spinal.vertices=currentVertices;
   
    }

   

    // Rileva quando il mouse entra o esce dal collider dell'oggetto
    void OnMouseEnter()
    {
        if (flex_spinal.GetComponent<Renderer>() != null)
            flex_spinal.GetComponent<Renderer>().material.color = highlightColor;
        isMouseOver = true;
    }

    void OnMouseExit()
    {
        if (flex_spinal.GetComponent<Renderer>() != null)
            flex_spinal.GetComponent<Renderer>().material.color=material;
        isMouseOver = false;
    }
    void OnMouseDown()
    {
        isDragging = true;
        dragStartWorld = GetMouseWorldPosition();
    }
    void OnMouseDrag(){
        if (mesh_flex_spinal==null) return;
        DeformMesh();
    }
    void OnMouseUp()
    {
        isDragging = false;

        if (flex_spinal.GetComponent<Renderer>() != null)
            flex_spinal.GetComponent<Renderer>().material.color = material;

        if (mesh_flex_spinal != null)
        {
            mesh_flex_spinal.RecalculateBounds();
            mesh_flex_spinal.RecalculateNormals();
        }
    }
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePos);
    }



}
