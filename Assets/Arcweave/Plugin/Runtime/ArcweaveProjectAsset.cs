using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Arcweave
{
    ///<summary>An arcweave project wrapper stored as a ScriptableObject asset</summary>
    [CreateAssetMenu(menuName = "Arcweave/Project Asset")]
    public class ArcweaveProjectAsset : ScriptableObject
    {
        internal const string API_URL = "https://arcweave.com/api/";

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

        ///<summary>Download the images and audio the project uses when importing from web.</summary>
        [HideInInspector] public bool downloadMedia = true;

        // Media downloaded by web imports, tracked by GUID so it can be refreshed and cleaned up after moves and renames.
        // The owner GUID detects duplicated project assets, which must not manage the original asset's media.
        [Serializable]
        internal struct DownloadedMedia
        {
            public string assetId;
            public string guid;

            public DownloadedMedia(string assetId, string guid) {
                this.assetId = assetId;
                this.guid = guid;
            }
        }
        [SerializeField, HideInInspector] internal string mediaOwnerGuid = "";
        [SerializeField, HideInInspector] internal string mediaFolderGuid = "";
        [SerializeField, HideInInspector] internal List<DownloadedMedia> downloadedMedia = new List<DownloadedMedia>();

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
        public void ImportProject(System.Action callback = null, System.Action<string> onError = null) {
            if ( importSource == ImportSource.FromJson && projectJsonFile != null ) {
                MakeProject(projectJsonFile.text, callback);
            }
            if ( importSource == ImportSource.FromWeb && !string.IsNullOrEmpty(userAPIKey) ) {
                SendWebRequest((j) => MakeProject(j, () => DownloadMedia(callback)), onError);
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
        void DownloadMedia(System.Action callback) {
#if UNITY_EDITOR
            if ( downloadMedia ) {
                ArcweaveMediaDownloader.Download(this, callback);
                return;
            }
#endif
            if ( callback != null ) { callback(); }
        }

        //...
        void SendWebRequest(System.Action<string> callbackSuccess, System.Action<string> callbackError) {
            Debug.Log("Sending Web Request...");

            UriBuilder builder = new UriBuilder(API_URL);
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
                if ( responseCode == 200 && callbackSuccess != null ) {
                    callbackSuccess(result);
                } else {
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