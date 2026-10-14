using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public class Cover
    {
        public enum Type
        {
            Undefined,
            Image,
            Youtube,
        }

        [field: SerializeField]
        public Type type { get; private set; }
        [field: SerializeField]
        public string filePath { get; private set; }
        ///<summary>The id of the Arcweave asset used as the cover, if any.</summary>
        [field: SerializeField]
        public string assetId { get; private set; }
        ///<summary>The image downloaded from Arcweave's web API when importing from web.</summary>
        [field: SerializeField]
        public Texture2D downloadedImage { get; internal set; }

        [System.NonSerialized]
        private Texture2D _cachedImage;

        public Cover(Type type, string filePath) {
            this.type = type;
            this.filePath = filePath;
        }

        public Cover(Type type, string filePath, string assetId) : this(type, filePath) {
            this.assetId = assetId;
        }

        ///<summary>Returns the image downloaded from Arcweave's web API, otherwise resolves the image by name from a 'Resources' folder.</summary>
        public Texture2D ResolveImage() {
            if ( downloadedImage != null ) {
                return downloadedImage;
            }
            var imageName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            if ( _cachedImage == null || _cachedImage.name != imageName ) {
                _cachedImage = Resources.Load<Texture2D>(imageName);
            }
            return _cachedImage;
        }
    }
}