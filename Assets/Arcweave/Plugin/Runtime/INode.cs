using UnityEngine;

namespace Arcweave.Interpreter.INodes
{
    ///<summary>An interface for all things a connection can have as source and/or target</summary>
    public partial interface INode
    {
        Vector2Int Pos { get; }
        void InitializeInProject(Arcweave.Project.Project project);
    }
}