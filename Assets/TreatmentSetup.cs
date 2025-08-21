using UnityEngine;

public class TreatmentSetup : MonoBehaviour
{
    [Header("Riferimenti oggetti in scena")]
    public GameObject vertebraC5;
    public GameObject vertebraC6;
    public GameObject discoC5C6;

    [Header("Materiali trasparenti")]
    public Material yellowTransparent;
    public Material redTransparent;

    void Start()
    {
        // Applica giallo semitrasparente a C5 e C6
        if (vertebraC5) vertebraC5.GetComponent<Renderer>().material = yellowTransparent;
        if (vertebraC6) vertebraC6.GetComponent<Renderer>().material = yellowTransparent;

        // Applica rosso semitrasparente al disco
        if (discoC5C6) discoC5C6.GetComponent<Renderer>().material = redTransparent;
    }
}

