﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Arcweave.Project;

namespace Arcweave
{
    ///A default example GUI for the ArcweavePlayer player
    public class ArcweavePlayerUI : MonoBehaviour
    {
        [Header("References")]
        public ArcweavePlayer player;
        public Text content;
        public RawImage cover;
        public Button buttonTemplate;
        public Button saveButton;
        public Button loadButton;
        public RawImage componentCover;

        private const float CROSSFADE_TIME = 0.3f;

        private List<Button> tempButtons = new List<Button>();

        void OnEnable() {
            componentCover.gameObject.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
            if ( !PlayerPrefs.HasKey(ArcweavePlayer.SAVE_KEY) ) {
                loadButton.gameObject.SetActive(false);
            }

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

        void OnElementEnter(Element e) {
            componentCover.gameObject.SetActive(false);
            content.text = "<i>[ No Content ]</i>";
            if (e.HasContent())
            {
                e.RunContentScript();
                content.text = e.RuntimeContent;
            }
            content.canvasRenderer.SetAlpha(0);
            content.CrossFadeAlpha(1f, CROSSFADE_TIME, false);

            var image = e.GetCoverOrFirstComponentImage();
            if ( cover.texture != image && image != null ) {
                cover.texture = image;
                cover.canvasRenderer.SetAlpha(0);
                cover.CrossFadeAlpha(1f, CROSSFADE_TIME, false);
            }
            if ( image == null ) {
                cover.canvasRenderer.SetAlpha(1);
                cover.CrossFadeAlpha(0f, CROSSFADE_TIME, false);
            }

            var compImage = e.GetFirstComponentCoverImage();
            if ( componentCover.texture != compImage && compImage != null ) {
                componentCover.texture = compImage;
                componentCover.canvasRenderer.SetAlpha(0);
                componentCover.CrossFadeAlpha(1f, CROSSFADE_TIME, false);
            }
            if ( compImage == null ) {
                componentCover.canvasRenderer.SetAlpha(1);
                componentCover.CrossFadeAlpha(0f, CROSSFADE_TIME, false);
            }
        }

        void OnElementOptions(Options options, System.Action<int> callback) {
            for ( var i = 0; i < options.Paths.Count; i++ ) {
                var _i = i; //local var for the delegate
                var text = !string.IsNullOrEmpty(options.Paths[i].label) ? options.Paths[i].label : "<i>[ N/A ]</i>";
                var button = MakeButton(text, () => callback(_i));
                var pos = button.transform.position;
                pos.y += buttonTemplate.GetComponent<RectTransform>().rect.height * ( options.Paths.Count - 1 - i );
                button.transform.position = pos;
            }
        }

        void OnWaitInputNext(System.Action callback) {
            MakeButton("...", callback);
        }

        void OnProjectFinish(Project.Project p) {
            MakeButton("Restart", player.PlayProject);
        }

        ///----------------------------------------------------------------------------------------------

        Button MakeButton(string name, System.Action call) {
            var button = Instantiate<Button>(buttonTemplate);
            tempButtons.Add(button);

            var text = button.GetComponentInChildren<Text>();
            var image = button.GetComponent<Image>();
            text.text = name;
            button.transform.SetParent(buttonTemplate.transform.parent);
            button.transform.position = buttonTemplate.transform.position;
            button.gameObject.SetActive(true);

            text.canvasRenderer.SetAlpha(0);
            text.CrossFadeAlpha(1f, CROSSFADE_TIME, false);
            image.canvasRenderer.SetAlpha(0);
            image.CrossFadeAlpha(1f, CROSSFADE_TIME, false);

            button.onClick.AddListener(() =>
            {
                ClearTempButtons();
                call();
            });

            return button;
        }

        void ClearTempButtons() {
            foreach ( var b in tempButtons ) {
                Destroy(b.gameObject);
            }
            tempButtons.Clear();
        }
    }
}