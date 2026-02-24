using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.IO;
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
    // Con scala 100, potresti aver bisogno di una forza molto più alta
    public float forceMultiplier = 1f; 

    public float pitch = 0.02f;
    public float dt = 0.001f;
    public float youngsModulus = 5000.0f;
    public float poissonRatio = 0.45f;
    public float density = 1000.0f;

    // Variabili di stato per la collisione
    private bool isTouched = false;
    private Vector3 currentForceVector = Vector3.zero;
    private Vector3 currentContactPoint = Vector3.zero;

    private MeshFilter mf;
    private Mesh mesh;
    private Vector3[] vertices;
    private int vertexCount;

    private TcpClient client;
    private NetworkStream stream;
    private BinaryReader reader;
    private BinaryWriter writer;
    private byte[] receiveBuffer;

    private bool isDragging = false;

    void Start()
    {
        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            mesh = mf.mesh; 
            mesh.MarkDynamic(); 
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

           // Debug.Log($"[UNITY] Invio {localVerts.Length} vertici. Primo vertice (ID:0): {localVerts[0]}");

            foreach (Vector3 v in localVerts)
            {
                // Converte da locale (0-1) a mondo (0-100+)
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
            //Debug.Log($"Mesh inviata. Scala rilevata:{transform.lossyScale}");
        } catch (Exception e) { Debug.LogError($"Errore invio mesh: {e.Message}"); }
    }

    void Update()
    {
        if (client == null || !client.Connected) return;

        /*
        if (Input.GetMouseButtonDown(0)) isDragging = true;
        if (Input.GetMouseButtonUp(0)) isDragging = false;
        */

        SendForceInput();
        ReceiveMeshUpdate();

        // Reset dello stato a fine frame: se il dito non è più in collisione, smette di applicare forza
        isTouched = false;
    }

    // Viene chiamato automaticamente da Unity finché il dito (con Rigidbody) tocca il muscolo
    void OnCollisionStay(Collision collision)
    {
        // 1. Estraiamo il punto esatto di contatto nello spazio 3D
        ContactPoint contact = collision.GetContact(0);
        currentContactPoint = contact.point;

        // 2. Calcoliamo la DIREZIONE della forza.
        // La "normal" è il vettore che esce perpendicolare dal muscolo. 
        // Il dito spinge nella direzione opposta (-normal).
        Vector3 pushDirection = contact.normal;

        // 3. Calcoliamo la FORZA (Magnitudo).
        // Usiamo la velocità di impatto o la massa del dito (se ha un Rigidbody dinamico).
        // Se il dito è cinematico, possiamo usare un valore fisso moltiplicato per quanto affonda.
        float pushForce = 10.0f; 
        if (collision.rigidbody != null) {
            // Se il dito si muove velocemente, applicherà più forza
            pushForce = collision.relativeVelocity.magnitude + 0.1f; 
        }

        // Vettore finale: Direzione * Forza * Moltiplicatore
        currentForceVector = pushDirection * (pushForce * forceMultiplier);
        
        isTouched = true;

        // Debug visivo nell'editor di Unity (Disegna una linea rossa che mostra come spinge il dito)
        //Debug.DrawRay(currentContactPoint, currentForceVector, Color.red);
    }



    void SendForceInput()
    {
        //Vector3 targetForce = Vector3.zero;

        Vector3 forceToSend = isTouched ? currentForceVector : Vector3.zero;
        Vector3 posToSend = isTouched ? currentContactPoint : Vector3.zero;

        /*
        if (isDragging)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            // Forza calcolata in base alla telecamera (World Space)
            targetForce = (Camera.main.transform.right * mouseX + Camera.main.transform.up * mouseY) * forceMultiplier;
            
            // Debug visivo della forza nel mondo
            Debug.DrawRay(transform.position, targetForce, Color.red);
        }*/
        
        try {
            /*
            writer.Write(targetForce.x);
            writer.Write(targetForce.y);
            writer.Write(targetForce.z);*/

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
                    // Torna allo spazio locale (divide per 100 internamente)
                    //Debug.Log($"[UNITY] Ricevuto aggiornamento. Primo vertice mondiale (ID:0): {worldPos}");
                    vertices[i] = transform.InverseTransformPoint(worldPos); 
                }
            }
            mesh.vertices = vertices;


            // Aggiorniamo anche il collider affinché il dito possa scivolare lungo la nuova curva del muscolo!
            if (GetComponent<MeshCollider>() != null) {
                GetComponent<MeshCollider>().sharedMesh = mesh;
            }

            mesh.RecalculateBounds();
            mesh.RecalculateNormals(); // Utile per l'illuminazione
        } catch {}
    }




}