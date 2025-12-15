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
        [Range(0.1f, 2f)]
        [Tooltip("Duration of fade in/out animations")]
        public float crossfadeTime = 0.5f;

        private List<Button> tempButtons = new List<Button>();

        void OnEnable() {
            // Hide template and initialize UI elements
            buttonTemplate.gameObject.SetActive(false);
            InitializeImage(cover);
            InitializeImage(componentCover);

            // Setup save/load buttons
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
            loadButton.gameObject.SetActive(PlayerPrefs.HasKey(ArcweavePlayer.SAVE_KEY));

            // Subscribe to player events
            player.onElementEnter += OnElementEnter;
            player.onElementOptions += OnElementOptions;
            player.onWaitInputNext += OnWaitInputNext;
            player.onProjectFinish += OnProjectFinish;
        }

        void OnDisable() {
            // Unsubscribe from player events
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
            // Display element content
            DisplayContent(e);

            // Display cover image (element's cover or first component image)
            DisplayImage(cover, e.GetCoverOrFirstComponentImage());

            // Display component cover image (character/entity portrait)
            DisplayImage(componentCover, e.GetFirstComponentCoverImage());
        }

        void OnElementOptions(Options options, System.Action<int> callback) {
            // Create a button for each available choice
            for (var i = 0; i < options.Paths.Count; i++) {
                var index = i; // Capture index for closure
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
            content.text = e.HasContent() ? e.RuntimeContent : noContentText;
            if (e.HasContent()) {
                e.RunContentScript();
            }

            // Fade in content text
            content.canvasRenderer.SetAlpha(0);
            content.CrossFadeAlpha(1f, crossfadeTime, false);
        }

        void DisplayImage(RawImage image, Texture2D texture) {
            if (image == null) return;

            if (texture != null) {
                // Show image with fade-in if it changed
                image.gameObject.SetActive(true);
                if (image.texture != texture) {
                    image.texture = texture;
                    UpdateAspectRatio(image, texture);
                    image.canvasRenderer.SetAlpha(0);
                    image.CrossFadeAlpha(1f, crossfadeTime, false);
                }
            } else {
                // Fade out if no image
                image.CrossFadeAlpha(0f, crossfadeTime, false);
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
            // Create button from template
            var button = Instantiate(buttonTemplate, buttonTemplate.transform.parent, false);
            button.transform.localPosition = buttonTemplate.transform.localPosition;
            button.gameObject.SetActive(true);
            tempButtons.Add(button);

            // Set label
            var text = button.GetComponentInChildren<Text>();
            text.text = label;

            // Fade in button and text
            var image = button.GetComponent<Image>();
            text.canvasRenderer.SetAlpha(0);
            text.CrossFadeAlpha(1f, crossfadeTime, false);
            image.canvasRenderer.SetAlpha(0);
            image.CrossFadeAlpha(1f, crossfadeTime, false);

            // Add click handler
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
