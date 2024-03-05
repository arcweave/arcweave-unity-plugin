using System.Collections.Generic;
using System.Linq;
using System.IO;
using Arcweave.FullSerializer;
using Arcweave.Interpreter.INodes;

namespace Arcweave.Project
{
    //Responsible for making the C# project from json (either directly or via web API)
    public class ProjectMaker
    {
        const string SUB_PROJECT_JSON_KEY = "project";
        const string ARCSCRIPT_CODE_JSON_KEY = "ArcscriptImplementations";

        private fsData jarcscript;
        private fsData jproject;
        private fsData jboards;
        private fsData jelements;
        private fsData jbranches;
        private fsData jjumpers;
        private fsData jconditions;
        private fsData jconnections;
        private fsData jcomponents;
        private fsData jattributes;
        private fsData jvariables;
        private fsData jnotes;

        private Dictionary<string, Board> boards;
        private Dictionary<string, INode> nodes;
        private Dictionary<string, Connection> connections;
        private Dictionary<string, Component> components;
        private Dictionary<string, Attribute> attributes;
        private Dictionary<string, Variable> variables;
        private Dictionary<string, Note> notes;

        public ProjectMaker(string json, ArcweaveProjectAsset projectAsset) {

            this.jproject = fsJsonParser.Parse(json);
            if ( jproject.AsDictionary.TryGetValue(SUB_PROJECT_JSON_KEY, out var project) ) {
                this.jproject = project;
            }

            this.jboards = jproject.AsDictionary["boards"];
            this.jelements = jproject.AsDictionary["elements"]; //inode
            this.jbranches = jproject.AsDictionary["branches"]; //inode
            this.jjumpers = jproject.AsDictionary["jumpers"]; //inode
            this.jconditions = jproject.AsDictionary["conditions"]; //inode
            this.jconnections = jproject.AsDictionary["connections"];
            this.jcomponents = jproject.AsDictionary["components"];
            this.jattributes = jproject.AsDictionary["attributes"];
            this.jvariables = jproject.AsDictionary["variables"];
            this.jnotes = jproject.AsDictionary["notes"];

            this.boards = new Dictionary<string, Board>();
            this.nodes = new Dictionary<string, INode>();
            this.connections = new Dictionary<string, Connection>();
            this.components = new Dictionary<string, Component>();
            this.attributes = new Dictionary<string, Attribute>();
            this.variables = new Dictionary<string, Variable>();
            this.notes = new Dictionary<string, Note>();
        }

        public Project MakeProject() {
            var name = jproject["name"]?.AsString;
            var startElementid = jproject["startingElement"]?.AsString;
            var startElement = TryMakeElement(startElementid);
            var projBoards = new List<Board>();
            var projComponents = new List<Component>();
            var projVariables = new List<Variable>();
            foreach ( var key in jboards.AsDictionary.Keys ) {
                var board = TryMakeBoard(key);
                if ( board != null ) { projBoards.Add(board); } //null = has children
            }
            foreach ( var key in jcomponents.AsDictionary.Keys ) {
                var component = TryMakeComponent(key);
                if ( component != null ) { projComponents.Add(component); } //null = has children
            }
            foreach ( var key in jvariables.AsDictionary.Keys ) {
                var variable = TryMakeVariable(key);
                if ( variable != null ) { projVariables.Add(variable); } //null = has children
            }
            return new Project(name, startElement, projBoards, projComponents, projVariables);
        }

        ///----------------------------------------------------------------------------------------------

        //...
        Board TryMakeBoard(string id) {

            if ( HasChildren(jboards, id) ) { return null; }

            var name = GetProp(jboards, id, "name")?.AsString;
            var boardNodes = new List<INode>();
            foreach ( var key in GetProp(jboards, id, "elements").AsList ) { boardNodes.Add(TryMakeElement(key.AsString)); }
            foreach ( var key in GetProp(jboards, id, "branches").AsList ) { boardNodes.Add(TryMakeBranch(key.AsString)); }
            foreach ( var key in GetProp(jboards, id, "jumpers").AsList ) { boardNodes.Add(TryMakeJumper(key.AsString)); }

            foreach ( var branch in boardNodes.OfType<Branch>().ToArray() ) {
                foreach ( var condition in branch.Conditions ) {
                    boardNodes.Add(condition);
                }
            }

            var boardNotes = new List<Note>();
            foreach ( var key in GetProp(jboards, id, "notes").AsList ) { boardNotes.Add(TryMakeNote(key.AsString)); }

            return boards[id] = new Board(id, name, boardNodes, boardNotes);
        }

