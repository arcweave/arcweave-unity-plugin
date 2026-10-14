using System.Threading.Tasks;

namespace Arcweave.Tests
{
    ///<summary>A project asset that imports from web through a local stub of Arcweave's web API.</summary>
    public class StubbedWebProjectAsset : ArcweaveProjectAsset
    {
        public string apiUrl;
        public string projectJson;

        protected override string ApiUrl => apiUrl;

        protected override Task<string> FetchProjectJson() => Task.FromResult(projectJson);
    }
}
