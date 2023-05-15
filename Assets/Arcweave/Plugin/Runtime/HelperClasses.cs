using System.Collections.Generic;

namespace Arcweave
{
    ///<summary>Represents the currenst state of an Element with possible outgoing paths. Can be used to control the arcweave flow easier.</summary>
    public class State
    {
        ///<summary>The element this state was generated from</summary>
        public Element element { get; private set; }
        ///<summary>The possible paths outgoing the element</summary>
        public Path[] paths;
        ///<summary>Utility check if there are any paths</summary>
        public bool hasPaths => paths != null;
        ///<summary>Utility check if there are actually options (also checks hasPaths)</summary>
        public bool hasOptions => hasPaths && ( paths.Length > 1 || !string.IsNullOrEmpty(paths[0].label) );

        ///<summary>Make a state object by provided Element.</summary>
        public State(Element element) {
            this.element = element;
            var validPaths = new List<Path>();
            foreach ( var output in element.outputs ) {
                var path = output.ResolvePath(new Path());
                if ( path.isValid ) { validPaths.Add(path); }
            }
            paths = validPaths.Count > 0 ? validPaths.ToArray() : null;

            //NOTE: Trick to keep the aw player behaviour same in regards to single outputs
            if ( paths != null && paths.Length == 1 ) {
                if ( paths[0].label == paths[0].targetElement.title ) {
                    paths[0].label = null;
                }
            }
        }
    }

    ///<summary>Represents the path from an Element to the next possible Element if any, with the according label that led to that Element.</summary>
    public struct Path
    {
        ///<summary>The last label that lead to the target element</summary>
        public string label;
        ///<summary>The element that this path will lead/land to</summary>
        public Element targetElement;
        private List<Connection> connections;
        internal bool isValid => targetElement != null;
        internal static Path Invalid => default(Path);

        ///Appends a connection to the path. This is called from Connection when it resolves path
        internal void AppendConnection(Connection connection) {
            if ( connections == null ) { connections = new List<Connection>(); }
            connections.Add(connection);
        }

        ///Executes the appended connection labels (and thus execute arcscript in those connections)
        internal void ExecuteAppendedConnectionLabels() {
            foreach ( var connection in connections ) {
                connection.GetRuntimeLabel(); // we dont care what it returns, we just call to execute
            }
        }
    }
}