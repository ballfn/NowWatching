using Newtonsoft.Json.Linq;
using System;
using ReMod.Core.Managers;
using ReMod.Core.UI.QuickMenu;
using TMPro;
using System.Collections;

using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MelonLoader;
using ReMod.Core.VRChat;
using UnityEngine;
using UnityEngine.UI;
using VRC.UI.Core;

namespace NowWatching
{
    public class WatchUI
    {
        public static ReTabButton MyTabButton;

        public static bool IsUIOpen { get; private set; }
        public static bool UIInited;

        public static MenuPanel VidPanel;
        public static TextMeshProUGUI GUITitle;
        public static MenuRow GUIUploader;
        public static MenuRow GUIView;
        public static MenuRow GUIRating;
        public static TextMeshProUGUI GUIDes;
        
        public static MenuPanel InfoPanel;
        public static TextMeshProUGUI GUIUrl;
        //public static TextMeshProUGUI GUIResolved;
        public static TextMeshProUGUI GUIError;

        public static ReMenuCategory DebugContainer;
        public static ReMenuButton ViewUrl;
        public static ReMenuButton CopyUrl;
        public static ReMenuButton CopyResolved;

        public static void PrepareMenu()
        {
             var page = new ReCategoryPage("Now Watching", true);
            var pageContent = page.RectTransform.Find("ScrollRect").GetComponent<ScrollRect>().content;
            MyTabButton = ReTabButton.Create("WatchingTab", "Now Watching", "NowWatching", ResourceManager.GetSprite("NowWatching.popcorn"));
            //*********Video Data Panel
            VidPanel = MenuPanel.Create("VidData",pageContent,true);
            VidPanel.ResizeImage(new Vector2(400,225));
            GUITitle = VidPanel.AddSubTitle("VidTitle","Title");
            GUITitle.maxVisibleLines = 4;
            GUITitle.rectTransform.anchorMin = new Vector2(0, 1);
            GUITitle.rectTransform.anchorMax = new Vector2(0, 1);
            GUITitle.gameObject.AddComponent<ContentSizeFitter>()
                .verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            GUIUploader= VidPanel.AddRoll("VidUpload","",ResourceManager.GetSprite("NowWatching.upload"));;
            GUIView= VidPanel.AddRoll("VidView","",ResourceManager.GetSprite("NowWatching.view"));
            GUIRating= VidPanel.AddRoll("VidRating","",ResourceManager.GetSprite("NowWatching.like"));
            
            //**********Buttons
            var actions = page.AddCategory("Actions");
            ViewUrl = actions.AddButton("Open in browser", "Open URL in browser", ButtonOpenURL,
                ResourceManager.GetSprite("NowWatching.web"));
            CopyUrl = actions.AddButton("Copy URL", "Copy URL to clipboard", ButtonCopyURL,
                ResourceManager.GetSprite("NowWatching.url"));
            CopyResolved = actions.AddButton("Copy resolved", "Copy resolved URL to clipboard !THIS MAY LEAK IP!", ButtonCopyResolved,
                ResourceManager.GetSprite("NowWatching.resolved"));
            
            var container = MenuPanel.CreateContainer("VidInfo", pageContent);
            GUIUrl = MenuPanel.CreateSubTitle("VidUrl", "Video information will be shown here",container);
            //GUIUrl.maxVisibleLines = 3;
            GUIError = MenuPanel.CreateSubTitle("Resolved", "",container);
            //GUIError.maxVisibleLines = 3;
            DebugContainer = page.AddCategory("Debug");
            DebugContainer.AddButton("Copy JSON", "Copy JSON", ButtonCopyJSON,
                ResourceManager.GetSprite("NowWatching.url"));
            
            GUIDes= MenuPanel.CreateHeading("VidDes","Description",container);

            ResetVidPanel();
            
            page.OnOpen += () =>
            {
                IsUIOpen = true;
                UpdateInfo();
            };
            page.OnClose += () =>
            {
                IsUIOpen = false;
            };

            UIInited = true;
            UpdateSettings();

        }

        public static void UpdateSettings()
        {
            if(!UIInited) return;
            DebugContainer.Active = Mod.ShowDebug.Value;
            UpdateButtonState();
        }
        public static void UpdateInfo()
        {
            //if(!IsUIOpen) return;
            if (!UIInited) return;
            UpdateButtonState();
            if (NowWatching.Mod.LastPlay == null) return;
            
            //GUIUrl.gameObject.SetActive(Mod.LastPlay.Url!="");
            if (Mod.LastPlay.Url == "")
            {
                GUIUrl.text = "Video information will be shown here";
            }
            else
            {
                GUIUrl.text = Mod.ShowFullUrl.Value ? $"URL: {Mod.LastPlay.Url}":$"{new Uri(Mod.LastPlay.Url).Host}";
            }

            GUIError.gameObject.SetActive(Mod.LastPlay.Error!="");
            GUIError.text = $"ERROR: {Mod.LastPlay.Error}";

            if (Mod.LastPlay.DlData!=null&&!Mod.LastPlay.YtdlFailed)
            {
                UpdateVidPanel();
            }
            else
            {
                ResetVidPanel();
                if(!Mod.LastPlay.YtdlFailed)new Task(() => { Mod.UpdateMetaData(Mod.LastPlay);}).Start();
            }
        }

