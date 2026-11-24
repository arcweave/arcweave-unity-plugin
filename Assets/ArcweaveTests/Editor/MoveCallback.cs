using System;
using UnityEditor;

namespace Arcweave.Tests
{
    ///<summary>Runs a test's callback when Unity is about to move an asset, like another editor extension could.</summary>
    public class MoveCallback : AssetModificationProcessor
    {
        public static Action<string, string> onWillMoveAsset;

        static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            onWillMoveAsset?.Invoke(sourcePath, destinationPath);
            return AssetMoveResult.DidNotMove;
        }
    }
}
