using UnityEngine;
//using System.Collections;
public class Isolation_musculi_longus : MonoBehaviour
{
    //Fixed max distance tra pos del mouse e pos del vertex
    private float max_Radius = 35f;

    // La forza con cui i vertici della mesh vengono spinti
    private float pushForce = 20f;

    // La velocità  con cui il mesh ritorna alla posizione originale
    private float returnSpeed = 1.0f;
    
    //salva oggetto
    public GameObject flex_spinal;
    private Mesh mesh_flex_spinal;
    
    //salva original vertex della mesh dell'oggetto 
    private Vector3[] init_Vertices;
    
    //salva current vertex "modified" della mesh dell'oggetto
    private Vector3[] currentVertices;

    //check se mouse è sopra all'oggetto :) 
    private bool isMouseOver = false;

    // La posizione del mouse nello spazio 3D
    private Vector3 mouseWorldPosition;

    //camera che osserva se è stato selezionato un oggetto 
    Camera cam;
    
    void Start()
    {
       cam = Camera.main;
      
       //take mesh
       MeshFilter meshFilter = flex_spinal.GetComponent<MeshFilter>();
       if (meshFilter!=null)
       {
            mesh_flex_spinal=meshFilter.mesh;

            init_Vertices = mesh_flex_spinal.vertices;
            currentVertices = new Vector3[init_Vertices.Length]; 

            //copy init state della mesh
            init_Vertices.CopyTo(currentVertices, 0);
       }
       
    }
    void UpdateMouseWorldPosition()
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
                //Debug.Log("position mouse: "+mouseWorldPosition);
            }
        }else{
            Debug.Log("muscolo non intercettato ");
        }
    }
    void DeformMesh()
    {
        for (int i = 0; i < init_Vertices.Length; i++)
        {
            //Debug.Log("DEFORm");
            //trasformiamo in World Frames (RF0)
            Vector3 vertexWorldPos = transform.TransformPoint(init_Vertices[i]);
            
            //dist tra vertice e pos del mouse
            float distance = Vector3.Distance(vertexWorldPos, mouseWorldPosition);
            //Debug.Log("distance:"+distance);
            //confronto la dist dall'applicazione del mouse sia maggiore di threshold max radius dell' i-esimo punto 
            if (distance < max_Radius)
            {
                //TODO: apply falloff
                //float falloff = 1f - (distance / max_Radius);
                //calcola direzione in cui applicare la forza
                Vector3 direction = (vertexWorldPos - mouseWorldPosition).normalized;
                
                //update la posizione dell'iesimo vertice data la direzione e la forza applicata 
                currentVertices[i] = init_Vertices[i] + (direction * pushForce);
                //Debug.Log("posso deformare new vertex:"+currentVertices[i]);
            }
            else
            {   
                currentVertices[i] = init_Vertices[i];
            }
           
           
        }

        //aggiorna i vertici correnti a quelli della mesh
        mesh_flex_spinal.vertices=currentVertices;
        //ricalcola le normali rispetto alle facce dell'oggetto in modo tale che possa essere illuminato correttamente nella simulazione
        mesh_flex_spinal.RecalculateNormals();
    }

    void ReturnToOriginalPosition()
    {
            for (int i = 0; i < currentVertices.Length; i++)
            {
            //tramite interpolazione lineare ritorno alle coordinate originali con un tempo t *returnSpeed
            currentVertices[i] = Vector3.Lerp(currentVertices[i], init_Vertices[i], Time.deltaTime * returnSpeed);
            }
            mesh_flex_spinal.vertices=currentVertices;
            mesh_flex_spinal.RecalculateNormals();
    }

    // Rileva quando il mouse entra o esce dal collider dell'oggetto
    void OnMouseEnter()
    {
        isMouseOver = true;
    }

    void OnMouseExit()
    {
        isMouseOver = false;
    }

    void Update()
    {

        if (Input.GetMouseButton(0))
        {
            // Aggiorna la posizione del mouse nel mondo 3D e applica la deformazione
            UpdateMouseWorldPosition();
            DeformMesh();
        }
        else if (Input.GetMouseButton(1))
        {
            // Quando il mouse non è cliccato sull'oggetto, ritorna gradualmente alla posizione originale
            ReturnToOriginalPosition();
        }
    }

    

    

}
