using Arcweave;
using Arcweave.Project;
using UnityEngine;

public class ArcweaveSaveHandler : MonoBehaviour
{
    public const string SAVE_KEY = "arcweave_save";

    [SerializeField, Tooltip("Activate to print debug logs for save handler")]
    private bool debug = false;

    [SerializeField, Min(0f), Tooltip("Saves that are not written to disk right away (autosaves) are written at most this many seconds later. " +
        "They are also written when the app is paused, loses focus or quits. This is the most progress a crash can lose.")]
    private float maxSecondsBeforeDiskWrite = 5f;

    // True when PlayerPrefs holds a save that is not on disk yet.
    private bool hasUnwrittenSave;
    // Time (unscaled) of the first save that is not on disk yet.
    private float unwrittenSince;

#region MonoBehaviour Callbacks
    private void Update()
    {
        if (hasUnwrittenSave && Time.unscaledTime - unwrittenSince >= maxSecondsBeforeDiskWrite)
        {
            WriteToDisk();
        }
    }

    // Mobile OSes can kill a paused app without calling OnApplicationQuit, so the save is written as soon as
    // the app goes to the background.
    private void OnApplicationPause(bool paused)
    {
        if (paused && hasUnwrittenSave) WriteToDisk();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && hasUnwrittenSave) WriteToDisk();
    }

    private void OnApplicationQuit()
    {
        if (hasUnwrittenSave) WriteToDisk();
    }

    // When the handler is destroyed (e.g. a scene change) nothing would write the pending save anymore.
    private void OnDestroy()
    {
        if (hasUnwrittenSave) WriteToDisk();
    }
#endregion

    /// <summary>
    /// Stores the save in PlayerPrefs. With writeToDiskNow false the disk write (PlayerPrefs.Save, a slow synchronous
    /// write) is postponed and batched with later saves: it happens within maxSecondsBeforeDiskWrite, or earlier
    /// when the app is paused, loses focus or quits.
    /// </summary>
    public bool HandleSaveRequest(string elementId, string variables, string visits, bool writeToDiskNow = true)
    {
        if(debug)
        {
            Debug.Log($"[ArcweaveSaveHandler] HandleSaveRequest() - ElementID: {elementId}, writeToDiskNow: {writeToDiskNow}");
            Debug.Log($"[ArcweaveSaveHandler] Variables to save: {variables}");
            Debug.Log($"[ArcweaveSaveHandler] Visits to save: {visits}");
        }

        PlayerPrefs.SetString(SAVE_KEY + "_currentElement", elementId);
        PlayerPrefs.SetString(SAVE_KEY + "_variables", variables);
        PlayerPrefs.SetString(SAVE_KEY + "_visits", visits);

        if (writeToDiskNow)
        {
            WriteToDisk();
        }
        else if (!hasUnwrittenSave)
        {
            hasUnwrittenSave = true;
            unwrittenSince = Time.unscaledTime;
        }

        return true;
    }

    /// <summary>Writes PlayerPrefs to disk now, including any save that was postponed.</summary>
    public void WriteToDisk()
    {
        PlayerPrefs.Save();
        hasUnwrittenSave = false;

        if (debug)
        {
            Debug.Log("[ArcweaveSaveHandler] Save written to disk");
        }
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
        WriteToDisk();

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
