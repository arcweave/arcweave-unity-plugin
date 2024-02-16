using System.Collections.Generic;

namespace Arcweave.Project
{
    ///<summary>Represents the currenst state of an Element with possible outgoing paths. Can be used to control the arcweave flow easier.</summary>
    public partial class Options
    {
        ///<summary>The element this state was generated from</summary>
        public Element Element { get; set; }
        ///<summary>The possible paths outgoing the element</summary>
        public List<Path> Paths { get; set; }
        ///<summary>Utility check if there are any paths</summary>
        public bool hasPaths => Paths != null;
        ///<summary>Utility check if there are actually options (also checks hasPaths)</summary>
        public bool hasOptions => hasPaths && ( Paths.Count > 1 || !string.IsNullOrEmpty(Paths[0].label) );

        ///<summary>Make a state object by provided Element.</summary>
        public Options(Element element) {
            Element = element;
            var validPaths = new List<Path>();
            foreach ( var output in element.Outputs ) {
                var path = output.ResolvePath(new Path());
                if ( path.isValid ) { validPaths.Add(path); }
            }
            Paths = validPaths.Count > 0 ? validPaths : null;

            //NOTE: Trick to keep the aw player behaviour same in regards to single outputs
            if ( Paths != null && Paths.Count == 1 ) {
                if (Paths[0].label == Paths[0].TargetElement.Title ) {
                    Paths[0].label = null;
                }
            }
        }
    }

    ///<summary>Represents the path from an Element to the next possible Element if any, with the according label that led to that Element.</summary>
    public partial class Path
    {
        ///<summary>The last label that lead to the target element</summary>
        public string label { get; set; }
        ///<summary>The element that this path will lead/land to</summary>
        public Element TargetElement { get; set; }
        public List<Connection> _connections { get; set; }
        internal bool isValid => TargetElement != null;
        internal static Path Invalid => default(Path);

        ///Appends a connection to the path. This is called from Connection when it resolves path
        public void AppendConnection(Connection connection) {
            if ( _connections == null ) { _connections = new List<Connection>(); }
            _connections.Add(connection);
        }

        ///Executes the appended connection labels (and thus execute arcscript in those connections)
        public void ExecuteAppendedConnectionLabels() {
            foreach ( var connection in _connections ) {
                connection.RunLabelScript();
            }
        }
    }
}