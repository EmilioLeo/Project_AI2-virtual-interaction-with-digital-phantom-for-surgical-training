using UnityEngine;
using System.Collections;

[RequireComponent(typeof(MeshFilter), typeof(Collider))]
public class DeformerFlexSpinal : MonoBehaviour
{
    [Header("Softness")]
    public float force = 0.02f;      // force to retract anatomical component
    public float radius = 0.15f;     // radius of deformation area
    public float restoreSpeed = 5f;  //  velocity with muscle returns normal
    public bool isResetting = false;

    MeshFilter mf;
    private int vertexCount;
    Mesh mesh;
    Vector3[] originalVertices;
    Vector3[] displacedVertices;
    Vector3[] vertices;
    
    Transform presser; 

    void Start()
    {
        mf = GetComponent<MeshFilter>();
         // Clone mesh to avoid breaking the original
        mesh = Instantiate(mf.mesh);
        mf.mesh = mesh;
        
        originalVertices = mesh.vertices;
        vertexCount = mesh.vertexCount;
        vertices=new Vector3[vertexCount];
        displacedVertices = new Vector3[originalVertices.Length];
        System.Array.Copy(originalVertices, displacedVertices, originalVertices.Length);
        
         // Component should be collider with is trigger fixed
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (presser != null)
        {
            Deform_flex_spinal();
        }

    }

    // when the finger get into muscle
    void OnTriggerEnter(Collider other)
    {
        
        if (other.gameObject.name.Contains("Thimble") || other.gameObject.name.Contains("Index"))
        {
            presser = other.transform;
        }
    }

    // when the finger get out
    void OnTriggerExit(Collider other)
    {
        if (presser != null && other.transform == presser)
        {
            presser = null;
        }
    }

    void Deform_flex_spinal()
{
    // We convert the position of the finger into the local space of the muscle
    Vector3 localPoint = transform.InverseTransformPoint(presser.position);

    // Determine active side based on contact point
    int activeSide = (localPoint.x < 0f) ? -1 : +1;

    for (int i = 0; i < displacedVertices.Length; i++)
    {
        // Distance between the vertex and the finger
        float vx = originalVertices[i].x;
        int vertSide = (vx < 0f) ? -1 : ((vx > 0f) ? +1 : 0);

        // If the vertex does not belong to the touched side → don't deform it
        if (vertSide == 0 || vertSide != activeSide)
        {
            continue;
        }

        // Distance between vertex and finger
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
        
    }

    mesh.vertices = displacedVertices;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();
}

   /*public void ResetMeshflexspinal()
   {

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = originalVertices[i];
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

   }*/
   public void StartResetAnimationF()
    {
        if (!isResetting)
        {
            StartCoroutine(ResetMeshflexspinal());
        }
    }


    public IEnumerator ResetMeshflexspinal()
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