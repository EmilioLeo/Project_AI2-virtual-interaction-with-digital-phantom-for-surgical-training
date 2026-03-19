using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deformation_muscle_general : MonoBehaviour
{
   [Header("Softness")]
    public float force = 0.02f;      // force to retract anatomical component
    public float radius = 0.15f;     //radius of deformation area
    public float restoreSpeed = 5f;  //  velocity with muscle returns normal
    private int vertexCount;
    MeshFilter mf;
    Mesh mesh;
    Vector3[] originalVertices;
    Vector3[] displacedVertices;
    private Vector3[] vertices;
    
    Transform presser; 

    void Start()
    {
        mf = GetComponent<MeshFilter>();
        // Clone mesh to avoid breaking the original
        mesh = Instantiate(mf.mesh);
        mf.mesh = mesh;
        vertexCount = mesh.vertexCount;
        vertices = new Vector3[vertexCount];
        originalVertices = mesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
        System.Array.Copy(originalVertices, displacedVertices, originalVertices.Length);
        
        // Component should be collider with is trigger fixed
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (presser != null)
        {
            Deform();
        }

    }

    // when the finger get into muscle
    void OnTriggerEnter(Collider other)
    {
        //control kind of finger 
        if (other.gameObject.name.Contains("Thimble") || other.gameObject.name.Contains("Index"))
        {
            presser = other.transform;
        }
    }

    //when the finger get out
    void OnTriggerExit(Collider other)
    {
        if (presser != null && other.transform == presser)
        {
            presser = null;
        }
    }

    void Deform()
    {
        // We convert the position of the finger into the local space of the muscle
        Vector3 localPoint = transform.InverseTransformPoint(presser.position);

        for (int i = 0; i < displacedVertices.Length; i++)
        {
            // Distance between the vertex and the finger
            float distance = Vector3.Distance(originalVertices[i], localPoint);

            if (distance < radius)
            {
                // Calculate how much to warp (closer = more warping)
                float deformation = force * (radius - distance) / radius;
                
                // Deformation direction: from the finger towards the vertex (or fixed inwards)
                // Here we push the vertex "in" along the inverse normal or away from the finger
                Vector3 pushDir = (originalVertices[i] - localPoint).normalized;
               

                Vector3 targetPos = originalVertices[i] + (pushDir * deformation);
                
                // Apply smooth motion
                displacedVertices[i] = Vector3.Lerp(displacedVertices[i], targetPos, Time.deltaTime * 10f);
            }
        }
        
        mesh.vertices = displacedVertices;
        mesh.RecalculateNormals(); // Update the highlights/shadows
    }

    public void ResetMeshCarotides()
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
