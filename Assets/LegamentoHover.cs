using UnityEngine;
using TMPro;

public class LegamentoHover : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text infoText;   // Riferimento alla Text (TMP) in Canvas

    [Header("Dati Legamento")]
    [TextArea]
    public string testoLegamento = "Nome o descrizione del legamento"; 

    [Header("Highlight")]
    public Color highlightColor = Color.yellow; // Colore quando il cursore passa sopra

    private Renderer rend;
    private Color originalColor;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalColor = rend.material.color;
    }

    void OnMouseEnter()
    {
        if (rend != null)
            rend.material.color = highlightColor;

        if (infoText != null)
            infoText.text = testoLegamento;
    }

    void OnMouseExit()
    {
        if (rend != null)
            rend.material.color = originalColor;

        if (infoText != null)
            infoText.text = "";
    }
}
