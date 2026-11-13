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
        /// <summary>Automatically saves every time a new element is entered. The save handler writes it to disk
        /// at most a few seconds later, and when the app is paused, loses focus or quits.</summary>
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
        // The project that was initialized. If aw.Project is replaced (e.g. ImportProjectAsync or Import in the
        // inspector) the new one is not initialized yet, so it is initialized again.
        private Project.Project initializedProject;
        private bool IsInitialized => initializedProject != null && initializedProject == aw?.Project;
        // Element of the save restored by EnsureInitialized. It is kept until StartProject uses it, even if
        // EnsureInitialized was called earlier by someone else (e.g. a DialogueTrigger).
        private Element restoredElement;
        // True once StartProject has started the project. A later PlayProject() restarts it from a clean state.
        private bool hasStarted;
        // Variables and visit counts captured before the current element was entered. Saves use this state so a load
        // re-enters the element from the same state and renders the same dialogue. It holds values, not JSON:
        // the JSON is only built when a save is made.
        private readonly StateSnapshot preEntryState = new StateSnapshot();

        //events that that UI (or otherwise) can subscribe to get notified and act accordingly.
        public event OnProjectStart onProjectStart;
        public event OnProjectFinish onProjectFinish;
        public event OnElementEnter onElementEnter;
        public event OnElementOptions onElementOptions;
        public event OnWaitingInputNext onWaitInputNext;
        public event OnProjectUpdated onProjectUpdated;
        /// <summary>Raised after the saved state is cleared.</summary>
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
            if (IsInitialized)
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
            InitializeProject();

            if (HasSaveHandler && saveHandler.HasSave())
            {
                restoredElement = RestoreSavedState();
                if (restoredElement == null)
                {
                    Debug.LogWarning("[ArcweavePlayer] Load request failed - save data was present but could not be loaded");
                }
            }

            if (onProjectUpdated != null) onProjectUpdated(aw.Project);
            return restoredElement != null;
        }

        /// <summary>
        /// Initializes aw.Project (variables, visits, starting element) and drops anything kept from a previous run
        /// or from a previous project.
        /// </summary>
        private void InitializeProject()
        {
            aw.Project.Initialize();

            // Elements of a replaced project are not part of the new one.
            if (initializedProject != aw.Project)
            {
                currentElement = null;
            }
            initializedProject = aw.Project;
            restoredElement = null;
            ClearPreEntryState();
        }

        /// <summary>Forgets the state captured when the current element was entered.</summary>
        private void ClearPreEntryState()
        {
            preEntryState.Clear();
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
                InitializeProject();
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

            // A restored save is re-entered from its pre-entry state, without autosaving over it.
            Next(startingElement, loadedSave);
        }

        /// <summary>
        /// Restarts the project from the beginning, resetting all variables and saves from the configured starting element.
        /// </summary>
        public void RestartProject()
        {
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
                currentElement = null;
                ClearPreEntryState();
                if (onProjectFinish != null) onProjectFinish(aw.Project);
                return;
            }

            // Directly driven demos may enter elements without calling PlayProject first.
            hasStarted = true;
            currentElement = element;

            // Keep the state from before this entry (variables and visits, taken at the same moment): that is what a
            // save stores. Only values are copied here; the JSON is built in RequestSave, when a save is actually made.
            // Without a save handler nothing can be saved, so nothing is captured.
            if (HasSaveHandler)
            {
                aw.Project.CaptureState(preEntryState);
            }
            else
            {
                ClearPreEntryState();
            }

            currentElement.Visits++;

            // Prepare rendered content before notifying listeners. A load restores the pre-entry state and enters
            // the element again, so the visit count and the script run exactly as the first time.
            // Limitation: expressions that are not deterministic (e.g. random()) can render differently.
            if (currentElement.HasContent())
            {
                currentElement.RunContentScript();
            }
            else
            {
                Debug.LogWarning($"Element '{currentElement.Title}' has no content");
            }

            if (!isLoad && saveMode == SaveMode.AutoSaveAlways && HasSaveHandler)
            {
                // Autosaves are not written to disk right away; the save handler batches the disk writes.
                Save(writeToDiskNow: false);
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
            ClearPreEntryState();
            if (onProjectFinish != null) onProjectFinish(aw.Project);
        }

