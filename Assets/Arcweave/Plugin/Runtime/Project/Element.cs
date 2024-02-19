using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Arcweave.Interpreter;
using Arcweave.Interpreter.INodes;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Element
    {
        [field: SerializeField]
        public string Id { get; private set; }
        [field: SerializeField]
        public Vector2Int Pos { get; private set; }
        [field: SerializeField]
        public string Title { get; private set; }
        [field: SerializeField]
        public string RawContent { get; private set; }
        [field: SerializeField]
        public string RuntimeContent { get; private set; }
        [field: SerializeField]
        public string ColorTheme { get; private set; }
        [field: SerializeReference]
        public List<Component> Components { get; private set; }
        [field: SerializeField]
        public List<Attribute> Attributes { get; private set; }
        [field: SerializeField]
        public Cover cover { get; private set; }
        [field: SerializeField]
        public List<Connection> Outputs { get; private set; }

        public Project Project { get; private set; }
        // private System.Func<Project, string> runtimeContentFunc { get; set; }

        ///The number of visits to this element
        public int Visits { get; set; }

        void INode.InitializeInProject(Project project) { Project = project; }
        Path INode.ResolvePath(Path p) {
            if ( string.IsNullOrEmpty(p.label) ) { p.label = Title; }
            p.TargetElement = this;
            return p;
        }

        internal void Set(string id, Vector2Int pos, List<Connection> outputs, string rawTitle, string rawContent, List<Component> components, List<Attribute> attributes, Cover cover, string colorTheme) {
            Id = id;
            Pos = pos;
            Outputs = outputs;
            Title = rawTitle;
            RawContent = rawContent;
            Components = components;
            Attributes = attributes;
            this.cover = cover;
            ColorTheme = colorTheme;
        }

        ///----------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Runs the content script of the element. This will also update
        /// the RuntimeContent of the Element.
        /// </summary>
        public void RunContentScript()
        {
            AwInterpreter i = new AwInterpreter(Project, Id);
            var output = i.RunScript(RawContent);
            if ( output.Changes.Count > 0 ) {
                foreach ( var change in output.Changes ) {
                    Project.SetVariable(change.Key, change.Value);
                }
            }

            RuntimeContent = Utils.CleanString(output.Output);
        }

        ///----------------------------------------------------------------------------------------------

        ///<summary>Represents the state of the element with possible paths to next elements taking into account conditions, invalid jumper links, etc.</summary>
        public Options GetOptions()
        {
            return new Options(this);
        }
        
        /// <summary>
        /// Adds an attribute to the element
        /// </summary>
        /// <param name="attribute">The attribute to add</param>
        public void AddAttribute(Attribute attribute) {  Attributes.Add(attribute); }

        ///<summary>Has any content at all?</summary>
        public bool HasContent() => !string.IsNullOrEmpty(RawContent);
        ///<summary>Has any Component?</summary>
        public bool HasComponent(string name) => TryGetComponent(name, out var component);
        ///<summary>Try get a Component by name.</summary>
        public bool TryGetComponent(string name, out Component component) {
            component = Components.FirstOrDefault(x => x.Name == name);
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
        public Texture2D GetFirstComponentCoverImage() => Components != null && Components.Count > 0 ? Components[0].GetCoverImage() : null;
    }
}