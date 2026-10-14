using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Arcweave
{
    ///<summary>An arcweave project wrapper stored as a ScriptableObject asset</summary>
    [CreateAssetMenu(menuName = "Arcweave/Project Asset")]
    public class ArcweaveProjectAsset : ScriptableObject
    {
        public enum ImportSource { FromJson, FromWeb, }

        public ImportSource importSource;
        [HideInInspector]
        public TextAsset projectJsonFile;
        [HideInInspector]
        public string userAPIKey;
        [HideInInspector]
        public string projectHash;
        [HideInInspector]
        public string locale;

        [HideInInspector] public bool fallbackLocales = false;

        [field: SerializeField, HideInInspector]
        public Project.Project Project { get; private set; }

        ///<summary>True when the asset holds a project that can be played.</summary>
        public bool IsImported => Project != null && Project.Boards != null && Project.StartingElement != null;
        ///<summary>True while an import is running.</summary>
        public bool IsImporting => importTask != null && !importTask.IsCompleted;

        // The running (or last finished) import, shared by every caller of this asset.
        [NonSerialized] private Task importTask;

        ///----------------------------------------------------------------------------------------------

        [ContextMenu("Clear Data")]
        void ClearData() => Project = null;

        //...
        protected void OnEnable() {
            if ( IsImported ) {
                Project.Initialize();
            }
        }

        ///<summary>Import project from json text file or web and get callback when finished.</summary>
        public async void ImportProject(System.Action callback = null, System.Action<string> onError = null) {
            try {
                await ImportProjectAsync();
            }
            catch ( Exception e ) {
                Debug.LogError("Arcweave project import failed: " + e.Message);
                if ( onError != null ) { onError(e.Message); }
                return;
            }
            if ( callback != null ) { callback(); }
        }

        ///<summary>Import project from json text file or web, joining the import already running if there is one.
        /// Faults if the import source is not configured, the download fails or the json cannot be parsed.
        /// On failure the previously imported project is kept.</summary>
        public Task ImportProjectAsync() {
            if ( !IsImporting ) {
                importTask = ImportProjectInternalAsync();
            }
            return importTask;
        }

        ///<summary>Import the project only if the asset has no playable project yet, or join the import already running.</summary>
        public Task EnsureImportedAsync() {
            if ( IsImported && !IsImporting ) {
                return Task.CompletedTask;
            }
            return ImportProjectAsync();
        }

        //...
        async Task ImportProjectInternalAsync() {
            string json;
            if ( importSource == ImportSource.FromJson ) {
                if ( projectJsonFile == null ) {
                    throw new InvalidOperationException("Importing from json requires a Project JSON File.");
                }
                json = projectJsonFile.text;
            } else {
                if ( string.IsNullOrEmpty(userAPIKey) || string.IsNullOrEmpty(projectHash) ) {
                    throw new InvalidOperationException("Importing from web requires both an API Key and a Project Hash.");
                }
                json = await FetchProjectJson();
            }

            Project = await MakeProject(json);
            Debug.Log("Done");
        }

        //...
        Task<Project.Project> MakeProject(string json) {
            // Parsing runs off the main thread so big projects don't freeze the editor or game.
            return Task.Run(() =>
            {
                Debug.Log("Parsing Json...");
                var maker = new Project.ProjectMaker(json, this);
                Debug.Log("Making Project...");
                return maker.MakeProject();
            });
        }

        ///<summary>Downloads the project json. Virtual so tests can replace the network.</summary>
        protected virtual Task<string> FetchProjectJson() {
            Debug.Log("Sending Web Request...");
            var result = new TaskCompletionSource<string>();

            UriBuilder builder = new UriBuilder("https://arcweave.com/api/");
            builder.Path += projectHash + "/unity";
            if (locale != null && locale.Length > 0)
            {
                builder.Query = "locale=" + Uri.EscapeDataString(locale);
                if (fallbackLocales)
                {
                    builder.Query += "&fallbackContents=true";
                }
            }
            var requestUrl = builder.ToString();
            var request = UnityWebRequest.Get(requestUrl);
            request.SetRequestHeader("Authorization", string.Format("Bearer {0}", userAPIKey));
            request.SetRequestHeader("Accept", "application/json");
            var requestOperation = request.SendWebRequest();
            requestOperation.completed += (op) =>
            {
                var responseCode = request.responseCode;
                Debug.Log(string.Format("Web Request Completed (code = {0})...", responseCode));
                if ( responseCode == 200 ) {
                    result.SetResult(request.downloadHandler?.text);
                } else {
                    result.SetException(new Exception(string.Format("Web Request Failed (code = {0}): {1}", responseCode, request.error)));
                }
                request.Dispose();
            };
            return result.Task;
        }
    }
}