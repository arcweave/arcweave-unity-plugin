using Arcweave.Interpreter.INodes;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arcweave.Project
{
    //...
    [System.Serializable]
    public partial class Board : ISerializationCallbackReceiver
    {
        [field: SerializeField]
        public string Id { get; private set; }
        [field: SerializeField]
        /* Custom id used to distinguish between different board variables */
        public string CustomId { get; private set; }
        [field: SerializeField]
        public string Name { get; private set; }
        [field: SerializeReference]
        public List<INode> Nodes { get; private set; }
        [field: SerializeField]
        public List<Note> Notes { get; private set; }
        /* Old constructor for backward compatibility */
        [field: SerializeField]
        public List<Variable> Variables { get; private set; }

        public Board(string id, List<INode> nodes, string customId = null)
        {
            Id = id;
            Nodes = nodes;
            CustomId = customId;
            Variables = new List<Variable>();
        }


        public Board(string id, string customId, string name, List<INode> nodes, List<Note> notes) {
            Id = id;
            CustomId = customId;
            Name = name;
            Nodes = nodes;
            Notes = notes;
            Variables = new List<Variable>();
        }

        public Board(string id, List<INode> nodes, List<Variable> variables, string customId = null)
        {
            Id = id;
            Nodes = nodes;
            foreach (var variable in variables)
            {
                variable.Parent = this;
            }
            Variables = variables;
            CustomId = customId;
        }

        ///<summary>Returns INode of type T with id.</summary>
        public T NodeWithID<T>(string id) where T : INode => Nodes.OfType<T>().FirstOrDefault(x => x.Id == id);
        ///<summary>Returns Element with id.</summary>
        public Element ElementWithID(string id) => NodeWithID<Element>(id);

        public void AddVariable(Variable variable)
        {
            variable.Parent = this;
            Variables.Add(variable);
        }




        public void PrintBoard()
        {
            string boardInfo = $"Board: {Name} (id: {Id})\n";

            boardInfo += "------------Nodes ---------------\n";
            foreach (var node in Nodes)
            {
               boardInfo += $"  Node: {node.GetType().Name} (id: {node.Id})\n";

              
            }
            
            boardInfo += "------------Variables -----------\n";
            foreach (var variable in Variables)
            {
                boardInfo += variable.ContentAsString(false) + " \n";
            }
            
            Debug.Log(boardInfo);
        }

 #region ISerializationCallbackReceiver Interface

        public void OnBeforeSerialize()
        {
            //throw new System.NotImplementedException();
        }

        public void OnAfterDeserialize()
        {
            /* Parent is lost when the game is closed and reopened, so we need to set it again after deserialization */
            foreach (var variable in Variables)
            {
                variable.Parent = this;
            }
        }
#endregion

    }
}