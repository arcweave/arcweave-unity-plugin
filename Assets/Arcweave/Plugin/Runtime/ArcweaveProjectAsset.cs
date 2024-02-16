using UnityEngine;
using UnityEngine.Networking;

namespace Arcweave
{
    ///<summary>An arcweave project wrapper stored as a ScriptableObject asset</summary>
    [CreateAssetMenu(menuName = "Arcweave/Project Asset")]
    public class ArcweaveProjectAsset : ScriptableObject
    {
        public enum ImportSource { FromJson, FromWeb, }

        public ImportSource importSource;
        public TextAsset projectJsonFile;
        public string userAPIKey;
        public string projectHash;

        [field: SerializeField, HideInInspector]
        public Project.Project Project { get; private set; }

        ///----------------------------------------------------------------------------------------------

        [ContextMenu("Clear Data")]
        void ClearData() => Project = null;

        //...
        protected void OnEnable() {
            if ( Project != null ) {
                Project.Initialize();
            }
        }

        ///<summary>Import project from json text file or web and get callback when finished.</summary>
        public void ImportProject(System.Action callback = null) {
            if ( importSource == ImportSource.FromJson && projectJsonFile != null ) {
                MakeProject(projectJsonFile.text, callback);
            }
            if ( importSource == ImportSource.FromWeb && !string.IsNullOrEmpty(userAPIKey) ) {
                SendWebRequest((j) => MakeProject(j, callback));
            }
        }

        //...
        async void MakeProject(string json, System.Action callback) {
            Project.ProjectMaker maker = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                Debug.Log("Parsing Json...");
                maker = new Project.ProjectMaker(json, this);
                Debug.Log("Making Project...");
                Project = maker.MakeProject();
            });

            Debug.Log("Done");
            if ( callback != null ) { callback(); }
        }

        //...
        void SendWebRequest(System.Action<string> callbackSuccess) {
            Debug.Log("Sending Web Request...");
            var requestUrl = string.Format("https://arcweave.com/api/{0}/unity", projectHash);
            var request = UnityWebRequest.Get(requestUrl);
            request.SetRequestHeader("Authorization", string.Format("Bearer {0}", userAPIKey));
            request.SetRequestHeader("Accept", "application/json");
            var requestOperation = request.SendWebRequest();
            requestOperation.completed += (op) =>
            {
                var responseCode = request.responseCode;
                Debug.Log(string.Format("Web Request Completed (code = {0})...", responseCode));
                var result = request.downloadHandler?.text;
                if ( responseCode == 200 && callbackSuccess != null ) {
                    callbackSuccess(result);
                }
                request.Dispose();
            };
        }
    }
}