#region SAVES
        /// <summary>
        /// True while an element is playing. There is nothing to save before the project starts or after it finishes.
        /// </summary>
        public bool CanSave => currentElement != null;

        /// <summary>True when a save handler is assigned.</summary>
        public bool HasSaveHandler => saveHandler != null;

        /// <summary>
        /// Requests a save of the current game state.
        /// If a saveHandler is assigned, it will handle saving the current element ID and project variables.
        /// This can be called manually at any time, or automatically based on the saveMode setting.
        /// A manual save is written to disk right away.
        /// </summary>
        public void RequestSave()
        {
            Save(writeToDiskNow: true);
        }

        private void Save(bool writeToDiskNow)
        {
            if (!CanSave)
            {
                Debug.LogWarning("Cannot request save - no current element");
                return;
            }

            if (!HasSaveHandler)
            {
                Debug.LogWarning("Save request was made but no handlers are set in the ArcweavePlayer");
                return;
            }

            var id = currentElement.Id;
            // Variables and visits from when the element was entered (see Next). Changes made by game code while
            // the element is on screen are not part of the save.
            string variables, visits;
            if (preEntryState.IsCaptured)
            {
                variables = aw.Project.SaveVariables(preEntryState);
                visits = aw.Project.SaveVisits(preEntryState);
            }
            else
            {
                variables = aw.Project.SaveVariables();
                visits = aw.Project.SaveVisits();
            }

            saveHandler.HandleSaveRequest(id, variables, visits, writeToDiskNow);
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
            if (aw == null || aw.Project == null)
            {
                return false;
            }

            // A replaced project (e.g. re-imported) is not initialized: its elements would have no Project.
            if (!IsInitialized)
            {
                InitializeProject();
            }

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
            if (!saveHandler.HandleLoadRequest(out elementId, out variables, out visits))
            {
                Debug.Log("[ArcweavePlayer] Load request handler returned false - no saved data available");
                return null;
            }

            var element = aw.Project.ElementWithId(elementId);
            if (element == null)
            {
                DiscardUnusableSave($"element with ID '{elementId}' not found");
                return null;
            }

            // Keep the current state, so a save that fails halfway (e.g. bad visits after the variables were
            // already applied) leaves the project exactly as it was.
            var previousVariables = aw.Project.SaveVariables();
            var previousVisits = aw.Project.SaveVisits();
            try
            {
                aw.Project.LoadVariables(variables);
                aw.Project.LoadVisits(visits);
                return element;
            }
            catch (System.Exception e)
            {
                aw.Project.LoadVariables(previousVariables);
                aw.Project.LoadVisits(previousVisits);
                DiscardUnusableSave(e.Message);
                return null;
            }
        }

        /// <summary>
        /// Deletes a save that can't be restored (corrupt, hand-edited, or from a changed project), so it doesn't
        /// fail again on every launch.
        /// </summary>
        private void DiscardUnusableSave(string reason)
        {
            Debug.LogError($"[ArcweavePlayer] The save could not be loaded and was deleted: {reason}");
            saveHandler.ResetSave();
            if (onSavesReset != null) onSavesReset();
        }

        /// <summary>
        /// Resets all project variables to their default values and clears any saved state.
        /// If a saveHandler is assigned, it will clear the saved data.
        /// This is useful for starting a fresh playthrough.
        /// </summary>
        public void ResetVariables()
        {
            if (HasSaveHandler)
            {
                saveHandler.ResetSave();
            }

            ClearPreEntryState();
            restoredElement = null;
            hasStarted = false;

            if (aw != null && aw.Project != null)
            {
                InitializeProject(); // This will reset all variables to their default values
            }

            if (onSavesReset != null)
            {
                onSavesReset();
            }
        }

        /// <summary>
        /// Clears the saved game without resetting or initializing the in-memory project.
        /// Returns true if a complete save existed before it was cleared.
        /// </summary>
        public bool ResetSave()
        {
            if (!HasSaveHandler)
            {
                Debug.LogWarning("Cannot reset save - no save handler is assigned to the ArcweavePlayer");
                return false;
            }

            var hadSave = saveHandler.HasSave();
            saveHandler.ResetSave();
            if (onSavesReset != null)
            {
                onSavesReset();
            }

            return hadSave;
        }

        public bool HasSave()
        {
            if (HasSaveHandler)
            {
                return saveHandler.HasSave();
            }

            return false;
        }
#endregion

    }// end class

}// end namespace