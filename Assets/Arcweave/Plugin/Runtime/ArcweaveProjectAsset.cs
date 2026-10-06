using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Arcweave
{
    static class ArcweaveApi
    {
        public const string BaseUrl = "https://arcweave.com/api/";
    }
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

        /// The single in-flight (or last finished) import. Owned by the asset so that every consumer
        /// (players, editor, user code) shares it. Only touched from the main thread.
        [NonSerialized] private Task _importTask;

        /// True when a project has been imported and is usable.
        public bool IsImported 
        {
            get { return Project?.StartingElement != null; }
        }
        /// True while an import is running.
        public bool IsImporting => _importTask != null && !_importTask.IsCompleted;

        ///----------------------------------------------------------------------------------------------

        [ContextMenu("Clear Data")]
        void ClearData() => Project = null;

        //...
        protected void OnEnable()
        {
            if (Project != null)
            {
                Project.Initialize();
                return;
            }
        }

        ///<summary>Import project from json text file or web and get callback when finished.</summary>
        public void ImportProject(System.Action callback = null, System.Action<string> onError = null)
        {
            _ = ImportProjectAsync(callback, onError);
        }

        ///<summary>
        /// Callback-style wrapper kept for compatibility. Always (re)imports, joining an import that is already running.
        /// The returned task completes only after download + parsing are done and never throws: failures go to onError.
        ///</summary>
        public async Task ImportProjectAsync(System.Action callback, System.Action<string> onError)
        {
            try
            {
                await EnsureImportedAsync(force: true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Project import failed: {exception}");
                onError?.Invoke(exception.Message);
                return;
            }

            callback?.Invoke();
        }

        ///<summary>
        /// Makes sure the project is imported. Completes immediately if it already is (unless force is true),
        /// joins the running import if there is one, otherwise starts a new one.
        /// The task faults if the configuration is invalid, the download fails or the json cannot be parsed.
        /// A faulted import is not cached: the next call starts a fresh attempt.
        ///</summary>
        public Task EnsureImportedAsync(bool force = false)
        {
            if (IsImporting)
            {
                return _importTask;
            }

            if (!force && IsImported)
            {
                return Task.CompletedTask;
            }

            _importTask = ImportCoreAsync();
            return _importTask;
        }

        async Task ImportCoreAsync()
        {
            string json;
            switch (importSource)
            {
                case ImportSource.FromJson:
                    if (projectJsonFile == null)
                    {
                        throw new InvalidOperationException("Import source FromJson requires a project json file.");
                    }
                    json = projectJsonFile.text;
                    break;

                case ImportSource.FromWeb:
                    if (string.IsNullOrEmpty(userAPIKey) || string.IsNullOrEmpty(projectHash))
                    {
                        throw new InvalidOperationException("Import source FromWeb requires both the API key and the project hash.");
                    }
                    json = await FetchProjectJsonAsync();
                    break;

                default:
                    throw new InvalidOperationException($"Import source {importSource} is not supported.");
            }

            // Assigned only on success, so a failed re-import keeps the previous project.
            Project = await MakeProjectAsync(json);
            Debug.Log("Done");
        }

        //...
        async Task<Project.Project> MakeProjectAsync(string json)
        {
            // Avoids freezing the main thread while parsing the json and making the project, which can take a while for big projects.
            return await Task.Run(() =>
            {
                Debug.Log("Parsing Json...");
                var maker = new Project.ProjectMaker(json, this);
                Debug.Log("Making Project...");
                return maker.MakeProject();
            });
        }

        ///<summary>Downloads the project json. Faults on any non-200 response. Virtual so tests can replace the network.</summary>
        protected virtual Task<string> FetchProjectJsonAsync()
        {
            Debug.Log("Sending Web Request...");

            var fetchProjectTask = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            UriBuilder builder = new UriBuilder(ArcweaveApi.BaseUrl);
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
                try
                {
                    var responseCode = request.responseCode;
                    Debug.Log(string.Format("Web Request Completed (code = {0})...", responseCode));
                    if (responseCode == (long)HttpStatusCode.OK)
                    {
                        fetchProjectTask.TrySetResult(request.downloadHandler?.text);
                    }
                    else
                    {
                        var message = string.Format("Web Request Failed (code = {0}): {1}", responseCode, request.error);
                        Debug.LogError(message);
                        fetchProjectTask.TrySetException(new Exception(message));
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };

            return fetchProjectTask.Task;
        }
    }
}