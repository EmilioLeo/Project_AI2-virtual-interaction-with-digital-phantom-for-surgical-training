using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.IO;
using System;
/*
[RequireComponent(typeof(MeshFilter), typeof(Renderer), typeof(Collider))]
public class RetractMuscle : MonoBehaviour
{
    [Header("Settings")]
    public string structureName;
    public Color highlightColor = Color.green;

    [Header("UI Reference")]
    public TMP_Text uiText;

    [Header("Elastic Deformation")]
    public Vector3 dragAxis = Vector3.right; // Asse di deformazione
    public float dragSensitivity = 0.01f;    // Sensibilità del mouse
    public float maxOffset = 0.02f;          // Massimo spostamento dei vertici
    public float falloff = 1.5f;             // Decadimento dell’effetto verso estremi

    private MeshFilter mf;
    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] vertices;

    private Renderer rend;
    private Color originalColor;

    private bool isDragging = false;
    private Vector3 dragStartWorld;

    void Start()
    {
        rend = GetComponent<Renderer>();
        originalColor = rend.material.color;

        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            // Copia della mesh runtime per poterla deformare
            mesh = Instantiate(mf.mesh);
            mf.mesh = mesh;

            originalVertices = mesh.vertices;
            vertices = new Vector3[originalVertices.Length];
            System.Array.Copy(originalVertices, vertices, originalVertices.Length);
        }
        else
        {
            Debug.LogError("MeshFilter mancante su " + gameObject.name);
        }
    }

    void OnMouseEnter()
    {
        if (rend != null)
            rend.material.color = highlightColor;

    }

    void OnMouseExit()
    {
        if (!isDragging && rend != null)
            rend.material.color = originalColor;

    }

    void OnMouseDown()
    {
        isDragging = true;
        dragStartWorld = GetMouseWorldPosition();

        if (uiText != null)
        {
            uiText.gameObject.SetActive(true);
            uiText.text = structureName; // Mostra il nome solo al click
        }
    }

    void OnMouseDrag()
    {
        if (mesh == null) return;

        Vector3 currentMouseWorld = GetMouseWorldPosition();
        float delta = (currentMouseWorld - dragStartWorld).x * dragSensitivity;
        delta = Mathf.Clamp(delta, -maxOffset, maxOffset);

        for (int i = 0; i < vertices.Length; i++)
        {
            float t = Mathf.InverseLerp(mesh.bounds.min.y, mesh.bounds.max.y, originalVertices[i].y);
            float weight = Mathf.Sin(t * Mathf.PI);
            vertices[i] = originalVertices[i] + dragAxis.normalized * delta * Mathf.Pow(weight, falloff);
        }

        mesh.vertices = vertices;
    }

    void OnMouseUp()
    {
        isDragging = false;

        if (rend != null)
            rend.material.color = originalColor;

        if (mesh != null)
        {
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        if (uiText != null)
        {
            uiText.text = "";
            uiText.gameObject.SetActive(false); // Nascondi il testo al rilascio del click
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(mousePos);
    }
    
}

*/




[RequireComponent(typeof(MeshFilter), typeof(Renderer), typeof(Collider))]
public class RetractMusclePython : MonoBehaviour
{
    [Header("Settings")]
    public string structureName;
    public Color highlightColor = Color.green;

    [Header("Network Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 65432;
    public bool connectOnStart = true;

    [Header("Physics Interaction")]
    public float forceMultiplier = 10.0f; // Amplifica il movimento del mouse

    [Header("UI Reference")]
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
    private Vector3 currentForceVector;
    private Vector3 forceToSend = Vector3.zero;

    void Start()
    {
        rend = GetComponent<Renderer>();
        originalColor = rend.material.color;

        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            // Importante: La mesh deve essere segnata come "Read/Write Enabled" nelle import settings
            mesh = mf.mesh; 
            
            // Inizializza array vertici
            vertexCount = mesh.vertexCount;
            Debug.Log($"UNITY: Mesh caricata con {vertexCount} vertici.");
            Debug.Log($"UNITY: Mi aspetto pacchetti da {vertexCount * 3 * 4} bytes.");
            vertices = new Vector3[vertexCount];
            
            // Buffer per ricevere i dati (3 float per vertice * 4 bytes per float)
            receiveBuffer = new byte[vertexCount * 3 * 4];
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
            client.NoDelay = true; // Riduce la latenza
            stream = client.GetStream();
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
            Debug.Log("Connesso al Server Python di Fisica.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Impossibile connettersi al server: {e.Message}. Assicurati che lo script Python sia in esecuzione.");
        }
    }

    void Update()
    {
        if (client == null || !client.Connected) return;

        // 1. Invia Input (Forza)
        SendForceInput();

        // 2. Ricevi e Aggiorna Mesh
        ReceiveMeshUpdate();
    }

    void SendForceInput()
    {
        // Se stiamo trascinando, calcola forza, altrimenti 0
       
        Vector3 targetForce=Vector3.zero;
        float smoothSpeed = 10.0f;
        float maxForce=10.0f;

        if (isDragging)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            // Crea un vettore forza basato sul movimento del mouse rispetto alla camera
            Vector3 cameraRight = Camera.main.transform.right;
            Vector3 cameraUp = Camera.main.transform.up;
            
            Vector3 rawForce = (cameraRight * mouseX + cameraUp * mouseY) * forceMultiplier;
            //Vector3.ClampMagnitude(rawForce, maxForce);
            targetForce = rawForce;
           
        }
        
        forceToSend = Vector3.Lerp(forceToSend, targetForce, Time.deltaTime * smoothSpeed);
        
        if(forceToSend.magnitude < 0.01f) forceToSend = Vector3.zero;
        //forceToSend = smoothedForce;
        try
        {
            // Invia 3 float (x, y, z) - Little Endian standard
            writer.Write(forceToSend.x);
            writer.Write(forceToSend.y);
            writer.Write(forceToSend.z);
            writer.Flush(); // Assicura l'invio immediato
        }
        catch (Exception e)
        {
            Debug.LogError("Errore invio dati: " + e.Message);
        }
    }

