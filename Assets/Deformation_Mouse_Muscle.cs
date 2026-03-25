using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.IO;
using System.Collections;
using System;

[RequireComponent(typeof(MeshFilter), typeof(Renderer), typeof(Collider))]
public class RetractMusclePython : MonoBehaviour
{
    [Header("Settings")]
    public string structureName;

    [Header("Network Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 65432;

    [Header("Physics Interaction")]
    
    public float forceMultiplier = 1f; 

    // physical variables for muscle deformation
    public float pitch = 0.02f;
    public float dt = 0.001f;
    public float youngsModulus = 5000.0f;
    public float poissonRatio = 0.45f;
    public float density = 1000.0f;

    // state variables for collision
    private bool isTouched = false;
    private Vector3 currentForceVector = Vector3.zero;
    private Vector3 currentContactPoint = Vector3.zero;

    //control flag for oscillation
    private bool isResetting = false;

    private MeshFilter mf;
    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] originalVertices;
    private int vertexCount;

    private TcpClient client;
    private NetworkStream stream;
    private BinaryReader reader;
    private BinaryWriter writer;
    private byte[] receiveBuffer;

    private bool isDragging = false;
    System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
    void Start()
    {
        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            mesh = mf.mesh; 
            mesh.MarkDynamic(); 
            originalVertices=mesh.vertices;
            vertexCount = mesh.vertexCount;
            vertices = new Vector3[vertexCount];
            receiveBuffer = new byte[vertexCount * 3 * 4];
        }

        ConnectToServer();
    }

    void ConnectToServer()
    {
        try {
            client = new TcpClient(serverIP, serverPort);
            client.NoDelay = true;
            stream = client.GetStream();
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
            SendMeshToServer();
        } catch (Exception e) { Debug.LogError($"Errore: {e.Message}"); }
    }

    void SendMeshToServer()
    {
        if (mesh == null) return;
        try {
            writer.Write(mesh.vertexCount);
            Vector3[] localVerts = mesh.vertices;

           

            foreach (Vector3 v in localVerts)
            {
                // Converts from local (0-1) to world (0-100+)
                Vector3 worldV = transform.TransformPoint(v); 
                writer.Write(worldV.x);
                writer.Write(worldV.y);
                writer.Write(worldV.z);
            }
            int[] triangles = mesh.triangles;
            writer.Write(triangles.Length);
            foreach (int idx in triangles) writer.Write(idx);


            //mesh pitch,youngs,poissonration,density
            writer.Write(pitch);
            writer.Write(dt);
            writer.Write(youngsModulus);
            writer.Write(poissonRatio);
            writer.Write(density);


            writer.Flush();
            
        } catch (Exception e) { Debug.LogError($"Errore invio mesh: {e.Message}"); }
    }

    void Update()
    {
        if (client == null || !client.Connected) return;

        // start to compute RTT
        stopwatch.Restart();
        
        SendForceInput();
        ReceiveMeshUpdate();
        
        //finish to compute RTT
        stopwatch.Stop();
        float rtt_ms = stopwatch.ElapsedMilliseconds;
        Debug.Log($"Round Trip Time: {rtt_ms} ms");

        // State reset at end of frame: if the finger is no longer colliding, it stops applying force
        isTouched = false;
    }

    // Automatically called by Unity as long as the finger (with Rigidbody) touches the muscle
    void OnCollisionStay(Collision collision)
    {
        // We extract the exact contact point in 3D space
        ContactPoint contact = collision.GetContact(0);
        currentContactPoint = contact.point;

        // We calculate the DIRECTION of the force.
        // The "normal" is the vector that comes out perpendicular to the muscle. 
        // The finger pushes in the opposite direction (-normal).
        Vector3 pushDirection = contact.normal;

        // Let's calculate the FORCE (Magnitude).
        // We use the impact velocity or the mass of the finger (if it has a dynamic Rigidbody).
        // If the finger is kinematic, we can use a fixed value multiplied by how much it sinks.
        float pushForce = 10.0f; 
        if (collision.rigidbody != null) {
            // If the finger moves quickly, it will apply more force
            pushForce = collision.relativeVelocity.magnitude + 0.1f; 
        }

        // Final vector: Direction * Force * Multiplier
        currentForceVector = pushDirection * (pushForce * forceMultiplier);
        
        isTouched = true;

    }



    void SendForceInput()
    {
        

        Vector3 forceToSend = isTouched ? currentForceVector : Vector3.zero;
        Vector3 posToSend = isTouched ? currentContactPoint : Vector3.zero;

        try {

            writer.Write(forceToSend.x);
            writer.Write(forceToSend.y);
            writer.Write(0);
            
            writer.Write(posToSend.x);
            writer.Write(posToSend.y);
            writer.Write(0);


            writer.Flush();
        } catch {}
    }

    void ReceiveMeshUpdate()
    {
        if (!stream.DataAvailable) return;
        try {
            int payloadSize = reader.ReadInt32();
            if (receiveBuffer.Length < payloadSize) receiveBuffer = new byte[payloadSize];
            
            int dataRead = 0;
            while(dataRead < payloadSize) 
                dataRead += stream.Read(receiveBuffer, dataRead, payloadSize - dataRead);

            using (MemoryStream ms = new MemoryStream(receiveBuffer))
            using (BinaryReader br = new BinaryReader(ms))
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    Vector3 worldPos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                    //updated vertices
                    vertices[i] = transform.InverseTransformPoint(worldPos); 
                }
            }
            mesh.vertices = vertices;


            // We also update the collider so that your finger can slide along the new curve of the muscle!
            if (GetComponent<MeshCollider>() != null) {
                GetComponent<MeshCollider>().sharedMesh = mesh;
            }

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        } catch {}
    }
    /*
    public void ResetMesh()
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = originalVertices[i];
        }

        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }*/


    public void StartResetAnimation()
    {
        if (!isResetting)
        {
            StartCoroutine(ResetMesh());
        }
    }


    public IEnumerator ResetMesh()
    {
        float duration = 2f;
        float elapsed = 0f;
        isResetting = true;
        Vector3[] startVertices = (Vector3[])vertices.Clone();
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            

            // Effetto molla
            float oscillation = Mathf.Sin(t * Mathf.PI * 6) * Mathf.Exp(-3 * t);
            float smoothT = Mathf.Clamp01(t + oscillation * 0.2f);

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Lerp(startVertices[i], originalVertices[i], smoothT);
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();

            yield return null;
        }


            mesh.vertices = originalVertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            isResetting = false;
    }




}