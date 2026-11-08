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
            // The downloaded asset itself, so builds include it and can relink it without the AssetDatabase.
            // Empty for assets imported before this field existed, and for files that are tracked but no longer used.
            public UnityEngine.Object media;

            public DownloadedMedia(string assetId, string guid, UnityEngine.Object media = null) {
                this.assetId = assetId;
                this.guid = guid;
                this.media = media;
            }
        }
        [SerializeField, HideInInspector] internal string mediaOwnerGuid = "";
        [SerializeField, HideInInspector] internal string mediaFolderGuid = "";
        [SerializeField, HideInInspector] internal List<DownloadedMedia> downloadedMedia = new List<DownloadedMedia>();

        // The imported project is stored as its json text and rebuilt when first used, instead of serializing the whole object graph.
        // Serializing the graph makes Unity (and packages such as SerializeReferenceEditor) walk every node whenever the asset is selected.
        [SerializeField, HideInInspector] private string projectJson = "";
        [SerializeField, HideInInspector] private string projectName = "";

        [NonSerialized] private Project.Project project;
        [NonSerialized] private bool buildFailed;

        ///<summary>The imported project, rebuilt from the stored json the first time it is needed. Null if nothing is imported.</summary>
        public Project.Project Project {
            get {
                if ( project == null && HasProject && !buildFailed ) {
                    try {
                        var built = new Project.ProjectMaker(projectJson, this).MakeProject();
                        FinishLoad(built);
                        project = built;
                    }
                    catch ( Exception e ) {
                        buildFailed = true;
                        Debug.LogError("Arcweave project could not be rebuilt, import it again: " + e.Message);
                    }
                }
                return project;
            }
            private set { project = value; }
        }

        ///<summary>True when the asset stores an imported project. Unlike Project, this never builds it.</summary>
        public bool HasProject => project != null || !string.IsNullOrEmpty(projectJson);
        ///<summary>The name of the imported project. Unlike Project, this never builds it.</summary>
        public string ProjectName => project != null ? project.name : projectName;

        ///<summary>True when the asset holds a project that can be played.</summary>
        public bool IsImported => IsPlayable(Project);

        static bool IsPlayable(Project.Project candidate) => candidate != null && candidate.Boards != null && candidate.StartingElement != null;
        ///<summary>True while an import is running.</summary>
        public bool IsImporting => importTask != null && !importTask.IsCompleted;

        // The running (or last finished) import, shared by every caller of this asset.
        [NonSerialized] private Task importTask;

        ///----------------------------------------------------------------------------------------------

        [ContextMenu("Clear Data")]
        void ClearData() {
            project = null;
            projectJson = "";
            projectName = "";
            buildFailed = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        //...
        protected void OnEnable() {
            // The project is rebuilt from the stored json on first use
            project = null;
            buildFailed = false;
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
            if ( IsImporting ) {
                return importTask;
            }
            if ( IsPlayable(project) ) {
                return Task.CompletedTask;
            }
            if ( HasProject && !buildFailed ) {
                // Rebuild the stored project off the main thread, like an import does
                importTask = LoadStoredProjectAsync();
                return importTask;
            }
            return ImportProjectAsync();
        }

        //...
        async Task LoadStoredProjectAsync() {
            Project.Project built;
            try {
                built = await MakeProject(projectJson);
            }
            catch ( Exception ) {
                // The next EnsureImportedAsync imports again instead of retrying a json that cannot be read
                buildFailed = true;
                throw;
            }
            if ( project == null ) {
                FinishLoad(built);
                project = built;
            }
        }

        ///<summary>Links the saved media to a project that was just rebuilt, and gets it ready to use.</summary>
        void FinishLoad(Project.Project built) {
            LinkSavedMedia(built);
            built.Initialize();
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

            var project = await MakeProject(json);
            // The media is linked before the project is published, so nothing can start playing it without its media.
            await LinkMedia(project);
            projectJson = json;
            projectName = project.name;
            buildFailed = false;
            Project = project;
            Debug.Log("Done");
        }

        //...
        Task LinkMedia(Project.Project project) {
            if ( !downloadMedia ) {
                return Task.CompletedTask;
            }
#if UNITY_EDITOR
            // Media is saved next to the project asset, so an instance that is not saved as an asset can't download it
            if ( importSource == ImportSource.FromWeb && UnityEditor.EditorUtility.IsPersistent(this) ) {
                return ArcweaveMediaDownloader.DownloadAsync(this, project);
            }
#endif
            // Media can only be downloaded in the editor for a saved asset, so other imports, eg. in a player, keep the media of the previous import.
            LinkSavedMedia(project);
            return Task.CompletedTask;
        }

        ///<summary>Links the saved media of previous imports to the covers and audio of the project that use the same Arcweave asset.
        /// Does nothing unless the project relies on downloaded media.</summary>
        void LinkSavedMedia(Project.Project target) {
            if ( !downloadMedia || downloadedMedia == null || downloadedMedia.Count == 0 ) {
                return;
            }
            // The first entry of each Arcweave asset id is the one in use
            var images = new Dictionary<string, Texture2D>();
            var clips = new Dictionary<string, AudioClip>();
            var seen = new HashSet<string>();
            foreach ( var entry in downloadedMedia ) {
                if ( string.IsNullOrEmpty(entry.assetId) || !seen.Add(entry.assetId) ) { continue; }
                var media = entry.media;
#if UNITY_EDITOR
                // Assets saved before the media itself was stored are found through their GUID
                if ( media == null && !string.IsNullOrEmpty(entry.guid) ) {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(entry.guid);
                    if ( !string.IsNullOrEmpty(path) ) { media = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path); }
                }
#endif
                if ( media is Texture2D image ) { images[entry.assetId] = image; }
                else if ( media is AudioClip clip ) { clips[entry.assetId] = clip; }
            }
            foreach ( var cover in target.GetAssetCovers() ) {
                if ( images.TryGetValue(cover.assetId, out var image) ) { cover.downloadedImage = image; }
            }
            foreach ( var audioAsset in target.GetAssetAudio() ) {
                if ( clips.TryGetValue(audioAsset.asset, out var clip) ) { audioAsset.downloadedClip = clip; }
            }
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