using System.Threading.Tasks;
using Arcweave.Project;
using UnityEngine;

namespace Arcweave
{
    /// <summary>
    /// Defines when the ArcweavePlayer should automatically save progress.
    /// </summary>
    public enum SaveMode
    {
        /// <summary>No automatic saving. Use RequestSave() manually to save progress.</summary>
        Manual,
        /// <summary>Automatically saves every time a new element is entered, and on quit</summary>
        AutoSaveAlways
    }

    ///This is not required to utilize an Arcweave project but can be helpful for some projects as well as a learning example.
    public class ArcweavePlayer : MonoBehaviour
    {
        //Delegates for the events.
        public delegate void OnProjectStart(Project.Project project);
        public delegate void OnProjectFinish(Project.Project project);
        public delegate void OnElementEnter(Element element);
        public delegate void OnElementOptions(Options options, System.Action<int> next);
        public delegate void OnWaitingInputNext(System.Action next);
        public delegate void OnProjectUpdated(Project.Project project);

        public Arcweave.ArcweaveProjectAsset aw;

        [Tooltip("If true, automatically starts playing the project when the scene loads.")]
        public bool autoStart = true;

        [Tooltip("Controls when the player automatically saves progress. Manual requires calling RequestSave() explicitly.")]
        public SaveMode saveMode = SaveMode.AutoSaveAlways;

        private Element currentElement;
        private bool isInitialized = false;
        // Element of the save restored by EnsureInitialized. It is kept until StartProject uses it, even if
        // EnsureInitialized was called earlier by someone else (e.g. a DialogueTrigger).
        private Element restoredElement;
        // True once StartProject has started the project. A later PlayProject() restarts it from a clean state.
        private bool hasStarted;
        // Variables as they were when the current element was entered, before its content script ran.
        // This is what a save stores, so a load re-enters the element from the same state and renders the same dialogue.
        private string preEntryVariables;

        //events that that UI (or otherwise) can subscribe to get notified and act accordingly.
        public event OnProjectStart onProjectStart;
        public event OnProjectFinish onProjectFinish;
        public event OnElementEnter onElementEnter;
        public event OnElementOptions onElementOptions;
        public event OnWaitingInputNext onWaitInputNext;
        public event OnProjectUpdated onProjectUpdated;
        /// <summary>Raised after ResetVariables cleared the saved state.</summary>
        public event System.Action onSavesReset;

        [SerializeField, Tooltip("Optional save handler component to handle save/load events. If not assigned, save/load events will be logged with a warning.")]
        private ArcweaveSaveHandler saveHandler;

        // The pending start, shared by overlapping calls so the project only starts once.
        // It is set before any start code runs, so event handlers calling PlayProject join it too.
        private TaskCompletionSource<bool> pendingStart;

        void Awake()
        {
            // Ensure we have a valid project asset
            if (aw == null)
            {
                Debug.LogError("No Arcweave Project Asset assigned to ArcweavePlayer");
            }
        }

        void Start()
        {
            if (autoStart) PlayProject();
        }

        /// <summary>
        /// Initialize the project if not already initialized
        /// </summary>
        /// <returns>True if a save was found and its variables and visits were restored during this call.
        /// The saved element is then available to StartProject; no navigation happens here.</returns>
        public bool EnsureInitialized()
        {
            Debug.Log("[ArcweavePlayer] EnsureInitialized() called");
            if (isInitialized)
            {
                Debug.Log("[ArcweavePlayer] Already initialized, skipping");
                return false;
            }

            if (aw == null || aw.Project == null)
            {
                Debug.LogError("Cannot initialize Arcweave project - missing project asset");
                return false;
            }

            // Initialize the project
            aw.Project.Initialize();

            restoredElement = null;
            if (saveHandler != null && saveHandler.HasSave())
            {
                restoredElement = RestoreSavedState();
                if (restoredElement == null)
                {
                    Debug.LogWarning("[ArcweavePlayer] Load request failed - save data was present but could not be loaded");
                }
            }

            isInitialized = true;

            if (onProjectUpdated != null) onProjectUpdated(aw.Project);
            return restoredElement != null;
        }

