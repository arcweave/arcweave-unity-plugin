using Arcweave;
using Arcweave.Project;
using UnityEngine;

public class ArcweaveSaveHandler : MonoBehaviour
{
    public const string SAVE_KEY = "arcweave_save";

    [SerializeField, Tooltip("Activate to print debug logs for save handler")]
    private bool debug = false;

    public bool HandleSaveRequest(string elementId, string variables, string visits)
    {
        if(debug)
        {
            Debug.Log($"[ArcweaveSaveHandler] HandleSaveRequest() - ElementID: {elementId}");
            Debug.Log($"[ArcweaveSaveHandler] Variables to save: {variables}");
            Debug.Log($"[ArcweaveSaveHandler] Visits to save: {visits}");
        }


        PlayerPrefs.SetString(SAVE_KEY + "_currentElement", elementId);
        PlayerPrefs.SetString(SAVE_KEY + "_variables", variables);
        PlayerPrefs.SetString(SAVE_KEY + "_visits", visits);

        PlayerPrefs.Save();
        if (debug)
        {
            Debug.Log("[ArcweaveSaveHandler] Save completed successfully");
        }

        return true;
    }

    public bool HandleLoadRequest(out string elementId, out string variables, out string visits)
    {
        if (debug)
        {
            Debug.Log("[ArcweaveSaveHandler] HandleLoadRequest() called");
        }

        elementId = null;
        variables = null;
        visits = null;

        if (!HasSave())
        {
            Debug.LogWarning("[ArcweaveSaveHandler] No saved state found");
            return false;
        }

        elementId = PlayerPrefs.GetString(SAVE_KEY + "_currentElement");
        variables = PlayerPrefs.GetString(SAVE_KEY + "_variables");
        visits = PlayerPrefs.GetString(SAVE_KEY + "_visits");

        if (debug)
        {
            Debug.Log($"[ArcweaveSaveHandler] Loaded - Element ID: {elementId}, Variables: {variables}");
        }

        if (string.IsNullOrEmpty(elementId) || string.IsNullOrEmpty(variables))
        {
            Debug.LogWarning("[ArcweaveSaveHandler] Saved state is invalid");
            return false;
        }

        return true;
    }

    public void ResetSave()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY + "_currentElement");
        PlayerPrefs.DeleteKey(SAVE_KEY + "_variables");
        PlayerPrefs.DeleteKey(SAVE_KEY + "_visits");
        PlayerPrefs.Save();

        if (debug)
        {
            Debug.Log("Save state reset");
        }
    }

    public bool HasSave()
    {
         if (!PlayerPrefs.HasKey(SAVE_KEY + "_currentElement")
            || !PlayerPrefs.HasKey(SAVE_KEY + "_variables")
            || !PlayerPrefs.HasKey(SAVE_KEY + "_visits")
           )
        {
            return false;
        }

        return true;
    }
}