        //..
        Note TryMakeNote(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !notes.TryGetValue(id, out var note) ) {
                notes[id] = note = new Note();
                var pos = GetPos(GetProp(jnotes, id, "x"), GetProp(jnotes, id, "y"));
                var content = GetProp(jnotes, id, "content")?.AsString;
                var theme = GetProp(jnotes, id, "theme")?.AsString;
                note.Set(id, pos, content, theme);
            }
            return note;
        }

        //...
        Element TryMakeElement(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !nodes.TryGetValue(id, out var node) ) {
                nodes[id] = node = new Element();
                var pos = GetPos(GetProp(jelements, id, "x"), GetProp(jelements, id, "y"));
                var title = GetProp(jelements, id, "title")?.AsString;
                var content = GetProp(jelements, id, "content")?.AsString;
                var theme = GetProp(jelements, id, "theme")?.AsString;
                var outputs = new List<Connection>();
                var outputids = GetProp(jelements, id, "outputs");
                foreach ( var connectionid in outputids.AsList ) {
                    outputs.Add(TryMakeConnection(connectionid.AsString));
                }

                var components = new List<Component>();
                var componentids = GetProp(jelements, id, "components");
                foreach ( var componentid in componentids.AsList ) {
                    var component = TryMakeComponent(componentid.AsString);
                    if ( component != null ) components.Add(component); //null = has children
                }

                var attributes = new List<Attribute>();
                if ( HasProperty(jelements, id, "attributes", out var attributeids) ) {
                    foreach ( var attributeid in attributeids.AsList ) {
                        attributes.Add(TryMakeAttribute(attributeid.AsString));
                    }
                }

                var cover = MakeCover(jelements, id);
                ( node as Element ).Set(id, pos, outputs, title, content, components, attributes, cover, theme);
            }
            return (Element)node;
        }

        //...
        Jumper TryMakeJumper(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !nodes.TryGetValue(id, out var node) ) {
                nodes[id] = node = new Jumper();
                var pos = GetPos(GetProp(jjumpers, id, "x"), GetProp(jjumpers, id, "y"));
                var elementId = GetProp(jjumpers, id, "elementId")?.AsString;
                var target = TryMakeElement(elementId);
                ( node as Jumper ).Set(id, pos, target);
            }
            return (Jumper)node;
        }

        //...
        Branch TryMakeBranch(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !nodes.TryGetValue(id, out var node) ) {
                nodes[id] = node = new Branch();
                var pos = GetPos(GetProp(jbranches, id, "x"), GetProp(jbranches, id, "y"));
                var conditions = new List<Condition>();
                var ifConditionid = GetProp(jbranches, id, "conditions.ifCondition")?.AsString;
                conditions.Add((Condition)TryMakeCondition(ifConditionid));

                if ( HasProperty(jbranches, id, "conditions.elseIfConditions", out var elseIfids) ) {
                    foreach ( var elseIfid in elseIfids.AsList ) {
                        conditions.Add((Condition)TryMakeCondition(elseIfid.AsString));
                    }
                }

                if ( HasProperty(jbranches, id, "conditions.elseCondition", out var elseid) ) {
                    conditions.Add((Condition)TryMakeCondition(elseid.AsString));
                }
                var theme = GetProp(jbranches, id, "theme")?.AsString;
                ( node as Branch ).Set(id, pos, conditions, theme);
            }
            return (Branch)node;
        }

        //...
        Condition TryMakeCondition(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !nodes.TryGetValue(id, out var node) ) {
                nodes[id] = node = new Condition();
                var outputid = GetProp(jconditions, id, "output")?.AsString;
                var script = GetProp(jconditions, id, "script")?.AsString;
                var output = TryMakeConnection(outputid);
                ( node as Condition ).Set(id, output, script);
            }
            return (Condition)node;
        }

        //...
        Connection TryMakeConnection(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !connections.TryGetValue(id, out var connection) ) {
                connections[id] = connection = new Connection(id);
                var label = GetProp(jconnections, id, "label")?.AsString;
                var sourceid = GetProp(jconnections, id, "sourceid")?.AsString;
                var targetid = GetProp(jconnections, id, "targetid")?.AsString;
                var sourceType = GetProp(jconnections, id, "sourceType")?.AsString;
                var targetType = GetProp(jconnections, id, "targetType")?.AsString;
                INode source = null;
                INode target = null;
                if ( sourceType == "elements" ) { source = TryMakeElement(sourceid); }
                if ( sourceType == "branches" ) { source = TryMakeBranch(sourceid); } //not used?
                if ( sourceType == "jumpers" ) { source = TryMakeJumper(sourceid); }
                if ( sourceType == "conditions" ) { source = TryMakeCondition(sourceid); }

                if ( targetType == "elements" ) { target = TryMakeElement(targetid); }
                if ( targetType == "branches" ) { target = TryMakeBranch(targetid); }
                if ( targetType == "jumpers" ) { target = TryMakeJumper(targetid); }
                if ( targetType == "conditions" ) { target = TryMakeCondition(targetid); } //not used?
                connection.Set(label, source, target);
            }
            return connection;
        }

        //...
        Component TryMakeComponent(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !components.TryGetValue(id, out var component) ) {

                if ( HasChildren(jcomponents, id) ) { return null; }

                components[id] = component = new Component();
                var name = GetProp(jcomponents, id, "name")?.AsString;
                var attributes = new List<Attribute>();
                var attributeids = GetProp(jcomponents, id, "attributes").AsList;
                foreach ( var attributeid in attributeids ) {
                    attributes.Add(TryMakeAttribute(attributeid.AsString));
                }
                var cover = MakeCover(jcomponents, id);
                component.Set(id, name, attributes, cover);
            }
            return component;
        }

        //...
        Attribute TryMakeAttribute(string id) {
            if ( !attributes.TryGetValue(id, out var attribute) ) {
                attributes[id] = attribute = new Attribute();
                var name = GetProp(jattributes, id, "name")?.AsString;

                var jcontainerType = GetProp(jattributes, id, "cType")?.AsString;
                IAttribute.ContainerType containerType = IAttribute.ContainerType.Undefined;
                if ( jcontainerType == "elements" ) { containerType = IAttribute.ContainerType.Element; }
                if ( jcontainerType == "components" ) { containerType = IAttribute.ContainerType.Component; }

                var containerId = GetProp(jattributes, id, "cId")?.AsString;

                IAttribute.DataType type = IAttribute.DataType.Undefined;
                object data = null;

                var jtype = GetProp(jattributes, id, "value.type")?.AsString;
                if ( jtype == "string" ) {
                    type = IAttribute.DataType.StringRichText;
                    data = GetProp(jattributes, id, "value.data")?.AsString;
                    if ( HasProperty(jattributes, id, "value.plain", out var isPlain) && isPlain.AsBool == true ) {
                        type = IAttribute.DataType.StringPlainText;
                    }
                }

                if ( jtype == "component-list" ) {
                    type = IAttribute.DataType.ComponentList;
                    var componentList = new List<Component>();
                    var componentids = GetProp(jattributes, id, "value.data").AsList;
                    foreach ( var componentid in componentids ) {
                        var component = TryMakeComponent(componentid.AsString);
                        if ( component != null ) { componentList.Add(component); } //null = has children
                    }
                    data = componentList;
                }
                attribute.Set(name, type, data, containerType, containerId);
            }
            return attribute;
        }

        //...
        Variable TryMakeVariable(string id) {

            if ( HasChildren(jvariables, id) ) { return null; }

            object value = null;
            var name = GetProp(jvariables, id, "name")?.AsString;
            var type = GetProp(jvariables, id, "type")?.AsString;
            var jvalue = GetProp(jvariables, id, "value");
            if ( type == "integer" ) { value = (int)jvalue.AsInt64; }
            if ( type == "float" ) { value = (float)jvalue.AsDouble; }
            if ( type == "string" ) { value = (string)jvalue.AsString; }
            if ( type == "boolean" ) { value = (bool)jvalue.AsBool; }
            return new Variable(name, value);
        }

        ///----------------------------------------------------------------------------------------------

        //...
        Cover MakeCover(fsData source, string id) {
            fsData coverSource = null;

            if ( HasProperty(source, id, "assets.cover.file", out coverSource) ) {
                return new Cover(Cover.Type.Image, coverSource.AsString);
            }

            if ( HasProperty(source, id, "assets.cover.id", out coverSource) ) {
                coverSource = jproject["assets." + coverSource.AsString + ".name"];
                return new Cover(Cover.Type.Image, coverSource.AsString);
            }

            if ( HasProperty(source, id, "assets.cover.url", out coverSource) ) {
                return new Cover(Cover.Type.Youtube, coverSource.AsString);
            }

            return null;
        }

        ///----------------------------------------------------------------------------------------------

        //...
        fsData GetProp(fsData source, string id, string propertyPath) {
            var result = source[id + '.' + propertyPath];
            return result.IsNull ? null : result;
        }

        //...
        bool HasChildren(fsData source, string id) { return HasProperty(source, id, "children", out var result); }

        //...
        bool HasProperty(fsData source, string id, string propertyPath, out fsData result) {
            result = source[id + '.' + propertyPath];
            return result != null;
        }

        UnityEngine.Vector2Int GetPos(fsData x, fsData y)
        {
            var xValue = x.Type == fsDataType.Double ? (int)x.AsDouble : (int)x.AsInt64;
            var yValue = y.Type == fsDataType.Double ? (int)y.AsDouble : (int)y.AsInt64;

            return new UnityEngine.Vector2Int(xValue, yValue);
        }
    }
}