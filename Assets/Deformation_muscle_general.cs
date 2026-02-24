using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deformation_muscle_general : MonoBehaviour
{
   [Header("Morbidezza")]
    public float force = 0.02f;      // Quanto va a fondo il dito
    public float radius = 0.15f;     // Quanto è larga l'area che si deforma
    public float restoreSpeed = 5f;  // Velocità con cui il muscolo torna normale

    MeshFilter mf;
    Mesh mesh;
    Vector3[] originalVertices;
    Vector3[] displacedVertices;
    
    // Per gestire il dito Weart
    Transform presser; 

    void Start()
    {
        mf = GetComponent<MeshFilter>();
        // Clona la mesh per non rompere l'originale
        mesh = Instantiate(mf.mesh);
        mf.mesh = mesh;
        
        originalVertices = mesh.vertices;
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

   

}
