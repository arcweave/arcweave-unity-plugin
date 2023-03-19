using UnityEngine;


namespace Arcweave
{
    ///This is not required to utilize an arweave project but can be helpful for some projects as well as a learning example.
    public class ArcweavePlayer : MonoBehaviour
    {
        public delegate void OnProjectStart(Project project);
        public delegate void OnProjectFinish(Project project);
        public delegate void OnElementEnter(Element element);
        public delegate void OnElementOptions(State state, System.Action<int> next);
        public delegate void OnWaitingInputNext(System.Action next);

        public const string SAVE_KEY = "arcweave_save";

        public Arcweave.ArcweaveProjectAsset aw;

        public bool autoStart = true;

        private Element currentElement;
        private State currentState;

        public event OnProjectStart onProjectStart;
        public event OnProjectFinish onProjectFinish;
        public event OnElementEnter onElementEnter;
        public event OnElementOptions onElementOptions;
        public event OnWaitingInputNext onWaitInputNext;

        //...
        void Start() { if ( autoStart ) PlayProject(); }

        //...
        public void PlayProject() {
            if ( onProjectStart != null ) onProjectStart(aw.project);
            Next(aw.project.startingElement);
        }

        //...
        void Next(Element element) {
            currentElement = element;
            if ( onElementEnter != null ) onElementEnter(element);
            currentState = currentElement.GetState();
            if ( currentState.hasPaths ) {
                if ( currentState.hasOptions ) {
                    if ( onElementOptions != null ) onElementOptions(currentState, (index) => Next(currentState.paths[index].targetElement));
                    return;
                }

                if ( onWaitInputNext != null ) onWaitInputNext(() => Next(currentState.paths[0].targetElement));
                return;
            }
            currentElement = null;
            currentState = null;
            if ( onProjectFinish != null ) onProjectFinish(aw.project);
        }

        ///----------------------------------------------------------------------------------------------

        //...
        public void Save() {
            var id = currentElement.id;
            var variables = aw.project.SaveVariables();
            var save = string.Join("|", id, variables);
            PlayerPrefs.SetString(SAVE_KEY, save);
        }

        //...
        public void Load() {
            var save = PlayerPrefs.GetString(SAVE_KEY);
            var split = save.Split('|');
            var element = aw.project.ElementWithID(split[0]);
            aw.project.LoadVariables(split[1]);
            Next(element);
        }
    }
}