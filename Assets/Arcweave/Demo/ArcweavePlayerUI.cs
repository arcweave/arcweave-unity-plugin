using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Arcweave.Project;

namespace Arcweave
{
    /// <summary>
    /// Default example GUI for the ArcweavePlayer.
    /// Displays narrative content, cover images, and interactive choice buttons.
    /// </summary>
    public class ArcweavePlayerUI : MonoBehaviour
    {
        [Header("Core")]
        public ArcweavePlayer player;

        [Header("Text Display")]
        public Text content;
        [Tooltip("Text shown when element has no content")]
        public string noContentText = "<i>[ No Content ]</i>";
        [Tooltip("Text shown for empty options")]
        public string emptyOptionText = "<i>[ N/A ]</i>";
        [Tooltip("Text shown for continue button")]
        public string continueButtonText = "Continue";
        [Tooltip("Text shown for restart button")]
        public string restartButtonText = "Restart";

        [Header("Images")]
        public RawImage cover;
        [Tooltip("Optional: Character/entity portrait image")]
        public RawImage componentCover;

        [Header("Buttons")]
        public Button buttonTemplate;
        public Button saveButton;
        public Button loadButton;

        [Header("Animation")]
        [Tooltip("Enable fade in/out animations")]
        public bool enableFade = true;
        [Range(0.1f, 2f)]
        [Tooltip("Duration of fade in/out animations")]
        public float crossfadeTime = 0.5f;

        private List<Button> tempButtons = new List<Button>();

        void OnEnable() {
            buttonTemplate.gameObject.SetActive(false);
            InitializeImage(cover);
            InitializeImage(componentCover);

            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
            loadButton.gameObject.SetActive(PlayerPrefs.HasKey(ArcweavePlayer.SAVE_KEY));

            player.onElementEnter += OnElementEnter;
            player.onElementOptions += OnElementOptions;
            player.onWaitInputNext += OnWaitInputNext;
            player.onProjectFinish += OnProjectFinish;
        }

        void OnDisable() {
            player.onElementEnter -= OnElementEnter;
            player.onElementOptions -= OnElementOptions;
            player.onWaitInputNext -= OnWaitInputNext;
            player.onProjectFinish -= OnProjectFinish;
        }

        void Save() {
            player.Save();
            loadButton.gameObject.SetActive(true);
        }

        void Load() {
            ClearTempButtons();
            player.Load();
        }

        ///----------------------------------------------------------------------------------------------
        /// Event Handlers
        ///----------------------------------------------------------------------------------------------

        void OnElementEnter(Element e) {
            DisplayContent(e);
            DisplayImage(cover, e.GetCoverOrFirstComponentImage());

            var elementHasCover = e.GetCoverImage() != null;
            DisplayImage(componentCover, elementHasCover ? e.GetFirstComponentCoverImage() : null);
        }

        void OnElementOptions(Options options, System.Action<int> callback) {
            for (var i = 0; i < options.Paths.Count; i++) {
                var index = i;
                var text = !string.IsNullOrEmpty(options.Paths[i].text) ? options.Paths[i].text : emptyOptionText;
                MakeButton(text, () => callback(index));
            }
        }

        void OnWaitInputNext(System.Action callback) {
            MakeButton(continueButtonText, callback);
        }

        void OnProjectFinish(Project.Project p) {
            MakeButton(restartButtonText, player.PlayProject);
        }

        ///----------------------------------------------------------------------------------------------
        /// UI Display Methods
        ///----------------------------------------------------------------------------------------------

        void DisplayContent(Element e) {
            if (e.HasContent()) {
                e.RunContentScript();
            }
            content.text = e.HasContent() ? e.RuntimeContent : noContentText;

            if (enableFade) {
                content.canvasRenderer.SetAlpha(0);
                content.CrossFadeAlpha(1f, crossfadeTime, false);
            } else {
                content.canvasRenderer.SetAlpha(1);
            }
        }

        void DisplayImage(RawImage image, Texture2D texture) {
            if (image == null) return;
            StartCoroutine(FadeImage(image, texture));
        }

        System.Collections.IEnumerator FadeImage(RawImage image, Texture2D texture) {
            if (enableFade && image.gameObject.activeSelf && image.texture != texture) {
                image.CrossFadeAlpha(0f, crossfadeTime, false);
                yield return new WaitForSeconds(crossfadeTime);
            }

            if (texture != null) {
                image.gameObject.SetActive(true);
                image.texture = texture;
                UpdateAspectRatio(image, texture);

                if (enableFade) {
                    image.canvasRenderer.SetAlpha(0);
                    image.CrossFadeAlpha(1f, crossfadeTime, false);
                } else {
                    image.canvasRenderer.SetAlpha(1);
                }
            } else {
                image.gameObject.SetActive(false);
                image.texture = null;
            }
        }

        void InitializeImage(RawImage image) {
            if (image != null) {
                image.gameObject.SetActive(false);
                image.canvasRenderer.SetAlpha(0);
            }
        }

        void UpdateAspectRatio(RawImage image, Texture2D texture) {
            var fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter != null) {
                fitter.aspectRatio = (float)texture.width / texture.height;
            }
        }

        ///----------------------------------------------------------------------------------------------
        /// Button Management
        ///----------------------------------------------------------------------------------------------

        Button MakeButton(string label, System.Action onClick) {
            var button = Instantiate(buttonTemplate, buttonTemplate.transform.parent, false);
            button.transform.localPosition = buttonTemplate.transform.localPosition;
            button.gameObject.SetActive(true);
            tempButtons.Add(button);

            var text = button.GetComponentInChildren<Text>();
            text.text = label;

            var image = button.GetComponent<Image>();
            if (enableFade) {
                text.canvasRenderer.SetAlpha(0);
                text.CrossFadeAlpha(1f, crossfadeTime, false);
                image.canvasRenderer.SetAlpha(0);
                image.CrossFadeAlpha(1f, crossfadeTime, false);
            } else {
                text.canvasRenderer.SetAlpha(1);
                image.canvasRenderer.SetAlpha(1);
            }

            button.onClick.AddListener(() => {
                ClearTempButtons();
                onClick();
            });

            return button;
        }

        void ClearTempButtons() {
            foreach (var button in tempButtons) {
                Destroy(button.gameObject);
            }
            tempButtons.Clear();
        }
    }
}
