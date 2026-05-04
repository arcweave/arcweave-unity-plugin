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
        /// <summary>Automatically saves when the project reaches an end element (no more paths).</summary>
        AutoSaveOnEnd
    }

    ///This is not required to utilize an arweave project but can be helpful for some projects as well as a learning example.
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
        public SaveMode saveMode = SaveMode.AutoSaveOnEnd;

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

            if(!RequestLoad())
            {
                Debug.LogWarning("[ArcweavePlayer] Load request failed - no saved data available");
            }

            isInitialized = true;

            if (onProjectUpdated != null) onProjectUpdated(aw.Project);
        }

        /// <summary>
        /// Play the Arcweave project from the beginning
        /// </summary>
        public void PlayProject()
        {
            if (aw == null)
            {
                Debug.LogError("There is no Arcweave Project assigned in the inspector of Arcweave Player");
                return;
            }

            // Ensure project is initialized
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
        public void Next(Element element)
        {
            if (element == null)
            {
                Debug.LogError("Cannot navigate to null element");
                if (onProjectFinish != null) onProjectFinish(aw.Project);
                return;
            }

            currentElement = element;
            currentElement.Visits++;

            // Check if element has content
            if (!currentElement.HasContent())
            {
                Debug.LogWarning($"Element '{currentElement.Title}' has no content");
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

            // No paths means the project has reached an end.
            if (saveMode == SaveMode.AutoSaveOnEnd)
            {
                RequestSave();
            }

            currentElement = null;
            if (onProjectFinish != null) onProjectFinish(aw.Project);
        }

#region SAVES
        /// <summary>
        /// Requests a save of the current game state.
        /// If a saveHandler is assigned, it will handle saving the current element ID and project variables.
        /// This can be called manually at any time, or automatically based on the saveMode setting.
        /// </summary>
        public void RequestSave()
        {
            if (currentElement == null)
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
        /// restore the project state, and navigate to the saved element.
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
                    Debug.Log($"[ArcweavePlayer] Variables loaded, navigating to element: {element.Title}");
                    Next(element);
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
                saveHandler.HasSave();
            }

            return false;
        }
#endregion

        private void OnApplicationQuit()
        {
            if (saveMode == SaveMode.AutoSaveOnEnd)
            {
                RequestSave();
            }
        }

    }// end class

}// end namespace