        public static void UpdateButtonState()
        {
            if (Mod.LastPlay == null)
            {
                ViewUrl.Active = false;
                CopyUrl.Active = false;
                CopyResolved.Active = false;
                return;
            }
            ViewUrl.Active = Mod.LastPlay.Url!="";
            CopyUrl.Active = Mod.LastPlay.Url!="";
            CopyResolved.Active = Mod.LastPlay.ResolvedUrl!=""&&Mod.LastPlay.Url!=Mod.LastPlay.ResolvedUrl&&Mod.ShowResolved.Value;
        }

        public static void Initialize()
        {
            OnUIManagerInitialized(PrepareMenu);
        }
        private static void OnUIManagerInitialized(Action code)
        {
            MelonCoroutines.Start(OnUiManagerInitCoroutine(code));
        }
        private static IEnumerator OnUiManagerInitCoroutine(Action code)
        {
            while (VRCUiManager.prop_VRCUiManager_0 == null) yield return null;
            CacheIcons();
            //early init

            while (UIManager.field_Private_Static_UIManager_0 == null)
                yield return null;
            while (GameObject.Find("UserInterface").GetComponentInChildren<VRC.UI.Elements.QuickMenu>(true) == null)
                yield return null;
            while (QuickMenuEx.Instance == null)
                yield return null;
            code();
            
        }
        public static void CacheIcons()
        {
            //https://github.com/RequiDev/ReModCE/blob/master/ReModCE/ReMod.cs
            var ourAssembly = Assembly.GetExecutingAssembly();
            var resources = ourAssembly.GetManifestResourceNames();
            foreach (var resource in resources)
            {
                if (!resource.EndsWith(".png"))
                    continue;

                var stream = ourAssembly.GetManifestResourceStream(resource);

                var ms = new MemoryStream();
                stream.CopyTo(ms);
                var resourceName = Regex.Match(resource, @"([a-zA-Z\d\-_]+)\.png").Groups[1].ToString();
                ResourceManager.LoadSprite("NowWatching", resourceName, ms.ToArray());
            }
        }

        public static void ResetVidPanel()
        {
            VidPanel.GameObject.SetActive(false);
            GUIUrl.text = "Video information will be shown here";
            GUITitle.text = "";
            GUIUploader.text = "";
            GUIRating.text = "";
            GUIView.text = "";
            VidPanel.EnablePicture = false;
            GUIDes.text = "";

        }

        public static void UpdateVidPanel()
        {
            if(Mod.LastPlay.DlData==null) return;
            bool anyHasValue = false;
            var d = Mod.LastPlay.DlData;
            if (d.TryGetValue("direct", out JToken t))
            {
                    VidPanel.GameObject.SetActive(anyHasValue);
                    return;
            }
            GetValue(GUITitle,"fulltitle");
            GetValueRow(GUIUploader,"uploader");
            GetValueRow(GUIRating,"like_count","{0}",true);
            GetValueRow(GUIView,"view_count","{0} Views",true);
            GetValue(GUIDes,"description","\n{0}");

            void GetValue(TextMeshProUGUI text,string key,string format = "{0}")
            {
                bool thisValue = false;
                if (d.TryGetValue(key, out JToken t))
                {
                    text.text = string.Format(format, t.ToString());
                    anyHasValue = true;
                    thisValue = true;
                }
                text.gameObject.SetActive(thisValue);
            }
            void GetValueRow(MenuRow text,string key,string format = "{0}",bool isInt = false)
            {
                bool thisValue = false;
                if (d.TryGetValue(key, out JToken t))
                {
                    string txt = t.ToString();
                    if (isInt)
                    {
                        if (int.TryParse(txt, out int i)) txt = $"{i:n0}";
                    } 
                    text.text =string.Format(format, txt);
                    anyHasValue = true;
                    thisValue = true;
                }
                text.gameObject.SetActive(thisValue);
            }
            VidPanel.EnablePicture= Mod.LastPlay.Thumbnail != null;
            if (Mod.LastPlay.Thumbnail)
            {
                VidPanel.ResizeImageScale(new Vector2(Mod.LastPlay.Thumbnail.width,Mod.LastPlay.Thumbnail.height));
                VidPanel.Picture.texture = Mod.LastPlay.Thumbnail;
            }
            VidPanel.UpdateInfoLayout();
            VidPanel.GameObject.SetActive(anyHasValue);
            //InfoPanel.UpdateInfoLayout();
        }

        static void ButtonCopyURL()
        {
            if (Mod.LastPlay==null) return;
            MelonLogger.Msg("ButtonCopyURL");
            Clipboard.SetText(Mod.LastPlay.Url);
        }
        static void ButtonCopyResolved()
        {
            if (Mod.LastPlay==null) return;
            MelonLogger.Msg("ButtonCopyResolved");
            Clipboard.SetText(Mod.LastPlay.ResolvedUrl);
        }
        static void ButtonCopyJSON()
        {
            if (Mod.LastPlay==null||Mod.LastPlay.DlData==null) return;
            MelonLogger.Msg("ButtonCopJSON");
            Clipboard.SetText(Mod.LastPlay.DlData.ToString());
        }
        static void ButtonOpenURL()
        {
            if (Mod.LastPlay==null) return;
            MelonLogger.Msg("ButtonOpenURL");
            UnityEngine.Application.OpenURL(Mod.LastPlay.Url);
        }
        
        
    }
    
    
}