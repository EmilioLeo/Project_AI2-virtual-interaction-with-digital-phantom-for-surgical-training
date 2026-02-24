using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.IO;
using System;

// Alias per evitare conflitti
using Debug = UnityEngine.Debug; 

[RequireComponent(typeof(MeshFilter), typeof(Renderer), typeof(Collider))]
public class RetractMusclePython : MonoBehaviour
{
    // ... (Tutte le tue variabili Header rimangono uguali) ...
    [Header("Settings")]
    public string structureName;
    public Color highlightColor = Color.green;

    [Header("Network Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 65432;
    public bool connectOnStart = true;

    [Header("Physics Interaction")]
    public float forceMultiplier = 10.0f;
    public TMP_Text uiText;

    // Componenti Mesh
    private MeshFilter mf;
    private Mesh mesh;
    private Vector3[] vertices;
    private int vertexCount;

    // Network
    private TcpClient client;
    private NetworkStream stream;
    private BinaryReader reader;
    private BinaryWriter writer;
    private byte[] receiveBuffer;

    // Interaction State
    private Renderer rend;
    private Color originalColor;
    private bool isDragging = false;
    private Vector3 forceToSend = Vector3.zero;

    void Start()
    {
        rend = GetComponent<Renderer>();
        originalColor = rend.material.color;

        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            // Usiamo mesh condivisa o istanza? Meglio istanza per deformazione
            mesh = mf.mesh; 
            // Importante: segna mesh come Dynamic se la aggiorni spesso
            mesh.MarkDynamic(); 

            vertexCount = mesh.vertexCount;
            vertices = new Vector3[vertexCount];
            receiveBuffer = new byte[vertexCount * 3 * 4];
            
            Debug.Log($"UNITY: Mesh pronta. Vertici: {vertexCount}, Triangoli: {mesh.triangles.Length / 3}");
        }

        if (connectOnStart)
        {
            ConnectToServer();
        }
    }

    void ConnectToServer()
    {
        try
        {
            client = new TcpClient(serverIP, serverPort);
            client.NoDelay = true;
            stream = client.GetStream();
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
            Debug.Log("Connesso al Server Python.");

            // --- NUOVO: INVIA LA MESH A PYTHON ---
            SendMeshToServer();
        }
        catch (Exception e)
        {
            Debug.LogError($"Errore connessione: {e.Message}");
        }
    }

    // --- NUOVA FUNZIONE PER INVIARE LA GEOMETRIA ---
    void SendMeshToServer()
    {
        if (mesh == null) return;

        Debug.Log("Invio dati geometrici a Python...");

        try
        {
            // 1. Invia Numero Vertici (int)
            writer.Write(mesh.vertexCount);

            // 2. Invia Vertici (3 * float per vertice)
            // Nota: Inviamo coordinate LOCALI. Assicurati che l'oggetto sia a (0,0,0) o gestisci la world position.
            Vector3[] verts = mesh.vertices;
            foreach (Vector3 v in verts)
            {
                // Manteniamo il sistema coordinate di Unity per l'invio, Python si adatterà
                // Oppure convertiamo qui: new Vector3(-v.x, v.z, v.y) se serve.
                // Per ora inviamo RAW (x, y, z)
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
            }

            // 3. Invia Numero Indici Triangoli (int)
            int[] triangles = mesh.triangles;
            writer.Write(triangles.Length);

            // 4. Invia Indici (int per indice)
            foreach (int idx in triangles)
            {
                writer.Write(idx);
            }

            writer.Flush();
            Debug.Log("Geometria inviata con successo!");
        }
        catch (Exception e)
        {
            Debug.LogError($"Errore durante l'invio della mesh: {e.Message}");
        }
    }

    void Update()
    {
        if (client == null || !client.Connected) return;

        SendForceInput();
        ReceiveMeshUpdate();
    }

    // ... (SendForceInput e ReceiveMeshUpdate rimangono uguali al tuo codice precedente) ...
    // ... (Anche la gestione Mouse rimane uguale) ...
    
    // Assicurati che SendForceInput usi writer.Write(...) come nel tuo script funzionante
    void SendForceInput()
    {
        // Logica mouse...
        Vector3 targetForce = Vector3.zero;
        if (isDragging)
        {
             float mouseX = Input.GetAxis("Mouse X");
             float mouseY = Input.GetAxis("Mouse Y");
             Vector3 cameraRight = Camera.main.transform.right;
             Vector3 cameraUp = Camera.main.transform.up;
             targetForce = (cameraRight * mouseX + cameraUp * mouseY) * forceMultiplier;
        }
        else
        {
            targetForce = Vector3.zero;
        }
        
        forceToSend = Vector3.Lerp(forceToSend, targetForce, Time.deltaTime * 10f);
        if(forceToSend.magnitude < 0.01f) forceToSend = Vector3.zero;

        try {
            writer.Write(forceToSend.x);
            writer.Write(forceToSend.y);
            writer.Write(forceToSend.z);
            writer.Flush();
        } catch {}
    }

    void ReceiveMeshUpdate()
    {
        // Copia il contenuto del tuo ReceiveMeshUpdate precedente
        // Ricordati: se inviamo i dati RAW (x,y,z), Python potrebbe restituirli RAW.
        // Controlla se serve la conversione (-x, z, y) in base a come Python elabora.
        if (!stream.DataAvailable) return;
        
        // ... codice lettura header e payload ...
        // Esempio lettura semplificata per brevità:
        try {
            byte[] sizeHeader = new byte[4];
            int read = stream.Read(sizeHeader, 0, 4);
            if(read==0) return;
            int payloadSize = BitConverter.ToInt32(sizeHeader, 0);
            
            if (receiveBuffer.Length < payloadSize) receiveBuffer = new byte[payloadSize];
            
            int dataRead = 0;
            while(dataRead < payloadSize) {
                dataRead += stream.Read(receiveBuffer, dataRead, payloadSize - dataRead);
            }

            using (MemoryStream ms = new MemoryStream(receiveBuffer))
            using (BinaryReader br = new BinaryReader(ms))
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    // Se Python lavora 1:1, usa (x,y,z). Se Python inverte, usa (-x,z,y).
                    // Dato che inviamo la mesh da Unity, Python userà quel sistema.
                    // Quindi probabilmente qui basterà:
                    vertices[i] = new Vector3(x, y, z); 
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        } catch {}
    }
}