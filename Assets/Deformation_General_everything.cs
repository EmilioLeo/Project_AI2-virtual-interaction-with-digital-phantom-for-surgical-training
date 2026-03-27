using UnityEngine;
using System.Collections;

[RequireComponent(typeof(MeshFilter), typeof(Collider))]
public class SoftTissueDeformer : MonoBehaviour
{
    [Header("Morbidezza")]
    public float force = 0.02f;      // Quanto va a fondo il dito
    public float radius = 0.15f;     // Quanto è larga l'area che si deforma
    public float restoreSpeed = 5f;
    public bool isResetting = false;  // Velocità con cui il muscolo torna normale

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
              
                float deformation = force * (radius - distance) / radius;
                
                
                Vector3 pushDir = (originalVertices[i] - localPoint).normalized;
                
               

                Vector3 targetPos = originalVertices[i] + (pushDir * deformation);
                
                // Applica movimento fluido
                displacedVertices[i] = Vector3.Lerp(displacedVertices[i], targetPos, Time.deltaTime * 10f);
            }
        }
        
        mesh.vertices = displacedVertices;
        mesh.RecalculateNormals(); // Aggiorna le luci/ombre
    }

   
    
    public void StartResetAnimationV()
    {
        if (!isResetting)
        {
            StartCoroutine(ResetMeshArteriesVeins());
        }
    }


    public IEnumerator ResetMeshArteriesVeins()
    {
        float duration = 2f;
        float elapsed = 0f;
        isResetting = true;
        Vector3[] startVertices = (Vector3[])displacedVertices.Clone();
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            

            // Effetto molla
            float oscillation = Mathf.Sin(t * Mathf.PI * 6) * Mathf.Exp(-3 * t);
            float smoothT = Mathf.Clamp01(t + oscillation * 0.2f);

            for (int i = 0; i < displacedVertices.Length; i++)
            {
                displacedVertices[i] = Vector3.Lerp(startVertices[i], originalVertices[i], smoothT);
            }

            mesh.vertices = displacedVertices;
            mesh.RecalculateNormals();

            yield return null;
        }


            mesh.vertices = originalVertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            isResetting = false;
    }

}