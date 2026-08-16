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

        public async Task ImportProjectAsync(System.Action callback, System.Action<string> onError)
        {
            if (importSource == ImportSource.FromJson && projectJsonFile != null)
            {
                await MakeProject(projectJsonFile.text, callback, onError);
                return;
            }

            if (importSource == ImportSource.FromWeb && !string.IsNullOrEmpty(userAPIKey) && !string.IsNullOrEmpty(projectHash))
            {
                SendWebRequest((j) => _ = MakeProject(j, callback, onError), onError);
                return;
            }

            var message = $"Import source {importSource} is not configured correctly.";
            Debug.LogError(message);
            onError?.Invoke(message);
        }

        //...
        async Task MakeProject(string json, System.Action callback, System.Action<string> onError)
        {
            try
            {
                Project.Project project = null;
                // It will avoid Unity freezing the main thread while parsing the json and making the project, which can take a while for big projects.
                await Task.Run(() =>
                {
                    Debug.Log("Parsing Json...");
                    var maker = new Project.ProjectMaker(json, this);
                    Debug.Log("Making Project...");
                    project = maker.MakeProject();
                });

                Project = project;
                Debug.Log("Done");
                callback?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Project import failed: {exception}");
                onError?.Invoke(exception.Message);
            }
        }

        //...
        void SendWebRequest(System.Action<string> callbackSuccess, System.Action<string> callbackError)
        {
            Debug.Log("Sending Web Request...");

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
                var result = request.downloadHandler?.text;
                if (responseCode == 200 && callbackSuccess != null)
                {
                    callbackSuccess(result);
                }
                else
                {
                    Debug.LogError(string.Format("Web Request Failed (code = {0}): {1}", responseCode, request.error));
                    if (callbackError != null)
                    {
                        callbackError(request.error);
                    }
                }
                request.Dispose();
            };
        }
    }
}