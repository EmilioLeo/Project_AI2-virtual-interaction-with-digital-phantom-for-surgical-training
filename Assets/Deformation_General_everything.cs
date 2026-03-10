using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(Collider))]
public class SoftTissueDeformer : MonoBehaviour
{
    [Header("Morbidezza")]
    public float force = 0.02f;      // Quanto va a fondo il dito
    public float radius = 0.15f;     // Quanto è larga l'area che si deforma
    public float restoreSpeed = 5f;  // Velocità con cui il muscolo torna normale

    MeshFilter mf;
    Mesh mesh;
    Vector3[] originalVertices;
    Vector3[] displacedVertices;
    private Vector3[] vertices;
    // Per gestire il dito Weart
    Transform presser; 
    private int vertexCount;
    void Start()
    {
        mf = GetComponent<MeshFilter>();
        // Clona la mesh per non rompere l'originale
        mesh = Instantiate(mf.mesh);
        mf.mesh = mesh;
        
        originalVertices = mesh.vertices;
        vertexCount = mesh.vertexCount;
        vertices = new Vector3[vertexCount];
        displacedVertices = new Vector3[originalVertices.Length];
        System.Array.Copy(originalVertices, displacedVertices, originalVertices.Length);
        
        // Assicuriamoci che il collider sia Trigger per permettere la "penetrazione"
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (presser != null)
        {
            Deform();
        }

    }

    // Quando il dito entra nel muscolo
    void OnTriggerEnter(Collider other)
    {
        // Controlla se è il dito (Thimble) o la mano
        if (other.gameObject.name.Contains("Thimble") || other.gameObject.name.Contains("Index"))
        {
            presser = other.transform;
        }
    }

    // Quando il dito esce
    void OnTriggerExit(Collider other)
    {
        if (presser != null && other.transform == presser)
        {
            presser = null;
        }
    }

    void Deform()
    {
        // Convertiamo la posizione del dito nello spazio locale del muscolo
        Vector3 localPoint = transform.InverseTransformPoint(presser.position);

        for (int i = 0; i < displacedVertices.Length; i++)
        {
            // Distanza tra il vertice e il dito
            float distance = Vector3.Distance(originalVertices[i], localPoint);

            if (distance < radius)
            {
                // Calcola quanto deformare (più vicino = più deformazione)
                // Usiamo una curva gaussiana per renderlo morbido
                float deformation = force * (radius - distance) / radius;
                
                // Direzione della deformazione: dal dito verso il vertice (o fissa verso l'interno)
                // Qui spingiamo il vertice "dentro" lungo la normale inversa o via dal dito
                Vector3 pushDir = (originalVertices[i] - localPoint).normalized;
                
                // Opzione B: Spingi sempre verso l'interno rispetto alla normale (più realistico per i muscoli)
                // Vector3 pushDir = -mesh.normals[i]; 

                Vector3 targetPos = originalVertices[i] + (pushDir * deformation);
                
                // Applica movimento fluido
                displacedVertices[i] = Vector3.Lerp(displacedVertices[i], targetPos, Time.deltaTime * 10f);
            }
        }
        
        mesh.vertices = displacedVertices;
        mesh.RecalculateNormals(); // Aggiorna le luci/ombre
    }

    /*void RestoreShape()
    {
        // Se nessun dito tocca, torna lentamente alla forma originale
        bool isRestored = true;
        for (int i = 0; i < displacedVertices.Length; i++)
        {
            if (displacedVertices[i] != originalVertices[i])
            {
                displacedVertices[i] = Vector3.Lerp(displacedVertices[i], originalVertices[i], Time.deltaTime * restoreSpeed);
                
                // Ottimizzazione: se è quasi tornato, scatta alla fine
                if (Vector3.Distance(displacedVertices[i], originalVertices[i]) > 0.0001f)
                    isRestored = false;
            }
        }
        
        mesh.vertices = displacedVertices;
        if (!isRestored) mesh.RecalculateNormals();
    }*/
    public void ResetMeshArteriesVeins()
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = originalVertices[i];
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

}