    void ReceiveMeshUpdate()
    {
        try{
        if (!stream.DataAvailable) return;

            // 1. LEGGI L'HEADER (4 Byte che indicano la dimensione)
            byte[] sizeHeader = new byte[4];
            int headerRead = 0;
            while (headerRead < 4)
            {
                int r = stream.Read(sizeHeader, headerRead, 4 - headerRead);
                if (r == 0) return; // Disconnesso
                headerRead += r;
            }
            int payloadSize = BitConverter.ToInt32(sizeHeader, 0);

            // 2. CONTROLLO DI SICUREZZA (Anti-Blocco)
            // Calcoliamo quanto si aspetta Unity
            int expectedSize = vertexCount * 3 * 4;

            // Se il buffer non è abbastanza grande, lo ridimensioniamo
            if (receiveBuffer.Length < payloadSize) receiveBuffer = new byte[payloadSize];

            // 3. LEGGI IL PAYLOAD (Tutto ciò che Python ha mandato)
            // Leggiamo ESATTAMENTE 'payloadSize', non 'expectedSize'. 
            int dataRead = 0;
            while (dataRead < payloadSize)
            {
                int r = stream.Read(receiveBuffer, dataRead, payloadSize - dataRead);
                if (r == 0) return;
                dataRead += r;
            }

            // we use using so we can free the memory instead of br.Dispose() 
            using (MemoryStream ms = new MemoryStream(receiveBuffer))
            using (BinaryReader br = new BinaryReader(ms))
            {
                // Iteriamo sul numero di vertici ricevuti
                for (int i = 0; i < vertexCount; i++)
                {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    vertices[i] = new Vector3(-x, z, y);
                }
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();

        }catch (Exception e)
        {
            Debug.LogError("Errore ricezione mesh: " + e.Message);
        }
        /*try
        {
            // Python invia un array di byte lungo esattamente quanto serve per tutti i vertici
            int bytesExpected = receiveBuffer.Length;
            int bytesRead = 0;

            // Leggi finché non riempiamo il buffer (potrebbe arrivare frammentato)
            while (bytesRead < bytesExpected)
            {
                int read = stream.Read(receiveBuffer, bytesRead, bytesExpected - bytesRead);
                if (read == 0) return; // Disconnesso
                bytesRead += read;
            }

            // Converti bytes in Vector3
            // Usiamo Buffer.BlockCopy o unsafe code per velocità, qui usiamo un approccio sicuro
            using (MemoryStream ms = new MemoryStream(receiveBuffer))
            using (BinaryReader br = new BinaryReader(ms))
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    vertices[i] = new Vector3(x, y, z);
                }
            }

            // Applica alla Unity Mesh
            mesh.vertices = vertices;
            
            // Recalculate è costoso, fallo solo se necessario o ogni N frame
            // mesh.RecalculateNormals(); 
            mesh.RecalculateBounds();
        }
        catch (Exception e)
        {
            Debug.LogError("Errore ricezione mesh: " + e.Message);
        }*/
    }

    // --- Gestione Mouse (Invariata o adattata) ---

    void OnMouseDown()
    {
        isDragging = true;
        if (uiText != null)
        {
            uiText.gameObject.SetActive(true);
            uiText.text = structureName;
        }
    }

    void OnMouseUp()
    {
        isDragging = false;
        if (uiText != null)
        {
            uiText.text = "";
            uiText.gameObject.SetActive(false);
        }
    }

    void OnMouseEnter()
    {
        if (rend != null) rend.material.color = highlightColor;
    }

    void OnMouseExit()
    {
        if (!isDragging && rend != null) rend.material.color = originalColor;
    }

    void OnApplicationQuit()
    {
        if (client != null) client.Close();
    }
}