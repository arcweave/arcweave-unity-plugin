#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private static readonly HashSet<string> RESERVED_NAMES = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };
        private const string INVALID_NAME_CHARS = "<>:\"/\\|?*";
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
        /// The files are named after the Arcweave assets and saved in folders that mirror the Arcweave asset folders, given as folder names by asset id.
        /// Never faults: media that cannot be downloaded or saved keeps its previous copy and logs a warning.</summary>
        public static async Task DownloadAsync(ArcweaveProjectAsset projectAsset, Project.Project project, Dictionary<string, string[]> assetFolders) {
            try {
                await DownloadMediaAsync(projectAsset, project, assetFolders);
            }
            catch ( Exception e ) {
                Debug.LogException(e);
            }
            finally {
                EditorUtility.ClearProgressBar();
            }
        }

        static async Task DownloadMediaAsync(ArcweaveProjectAsset projectAsset, Project.Project project, Dictionary<string, string[]> assetFolders) {
            if ( string.IsNullOrEmpty(AssetDatabase.GetAssetPath(projectAsset)) ) {
                Debug.LogWarning("Skipping media download because the Arcweave project is not saved as an asset.");
                return;
            }

            var covers = new Dictionary<string, List<Cover>>();
            var audioAssets = new Dictionary<string, List<AudioAsset>>();
            var names = new Dictionary<string, string>();
            CollectMedia(project, covers, audioAssets, names);
            // Sanitized before any path operation, since some of them throw on characters that aren't allowed in paths
            var fileNames = names.ToDictionary(entry => entry.Key, entry => SanitizeName(entry.Value));

            var downloads = new Dictionary<string, byte[]>();
            var ids = names.Keys.ToList();
            var progress = new Progress();
            for ( var i = 0; i < ids.Count && !progress.Canceled; i++ ) {
                var id = ids[i];
                var name = names[id];
                var extension = System.IO.Path.GetExtension(fileNames[id]).ToLowerInvariant();
                var supportedExtensions = covers.ContainsKey(id) ? IMAGE_EXTENSIONS : AUDIO_EXTENSIONS;
                if ( !supportedExtensions.Contains(extension) ) {
                    Debug.LogWarning($"Skipping '{name}' because Unity cannot import '{extension}' files.");
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

            // Nothing below awaits, so the paths resolved here are still current when the files are moved, written and deleted.
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
                projectAsset.mediaSubfolderGuids = new List<string>();
            }

            // Files downloaded by previous imports. Only these files are ever replaced, moved or deleted.
            // The linked file of each Arcweave asset id comes first, followed by any files that could not be deleted yet.
            var owned = new List<ArcweaveProjectAsset.DownloadedMedia>();
            var previousPaths = new Dictionary<string, string>();
            foreach ( var media in projectAsset.downloadedMedia ) {
                var path = AssetDatabase.GUIDToAssetPath(media.guid);
                if ( !string.IsNullOrEmpty(path) && File.Exists(path) ) {
                    owned.Add(media);
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
            if ( string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder) ) {
                folder = null;
            }

            // The path of each downloaded file and the file it keeps when it can't be saved.
            // Folders are created and files moved before the asset-editing batch, so they are in the asset database when the files are written.
            var targetPaths = new Dictionary<string, string>();
            var fallbackPaths = new Dictionary<string, string>();
            var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Targets that are the asset's own previous file, which are the only existing files that are ever overwritten
            var ownedTargets = new HashSet<string>();
            foreach ( var id in downloads.Keys ) {
                var extension = System.IO.Path.GetExtension(fileNames[id]).ToLowerInvariant();
                var hasPrevious = previousPaths.TryGetValue(id, out var previousPath);
                var keepsPrevious = hasPrevious && System.IO.Path.GetExtension(previousPath).ToLowerInvariant() == extension;
                if ( hasPrevious ) { fallbackPaths[id] = previousPath; }

                // A file the user moved out of the media folder stays where they put it and is updated in place
                var path = keepsPrevious && !IsInFolder(previousPath, folder) ? previousPath : null;
                try {
                    path = path ?? MirroredPath(id, keepsPrevious ? previousPath : null);
                }
                catch ( Exception e ) {
                    // Eg. a folder that can't be listed. The previous copy is kept.
                    Debug.LogWarning($"Could not find a path for '{names[id]}': {e.Message}");
                    path = keepsPrevious ? previousPath : null;
                }
                if ( path == null ) {
                    continue;
                }

                // Moving keeps the file's GUID, so references to it keep working after the asset is renamed or moved in Arcweave
                if ( keepsPrevious && path != previousPath ) {
                    var error = AssetDatabase.MoveAsset(previousPath, path);
                    if ( string.IsNullOrEmpty(error) ) {
                        fallbackPaths[id] = path;
                    } else {
                        Debug.LogWarning($"Could not move '{names[id]}' from {previousPath} to {path}: {error}");
                        path = previousPath;
                    }
                }
                if ( keepsPrevious ) { ownedTargets.Add(path); }
                targetPaths[id] = path;
                claimedPaths.Add(path);
            }

            ///<summary>Returns a free path that mirrors the Arcweave folders, creating its folders, or the previous path or null if they can't be created.</summary>
            string MirroredPath(string id, string previousPath) {
                // The media folder is only created when a file needs a path in it
                if ( folder == null ) {
                    var parentFolder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
                    var folderName = System.IO.Path.GetFileName(AssetDatabase.GenerateUniqueAssetPath(parentFolder + "/" + projectAsset.name + " Media"));
                    projectAsset.mediaFolderGuid = AssetDatabase.CreateFolder(parentFolder, folderName);
                    folder = AssetDatabase.GUIDToAssetPath(projectAsset.mediaFolderGuid);
                }
                string[] folderNames = null;
                assetFolders?.TryGetValue(id, out folderNames);
                var desiredFolder = string.IsNullOrEmpty(folder) ? null : CreateFolders(projectAsset, folder, folderNames, claimedPaths);
                return desiredFolder == null ? previousPath : UniquePath(desiredFolder + "/" + fileNames[id], previousPath, claimedPaths);
            }

            var saved = 0;
            var undeletedPaths = new List<KeyValuePair<string, string>>();
            AssetDatabase.StartAssetEditing();
            try {
                foreach ( var target in targetPaths ) {
                    var id = target.Key;
                    var path = target.Value;
                    var bytes = downloads[id];
                    try {
                        // A callback, eg. of another editor extension, may have put a file at a free path since it was chosen
                        if ( !ownedTargets.Contains(path) && File.Exists(path) ) {
                            throw new IOException("Another file was saved at the path.");
                        }
                        if ( !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes) ) {
                            WriteFile(path, bytes);
                            AssetDatabase.ImportAsset(path);
                        }
                        paths[id] = path;
                        saved++;
                    }
                    catch ( Exception e ) {
                        // Eg. a read-only file checked in to version control
                        Debug.LogWarning($"Could not save '{names[id]}' to {path}: {e.Message}");
                        if ( fallbackPaths.TryGetValue(id, out var fallbackPath) ) { paths[id] = fallbackPath; }
                    }
                }
                foreach ( var id in downloads.Keys.Where(id => !targetPaths.ContainsKey(id)) ) {
                    if ( fallbackPaths.TryGetValue(id, out var fallbackPath) ) { paths[id] = fallbackPath; }
                }

                // Owned files are compared by GUID, since some were moved
                var linkedGuids = new HashSet<string>(paths.Values.Select(AssetDatabase.AssetPathToGUID));
                foreach ( var media in owned.GroupBy(media => media.guid).Select(group => group.First()) ) {
                    var path = AssetDatabase.GUIDToAssetPath(media.guid);
                    if ( string.IsNullOrEmpty(path) || linkedGuids.Contains(media.guid) ) { continue; }
                    if ( !AssetDatabase.DeleteAsset(path) ) {
                        // Still owned, so a later import tries again
                        Debug.LogWarning($"Could not delete unused media file {path}.");
                        undeletedPaths.Add(new KeyValuePair<string, string>(media.assetId, path));
                    }
                }
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            DeleteEmptyFolders(projectAsset);

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

        ///<summary>Makes a file or folder name safe on every OS and visible to Unity, so it can never leave its folder.</summary>
        internal static string SanitizeName(string name) {
            var builder = new StringBuilder(name ?? "");
            for ( var i = 0; i < builder.Length; i++ ) {
                if ( char.IsControl(builder[i]) || INVALID_NAME_CHARS.IndexOf(builder[i]) >= 0 ) { builder[i] = '_'; }
            }
            // Windows drops trailing dots and spaces, which also turns "." and ".." into ""
            var result = builder.ToString().TrimEnd('.', ' ');
            if ( result.Length == 0 ) { return "_"; }

            // Windows reserves device names, even with an extension
            var dot = result.IndexOf('.');
            var stem = dot < 0 ? result : result.Substring(0, dot);
            if ( RESERVED_NAMES.Contains(stem.TrimEnd(' ')) ) {
                result = stem + "_" + result.Substring(stem.Length);
            }

            // Unity ignores hidden names, names ending with '~', 'cvs' and '.tmp'
            if ( result[0] == '.' ) { result = "_" + result; }
            if ( result.EndsWith("~") || result.Equals("cvs", StringComparison.OrdinalIgnoreCase) || result.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ) {
                result += "_";
            }
            return result;
        }

        ///<summary>Returns the folder inside the media folder that mirrors the Arcweave folders, creating any that are missing,
        /// or null if it can't be created. A folder whose name is taken by a file gets a number, like files do.</summary>
        static string CreateFolders(ArcweaveProjectAsset projectAsset, string mediaFolder, string[] folderNames, HashSet<string> claimedPaths) {
            var folder = mediaFolder;
            foreach ( var folderName in folderNames ?? new string[0] ) {
                var name = SanitizeName(folderName);
                string next = null;
                for ( var number = 0; next == null; number++ ) {
                    var candidate = number == 0 ? name : $"{name} {number}";
                    var path = folder + "/" + candidate;
                    // A file of this import may not be written yet
                    if ( claimedPaths.Contains(path) ) { continue; }
                    var existing = ExistingEntry(path);
                    if ( existing != null ) {
                        // Reuses a folder whose name differs only in case, since it is the same folder on case-insensitive file systems
                        if ( AssetDatabase.IsValidFolder(existing) ) { next = existing; }
                        continue;
                    }
                    var guid = AssetDatabase.CreateFolder(folder, candidate);
                    if ( !string.IsNullOrEmpty(guid) ) { projectAsset.mediaSubfolderGuids.Add(guid); }
                    if ( string.IsNullOrEmpty(guid) || AssetDatabase.GUIDToAssetPath(guid) != path ) {
                        Debug.LogWarning($"Could not create the media folder {path}.");
                        return null;
                    }
                    next = path;
                }
                folder = next;
            }
            return folder;
        }

        ///<summary>Returns the path, or the first of "name 1.ext", "name 2.ext"... (as AssetDatabase.GenerateUniqueAssetPath names them) that is free.
        /// Names that differ only in case are taken, since they are the same path on case-insensitive file systems and to Unity.
        /// The file's own previous path counts as free, so a file that had to be numbered keeps its name on later imports.
        /// Paths claimed by other files of this import are taken, since they may not be written yet.</summary>
        static string UniquePath(string path, string previousPath, HashSet<string> claimedPaths) {
            var folder = path.Substring(0, path.LastIndexOf('/'));
            var stem = System.IO.Path.GetFileNameWithoutExtension(path);
            var extension = System.IO.Path.GetExtension(path);
            for ( var number = 1; ; number++ ) {
                if ( !claimedPaths.Contains(path) ) {
                    var existing = ExistingEntry(path);
                    if ( existing == null || ( previousPath != null && IsSamePath(existing, previousPath) ) ) {
                        return path;
                    }
                }
                path = $"{folder}/{stem} {number}{extension}";
            }
        }

        ///<summary>Returns the path of the file or folder in the same folder whose name matches ignoring case, or null if there is none.</summary>
        static string ExistingEntry(string path) {
            var separator = path.LastIndexOf('/');
            var folder = path.Substring(0, separator);
            var name = path.Substring(separator + 1);
            if ( !Directory.Exists(folder) ) { return null; }
            var entry = Directory.EnumerateFileSystemEntries(folder)
                .Select(System.IO.Path.GetFileName)
                .FirstOrDefault(entryName => string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase));
            return entry == null ? null : folder + "/" + entry;
        }

        ///<summary>True if both paths are the same file: the same name in folders that match ignoring case, since the folder may be written in another case.</summary>
        static bool IsSamePath(string path, string otherPath) {
            return System.IO.Path.GetFileName(path) == System.IO.Path.GetFileName(otherPath)
                && string.Equals(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetDirectoryName(otherPath), StringComparison.OrdinalIgnoreCase);
        }

        static bool IsInFolder(string path, string folder) {
            return !string.IsNullOrEmpty(folder) && path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);
        }

        ///<summary>Deletes the folders the downloader created that are empty, deepest first. A folder with anything in it is kept.</summary>
        static void DeleteEmptyFolders(ArcweaveProjectAsset projectAsset) {
            var mediaFolder = string.IsNullOrEmpty(projectAsset.mediaFolderGuid) ? null : AssetDatabase.GUIDToAssetPath(projectAsset.mediaFolderGuid);
            if ( string.IsNullOrEmpty(mediaFolder) || !AssetDatabase.IsValidFolder(mediaFolder) ) {
                return;
            }
            var folders = projectAsset.mediaSubfolderGuids
                .Select(guid => new KeyValuePair<string, string>(guid, AssetDatabase.GUIDToAssetPath(guid)))
                .Where(folder => !string.IsNullOrEmpty(folder.Value) && AssetDatabase.IsValidFolder(folder.Value))
                .ToList();
            var remaining = new List<string>();
            foreach ( var folder in folders.OrderByDescending(folder => folder.Value.Count(c => c == '/')) ) {
                // Folders the user moved out of the media folder are left alone
                if ( !IsInFolder(folder.Value, mediaFolder) || !IsEmpty(folder.Value) || !AssetDatabase.DeleteAsset(folder.Value) ) {
                    remaining.Add(folder.Key);
                }
            }
            projectAsset.mediaSubfolderGuids = remaining;
            if ( IsEmpty(mediaFolder) ) {
                AssetDatabase.DeleteAsset(mediaFolder);
            }
        }

        ///<summary>True if the folder has nothing in it. A folder that can't be listed counts as not empty, so it is kept.</summary>
        static bool IsEmpty(string folder) {
            try {
                return !Directory.EnumerateFileSystemEntries(folder).Any();
            }
            catch ( Exception e ) {
                Debug.LogWarning($"Could not check whether {folder} is empty: {e.Message}");
                return false;
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
