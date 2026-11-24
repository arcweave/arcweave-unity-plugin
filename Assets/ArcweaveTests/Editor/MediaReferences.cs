using UnityEngine;

namespace Arcweave.Tests
{
    ///<summary>An asset that references downloaded media, like a user's scene or prefab would.</summary>
    public class MediaReferences : ScriptableObject
    {
        public Texture2D image;
        public AudioClip clip;
    }
}
