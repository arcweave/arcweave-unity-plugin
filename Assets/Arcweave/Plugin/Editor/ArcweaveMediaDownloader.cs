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
        // Leaves room for the number of a unique path and for Unity's own paths, eg. in the Library folder.
        // File systems such as ext4 and APFS limit a name to 255 bytes, which must also fit the extension, the number and ".meta".
        private const int MAX_NAME_LENGTH = 100;
        private const int MAX_NAME_BYTES = 200;
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
            }

            // Files downloaded by previous imports, by GUID. Only these files are ever replaced, moved or deleted.
            // The linked file of each Arcweave asset id comes first, followed by any files that could not be deleted yet.
            var owned = new List<ArcweaveProjectAsset.DownloadedMedia>();
            var previousGuids = new Dictionary<string, string>();
            foreach ( var media in projectAsset.downloadedMedia ) {
                if ( OwnedPath(media.guid) != null ) {
                    owned.Add(media);
                    if ( !previousGuids.ContainsKey(media.assetId) ) { previousGuids[media.assetId] = media.guid; }
                }
            }

            // The owned file each asset stays linked to, by GUID, and the paths of new files. Owned files are found by GUID
            // whenever they are used, since callbacks of other editor extensions can move files during the import.
            // A previous file is kept when its replacement cannot be downloaded or saved, so a failed import does not remove media.
            var keptGuids = new Dictionary<string, string>();
            var paths = new Dictionary<string, string>();
            foreach ( var id in ids ) {
                if ( !downloads.ContainsKey(id) && previousGuids.TryGetValue(id, out var previousGuid) ) {
                    keptGuids[id] = previousGuid;
                }
            }

            var folder = string.IsNullOrEmpty(projectAsset.mediaFolderGuid) ? null : AssetDatabase.GUIDToAssetPath(projectAsset.mediaFolderGuid);
            if ( string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder) ) {
                folder = null;
            }

            // The path of each downloaded file, and whether it is the asset's own previous file.
            // The media folder is created and files are moved before the asset-editing batch, so they are in the asset database when the files are written.
            var targetPaths = new Dictionary<string, string>();
            var ownedTargets = new HashSet<string>();
            var claimedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ( var id in downloads.Keys ) {
                var extension = System.IO.Path.GetExtension(fileNames[id]).ToLowerInvariant();
                var previousPath = previousGuids.TryGetValue(id, out var previousGuid) ? OwnedPath(previousGuid) : null;
                var keepsPrevious = previousPath != null && System.IO.Path.GetExtension(previousPath).ToLowerInvariant() == extension;

                // A file keeps its name and place once it is saved, so renaming or moving the asset in Arcweave or the file
                // in Unity never breaks references to it. Only files still named after their asset id by 3.1.0 are renamed, once.
                string path;
                if ( keepsPrevious && ( !IsNamedByVersion310(previousPath, id, folder) || System.IO.Path.GetFileName(previousPath) == fileNames[id] ) ) {
                    path = previousPath;
                } else {
                    try {
                        path = NewPath(id);
                    }
                    catch ( Exception e ) {
                        // Eg. a folder that can't be listed
                        Debug.LogWarning($"Could not find a path for '{names[id]}': {e.Message}");
                        path = null;
                    }
                    if ( keepsPrevious ) {
                        // Moving keeps the file's GUID, so references to it keep working
                        var error = path == null ? "" : AssetDatabase.MoveAsset(previousPath, path);
                        if ( path == null || !string.IsNullOrEmpty(error) ) {
                            if ( path != null ) { Debug.LogWarning($"Could not rename '{names[id]}' from {previousPath} to {path}: {error}"); }
                            path = previousPath;
                        }
                    }
                }
                if ( path == null ) {
                    continue;
                }
                if ( keepsPrevious ) { ownedTargets.Add(id); }
                targetPaths[id] = path;
                claimedPaths.Add(path);
            }

            ///<summary>Returns a free path in the media folder named after the asset, creating the folder, or null if it can't be created.</summary>
            string NewPath(string id) {
                // The media folder is only created when a file needs a path in it
                if ( folder == null ) {
                    var parentFolder = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
                    var folderName = System.IO.Path.GetFileName(AssetDatabase.GenerateUniqueAssetPath(parentFolder + "/" + projectAsset.name + " Media"));
                    projectAsset.mediaFolderGuid = AssetDatabase.CreateFolder(parentFolder, folderName);
                    folder = AssetDatabase.GUIDToAssetPath(projectAsset.mediaFolderGuid);
                }
                return string.IsNullOrEmpty(folder) ? null : UniquePath(folder + "/" + fileNames[id], claimedPaths);
            }

            var saved = 0;
            var undeleted = new List<ArcweaveProjectAsset.DownloadedMedia>();
            AssetDatabase.StartAssetEditing();
            try {
                foreach ( var target in targetPaths ) {
                    var id = target.Key;
                    var path = target.Value;
                    var bytes = downloads[id];
                    var isOwned = ownedTargets.Contains(id);
                    try {
                        // Checked again right before writing, since a callback, eg. of another editor extension, may have moved the
                        // asset's file or saved another file at the path since it was chosen
                        if ( isOwned ? OwnedPath(previousGuids[id]) != path : ExistsIgnoringCase(path) ) {
                            throw new IOException("The file at the path changed during the import.");
                        }
                        if ( !File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes) ) {
                            WriteFile(path, bytes);
                            AssetDatabase.ImportAsset(path);
                        }
                        if ( isOwned ) {
                            keptGuids[id] = previousGuids[id];
                        } else {
                            paths[id] = path;
                        }
                        saved++;
                    }
                    catch ( Exception e ) {
                        // Eg. a read-only file checked in to version control
                        Debug.LogWarning($"Could not save '{names[id]}' to {path}: {e.Message}");
                        if ( previousGuids.TryGetValue(id, out var previousGuid) ) { keptGuids[id] = previousGuid; }
                    }
                }
                foreach ( var id in downloads.Keys.Where(id => !targetPaths.ContainsKey(id)) ) {
                    if ( previousGuids.TryGetValue(id, out var previousGuid) ) { keptGuids[id] = previousGuid; }
                }

                var linkedGuids = new HashSet<string>(keptGuids.Values);
                foreach ( var media in owned.GroupBy(media => media.guid).Select(group => group.First()) ) {
                    var path = OwnedPath(media.guid);
                    if ( path == null || linkedGuids.Contains(media.guid) ) { continue; }
                    if ( !AssetDatabase.DeleteAsset(path) ) {
                        // Still owned, so a later import tries again
                        Debug.LogWarning($"Could not delete unused media file {path}.");
                        undeleted.Add(media);
                    }
                }
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            foreach ( var kept in keptGuids ) {
                var path = OwnedPath(kept.Value);
                if ( path != null ) { paths[kept.Key] = path; }
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

            projectAsset.downloadedMedia = paths
                .Select(entry => new ArcweaveProjectAsset.DownloadedMedia(entry.Key, AssetDatabase.AssetPathToGUID(entry.Value)))
                .Concat(undeleted)
                .ToList();
            EditorUtility.SetDirty(projectAsset);
            Debug.Log($"Downloaded {saved} of {names.Count} media files for {projectAsset.name}");
        }

        ///<summary>Returns the path of a downloaded file by GUID, or null if it no longer exists.</summary>
        static string OwnedPath(string guid) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
        }

        ///<summary>True if the file is still where and how 3.1.0 saved it: in the media folder, named after its asset id with a
        /// lowercase extension. A file the user renamed or moved, even only changing the case of its extension, is not.</summary>
        static bool IsNamedByVersion310(string path, string id, string mediaFolder) {
            return !string.IsNullOrEmpty(mediaFolder)
                && string.Equals(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), mediaFolder, StringComparison.OrdinalIgnoreCase)
                && System.IO.Path.GetFileName(path) == id + System.IO.Path.GetExtension(path).ToLowerInvariant();
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

        ///<summary>Makes a file name safe on every OS and visible to Unity, so it can never leave the media folder,
        /// and shortens it to MAX_NAME_LENGTH characters and MAX_NAME_BYTES UTF-8 bytes before its extension.</summary>
        internal static string SanitizeName(string name) {
            var builder = new StringBuilder(name ?? "");
            for ( var i = 0; i < builder.Length; i++ ) {
                if ( char.IsControl(builder[i]) || INVALID_NAME_CHARS.IndexOf(builder[i]) >= 0 ) { builder[i] = '_'; }
            }
            // Windows drops trailing dots and spaces, which also turns "." and ".." into ""
            var result = builder.ToString().TrimEnd('.', ' ');

            var extension = System.IO.Path.GetExtension(result);
            var stem = result.Substring(0, result.Length - extension.Length);
            // Making the name safe adds at most one character, so it is shortened by one more if that goes over the limits
            var safeStem = MakeSafe(Shorten(stem, MAX_NAME_LENGTH, MAX_NAME_BYTES));
            if ( safeStem.Length > MAX_NAME_LENGTH || Encoding.UTF8.GetByteCount(safeStem) > MAX_NAME_BYTES ) {
                safeStem = MakeSafe(Shorten(stem, MAX_NAME_LENGTH - 1, MAX_NAME_BYTES - 1));
            }
            return safeStem + extension;
        }

        ///<summary>Returns the name if it fits the limits, or else its start that does, without splitting a character made of
        /// two UTF-16 chars or ending with a dot or space.</summary>
        static string Shorten(string name, int maxLength, int maxBytes) {
            var length = Math.Min(name.Length, maxLength);
            while ( length > 0 && ( ( length < name.Length && char.IsHighSurrogate(name[length - 1]) ) || Encoding.UTF8.GetByteCount(name.Substring(0, length)) > maxBytes ) ) {
                length--;
            }
            return length == name.Length ? name : name.Substring(0, length).TrimEnd('.', ' ');
        }

        static string MakeSafe(string stem) {
            // Unity ignores hidden files, whose names start with a dot, eg. ".png"
            if ( stem.Length == 0 || stem[0] == '.' ) { stem = "_" + stem; }

            // Windows reserves device names, even with an extension
            var dot = stem.IndexOf('.');
            var device = dot < 0 ? stem : stem.Substring(0, dot);
            if ( RESERVED_NAMES.Contains(device.TrimEnd(' ')) ) {
                stem = device + "_" + stem.Substring(device.Length);
            }
            return stem;
        }

        ///<summary>Returns the path, or a unique one from AssetDatabase.GenerateUniqueAssetPath ("name 1.ext", "name 2.ext"...) if it is taken.
        /// Paths claimed by other files of this import are taken, since they may not be written yet, and so are names that differ
        /// only in case, since they are the same path on case-insensitive file systems and to Unity.</summary>
        static string UniquePath(string path, HashSet<string> claimedPaths) {
            var folder = path.Substring(0, path.LastIndexOf('/'));
            var stem = System.IO.Path.GetFileNameWithoutExtension(path);
            var extension = System.IO.Path.GetExtension(path);
            if ( !IsTaken(path, claimedPaths) ) {
                return path;
            }
            var candidate = AssetDatabase.GenerateUniqueAssetPath(path);
            for ( var number = 1; string.IsNullOrEmpty(candidate) || IsTaken(candidate, claimedPaths); number++ ) {
                var numbered = $"{folder}/{stem} {number}{extension}";
                var unique = AssetDatabase.GenerateUniqueAssetPath(numbered);
                candidate = string.IsNullOrEmpty(unique) ? numbered : unique;
            }
            return candidate;
        }

        static bool IsTaken(string path, HashSet<string> claimedPaths) => claimedPaths.Contains(path) || ExistsIgnoringCase(path);

        ///<summary>True if the folder has a file or folder whose name matches ignoring case.</summary>
        static bool ExistsIgnoringCase(string path) {
            var separator = path.LastIndexOf('/');
            var folder = path.Substring(0, separator);
            var name = path.Substring(separator + 1);
            return Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder)
                .Any(entry => string.Equals(System.IO.Path.GetFileName(entry), name, StringComparison.OrdinalIgnoreCase));
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
