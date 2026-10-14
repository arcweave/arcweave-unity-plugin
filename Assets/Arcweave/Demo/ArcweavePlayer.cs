using System.Threading.Tasks;
using UnityEngine;
using Arcweave.Project;

namespace Arcweave
{
    /// This is not required to utilize an Arcweave project but can be helpful for some projects as well as a learning example.
    public class ArcweavePlayer : MonoBehaviour
    {
        // Delegates for the events
        public delegate void OnProjectStart(Project.Project project);
        public delegate void OnProjectFinish(Project.Project project);
        public delegate void OnElementEnter(Element element);
        public delegate void OnElementOptions(Options options, System.Action<int> next);
        public delegate void OnWaitingInputNext(System.Action next);

        public const string SAVE_KEY = "arcweave_save";

        public Arcweave.ArcweaveProjectAsset aw;

        public bool autoStart = true;

        private Element currentElement;

        // Events that the UI (or otherwise) can subscribe to get notified and act accordingly
        public event OnProjectStart onProjectStart;
        public event OnProjectFinish onProjectFinish;
        public event OnElementEnter onElementEnter;
        public event OnElementOptions onElementOptions;
        public event OnWaitingInputNext onWaitInputNext;

        // The pending start, shared by overlapping calls so the project only starts once.
        // It is set before any start code runs, so event handlers calling PlayProject join it too.
        private TaskCompletionSource<bool> pendingStart;

        void Start() { if ( autoStart ) PlayProject(); }

        /// Starts playing the project, importing it first if needed. Use PlayProjectAsync to await it.
        public async void PlayProject() {
            try {
                await PlayProjectAsync();
            }
            catch ( System.Exception e ) {
                Debug.LogException(e);
            }
        }

        /// Starts playing the project, importing it first if needed.
        /// Calls made while a start is still pending return that same pending start.
        public Task PlayProjectAsync() {
            var start = pendingStart;
            if ( start == null ) {
                start = pendingStart = new TaskCompletionSource<bool>();
                RunStart(start);
            }
            return start.Task;
        }

        async void RunStart(TaskCompletionSource<bool> start) {
            System.Exception error = null;
            try {
                await StartProject();
            }
            catch ( System.Exception e ) {
                error = e;
            }
            // Cleared before completing, so whoever awaits the start can restart the project right away.
            pendingStart = null;
            if ( error == null ) { start.SetResult(true); } else { start.SetException(error); }
        }

        async Task StartProject() {

            if ( aw == null ) {
                Debug.LogError("There is no Arcweave Project assigned in the inspector of Arcweave Player");
                return;
            }

            if ( !aw.IsImported ) {
                Debug.LogWarning("The Arcweave Project Asset has not been imported yet, importing it now.");
                await aw.EnsureImportedAsync();
                // Let anything else waiting on the same import (eg. an ImportProject callback calling PlayProject) join this start first.
                await Task.Yield();
                if ( this == null ) { return; } // destroyed while importing
                if ( !aw.IsImported ) {
                    throw new System.InvalidOperationException("The imported Arcweave project has no starting element.");
                }
            }

            aw.Project.Initialize();
            if ( onProjectStart != null ) onProjectStart(aw.Project);
            Next(aw.Project.StartingElement);
        }

        /// Moves to the next element through a path
        void Next(Path path) {
            path.ExecuteAppendedConnectionLabels();
            Next(path.TargetElement);
        }

        /// Moves to the next element directly
        void Next(Element element) {
            currentElement = element;
            currentElement.Visits++;
            if ( onElementEnter != null ) onElementEnter(element);
            var currentState = currentElement.GetOptions();
            if ( currentState.hasPaths ) {
                if ( currentState.hasOptions ) {
                    if ( onElementOptions != null ) {
                        onElementOptions(currentState, (index) => Next(currentState.Paths[index]));
                    }
                    return;
                }

                if ( onWaitInputNext != null ) onWaitInputNext(() => Next(currentState.Paths[0]));
                return;
            }
            currentElement = null;
            if ( onProjectFinish != null ) onProjectFinish(aw.Project);
        }

        ///----------------------------------------------------------------------------------------------

        /// Saves the current element and the variables
        public void Save() {
            var id = currentElement.Id;
            var variables = aw.Project.SaveVariables();
            PlayerPrefs.SetString(SAVE_KEY+"_currentElement", id);
            PlayerPrefs.SetString(SAVE_KEY+"_variables", variables);
        }

        /// Loads the previously saved element and the variables and moves to that element
        public void Load() {
            var id = PlayerPrefs.GetString(SAVE_KEY+"_currentElement");
            var variables = PlayerPrefs.GetString(SAVE_KEY+"_variables");
            var element = aw.Project.ElementWithId(id);
            aw.Project.LoadVariables(variables);
            Next(element);
        }
    }
}