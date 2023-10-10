using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Arcweave.Transpiler;

namespace Arcweave
{
    //...
    [System.Serializable]
    public class Element : INode
    {
        [field: SerializeField]
        public string id { get; private set; }
        [field: SerializeField]
        public Vector2Int pos { get; private set; }
        [field: SerializeField]
        public string rawTitle { get; private set; }
        [field: SerializeField]
        public string rawContent { get; private set; }
        [field: SerializeField]
        public string colorTheme { get; private set; }
        [field: SerializeReference]
        public List<Component> components { get; private set; }
        [field: SerializeField]
        public List<Attribute> attributes { get; private set; }
        [field: SerializeField]
        public Cover cover { get; private set; }
        [field: SerializeField]
        public List<Connection> outputs { get; private set; }

        public Project project { get; private set; }

        public string displayTitle => Utils.CleanString(rawTitle);
        public string displayContent => Utils.CleanString(rawContent);

        ///The number of visits to this element
        public int visits { get; set; }

        void INode.InitializeInProject(Project project) { this.project = project; }
        Path INode.ResolvePath(Path p) {
            if ( string.IsNullOrEmpty(p.label) ) { p.label = displayTitle; }
            p.targetElement = this;
            return p;
        }

        internal void Set(string id, Vector2Int pos, List<Connection> outputs, string rawTitle, string rawContent, List<Component> components, List<Attribute> attributes, Cover cover, string colorTheme) {
            this.id = id;
            this.pos = pos;
            this.outputs = outputs;
            this.rawTitle = rawTitle;
            this.rawContent = rawContent;
            this.components = components;
            this.attributes = attributes;
            this.cover = cover;
            this.colorTheme = colorTheme;
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Returns the runtime content taking into account and executing arcscript</summary>
        public string GetRuntimeContent() {
            var i = new Interpreter(project, id);
            var output = i.RunScript(rawContent);
            if ( output.changes.Count > 0 ) {
                foreach ( var change in output.changes ) {
                    project.SetVariable(change.Key, change.Value);
                }
            }
            return output.output;
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Represents the state of the element with possible paths to next elements taking into account conditions, invalid jumper links, etc.</summary>
        public State GetState() {
            var save = project.SaveVariables();
            var state = new State(this);
            project.LoadVariables(save);
            return state;
        }

        ///<summary>Has any content at all?</summary>
        public bool HasContent() => !string.IsNullOrEmpty(rawContent);
        ///<summary>Has any Component?</summary>
        public bool HasComponent(string name) => TryGetComponent(name, out var component);
        ///<summary>Try get a Component by name.</summary>
        public bool TryGetComponent(string name, out Component component) {
            component = components.FirstOrDefault(x => x.name == name);
            return component != null;
        }

        ///<summary>Returns the cover image if exists, otherwise the first component image.</summary>
        public Texture2D GetCoverOrFirstComponentImage() {
            var result = GetCoverImage();
            return result != null ? result : GetFirstComponentCoverImage();
        }
        ///<summary>Returns a Texture2D asset by the same image name as the one used in Arcweave and loaded from a "Resources" asset folder.</summary>
        public Texture2D GetCoverImage() => cover?.ResolveImage();
        ///<summary>Returns a Texture2D asset of the first component attached to the element by the same image name as the one used in Arcweave and loaded from a "Resources" asset folder.</summary>
        public Texture2D GetFirstComponentCoverImage() => components != null && components.Count > 0 ? components[0].GetCoverImage() : null;
    }
}