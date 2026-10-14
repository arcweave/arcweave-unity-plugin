#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Arcweave.Project;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Arcweave
{
    ///<summary>Downloads the images and audio of a project imported from web through Arcweave's web API and links them to the project.</summary>
    internal static class ArcweaveMediaDownloader
    {
        private static readonly string[] IMAGE_EXTENSIONS = { ".png", ".jpg", ".jpeg", ".gif" };
        private static readonly string[] AUDIO_EXTENSIONS = { ".mp3", ".ogg", ".wav" };
        private const int MAX_ATTEMPTS = 5;
        private const int MAX_RETRY_SECONDS = 60;
        private const int POLL_MILLISECONDS = 100;
        private const string PROGRESS_TITLE = "Arcweave";

        ///<summary>A cancelable progress bar. Once canceled, the remaining downloads are skipped.</summary>
        sealed class Progress
        {
            public bool Canceled { get; private set; }

            ///<summary>Shows the progress and returns true if the download was canceled.</summary>
            public bool Update(string info, float value) {
                if ( EditorUtility.DisplayCancelableProgressBar(PROGRESS_TITLE, info, value) ) { Canceled = true; }
                return Canceled;
            }
        }

        ///<summary>Downloads the media of a newly made project of the project asset and links it to the project.
        /// Never faults: media that cannot be downloaded or saved keeps its previous copy and logs a warning.</summary>
        public static async Task DownloadAsync(ArcweaveProjectAsset projectAsset, Project.Project project) {
            try {
                await DownloadMediaAsync(projectAsset, project);
            }
            catch ( Exception e ) {
                Debug.LogException(e);
            }
            finally {
                EditorUtility.ClearProgressBar();
            }
        }

        static async Task DownloadMediaAsync(ArcweaveProjectAsset projectAsset, Project.Project project) {
            if ( string.IsNullOrEmpty(AssetDatabase.GetAssetPath(projectAsset)) ) {
                Debug.LogWarning("Skipping media download because the Arcweave project is not saved as an asset.");
                return;
            }

            var covers = new Dictionary<string, List<Cover>>();
            var audioAssets = new Dictionary<string, List<AudioAsset>>();
            var names = new Dictionary<string, string>();
            CollectMedia(project, covers, audioAssets, names);

            var downloads = new Dictionary<string, byte[]>();
            var ids = names.Keys.ToList();
            var progress = new Progress();
            for ( var i = 0; i < ids.Count && !progress.Canceled; i++ ) {
                var id = ids[i];
                var name = names[id];
                var extension = System.IO.Path.GetExtension(name).ToLowerInvariant();
                var supportedExtensions = covers.ContainsKey(id) ? IMAGE_EXTENSIONS : AUDIO_EXTENSIONS;
                if ( !supportedExtensions.Contains(extension) ) {
                    Debug.LogWarning($"Skipping '{name}' because Unity cannot import '{extension}' files.");
                    continue;
                }
                // The id names the downloaded file, so anything that is not a plain file name is never used in a path
                if ( id.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || id.Trim('.').Length == 0 ) {
                    Debug.LogWarning($"Skipping '{name}' because its asset id '{id}' is not a valid file name.");
                    continue;
                }

                var url = projectAsset.ApiUrl + Uri.EscapeDataString(projectAsset.projectHash) + "/asset/" + Uri.EscapeDataString(id) + "/file";
                var bytes = await FetchAsync(url, projectAsset.userAPIKey, name, $"Downloading media {i + 1}/{ids.Count}: {name}", (float)i / ids.Count, progress);
                if ( bytes != null ) {
                    downloads[id] = bytes;
                }
            }
            if ( progress.Canceled ) {
                Debug.LogWarning("Media download canceled. Media that was not downloaded keeps its previous copy.");
            }

            // Nothing below awaits, so the paths resolved here are still current when the files are written and deleted.
            var assetPath = AssetDatabase.GetAssetPath(projectAsset);
            if ( string.IsNullOrEmpty(assetPath) ) {
                Debug.LogWarning("Skipping media update because the Arcweave project asset was deleted during the download.");
                return;
            }

            // A duplicated project asset starts with its own media instead of managing the original's
            var ownerGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if ( projectAsset.mediaOwnerGuid != ownerGuid ) {
                projectAsset.mediaOwnerGuid = ownerGuid;
                projectAsset.mediaFolderGuid = "";
                projectAsset.downloadedMedia = new List<ArcweaveProjectAsset.DownloadedMedia>();
            }

            // Files downloaded by previous imports. Only these files are ever replaced or deleted.
            // The linked file of each Arcweave asset id comes first, followed by any files that could not be deleted yet.
            var ownedPaths = new List<KeyValuePair<string, string>>();
            var previousPaths = new Dictionary<string, string>();
            foreach ( var media in projectAsset.downloadedMedia ) {
                var path = AssetDatabase.GUIDToAssetPath(media.guid);
                if ( !string.IsNullOrEmpty(path) && File.Exists(path) ) {
                    ownedPaths.Add(new KeyValuePair<string, string>(media.assetId, path));
                    if ( !previousPaths.ContainsKey(media.assetId) ) { previousPaths[media.assetId] = path; }
                }
            }

            // A previous file is kept when its replacement cannot be downloaded or saved, so a failed import does not remove media.
            var paths = new Dictionary<string, string>();
            foreach ( var id in ids ) {
                if ( !downloads.ContainsKey(id) && previousPaths.TryGetValue(id, out var previousPath) ) {
                    paths[id] = previousPath;
                }
            }

            var folder = string.IsNullOrEmpty(projectAsset.mediaFolderGuid) ? null : AssetDatabase.GUIDToAssetPath(projectAsset.mediaFolderGuid);
            if ( downloads.Count > 0 && ( string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder) ) ) {
                var parentFolder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
                var folderName = System.IO.Path.GetFileName(AssetDatabase.GenerateUniqueAssetPath(parentFolder + "/" + projectAsset.name + " Media"));
                projectAsset.mediaFolderGuid = AssetDatabase.CreateFolder(parentFolder, folderName);
                folder = parentFolder + "/" + folderName;
            }

            var saved = 0;
            var undeletedPaths = new List<KeyValuePair<string, string>>();
            AssetDatabase.StartAssetEditing();
            try {
                foreach ( var download in downloads ) {
                    // Overwrite the previous file in place when possible, so its GUID and any references to it are kept.
                    // Otherwise use a unique path, so files the importer does not own are never overwritten.
                    var extension = System.IO.Path.GetExtension(names[download.Key]).ToLowerInvariant();
                    var hasPrevious = previousPaths.TryGetValue(download.Key, out var previousPath);
                    var path = hasPrevious && System.IO.Path.GetExtension(previousPath).ToLowerInvariant() == extension
                        ? previousPath
                        : AssetDatabase.GenerateUniqueAssetPath($"{folder}/{download.Key}{extension}");
                    try {
                        if ( !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(download.Value) ) {
                            WriteFile(path, download.Value);
                            AssetDatabase.ImportAsset(path);
                        }
                        paths[download.Key] = path;
                        saved++;
                    }
                    catch ( Exception e ) {
                        // Eg. a read-only file checked in to version control
                        Debug.LogWarning($"Could not save '{names[download.Key]}' to {path}: {e.Message}");
                        if ( hasPrevious ) { paths[download.Key] = previousPath; }
                    }
                }

                var linkedPaths = new HashSet<string>(paths.Values);
                foreach ( var owned in ownedPaths.GroupBy(entry => entry.Value).Select(group => group.First()) ) {
                    if ( !linkedPaths.Contains(owned.Value) && !AssetDatabase.DeleteAsset(owned.Value) ) {
                        // Still owned, so a later import tries again
                        Debug.LogWarning($"Could not delete unused media file {owned.Value}.");
                        undeletedPaths.Add(owned);
                    }
                }
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            // Every cover and audio asset is linked, to its file or to nothing, so the project never references another asset's media.
            foreach ( var entry in covers ) {
                Texture2D image = null;
                if ( paths.TryGetValue(entry.Key, out var path) ) {
                    image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if ( image == null ) { Debug.LogWarning($"Could not load '{names[entry.Key]}' as an image from {path}."); }
                }
                foreach ( var cover in entry.Value ) {
                    cover.downloadedImage = image;
                }
            }
            foreach ( var entry in audioAssets ) {
                AudioClip clip = null;
                if ( paths.TryGetValue(entry.Key, out var path) ) {
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if ( clip == null ) { Debug.LogWarning($"Could not load '{names[entry.Key]}' as audio from {path}."); }
                }
                foreach ( var audioAsset in entry.Value ) {
                    audioAsset.downloadedClip = clip;
                }
            }

            projectAsset.downloadedMedia = paths.Concat(undeletedPaths)
                .Select(entry => new ArcweaveProjectAsset.DownloadedMedia(entry.Key, AssetDatabase.AssetPathToGUID(entry.Value)))
                .ToList();
            EditorUtility.SetDirty(projectAsset);
            Debug.Log($"Downloaded {saved} of {names.Count} media files for {projectAsset.name}");
        }

        ///<summary>Collects the image covers and audio assets of the project grouped by Arcweave asset id.</summary>
        static void CollectMedia(Project.Project project, Dictionary<string, List<Cover>> covers, Dictionary<string, List<AudioAsset>> audioAssets, Dictionary<string, string> names) {
            foreach ( var cover in project.GetAssetCovers() ) {
                if ( !covers.TryGetValue(cover.assetId, out var list) ) {
                    covers[cover.assetId] = list = new List<Cover>();
                    names[cover.assetId] = cover.filePath;
                }
                list.Add(cover);
            }
            foreach ( var audioAsset in project.GetAssetAudio() ) {
                if ( !audioAssets.TryGetValue(audioAsset.asset, out var list) ) {
                    audioAssets[audioAsset.asset] = list = new List<AudioAsset>();
                    names[audioAsset.asset] = audioAsset.name;
                }
                list.Add(audioAsset);
            }
        }

        ///<summary>Downloads an asset file, waiting and retrying while rate limited. Returns null if the download fails or is canceled.</summary>
        static async Task<byte[]> FetchAsync(string url, string apiKey, string name, string info, float value, Progress progress) {
            for ( var attempt = 1; ; attempt++ ) {
                using ( var request = UnityWebRequest.Get(url) ) {
                    request.SetRequestHeader("Authorization", string.Format("Bearer {0}", apiKey));
                    // Makes authentication errors respond with 401 instead of redirecting to the login page
                    request.SetRequestHeader("Accept", "application/json");
                    var operation = request.SendWebRequest();
                    while ( !operation.isDone ) {
                        if ( progress.Update(info, value) ) {
                            request.Abort();
                            return null;
                        }
                        await Task.Delay(POLL_MILLISECONDS);
                    }

                    if ( request.result == UnityWebRequest.Result.Success && request.responseCode == 200 ) {
                        return request.downloadHandler.data;
                    }

                    if ( request.responseCode == 429 && attempt < MAX_ATTEMPTS ) {
                        var retryAt = DateTime.UtcNow.AddSeconds(RetryDelaySeconds(request.GetResponseHeader("Retry-After")));
                        while ( DateTime.UtcNow < retryAt ) {
                            var seconds = (int)Math.Ceiling(( retryAt - DateTime.UtcNow ).TotalSeconds);
                            if ( progress.Update($"{info} (rate limited, retrying in {seconds}s)", value) ) { return null; }
                            await Task.Delay(POLL_MILLISECONDS);
                        }
                        continue;
                    }

                    Debug.LogWarning(string.Format("Could not download '{0}' (code = {1}): {2}", name, request.responseCode, request.error));
                    return null;
                }
            }
        }

        ///<summary>Returns how long to wait before retrying a rate limited request, from its Retry-After header in seconds or as a date, capped so the editor is never blocked for long.</summary>
        static double RetryDelaySeconds(string retryAfter) {
            var seconds = (double)MAX_RETRY_SECONDS;
            if ( int.TryParse(retryAfter, out var delay) ) {
                seconds = delay;
            } else if ( DateTimeOffset.TryParse(retryAfter, out var date) ) {
                seconds = ( date - DateTimeOffset.UtcNow ).TotalSeconds;
            }
            return Math.Min(Math.Max(1, seconds), MAX_RETRY_SECONDS);
        }

        ///<summary>Writes the file through a temporary file, so a failed write never leaves it truncated. Replacing keeps its .meta and GUID.</summary>
        static void WriteFile(string path, byte[] bytes) {
            Directory.CreateDirectory("Temp");
            var temporaryPath = System.IO.Path.Combine("Temp", "ArcweaveMedia-" + Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(path));
            try {
                File.WriteAllBytes(temporaryPath, bytes);
                if ( File.Exists(path) ) {
                    File.Replace(temporaryPath, path, null);
                } else {
                    File.Move(temporaryPath, path);
                }
            }
            finally {
                if ( File.Exists(temporaryPath) ) { TryDeleteFile(temporaryPath); }
            }
        }

        static void TryDeleteFile(string path) {
            try {
                File.Delete(path);
            }
            catch ( Exception e ) {
                Debug.LogWarning($"Could not delete {path}: {e.Message}");
            }
        }
    }
}

#endif
