using UnityEngine;

namespace Arcweave.Project
{
    // Class representing the audio asset data found in the json
    [System.Serializable]
    public class AudioAsset
    {

        public enum Mode
        {
            Once,
            Stop, // should stop all the instances of the audio with the same "asset" value
            Loop,
        }

        /* Playback mode for the audio asset */
        [field: SerializeField]
        public Mode mode { get; private set; }

        // Unique identifier for the instance of the audio asset
        [field: SerializeField]
        public string id { get; private set; }

        /** Use this Id to retrieve the audio asset name/url from the json */
        [field: SerializeField]
        public string asset { get; private set; }

        // Delay before playing the audio asset
        [field: SerializeField]
        public float delay { get; private set; }

        // Reference to the loaded AudioClip
        private AudioClip _audioClip;

        // Volume level for the audio asset
        [field: SerializeField]
        public float volume { get; private set; }

        /** Name of the file comphrensive of the extension */
        [field: SerializeField]
        public string name { get; private set; }

        public AudioAsset(string audioId, string mode, string asset, float delay, string name, float volume)
        {
            this.id = audioId;
            this.asset = asset;
            this.delay = delay;
            this.name = name;
            this.volume = volume;

            if (System.Enum.TryParse(mode, true, out Mode parsedMode))
            {
                this.mode = parsedMode;
            }
            else
            {
                this.mode = Mode.Once;
            }
        }

        /// <summary>
        /// Attempts to retrieve the AudioClip from the Resources folder using the asset's name.
        /// </summary>
        /// <returns>The loaded AudioClip, or null if not found.</returns>
        public AudioClip TryGetAudioClip()
        {
            
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            string nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(name);
            if (!string.IsNullOrEmpty(nameWithoutExtension))
            {

                if (_audioClip == null || _audioClip.name != nameWithoutExtension)
                {
                    _audioClip = Resources.Load<AudioClip>(nameWithoutExtension);
                }
            }
            return _audioClip;
        }
    }

}