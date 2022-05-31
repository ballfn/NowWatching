using System;
using MelonLoader;
using ReMod.Core.VRChat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRC.UI.Core.Styles;
using Object = UnityEngine.Object;

namespace ReMod.Core.UI.QuickMenu
{
    public class MenuPanel : UiElement
    {
        private static GameObject _panelPrefab;
        private static GameObject _titlePrefab;
        private static GameObject _subtitlePrefab;
        private static GameObject _headingPrefab;
        private static GameObject _rowPrefab;

        private static GameObject TitlePrefab
        { get { if (_titlePrefab == null) { var v = PanelPrefab; }
                return _titlePrefab; } }
        private static GameObject SubtitlePrefab
        { get { if (_subtitlePrefab == null) { var v = PanelPrefab; } 
                return _subtitlePrefab; } 
        }
        private static GameObject HeadingPrefab
        { get { if (_headingPrefab == null) { var v = PanelPrefab; }
                return _headingPrefab; } 
        }
        public static GameObject RowPrefab
        { get { if (_rowPrefab == null) { var v = PanelPrefab; }
                return _rowPrefab; } 
        }

        private static GameObject PanelPrefab
        {
            get
            {
                if (_panelPrefab == null)
                {
                    MelonLogger.Msg("Finding Panel prefab");
                    _panelPrefab = QuickMenuEx.Instance.field_Public_Transform_0
                        .Find("Window/QMParent/Menu_QM_AvatarDetails/ScrollRect").GetComponent<ScrollRect>().content
                        .Find("Panel_AvatarDetailsCompact").gameObject;

                    _titlePrefab = _panelPrefab.transform.Find("Panel/Info/Text_AvatarName").gameObject;
                    _subtitlePrefab = _panelPrefab.transform.Find("Panel/Info/Text_AuthorName").gameObject;
                    _headingPrefab = _panelPrefab.transform.Find("Panel/Info/Text_DetailsHeader").gameObject;
                    _rowPrefab = _panelPrefab.transform.Find("Panel/Info/Row/Text_OriginalPerformance").parent.gameObject;

                    MelonLogger.Msg($"Found _titlePrefab|{_titlePrefab.name} _subtitlePrefab|{_subtitlePrefab.name} _headingPrefab|{_headingPrefab.name} _rowPrefab|{_rowPrefab.name}");
                    
                }
                return _panelPrefab;
            }
        }

        public UnityEngine.UI.Image Background;
        public UnityEngine.UI.RawImage Picture;
        public RectTransform InfoBox;
        public MenuPanel(string name, Transform parent,bool enablePicture) : base(PanelPrefab,
            parent, $"Panel_{name}")
        {
            var infoBox = RectTransform.Find("Panel/Info");
            int count = infoBox.childCount;
            for(int i = 0; i < count; i++)
            {
                Transform child = infoBox.GetChild(0);
                Object.DestroyImmediate(child.gameObject);
            }
            Background = RectTransform.Find("Panel/PanelBG").GetComponent<Image>();
            Picture = RectTransform.Find("Panel/Image_Mask/Image").GetComponent<RawImage>();
            InfoBox = RectTransform.Find("Panel/Info").GetComponent<RectTransform>();
            
            /*
            Background.rectTransform.anchorMin = new Vector2(0, 0);
            Background.rectTransform.anchorMax = new Vector2(1, 1);
            Background.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var fit = infoBox.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.MinSize;*/
            EnablePicture = enablePicture;
        }

        private bool _enablePicture = true;

        public bool EnablePicture
        {
            get => _enablePicture; 
            set
            {
                if (value)
                {
                    Picture.gameObject.SetActive(true);
                    InfoBox.offsetMin = new Vector2(443.15f, 12f);
                }
                else
                {
                    Picture.gameObject.SetActive(false);
                    InfoBox.offsetMin = new Vector2(12f, 12f);
                }
            }
        }
        public TextMeshProUGUI AddTitle(string name, string text)
        {
            return CreateTitle(name,text,InfoBox);
        }
        public TextMeshProUGUI AddSubTitle(string name, string text)
        {
            return CreateSubTitle(name,text,InfoBox);;
        }
        public TextMeshProUGUI AddHeading(string name, string text)
        {
            return CreateHeading(name,text,InfoBox);;
        }
        public MenuRow AddRoll(string name, string text,Sprite sprite = null)
        {
            return CreateRoll(name,text,InfoBox,sprite);
        }
        public static TextMeshProUGUI CreateTitle(string name, string text,RectTransform trans)
        {
            var obj = Object.Instantiate(TitlePrefab, trans);
            obj.name = $"TextHeading_{name}";
            var gui = obj.GetComponent<TextMeshProUGUI>();
            gui.text = text;
            return gui;
        }
        public static TextMeshProUGUI CreateSubTitle(string name, string text,RectTransform trans)
        {
            var obj = Object.Instantiate(SubtitlePrefab, trans);
            obj.name = $"TextHeading_{name}";
            var gui = obj.GetComponent<TextMeshProUGUI>();
            gui.text = text;
            return gui;
        }
        public static TextMeshProUGUI CreateHeading(string name, string text,RectTransform trans)
        {
            var obj = Object.Instantiate(HeadingPrefab, trans);
            obj.name = $"TextHeading_{name}";
            var gui = obj.GetComponent<TextMeshProUGUI>();
            gui.text = text;
            return gui;
        }
        
        public static MenuRow CreateRoll(string name, string text,RectTransform trans,Sprite sprite = null)
        {
            return new MenuRow(name, text, trans, sprite);
        }
        public static MenuPanel Create(string name, Transform parent,bool enablePicture = true)
        {
            return new MenuPanel(  name,  parent, enablePicture);
        }
    }

    public class MenuRow
    {
        public string text
        {
            get => TextGUI.text;
            set => TextGUI.text = value;
        }

        public GameObject gameObject;
        
        public TextMeshProUGUI TextGUI;
        public Image Icon;

        public MenuRow(string name, string text,RectTransform trans,Sprite sprite = null)
        {
            var obj = Object.Instantiate(MenuPanel.RowPrefab, trans);
            gameObject = obj;
            obj.name = $"TextRow_{name}";
            TextGUI = obj.GetComponentInChildren<TextMeshProUGUI>();
            TextGUI.name = $"Text_{name}";
            TextGUI.text = text;
            Icon = obj.GetComponentInChildren<Image>();
            Icon.name = $"Icon_{name}";
            SetSprite(sprite);
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite)
            {
                Icon.sprite = sprite;
            } 
            Icon.gameObject.SetActive(sprite!=null);
            
        }
    }
}
