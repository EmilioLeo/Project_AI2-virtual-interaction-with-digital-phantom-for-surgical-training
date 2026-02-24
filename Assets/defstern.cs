/*using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.IO;
using System;
using WeArt.Components; 
using WeArt.Core;       

[RequireComponent(typeof(MeshFilter), typeof(Renderer), typeof(MeshCollider))]
[RequireComponent(typeof(WeArtHapticObject))] 
public class TouchMuscleWeArt : MonoBehaviour
{
    [Header("Settings")]
    public string structureName = "Muscolo";
    public Color highlightColor = Color.green;

    [Header("Network Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 65432;
    public bool connectOnStart = true;

    [Header("Physics Interaction")]
    public Transform fingerTipTransform; // TRASCINA QUI IL TUO "FingerContactPoint"
    public float forceMultiplier = 80.0f; // Aumentato perché lo spostamento sarà piccolo
    
    [Header("Haptic Feedback")]
    [Range(0, 1)] public float maxHapticForce = 1.0f; 

    [Header("UI Reference")]
    public TMP_Text uiText;

    // --- Variabili Interne ---
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

    // Stato
    private Renderer rend;
    private Color originalColor;
    private WeArtHapticObject hapticObject;
    
    private bool isTouching = false;
    private Vector3 entryPoint; // Dove hai toccato il muscolo inizialmente
    private Vector3 forceToSend = Vector3.zero;

    void Start()
    {
        rend = GetComponent<Renderer>();
        hapticObject = GetComponent<WeArtHapticObject>(); 
        originalColor = rend.material.color;

        mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            // Clona la mesh per renderla dinamica
            mesh = Instantiate(mf.mesh);
            mf.mesh = mesh;
            vertexCount = mesh.vertexCount;
            vertices = new Vector3[vertexCount];
            receiveBuffer = new byte[vertexCount * 3 * 4];
        }

        if (connectOnStart) ConnectToServer();
    }

    void ConnectToServer()
    {
        try {
            client = new TcpClient(serverIP, serverPort);
            client.NoDelay = true; 
            stream = client.GetStream();
            reader = new BinaryReader(stream);
            writer = new BinaryWriter(stream);
            Debug.Log("TouchMuscle: Connesso al Server Python.");
        } catch (Exception e) { Debug.LogError($"Errore connessione: {e.Message}"); }
    }

    void Update()
    {
        // 1. Calcola la forza basata sul tocco
        CalculateTouchForce();

        // 2. Network Sync
        if (client != null && client.Connected)
        {
            SendForceInput();
            ReceiveMeshUpdate();
        }
    }

    void CalculateTouchForce()
    {
        if (fingerTipTransform == null) return;

        if (isTouching)
        {
            // La forza è la differenza tra dove sei entrato e dove sei ora
            // (Simula la penetrazione/spinta nel tessuto)
            Vector3 displacement = fingerTipTransform.position - entryPoint;
            
            // Applichiamo il moltiplicatore
            forceToSend = displacement * forceMultiplier;

            // --- FEEDBACK APTICO ---
            // Più spingi dentro, più vibra/resiste
            float pushDepth = displacement.magnitude;
            float hapticIntensity = Mathf.Clamp01(pushDepth * 20.0f); // Sensibilità tattile
            hapticObject.Force.Value = hapticIntensity * maxHapticForce;
            
            if (uiText != null) uiText.text = $"{structureName}\nForce: {forceToSend.magnitude:F1}";
        }
        else
        {
            // Se non tocchi, la forza torna gradualmente a zero
            forceToSend = Vector3.Lerp(forceToSend, Vector3.zero, Time.deltaTime * 5f);
            hapticObject.Force.Value = 0.0f;
        }
    }

    void SendForceInput()
    {
        try {
            writer.Write(forceToSend.x);
            writer.Write(forceToSend.y);
            writer.Write(forceToSend.z);
            writer.Flush();
        } catch (Exception) { }
    }

    void ReceiveMeshUpdate()
    {
        try {
            if (!stream.DataAvailable) return;

            // Header Size
            byte[] sizeHeader = new byte[4];
            int headerRead = 0;
            while (headerRead < 4) {
                int r = stream.Read(sizeHeader, headerRead, 4 - headerRead);
                if (r == 0) return; headerRead += r;
            }
            int payloadSize = BitConverter.ToInt32(sizeHeader, 0);

            // Payload Data
            if (receiveBuffer.Length < payloadSize) receiveBuffer = new byte[payloadSize];
            int dataRead = 0;
            while (dataRead < payloadSize) {
                int r = stream.Read(receiveBuffer, dataRead, payloadSize - dataRead);
                if (r == 0) return; dataRead += r;
            }

            // Update Mesh Vertices
            using (MemoryStream ms = new MemoryStream(receiveBuffer))
            using (BinaryReader br = new BinaryReader(ms)) {
                for (int i = 0; i < vertexCount; i++) {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    vertices[i] = new Vector3(-x, z, y); // Mapping assi
                }
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds(); 
        } catch (Exception) { }
    }

    // --- GESTIONE CONTATTO ---

    void OnTriggerEnter(Collider other)
    {
        // Se a entrare è il nostro dito configurato
        if (other.transform == fingerTipTransform)
        {
            isTouching = true;
            entryPoint = fingerTipTransform.position; // Salviamo il punto di ingresso
            
            if (rend != null) rend.material.color = highlightColor;
            Debug.Log("Contatto Muscolo Iniziato");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.transform == fingerTipTransform)
        {
            isTouching = false;
            
            if (rend != null) rend.material.color = originalColor;
            if (uiText != null) uiText.text = "";
            Debug.Log("Contatto Finito");
        }
    }

    void OnApplicationQuit() {
        if (client != null) client.Close();
    }
}
*/