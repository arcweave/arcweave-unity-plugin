using System.Collections.Generic;

namespace Arcweave
{
    ///Represents the currenst state of an Element with possible outgoing paths. Can be used to control the arcweave flow easier.
    public class State
    {
        ///The elementthis state was generated from
        public Element element { get; private set; }
        ///The possible paths outgoing the element
        public Path[] paths;
        ///Utility check if there are any paths
        public bool hasPaths => paths != null;
        ///Utility check if there are actually options (also checks hasPaths)
        public bool hasOptions => hasPaths && ( paths.Length > 1 || !string.IsNullOrEmpty(paths[0].label) );
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

    ///Represents the path from an Element to the next possible Element if any, with the according label that led to that Element.
    public struct Path
    {
        ///The last label that lead to the target element
        public string label;
        ///The element that this path will lead/land to
        public Element targetElement;
        internal bool isValid => targetElement != null;
        internal static Path Invalid => default(Path);
    }
}