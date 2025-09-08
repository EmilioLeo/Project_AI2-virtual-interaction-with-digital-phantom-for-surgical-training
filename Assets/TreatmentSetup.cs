using UnityEngine;
using TMPro;

public class TreatmentSetup : MonoBehaviour
{
    [Header("Riferimenti oggetti in scena")]
    public GameObject[] vertebre;   // C1–C7
    public GameObject[] dischi;     // C1-2, C2-3, … C6-7

    [Header("Nomi personalizzati")]
    public string[] vertebreNomi =
        { "Vertebra C1", "Vertebra C2", "Vertebra C3",
          "Vertebra C4", "Vertebra C5", "Vertebra C6", "Vertebra C7" };

    public string[] dischiNomi =
        { "Disco Intervertebrale C1-2", "Disco Intervertebrale C2-3",
          "Disco Intervertebrale C3-4", "Disco Intervertebrale C4-5",
          "Disco Intervertebrale C5-6", "Disco Intervertebrale C6-7" };

    [Header("UI Messaggi")]
    public GameObject infoPanel;         // pannello azzurrino trasparente
    public TextMeshProUGUI infoText;     // testo sopra il pannello

    [Header("Materiali trasparenti")]
    public Material yellowTransparent; // colore vertebra selezionata
    public Material redTransparent;    // colore disco selezionato

    Camera cam;

    bool[] vertebreMarked;
    bool[] dischiMarked;

    Material[] originalVertebraMaterials;
    Material[] originalDischiMaterials;

    void Start()
    {
        cam = Camera.main;

        vertebreMarked = new bool[vertebre.Length];
        dischiMarked = new bool[dischi.Length];

        if (infoPanel != null)
            infoPanel.SetActive(false); // all’avvio pannello spento

        for (int i = 0; i < dischi.Length; i++)
        {
            // esempio: coloriamo già il disco C4-5
            if (i == 2 && dischi[i] != null)
            {
                dischi[i].GetComponent<Renderer>().material = redTransparent;
            }
        }

        // salva materiali originali vertebre
        originalVertebraMaterials = new Material[vertebre.Length];
        for (int i = 0; i < vertebre.Length; i++)
            originalVertebraMaterials[i] = vertebre[i].GetComponent<Renderer>().material;

        // salva materiali originali dischi
        originalDischiMaterials = new Material[dischi.Length];
        for (int i = 0; i < dischi.Length; i++)
            originalDischiMaterials[i] = dischi[i].GetComponent<Renderer>().material;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // click sinistro → selezione
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // --- VERTEBRE ---
                for (int i = 0; i < vertebre.Length; i++)
                {
                    if (vertebre[i] != null && !vertebreMarked[i] && hit.collider.gameObject == vertebre[i])
                    {
                        vertebre[i].GetComponent<Renderer>().material = yellowTransparent;
                        vertebreMarked[i] = true;
                        ShowMessage($"{vertebreNomi[i]}");
                    }
                }

                // --- DISCHI ---
                for (int i = 0; i < dischi.Length; i++)
                {
                    if (dischi[i] != null && !dischiMarked[i] && hit.collider.gameObject == dischi[i])
                    {
                        dischi[i].GetComponent<Renderer>().material = redTransparent;
                        dischiMarked[i] = true;
                        ShowMessage($"{dischiNomi[i]}");
                    }
                }
            }
        }
        else if (Input.GetMouseButtonDown(1)) // click destro → deselezione
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // --- VERTEBRE ---
                for (int i = 0; i < vertebre.Length; i++)
                {
                    if (vertebre[i] != null && hit.collider.gameObject == vertebre[i] && vertebreMarked[i])
                    {
                        vertebre[i].GetComponent<Renderer>().material = originalVertebraMaterials[i];
                        vertebreMarked[i] = false;
                        HideMessage();
                    }
                }

                // --- DISCHI ---
                for (int i = 0; i < dischi.Length; i++)
                {
                    if (dischi[i] != null && hit.collider.gameObject == dischi[i] && dischiMarked[i])
                    {
                        dischi[i].GetComponent<Renderer>().material = originalDischiMaterials[i];
                        dischiMarked[i] = false;
                        HideMessage();
                    }
                }
            }
        }
    }

    // --------------------------
    // Gestione pannello messaggi
    // --------------------------
    public void ShowMessage(string msg)
    {
        if (infoPanel == null || infoText == null)
        {
            Debug.Log(msg);
            return;
        }

        infoPanel.SetActive(true);
        infoText.text = msg;
    }

    void HideMessage()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }
}
