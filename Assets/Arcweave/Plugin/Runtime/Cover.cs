using UnityEngine;

namespace Arcweave
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

        [System.NonSerialized]
        private Texture2D _cachedImage;

        public Cover(Type type, string filePath) {
            this.type = type;
            this.filePath = filePath;
        }

        public Texture2D ResolveImage() {
            var imageName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            if ( _cachedImage == null || _cachedImage.name != imageName ) {
                _cachedImage = Resources.Load<Texture2D>(imageName);
            }
            return _cachedImage;
        }
    }
}