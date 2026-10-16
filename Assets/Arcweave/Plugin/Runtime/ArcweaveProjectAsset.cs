using System;
using System.Collections.Generic;
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

        ///<summary>Download the images and audio the project uses when importing from web in the editor, and link them to the project.
        /// Disabled by default. When disabled, media is resolved by name from a 'Resources' folder.</summary>
        [HideInInspector] public bool downloadMedia = false;

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
        // Folders the downloader created inside the media folder to mirror Arcweave's asset folders, so it can delete them once empty.
        [SerializeField, HideInInspector] internal List<string> mediaSubfolderGuids = new List<string>();

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

            var (project, assetFolders) = await MakeProject(json);
            // The media is linked before the project is published, so nothing can start playing it without its media.
            await LinkMedia(project, assetFolders);
            Project = project;
            Debug.Log("Done");
        }

        //...
        Task LinkMedia(Project.Project project, Dictionary<string, string[]> assetFolders) {
            if ( !downloadMedia ) {
                return Task.CompletedTask;
            }
#if UNITY_EDITOR
            // Media is saved next to the project asset, so an instance that is not saved as an asset can't download it
            if ( importSource == ImportSource.FromWeb && UnityEditor.EditorUtility.IsPersistent(this) ) {
                return ArcweaveMediaDownloader.DownloadAsync(this, project, assetFolders);
            }
#endif
            // Media can only be downloaded in the editor for a saved asset, so other imports, eg. in a player, keep the media of the previous import.
            CarryOverMedia(Project, project);
            return Task.CompletedTask;
        }

        ///<summary>Links the media of the previous project to the covers and audio of the new one that use the same Arcweave asset.</summary>
        static void CarryOverMedia(Project.Project previous, Project.Project project) {
            if ( previous == null || previous.Boards == null ) {
                return;
            }
            var images = new Dictionary<string, Texture2D>();
            foreach ( var cover in previous.GetAssetCovers() ) {
                if ( cover.downloadedImage != null ) { images[cover.assetId] = cover.downloadedImage; }
            }
            var clips = new Dictionary<string, AudioClip>();
            foreach ( var audioAsset in previous.GetAssetAudio() ) {
                if ( audioAsset.downloadedClip != null ) { clips[audioAsset.asset] = audioAsset.downloadedClip; }
            }
            foreach ( var cover in project.GetAssetCovers() ) {
                if ( images.TryGetValue(cover.assetId, out var image) ) { cover.downloadedImage = image; }
            }
            foreach ( var audioAsset in project.GetAssetAudio() ) {
                if ( clips.TryGetValue(audioAsset.asset, out var clip) ) { audioAsset.downloadedClip = clip; }
            }
        }

        ///<summary>Makes the project, and the Arcweave folders of its assets by asset id, which name the folders of downloaded media.</summary>
        Task<(Project.Project project, Dictionary<string, string[]> assetFolders)> MakeProject(string json) {
            // Parsing runs off the main thread so big projects don't freeze the editor or game.
            return Task.Run(() =>
            {
                Debug.Log("Parsing Json...");
                var maker = new Project.ProjectMaker(json, this);
                Debug.Log("Making Project...");
                return (maker.MakeProject(), maker.MakeAssetFolders());
            });
        }

        ///<summary>The base url of Arcweave's web API. Virtual so tests can replace the network.</summary>
        protected internal virtual string ApiUrl => "https://arcweave.com/api/";

        ///<summary>Downloads the project json. Virtual so tests can replace the network.</summary>
        protected virtual Task<string> FetchProjectJson() {
            Debug.Log("Sending Web Request...");
            var result = new TaskCompletionSource<string>();

            UriBuilder builder = new UriBuilder(ApiUrl);
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