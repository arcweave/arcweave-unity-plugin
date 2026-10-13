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
        private const int DEFAULT_RETRY_SECONDS = 60;
        private const string PROGRESS_TITLE = "Arcweave";

        ///<summary>Downloads the media of the project asset and invokes the callback when finished, even if some or all downloads fail.</summary>
        public static async void Download(ArcweaveProjectAsset projectAsset, Action callback) {
            try {
                await DownloadAsync(projectAsset);
            }
            catch ( Exception e ) {
                Debug.LogException(e);
            }
            finally {
                EditorUtility.ClearProgressBar();
            }
            if ( callback != null ) { callback(); }
        }

        static async Task DownloadAsync(ArcweaveProjectAsset projectAsset) {
            var assetPath = AssetDatabase.GetAssetPath(projectAsset);
            if ( string.IsNullOrEmpty(assetPath) ) {
                Debug.LogWarning("Skipping media download because the Arcweave project is not saved as an asset.");
                return;
            }

            // A duplicated project asset starts with its own media instead of managing the original's
            var ownerGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if ( projectAsset.mediaOwnerGuid != ownerGuid ) {
                projectAsset.mediaOwnerGuid = ownerGuid;
                projectAsset.mediaFolderGuid = "";
                projectAsset.downloadedMedia = new List<ArcweaveProjectAsset.DownloadedMedia>();
            }

            var project = projectAsset.Project;
            var covers = new Dictionary<string, List<Cover>>();
            var audioAssets = new Dictionary<string, List<AudioAsset>>();
            var names = new Dictionary<string, string>();
            CollectMedia(project, covers, audioAssets, names);

            // Files downloaded by previous imports, by Arcweave asset id. Only these files are ever replaced or deleted.
            var previousPaths = new Dictionary<string, string>();
            foreach ( var media in projectAsset.downloadedMedia ) {
                var path = AssetDatabase.GUIDToAssetPath(media.guid);
                if ( !string.IsNullOrEmpty(path) && File.Exists(path) ) {
                    previousPaths[media.assetId] = path;
                }
            }

            // A previous file is kept when its replacement cannot be downloaded, so a failed import does not remove media.
            var paths = new Dictionary<string, string>();
            var downloads = new Dictionary<string, byte[]>();
            var ids = names.Keys.ToList();
            for ( var i = 0; i < ids.Count; i++ ) {
                var id = ids[i];
                var name = names[id];
                previousPaths.TryGetValue(id, out var previousPath);
                var extension = System.IO.Path.GetExtension(name).ToLowerInvariant();
                var supportedExtensions = covers.ContainsKey(id) ? IMAGE_EXTENSIONS : AUDIO_EXTENSIONS;
                if ( !supportedExtensions.Contains(extension) ) {
                    Debug.LogWarning($"Skipping '{name}' because Unity cannot import '{extension}' files.");
                    if ( previousPath != null ) { paths[id] = previousPath; }
                    continue;
                }

                var url = ArcweaveProjectAsset.API_URL + Uri.EscapeDataString(projectAsset.projectHash) + "/asset/" + Uri.EscapeDataString(id) + "/file";
                var progress = $"Downloading media {i + 1}/{ids.Count}: {name}";
                EditorUtility.DisplayProgressBar(PROGRESS_TITLE, progress, (float)i / ids.Count);
                var bytes = await FetchAsync(url, projectAsset.userAPIKey, name, progress, (float)i / ids.Count);
                if ( bytes != null ) {
                    downloads[id] = bytes;
                } else if ( previousPath != null ) {
                    paths[id] = previousPath;
                }
            }

            if ( projectAsset.Project != project ) {
                Debug.LogWarning("Skipping media update because the Arcweave project was imported again in the meantime.");
                return;
            }

            var folder = string.IsNullOrEmpty(projectAsset.mediaFolderGuid) ? null : AssetDatabase.GUIDToAssetPath(projectAsset.mediaFolderGuid);
            if ( downloads.Count > 0 && ( string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder) ) ) {
                var parentFolder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
                var folderName = System.IO.Path.GetFileName(AssetDatabase.GenerateUniqueAssetPath(parentFolder + "/" + projectAsset.name + " Media"));
                projectAsset.mediaFolderGuid = AssetDatabase.CreateFolder(parentFolder, folderName);
                folder = parentFolder + "/" + folderName;
            }

            AssetDatabase.StartAssetEditing();
            try {
                foreach ( var download in downloads ) {
                    // Overwrite the previous file in place when possible, so its GUID and any references to it are kept.
                    // Otherwise use a unique path, so files the importer does not own are never overwritten.
                    var extension = System.IO.Path.GetExtension(names[download.Key]).ToLowerInvariant();
                    var path = previousPaths.TryGetValue(download.Key, out var previousPath) && System.IO.Path.GetExtension(previousPath).ToLowerInvariant() == extension
                        ? previousPath
                        : AssetDatabase.GenerateUniqueAssetPath($"{folder}/{download.Key}{extension}");
                    if ( !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(download.Value) ) {
                        File.WriteAllBytes(path, download.Value);
                        AssetDatabase.ImportAsset(path);
                    }
                    paths[download.Key] = path;
                }

                var linkedPaths = new HashSet<string>(paths.Values);
                foreach ( var previousPath in previousPaths.Values ) {
                    if ( !linkedPaths.Contains(previousPath) ) {
                        AssetDatabase.DeleteAsset(previousPath);
                    }
                }
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            foreach ( var entry in paths ) {
                if ( covers.TryGetValue(entry.Key, out var assetCovers) ) {
                    var image = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Value);
                    if ( image == null ) {
                        Debug.LogWarning($"Could not load '{names[entry.Key]}' as an image from {entry.Value}.");
                    }
                    foreach ( var cover in assetCovers ) {
                        cover.downloadedImage = image;
                    }
                } else {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(entry.Value);
                    if ( clip == null ) {
                        Debug.LogWarning($"Could not load '{names[entry.Key]}' as audio from {entry.Value}.");
                    }
                    foreach ( var audioAsset in audioAssets[entry.Key] ) {
                        audioAsset.downloadedClip = clip;
                    }
                }
            }

            projectAsset.downloadedMedia = paths
                .Select(entry => new ArcweaveProjectAsset.DownloadedMedia(entry.Key, AssetDatabase.AssetPathToGUID(entry.Value)))
                .ToList();
            EditorUtility.SetDirty(projectAsset);
            Debug.Log($"Downloaded {downloads.Count} of {names.Count} media files for {projectAsset.name}");
        }

        ///<summary>Collects the image covers and audio assets of the project grouped by Arcweave asset id.</summary>
        static void CollectMedia(Project.Project project, Dictionary<string, List<Cover>> covers, Dictionary<string, List<AudioAsset>> audioAssets, Dictionary<string, string> names) {
            void AddCover(Cover cover) {
                if ( cover == null || cover.type != Cover.Type.Image || string.IsNullOrEmpty(cover.assetId) || string.IsNullOrEmpty(cover.filePath) ) { return; }
                if ( !covers.TryGetValue(cover.assetId, out var list) ) {
                    covers[cover.assetId] = list = new List<Cover>();
                    names[cover.assetId] = cover.filePath;
                }
                list.Add(cover);
            }

            void AddAudioAsset(AudioAsset audioAsset) {
                if ( string.IsNullOrEmpty(audioAsset.asset) || string.IsNullOrEmpty(audioAsset.name) ) { return; }
                if ( !audioAssets.TryGetValue(audioAsset.asset, out var list) ) {
                    audioAssets[audioAsset.asset] = list = new List<AudioAsset>();
                    names[audioAsset.asset] = audioAsset.name;
                }
                list.Add(audioAsset);
            }

            foreach ( var board in project.Boards ) {
                foreach ( var element in board.Nodes.OfType<Element>() ) {
                    AddCover(element.cover);
                    foreach ( var component in element.Components ) {
                        AddCover(component.cover);
                    }
                    if ( element.AudioAssets != null ) {
                        foreach ( var audioAsset in element.AudioAssets ) {
                            AddAudioAsset(audioAsset);
                        }
                    }
                }
            }
            foreach ( var component in project.Components ) {
                AddCover(component.cover);
            }
        }

        ///<summary>Downloads an asset file, waiting and retrying while rate limited. Returns null if the download fails.</summary>
        static async Task<byte[]> FetchAsync(string url, string apiKey, string name, string progress, float progressValue) {
            for ( var attempt = 1; ; attempt++ ) {
                using ( var request = UnityWebRequest.Get(url) ) {
                    request.SetRequestHeader("Authorization", string.Format("Bearer {0}", apiKey));
                    // Makes authentication errors respond with 401 instead of redirecting to the login page
                    request.SetRequestHeader("Accept", "application/json");
                    var completion = new TaskCompletionSource<bool>();
                    request.SendWebRequest().completed += (op) => completion.SetResult(true);
                    await completion.Task;

                    if ( request.result == UnityWebRequest.Result.Success && request.responseCode == 200 ) {
                        return request.downloadHandler.data;
                    }

                    if ( request.responseCode == 429 && attempt < MAX_ATTEMPTS ) {
                        var seconds = int.TryParse(request.GetResponseHeader("Retry-After"), out var retryAfter) ? Math.Max(1, retryAfter) : DEFAULT_RETRY_SECONDS;
                        EditorUtility.DisplayProgressBar(PROGRESS_TITLE, $"{progress} (rate limited, retrying in {seconds}s)", progressValue);
                        await Task.Delay(TimeSpan.FromSeconds(seconds));
                        continue;
                    }

                    Debug.LogWarning(string.Format("Could not download '{0}' (code = {1}): {2}", name, request.responseCode, request.error));
                    return null;
                }
            }
        }
    }
}

#endif
