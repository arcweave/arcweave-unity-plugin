using Arcweave.Project;
using System;
using System.Threading.Tasks;
using UnityEngine;

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

        // Only guards this player against being started twice while the asset is importing.
        // The import itself is owned (and shared between all consumers) by the ArcweaveProjectAsset.
        private bool isImporting;

        void Start()
        {
            if (autoStart)
            {
                PlayProject();
            }
        }

        /// <summary>
        /// Fire-and-forget entry point (usable from UnityEvents). Use PlayProjectAsync to await it.
        /// </summary>
        public async void PlayProject()
        {
            try
            {
                await PlayProjectAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Imports the project first if needed, then starts playing it.
        /// </summary>
        /// <returns></returns>
        public async Task PlayProjectAsync()
        {
            if (aw == null)
            {
                Debug.LogError("There is no Arcweave Project assigned in the inspector of Arcweave Player");
                return;
            }

            // Readiness depends only on the project data, never on the state of some previous import task.
            if (!aw.IsImported)
            {
                if (isImporting)
                {
                    Debug.LogWarning("The Arcweave project is already being imported, ignoring this play request.");
                    return;
                }

                isImporting = true;
                try
                {
                    Debug.LogWarning("The Arcweave Project Asset has not been imported yet - importing it now");
                    await aw.EnsureImportedAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Failed to import Arcweave project: {exception.Message}");
                    return;
                }
                finally
                {
                    isImporting = false;
                }

                if (this == null) return; // destroyed while the import was running

                if (!aw.IsImported)
                {
                    Debug.LogError("The Arcweave project was imported but has no starting element.");
                    return;
                }
            }

            aw.Project.Initialize();
            if (onProjectStart != null)
            {
                onProjectStart(aw.Project);
            }

            Next(aw.Project.StartingElement);
        }

        /// Moves to the next element through a path
        void Next(Path path)
        {
            path.ExecuteAppendedConnectionLabels();
            Next(path.TargetElement);
        }

        /// Moves to the next element directly
        void Next(Element element)
        {
            currentElement = element;
            currentElement.Visits++;
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

        ///----------------------------------------------------------------------------------------------

        /// Saves the current element and the variables
        public void Save()
        {
            var id = currentElement.Id;
            var variables = aw.Project.SaveVariables();
            PlayerPrefs.SetString(SAVE_KEY + "_currentElement", id);
            PlayerPrefs.SetString(SAVE_KEY + "_variables", variables);
        }

        /// Loads the previously saved element and the variables and moves to that element
        public void Load()
        {
            var id = PlayerPrefs.GetString(SAVE_KEY + "_currentElement");
            var variables = PlayerPrefs.GetString(SAVE_KEY + "_variables");
            var element = aw.Project.ElementWithId(id);
            aw.Project.LoadVariables(variables);
            Next(element);
        }
    }
}