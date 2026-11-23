using System.Threading.Tasks;

namespace Arcweave.Tests
{
    ///<summary>A project asset that imports from web through a local stub of Arcweave's web API.</summary>
    public class StubbedWebProjectAsset : ArcweaveProjectAsset
    {
        public string apiUrl;
        public string projectJson;
        ///<summary>The API key the project json was last downloaded with.</summary>
        public string fetchedWithApiKey;

        protected override string ApiUrl => apiUrl;

        protected override Task<string> FetchProjectJson(string apiKey) {
            fetchedWithApiKey = apiKey;
            return Task.FromResult(projectJson);
        }
    }
}
