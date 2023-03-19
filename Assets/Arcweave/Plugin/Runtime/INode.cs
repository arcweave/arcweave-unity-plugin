namespace Arcweave
{
    ///An interface for all things a connection can have as source and/or target
    public interface INode
    {
        string id { get; }
        void InitializeInProject(Project project);
        Path ResolvePath(Path path);
    }
}