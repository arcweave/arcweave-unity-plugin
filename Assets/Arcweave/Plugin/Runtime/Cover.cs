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

        private Texture2D _cachedImage;
        private bool _hasTriedLoad;

        public Cover(Type type, string filePath) {
            this.type = type;
            this.filePath = filePath;
        }

        public Texture2D ResolveImage() {
            if ( !_hasTriedLoad ) {
                _cachedImage = Resources.Load<Texture2D>(System.IO.Path.GetFileNameWithoutExtension(filePath));
                _hasTriedLoad = true;
            }
            return _cachedImage;
        }
    }
}