        /// <summary>
        /// Play the Arcweave project from the beginning, importing it first if needed. Use PlayProjectAsync to await it.
        /// </summary>
        public async void PlayProject()
        {
            try
            {
                await PlayProjectAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Play the Arcweave project from the beginning, importing it first if needed.
        /// Calls made while a start is still pending return that same pending start.
        /// </summary>
        public Task PlayProjectAsync()
        {
            var start = pendingStart;
            if (start == null)
            {
                start = pendingStart = new TaskCompletionSource<bool>();
                RunStart(start);
            }
            return start.Task;
        }

        async void RunStart(TaskCompletionSource<bool> start)
        {
            System.Exception error = null;
            try
            {
                await StartProject();
            }
            catch (System.Exception e)
            {
                error = e;
            }
            // Cleared before completing, so whoever awaits the start can restart the project right away.
            pendingStart = null;
            if (error == null) { start.SetResult(true); } else { start.SetException(error); }
        }

        async Task StartProject()
        {
            if (aw == null)
            {
                Debug.LogError("There is no Arcweave Project assigned in the inspector of Arcweave Player");
                return;
            }

            if (!aw.IsImported)
            {
                Debug.LogWarning("The Arcweave Project Asset has not been imported yet, importing it now.");
                await aw.EnsureImportedAsync();
                // Let anything else waiting on the same import (eg. an ImportProject callback calling PlayProject) join this start first.
                await Task.Yield();
                if (this == null) { return; } // destroyed while importing
                if (!aw.IsImported)
                {
                    throw new System.InvalidOperationException("The imported Arcweave project has no starting element.");
                }
            }

            if (hasStarted)
            {
                // The project already ran: PlayProject() restarts it from a clean state, like before the save system.
                // The save is left alone (RestartProject() and ResetVariables() are the ones that clear it).
                aw.Project.Initialize();
                restoredElement = null;
                isInitialized = true;
                if (onProjectUpdated != null) onProjectUpdated(aw.Project);
            }
            else
            {
                // Ensure project is initialized (also restores the save, if any). If someone already did,
                // restoredElement still holds the resume point.
                EnsureInitialized();
            }

            bool loadedSave = restoredElement != null;
            Element startingElement = loadedSave ? restoredElement : FindStartingElement();
            restoredElement = null;
            if (startingElement == null)
            {
                Debug.LogError("No starting element found with the specified criteria");
                return;
            }

            hasStarted = true;

            if (onProjectStart != null) onProjectStart(aw.Project);

            // A restored save is re-entered without counting a new visit and without autosaving over it.
            Next(startingElement, loadedSave);
        }

        /// <summary>
        /// Restarts the project from the beginning, resetting all variables and saves from the configured starting element.
        /// </summary>
        public void RestartProject()
        {
            isInitialized = false;
            ResetVariables();
            PlayProject();
        }

        /// <summary>
        /// Find a suitable starting element for the project.
        /// Returns the starting element configured in the Arcweave project.
        /// Note: in the 3D demo, autoStart should be false — DialogueTrigger
        /// drives dialogue per-NPC via EnsureInitialized() + Next(element) directly.
        /// </summary>
        private Element FindStartingElement()
        {
            return aw?.Project?.StartingElement;
        }

        /// <summary>
        /// Moves to the next element through a path
        /// </summary>
        void Next(Path path)
        {
            if (path == null)
            {
                Debug.LogError("Cannot navigate to null path");
                return;
            }

            path.ExecuteAppendedConnectionLabels();
            Next(path.TargetElement);
        }

        /// <summary>
        /// Moves to the next/an element directly
        /// </summary>
        public void Next(Element element, bool isLoad = false)
        {
            if (element == null)
            {
                Debug.LogError("Cannot navigate to null element");
                if (onProjectFinish != null) onProjectFinish(aw.Project);
                return;
            }

            currentElement = element;

            // Keep the state from before this element's script runs: that is what a save stores.
            preEntryVariables = aw.Project.SaveVariables();

            // A load restores the visits of the save, which already include this element.
            if (!isLoad)
            {
                currentElement.Visits++;
            }

            // Prepare rendered content before notifying listeners. On a load the saved variables are the
            // pre-entry ones, so running the script again reproduces the same variable changes and dialogue.
            // Limitation: expressions that are not deterministic (e.g. random()) can render differently.
            if (currentElement.HasContent())
            {
                currentElement.RunContentScript();
            }
            else
            {
                Debug.LogWarning($"Element '{currentElement.Title}' has no content");
            }

            if (!isLoad && saveMode == SaveMode.AutoSaveAlways)
            {
                RequestSave();
            }

            if (onElementEnter != null) onElementEnter(element);

            var currentState = currentElement.GetOptions();
            if (currentState.hasPaths)
            {
                if (currentState.hasOptions)
                {
                    if (onElementOptions != null)
                    {
                        onElementOptions(currentState, (index) => Next(currentState.Paths[index]));
                    }
                    return;
                }

                if (onWaitInputNext != null) onWaitInputNext(() => Next(currentState.Paths[0]));

                return;
            }

            currentElement = null;
            preEntryVariables = null;
            if (onProjectFinish != null) onProjectFinish(aw.Project);
        }

#region SAVES
        /// <summary>
        /// True while an element is playing. There is nothing to save before the project starts or after it finishes.
        /// </summary>
        public bool CanSave => currentElement != null;

        /// <summary>
        /// Requests a save of the current game state.
        /// If a saveHandler is assigned, it will handle saving the current element ID and project variables.
        /// This can be called manually at any time, or automatically based on the saveMode setting.
        /// </summary>
        public void RequestSave()
        {
            if (!CanSave)
            {
                Debug.LogWarning("Cannot request save - no current element");
                return;
            }

            var id = currentElement.Id;
            // Variables from when the element was entered (see Next). Changes made by game code while the
            // element is on screen are not part of the save.
            var variables = preEntryVariables ?? aw.Project.SaveVariables();
            var visits = aw.Project.SaveVisits();

            if (saveHandler != null)
            {
                saveHandler.HandleSaveRequest(id, variables, visits);
            }
            else
            {
                Debug.LogWarning("Save request was made but no handlers are set in the ArcweavePlayer");
            }
        }

        /// <summary>
        /// Requests loading of a previously saved game state.
        /// If a saveHandler is assigned, it will retrieve the saved element ID and variables,
        /// restore the project state, and set the starting element to the saved element.
        /// Call this method to resume a saved game session.
        /// </summary>
        /// <returns>True if the load was successful and navigation occurred; false otherwise.</returns>
        public bool RequestLoad()
        {
            Debug.Log("[ArcweavePlayer] RequestLoad() called");
            var element = RestoreSavedState();
            if (element == null)
            {
                return false;
            }

            Debug.Log($"[ArcweavePlayer] State restored, navigating to element: {element.Title}");
            Next(element, /*isLoad = */ true);
            return true;
        }

        /// <summary>
        /// Restores saved variables and visits and returns the saved element, without navigating. Null on failure.
        /// </summary>
        private Element RestoreSavedState()
        {
            if (saveHandler == null)
            {
                Debug.LogWarning("Load request was made but no handlers are set in the ArcweavePlayer");
                return null;
            }

            string elementId, variables, visits;
            if (saveHandler.HandleLoadRequest(out elementId, out variables, out visits))
            {
                var element = aw.Project.ElementWithId(elementId);
                if (element != null)
                {
                    aw.Project.LoadVariables(variables);
                    aw.Project.LoadVisits(visits);
                    return element;
                }
                Debug.LogError($"Cannot load - element with ID '{elementId}' not found");
            }
            else
            {
                Debug.Log("[ArcweavePlayer] Load request handler returned false - no saved data available");
            }

            return null;
        }

        /// <summary>
        /// Load the current save and restore the project state, then navigate to the saved element once.
        /// returns true if the load and navigation were successful; false otherwise.
        /// </summary>
        public bool LoadAndNavigateToSavedState()
        {
            if (RequestLoad())
            {
                return true;
            }

            Debug.LogWarning("No saved state to load");
            return false;
        }
        /// <summary>
        /// Resets all project variables to their default values and clears any saved state.
        /// If a saveHandler is assigned, it will clear the saved data.
        /// This is useful for starting a fresh playthrough.
        /// </summary>
        public void ResetVariables()
        {
            if (saveHandler != null)
            {
                saveHandler.ResetSave();
            }

            preEntryVariables = null;
            restoredElement = null;
            hasStarted = false;

            if (aw != null && aw.Project != null)
            {
                aw.Project.Initialize(); // This will reset all variables to their default values
                isInitialized = true;
            }

            if (onSavesReset != null)
            {
                onSavesReset();
            }
        }

        public bool HasSave()
        {
            if (saveHandler != null)
            {
                return saveHandler.HasSave();
            }

            return false;
        }
#endregion

        private void OnApplicationQuit()
        {
            if (saveMode == SaveMode.AutoSaveAlways && saveHandler != null && currentElement != null)
            {
                RequestSave();
            }
        }

    }// end class

}// end namespace