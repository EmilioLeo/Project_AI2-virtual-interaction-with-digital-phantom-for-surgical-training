using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(Collider))]
public class DeformerFlexSpinal : MonoBehaviour
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
            Deform_flex_spinal();
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

    void Deform_flex_spinal()
{
    // Punto del dito in spazio locale
    Vector3 localPoint = transform.InverseTransformPoint(presser.position);

    // Determina lato attivo in base al punto di contatto
    int activeSide = (localPoint.x < 0f) ? -1 : +1;

    for (int i = 0; i < displacedVertices.Length; i++)
    {
        // Determina lato del vertice
        float vx = originalVertices[i].x;
        int vertSide = (vx < 0f) ? -1 : ((vx > 0f) ? +1 : 0);

        // Se il vertice non appartiene al lato toccato → non deformarlo
        if (vertSide == 0 || vertSide != activeSide)
        {
            //displacedVertices[i] = originalVertices[i];
            continue;
        }

        // Distanza tra vertice e dito
        float distance = Vector3.Distance(originalVertices[i], localPoint);

        if (distance < radius)
        {
            float deformation = force * (radius - distance) / radius;

            Vector3 pushDir = (originalVertices[i] - localPoint).normalized;

            Vector3 targetPos = originalVertices[i] + (pushDir * deformation);

            displacedVertices[i] = Vector3.Lerp(
                displacedVertices[i],
                targetPos,
                Time.deltaTime * 10f
            );
        }
        else
        {
            // Se fuori raggio, torna alla posizione originale
            /*displacedVertices[i] = Vector3.Lerp(
                displacedVertices[i],
                originalVertices[i],
                Time.deltaTime * 5f
            );*/
        }
    }

    mesh.vertices = displacedVertices;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();
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
}