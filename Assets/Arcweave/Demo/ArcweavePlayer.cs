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
        /// <summary>Automatically saves when the project reaches an end element (no more paths) and on quit </summary>
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

        //events that that UI (or otherwise) can subscribe to get notified and act accordingly.
        public event OnProjectStart onProjectStart;
        public event OnProjectFinish onProjectFinish;
        public event OnElementEnter onElementEnter;
        public event OnElementOptions onElementOptions;
        public event OnWaitingInputNext onWaitInputNext;
        public event OnProjectUpdated onProjectUpdated;

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
        public void EnsureInitialized()
        {
            Debug.Log("[ArcweavePlayer] EnsureInitialized() called");
            if (isInitialized)
            {
                Debug.Log("[ArcweavePlayer] Already initialized, skipping");
                return;
            }

            if (aw == null || aw.Project == null)
            {
                Debug.LogError("Cannot initialize Arcweave project - missing project asset");
                return;
            }

            // Initialize the project
            aw.Project.Initialize();

            if (saveHandler != null && saveHandler.HasSave())
            {
                if (!RequestLoad())
                {
                    Debug.LogWarning("[ArcweavePlayer] Load request failed - save data was present but could not be loaded");
                }
            }

            isInitialized = true;

            if (onProjectUpdated != null) onProjectUpdated(aw.Project);
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

            // Ensure project is initialized (also restores the save, if any)
            EnsureInitialized();

            Element startingElement = FindStartingElement();
            if (startingElement == null)
            {
                Debug.LogError("No starting element found with the specified criteria");
                return;
            }

            if (onProjectStart != null) onProjectStart(aw.Project);

            Next(startingElement);
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


            // Prepare rendered content before notifying listeners.
            if (currentElement.HasContent())
            {
                currentElement.RunContentScript(applyVariableChanges: !isLoad);
            }
            else
            {
                Debug.LogWarning($"Element '{currentElement.Title}' has no content");
            }

            /* If coming to the element from a load request don't do these steps*/
            if (!isLoad)
            {
                currentElement.Visits++;
                // No paths means the project has reached an end.
                if (saveMode == SaveMode.AutoSaveAlways)
                {
                    RequestSave();
                }
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
            var variables = aw.Project.SaveVariables();
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
            if (saveHandler == null)
            {
                Debug.LogWarning("Load request was made but no handlers are set in the ArcweavePlayer");
                return false;
            }

            string elementId, variables, visits;
            if (saveHandler.HandleLoadRequest(out elementId, out variables, out visits))
            {
                var element = aw.Project.ElementWithId(elementId);
                if (element != null)
                {
                    Debug.Log($"[ArcweavePlayer] Loading variables before restoring state");
                    aw.Project.LoadVariables(variables);
                    aw.Project.LoadVisits(visits);
                    Debug.Log($"[ArcweavePlayer] Variables loaded, setting the starting element to: {element.Title}");
                    aw.Project.StartingElement = element; // Set the starting element to the loaded element
                    return true;
                }
                Debug.LogError($"Cannot load - element with ID '{elementId}' not found");
            }
            else
            {
                Debug.Log("[ArcweavePlayer] Load request handler returned false - no saved data available");
            }

            return false;
        }

        /// <summary>
        /// Load the current save and restore the project state, then navigate to the saved starting element.
        /// returns true if the load and navigation were successful; false otherwise.
        /// </summary>
        public bool LoadAndNavigateToSavedState()
        {
            if (RequestLoad())
            {
                var startingElement = FindStartingElement();
                if (startingElement != null)
                {
                    Next(startingElement, /*isLoad = */ true);
                    return true;
                }
                Debug.LogError("No starting element found after loading saved state");
            }
            else
            {
                Debug.LogWarning("No saved state to load");
            }
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

            if (aw != null && aw.Project != null)
            {
                aw.Project.Initialize(); // This will reset all variables to their default values
                isInitialized = true;
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