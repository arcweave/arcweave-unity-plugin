using System;
using System.Threading.Tasks;

namespace Arcweave.Tests
{
    ///<summary>A project asset that imports from web through a local stub of Arcweave's web API.</summary>
    public class StubbedWebProjectAsset : ArcweaveProjectAsset
    {
        public string apiUrl;
        public string projectJson;
        ///<summary>When set, the project json is downloaded from this server through the plugin's own request, so its headers can be checked.
        /// Otherwise it is returned without a request.</summary>
        [NonSerialized] public StubApiServer server;
        ///<summary>The API key the project json was last downloaded with. Not serialized, so saving the asset can't write a key.</summary>
        [NonSerialized] public string fetchedWithApiKey;

        // protected internal, since the plugin makes its internals visible to the tests
        protected internal override string ApiUrl => apiUrl;

        protected override Task<string> FetchProjectJson(string apiKey) {
            fetchedWithApiKey = apiKey;
            if ( server != null ) {
                server.SetProjectJson(projectJson);
                return base.FetchProjectJson(apiKey);
            }
            return Task.FromResult(projectJson);
        }
    }
}
