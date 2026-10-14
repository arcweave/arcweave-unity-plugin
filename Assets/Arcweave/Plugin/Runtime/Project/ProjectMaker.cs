using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Claims;
using Arcweave.FullSerializer;
using Arcweave.Interpreter.INodes;
using UnityEngine;

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
        private HashSet<string> reportedMissing = new HashSet<string>();

        public ProjectMaker(string json, ArcweaveProjectAsset projectAsset) {

            this.jproject = fsJsonParser.Parse(json);
            if ( jproject.AsDictionary.TryGetValue(SUB_PROJECT_JSON_KEY, out var project) ) {
                this.jproject = project;
            }

            this.jboards = GetCollection("boards");
            this.jelements = GetCollection("elements"); //inode
            this.jbranches = GetCollection("branches"); //inode
            this.jjumpers = GetCollection("jumpers"); //inode
            this.jconditions = GetCollection("conditions"); //inode
            this.jconnections = GetCollection("connections");
            this.jcomponents = GetCollection("components");
            this.jattributes = GetCollection("attributes");
            this.jvariables = GetCollection("variables");
            this.jnotes = GetCollection("notes");

            this.boards = new Dictionary<string, Board>();
            this.nodes = new Dictionary<string, INode>();
            this.connections = new Dictionary<string, Connection>();
            this.components = new Dictionary<string, Component>();
            this.attributes = new Dictionary<string, Attribute>();
            this.variables = new Dictionary<string, Variable>();
            this.notes = new Dictionary<string, Note>();
        }

        public Project MakeProject() {
            var name = GetProp(jproject, "name")?.AsString;
            var startElementid = GetProp(jproject, "startingElement")?.AsString;
            if ( string.IsNullOrEmpty(startElementid) ) {
                throw new System.InvalidOperationException("The project has no starting element. Set one in Arcweave and import again.");
            }
            var startElement = TryMakeElement(startElementid);
            if ( startElement == null ) {
                throw new System.InvalidOperationException($"The project's starting element '{startElementid}' was not found in the project.");
            }
            var projBoards = new List<Board>();
            var projComponents = new List<Component>();
            var projVariables = new List<Variable>();
            foreach ( var key in jboards.AsDictionary.Keys ) {
                var board = TryMakeBoard(key);
                if ( board != null ) { projBoards.Add(board); } //null = has children or missing
            }

            // Only nodes on an imported board are initialized, so the story must not start or lead anywhere else
            var boardNodes = new HashSet<INode>(projBoards.SelectMany(board => board.Nodes));
            if ( !boardNodes.Contains(startElement) ) {
                throw new System.InvalidOperationException($"The project's starting element '{startElementid}' is not on any imported board.");
            }
            DetachTargetsOutsideBoards(boardNodes);

            foreach ( var key in jcomponents.AsDictionary.Keys ) {
                var component = TryMakeComponent(key);
                if ( component != null ) { projComponents.Add(component); } //null = has children
            }
            foreach ( var key in jvariables.AsDictionary.Keys ) {
                var variable = TryMakeVariable(key);
                if ( variable != null && variable.Parent == null ) { projVariables.Add(variable); } //null = has children or a scoped variable
            }
            MakeAttributeVariables();
            return new Project(name, startElement, projBoards, projComponents, projVariables);
        }

        //connections and jumpers can lead to nodes that exist but are not on an imported board, eg. when an export leaves out some boards
        void DetachTargetsOutsideBoards(HashSet<INode> boardNodes) {
            // Connections and jumpers that are not on an imported board can't be reached, so only the ones that are matter
            foreach ( var connection in connections.Values ) {
                if ( connection.Source != null && !boardNodes.Contains(connection.Source) ) { continue; }
                if ( connection.Target == null ) {
                    Debug.LogWarning($"Arcweave import: connection '{connection.Id}' has no target, so it leads nowhere.");
                }
                else if ( !boardNodes.Contains(connection.Target) ) {
                    Debug.LogWarning($"Arcweave import: connection '{connection.Id}' leads to '{connection.Target.Id}', which is not on any imported board, so it leads nowhere.");
                    connection.Set(connection.RawLabel, connection.Source, null);
                }
            }
            foreach ( var jumper in boardNodes.OfType<Jumper>() ) {
                if ( jumper.Target != null && !boardNodes.Contains(jumper.Target) ) {
                    Debug.LogWarning($"Arcweave import: jumper '{jumper.Id}' leads to '{jumper.Target.Id}', which is not on any imported board, so it leads nowhere.");
                    jumper.Set(jumper.Id, jumper.Pos, null);
                }
            }
        }

        ///----------------------------------------------------------------------------------------------

        //...
        Board TryMakeBoard(string id) {

            if ( !Exists(jboards, id, "board") ) { return null; }
            if ( HasChildren(jboards, id) ) { return null; }

            var name = GetProp(jboards, id, "name")?.AsString;
            var boardNodes = new List<INode>();
            foreach ( var key in GetList(jboards, id, "elements") ) { AddIfNotNull(boardNodes, TryMakeElement(GetId(key))); }
            foreach ( var key in GetList(jboards, id, "branches") ) { AddIfNotNull(boardNodes, TryMakeBranch(GetId(key))); }
            foreach ( var key in GetList(jboards, id, "jumpers") ) { AddIfNotNull(boardNodes, TryMakeJumper(GetId(key))); }

            foreach ( var branch in boardNodes.OfType<Branch>().ToArray() ) {
                foreach ( var condition in branch.Conditions ) {
                    boardNodes.Add(condition);
                }
            }

            var boardNotes = new List<Note>();
            foreach ( var key in GetList(jboards, id, "notes") ) { AddIfNotNull(boardNotes, TryMakeNote(GetId(key))); }

            var boardAttributes = new List<Attribute>();
            foreach ( var attributeid in GetList(jboards, id, "attributes") ) {
                AddIfNotNull(boardAttributes, TryMakeAttribute(GetId(attributeid)));
            }

            var customId = GetProp(jboards, id, "customId")?.AsString;
            var board = boards[id] = new Board(id, name, customId, boardNodes, boardNotes);
            foreach ( var attribute in boardAttributes ) {
                board.AddAttribute(attribute);
            }
            return board;
        }

        //..
        Note TryMakeNote(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !notes.TryGetValue(id, out var note) ) {
                if ( !Exists(jnotes, id, "note") ) { return null; }
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
                if ( !Exists(jelements, id, "element") ) { return null; }
                nodes[id] = node = new Element();
                var pos = GetPos(GetProp(jelements, id, "x"), GetProp(jelements, id, "y"));
                var title = GetProp(jelements, id, "title")?.AsString;
                var content = GetProp(jelements, id, "content")?.AsString;
                var theme = GetProp(jelements, id, "theme")?.AsString;
                var outputs = new List<Connection>();
                foreach ( var connectionid in GetList(jelements, id, "outputs") ) {
                    AddIfNotNull(outputs, TryMakeConnection(GetId(connectionid)));
                }

                var components = new List<Component>();
                foreach ( var componentid in GetList(jelements, id, "components") ) {
                    AddIfNotNull(components, TryMakeComponent(GetId(componentid))); //null = has children or missing
                }

                var attributes = new List<Attribute>();
                foreach ( var attributeid in GetList(jelements, id, "attributes") ) {
                    AddIfNotNull(attributes, TryMakeAttribute(GetId(attributeid)));
                }

                var cover = MakeCover(jelements, id);
                AudioAsset[] audioAssets = MakeAudioAssets(jelements, id);

                (node as Element).Set(id, pos, outputs, title, content, components, attributes, cover, theme, audioAssets);
            }
            return (Element)node;
        }

        //...
        Jumper TryMakeJumper(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !nodes.TryGetValue(id, out var node) ) {
                if ( !Exists(jjumpers, id, "jumper") ) { return null; }
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
                if ( !Exists(jbranches, id, "branch") ) { return null; }
                nodes[id] = node = new Branch();
                var pos = GetPos(GetProp(jbranches, id, "x"), GetProp(jbranches, id, "y"));
                var conditions = new List<Condition>();
                var ifConditionid = GetProp(jbranches, id, "conditions.ifCondition")?.AsString;
                if ( string.IsNullOrEmpty(ifConditionid) ) {
                    Debug.LogWarning($"Arcweave import: branch '{id}' has no if condition.");
                }
                AddIfNotNull(conditions, TryMakeCondition(ifConditionid));

                foreach ( var elseIfid in GetList(jbranches, id, "conditions.elseIfConditions") ) {
                    AddIfNotNull(conditions, TryMakeCondition(GetId(elseIfid)));
                }

                var elseid = GetProp(jbranches, id, "conditions.elseCondition")?.AsString;
                AddIfNotNull(conditions, TryMakeCondition(elseid));
                var theme = GetProp(jbranches, id, "theme")?.AsString;
                ( node as Branch ).Set(id, pos, conditions, theme);
            }
            return (Branch)node;
        }

        //...
        Condition TryMakeCondition(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !nodes.TryGetValue(id, out var node) ) {
                if ( !Exists(jconditions, id, "condition") ) { return null; }
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
                if ( !Exists(jconnections, id, "connection") ) { return null; }
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

                if ( !Exists(jcomponents, id, "component") ) { return null; }
                if ( HasChildren(jcomponents, id) ) { return null; }

                components[id] = component = new Component();
                var name = GetProp(jcomponents, id, "name")?.AsString;
                var customId = GetProp(jcomponents, id, "customId")?.AsString;
                var attributes = new List<Attribute>();
                foreach ( var attributeid in GetList(jcomponents, id, "attributes") ) {
                    AddIfNotNull(attributes, TryMakeAttribute(GetId(attributeid)));
                }
                var cover = MakeCover(jcomponents, id);
                component.Set(id, customId, name, attributes, cover);
            }
            return component;
        }

        //...
        Attribute TryMakeAttribute(string id) {
            if ( string.IsNullOrEmpty(id) ) { return null; }
            if ( !attributes.TryGetValue(id, out var attribute) ) {
                if ( !Exists(jattributes, id, "attribute") ) { return null; }
                attributes[id] = attribute = new Attribute();
                var name = GetProp(jattributes, id, "name")?.AsString;
                var customId = GetProp(jattributes, id, "customId")?.AsString;

                var jcontainerType = GetProp(jattributes, id, "cType")?.AsString;
                IAttribute.ContainerType containerType = IAttribute.ContainerType.Undefined;
                if ( jcontainerType == "elements" ) { containerType = IAttribute.ContainerType.Element; }
                if ( jcontainerType == "components" ) { containerType = IAttribute.ContainerType.Component; }
                if ( jcontainerType == "boards" ) { containerType = IAttribute.ContainerType.Board; }

                var containerId = GetProp(jattributes, id, "cId")?.AsString;

                IAttribute.DataType type = IAttribute.DataType.Undefined;
                object data = null;

                var jtype = GetProp(jattributes, id, "value.type")?.AsString;
                if ( jtype == "string" ) {
                    var isPlain = HasProperty(jattributes, id, "value.plain", out var plain) && plain.AsBool;
                    type = isPlain ? IAttribute.DataType.StringPlainText : IAttribute.DataType.StringRichText;
                    data = GetProp(jattributes, id, "value.data")?.AsString ?? (isPlain ? string.Empty : null);
                }

                if ( jtype == "component-list" ) {
                    type = IAttribute.DataType.ComponentList;
                    var componentList = new List<Component>();
                    foreach ( var componentid in GetList(jattributes, id, "value.data") ) {
                        AddIfNotNull(componentList, TryMakeComponent(GetId(componentid))); //null = has children or missing
                    }
                    data = componentList;
                }

                var jdata = GetProp(jattributes, id, "value.data");
                if ( jtype == "boolean" && jdata != null ) {
                    type = IAttribute.DataType.Boolean;
                    data = jdata.AsBool;
                }
                if ( jtype == "integer" && jdata != null ) {
                    type = IAttribute.DataType.Integer;
                    data = (int)jdata.AsInt64;
                }
                if ( jtype == "float" && jdata != null ) {
                    type = IAttribute.DataType.Float;
                    data = jdata.Type == fsDataType.Double ? jdata.AsDouble : (double)jdata.AsInt64;
                }
                attribute.Set(id, customId, name, type, data, containerType, containerId);
            }
            return attribute;
        }

        //...
        Variable TryMakeVariable(string id) {

            if ( !Exists(jvariables, id, "variable") ) { return null; }
            if ( HasChildren(jvariables, id) ) { return null; }
            if ( variables.TryGetValue(id, out var existingVariable) ) { return existingVariable; }

            object value = null;
            var name = GetProp(jvariables, id, "name")?.AsString;
            var type = GetProp(jvariables, id, "type")?.AsString;
            var jvalue = GetProp(jvariables, id, "value");
            var cType = GetProp(jvariables, id, "cType")?.AsString;

            if ( jvalue == null ) {
                Debug.LogWarning($"Arcweave import: variable '{name}' has no value, so it starts with the default value of its type.");
            }

            if ( type == "integer" ) { value = jvalue != null ? (int)jvalue.AsInt64 : 0; }

            if (type == "float")
            {
                if (jvalue == null)
                {
                    value = 0d;
                }
                else if (jvalue.Type == fsDataType.Double)
                {
                    value = (double)jvalue.AsDouble;
                }
                else
                {
                    value = (double)jvalue.AsInt64;
                }
            }
            if ( type == "string" ) { value = jvalue != null ? (string)jvalue.AsString : string.Empty; }
            if ( type == "boolean" ) { value = jvalue != null && (bool)jvalue.AsBool; }
            if (!string.IsNullOrEmpty(cType) && cType == "boards")
            {
                var boardId = GetProp(jvariables, id, "cId")?.AsString;
                if (boardId != null && boards.TryGetValue(boardId, out var board))
                {
                    var variable = new Variable(id, name, value, board);
                    board.AddVariable(variable);
                    variables[id] = variable;
                    return variable;
                }
                else
                {
                    Debug.LogWarning($"Variable '{name}' is supposed to be attached to board with id '{boardId}', but no such board was found.");
                    return null;
                }
            }
            return variables[id] = new Variable(id, name, value);
        }

        void MakeAttributeVariables() {
            foreach ( var board in boards.Values ) {
                MakeAttributeVariables(board, board.Id, IAttribute.ContainerType.Board);
            }
            foreach ( var component in components.Values ) {
                MakeAttributeVariables(component, component.Id, IAttribute.ContainerType.Component);
            }
        }

        void MakeAttributeVariables(IHasVariables container, string containerId, IAttribute.ContainerType containerType) {
            if ( string.IsNullOrEmpty(container.CustomId) ) { return; }

            var containerAttributes = container is Board board ? board.Attributes : ((Component)container).Attributes;
            foreach ( var attribute in containerAttributes ) {
                if ( !IsVariableAttribute(attribute, containerId, containerType) ) { continue; }
                if ( variables.ContainsKey(attribute.Id) ) { continue; }

                var variable = new Variable(attribute.Id, attribute.CustomId, attribute.data, container);
                variables[attribute.Id] = variable;
                container.AddVariable(variable);
            }
        }

        bool IsVariableAttribute(Attribute attribute, string containerId, IAttribute.ContainerType containerType) {
            if ( attribute == null || string.IsNullOrEmpty(attribute.CustomId) ) { return false; }
            if ( attribute.containerType != containerType || attribute.containerId != containerId ) { return false; }

            return attribute.Type == IAttribute.DataType.Boolean
                || attribute.Type == IAttribute.DataType.Integer
                || attribute.Type == IAttribute.DataType.Float
                || attribute.Type == IAttribute.DataType.StringPlainText;
        }

        ///----------------------------------------------------------------------------------------------

        //...
        Cover MakeCover(fsData source, string id) {
            var file = GetProp(source, id, "assets.cover.file")?.AsString;
            if ( !string.IsNullOrEmpty(file) ) {
                return new Cover(Cover.Type.Image, file);
            }

            var assetId = GetProp(source, id, "assets.cover.id")?.AsString;
            if ( !string.IsNullOrEmpty(assetId) ) {
                var assetName = GetProp(jproject, "assets." + assetId + ".name")?.AsString;
                if ( string.IsNullOrEmpty(assetName) ) {
                    Debug.LogWarning($"Arcweave import: the cover of '{id}' uses asset '{assetId}', which was not found in the project's assets, so it has no cover.");
                    return null;
                }
                return new Cover(Cover.Type.Image, assetName, assetId);
            }

            var url = GetProp(source, id, "assets.cover.url")?.AsString;
            if ( !string.IsNullOrEmpty(url) ) {
                return new Cover(Cover.Type.Youtube, url);
            }

            return null;
        }

        // Audio asset can be more than one per element
        AudioAsset[] MakeAudioAssets(fsData source, string id)
        {
            // Check if "assets.audio" exists and is a list (array)
            var audioProp = GetProp(source, id, "assets.audio");
            if (audioProp != null && audioProp.Type == fsDataType.Array)
            {
                var audioList = audioProp.AsList;
                var audioAssets = new AudioAsset[audioList.Count];
                for (int i = 0; i < audioList.Count; i++)
                {
                    audioAssets[i] = MakeAudioAsset(audioList[i]);
                }
                return audioAssets;
            }
            return null;
        }

        private AudioAsset MakeAudioAsset(fsData audioEntry)
        {
            var audioId = GetProp(audioEntry, "id")?.AsString;
            var mode = GetProp(audioEntry, "mode")?.AsString;
            string asset = GetProp(audioEntry, "asset")?.AsString;
            var delayProp = GetProp(audioEntry, "delay");
            float delay = 0f;
            if (delayProp != null)
            {
                delay = delayProp.Type == fsDataType.Double ? (float)delayProp.AsDouble : (float)delayProp.AsInt64;
            }

            if (string.IsNullOrEmpty(asset))
            {
                UnityEngine.Debug.LogWarning($"Audio asset with id {audioId} is missing the 'asset' field, " +
                $"which is required to retrieve the other info");
                return new AudioAsset(audioId, mode, asset, delay, null, 1f);
            }
            // Name and volume are stored in "assets" so we look for them using the "asset" value, which is the id of the audio asset
            var name = GetProp(jproject, "assets." + asset + ".name")?.AsString;
            var volumeProp = GetProp(jproject, "assets." + asset + ".volume");
            float volume = 1f;
            if (volumeProp != null)
            {
                volume = volumeProp.Type == fsDataType.Double ? (float)volumeProp.AsDouble : (float)volumeProp.AsInt64;
            }
            return new AudioAsset(audioId, mode, asset, delay, name, volume);
        }
        ///----------------------------------------------------------------------------------------------

        //...
        fsData GetProp(fsData source, string id, string propertyPath) => GetProp(source, id + '.' + propertyPath);

        //missing and null properties are both returned as null
        fsData GetProp(fsData source, string propertyPath) {
            var result = source[propertyPath];
            return result == null || result.IsNull ? null : result;
        }

        //missing, null or non-array properties are treated as empty lists, since exports may omit empty arrays
        List<fsData> GetList(fsData source, string id, string propertyPath) {
            var result = GetProp(source, id, propertyPath);
            return result != null && result.IsList ? result.AsList : new List<fsData>();
        }

        //a top level collection of the project, eg. "elements", treated as empty when missing
        fsData GetCollection(string key) {
            var result = GetProp(jproject, key);
            return result != null && result.IsDictionary ? result : new fsData(new Dictionary<string, fsData>());
        }

        //the id of a list entry, or null when the entry is not an id
        static string GetId(fsData entry) => entry != null && entry.IsString ? entry.AsString : null;

        //an id can point to an item that is not in the project, eg. an element of a board that was left out of the export
        bool Exists(fsData source, string id, string kind) {
            if ( GetProp(source, id) != null ) { return true; }
            // An item can be referenced many times, eg. by every jumper to an element of a board that was left out
            if ( reportedMissing.Add(kind + ' ' + id) ) {
                Debug.LogWarning($"Arcweave import: {kind} '{id}' was not found in the project, so it is skipped.");
            }
            return false;
        }

        static void AddIfNotNull<T>(List<T> list, T item) where T : class {
            if ( item != null ) { list.Add(item); }
        }

        //...
        bool HasChildren(fsData source, string id) { return HasProperty(source, id, "children", out var result); }

        //...
        bool HasProperty(fsData source, string id, string propertyPath, out fsData result) {
            result = GetProp(source, id, propertyPath);
            return result != null;
        }

        //missing coordinates default to 0
        UnityEngine.Vector2Int GetPos(fsData x, fsData y) => new UnityEngine.Vector2Int(GetCoordinate(x), GetCoordinate(y));

        static int GetCoordinate(fsData value) {
            if ( value == null ) { return 0; }
            return value.Type == fsDataType.Double ? (int)value.AsDouble : (int)value.AsInt64;
        }
    }
}
