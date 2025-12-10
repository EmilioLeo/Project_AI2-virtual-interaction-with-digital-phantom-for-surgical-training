using UnityEngine;
using TMPro;

public class RemovableDisc : MonoBehaviour
{
    [Header("Proprietà disco")]
    [Range(0f,1f)]
    public float integrity = 1f;              // 1 = integro, 0 = rimosso
    public Transform visualRoot;              // root della mesh da scalare
    public Renderer tissueRenderer;           // per shader dissolve (opzionale)
    public string dissolveProperty = "_DissolveAmount";
    public float minScale = 0.1f;
    public bool destroyWhenZero = false;      // distrugge il disco se true

    [Header("Messaggi")]
    public TextMeshProUGUI removalText;       // TextMeshProUGUI nel Canvas
    public float messageDuration = 2f;        // durata del messaggio

    [Header("Vincoli")]
    public MuscleHandleDrag larynx;           // riferimento alla laringe da muovere prima

    private float messageTimer = 0f;
    private bool isRemoved = false;           // flag per evitare messaggi ripetuti

    void Reset()
    {
        visualRoot = transform;
        tissueRenderer = GetComponentInChildren<Renderer>();
    }

    void Update()
    {
        // Timer per nascondere il messaggio
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && removalText != null)
            {
                removalText.gameObject.SetActive(false);
            }
        }
    }

    public void ApplyRemoval(float amount)
    {
        // Controllo vincolo laringe
        if (larynx != null && !larynx.isMoved)
        {
            ShowMessage("Sposta prima la laringe!");
            return; // blocca la rimozione
        }

        // Applica rimozione
        integrity = Mathf.Clamp01(integrity - amount);
        Debug.Log($"{name} integrity: {integrity:F3}");
        UpdateVisuals();

        // Controllo se il disco è praticamente rimosso
        if (!isRemoved && integrity <= 1f)
        {
            isRemoved = true; // evita ripetizioni
            ShowMessage(name + " rimosso");

            if (destroyWhenZero)
                Destroy(gameObject);
        }
    }

    void UpdateVisuals()
    {
        if (visualRoot != null)
        {
            float s = Mathf.Lerp(minScale, 1f, integrity);
            visualRoot.localScale = new Vector3(s, s, s);
        }

        if (tissueRenderer != null && tissueRenderer.material.HasProperty(dissolveProperty))
        {
            tissueRenderer.material.SetFloat(dissolveProperty, 1f - integrity);
        }
    }

    void ShowMessage(string msg)
    {
        if (removalText == null) return;

        removalText.text = msg;
        removalText.gameObject.SetActive(true);
        messageTimer = messageDuration;
    }
}
