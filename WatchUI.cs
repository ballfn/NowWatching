using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using ReMod.Core.Managers;
using ReMod.Core.UI.QuickMenu;
using TMPro;
using System.Collections;

using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MelonLoader;
using ReMod.Core.VRChat;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using VRC;
using VRC.UI.Core;
using WebSocketSharp;

namespace NowWatching
{
    public class WatchUI
    {
        public static ReTabButton MyTabButton;

        public static bool IsUIOpen { get; private set; }

        public static MenuPanel VidPanel;
        public static TextMeshProUGUI GUITitle;
        public static TextMeshProUGUI GUIUploader;
        public static MenuRow GUIView;
        public static TextMeshProUGUI GUIDes;
        
        public static MenuPanel InfoPanel;
        public static TextMeshProUGUI GUIUrl;
        public static TextMeshProUGUI GUIResolved;
        public static TextMeshProUGUI GUIError;

        public static void PrepareMenu()
        {
             var page = new ReCategoryPage("Now Watching", true);
            var pageContent = page.RectTransform.Find("ScrollRect").GetComponent<ScrollRect>().content;
            MyTabButton = ReTabButton.Create("WatchingTab", "Now Watching tab", "NowWatching", ResourceManager.GetSprite("NowWatching.popcorn"));
            //*********Video Data Panel
            VidPanel = MenuPanel.Create("VidData",pageContent,true);
            GUITitle = VidPanel.AddSubTitle("VidTitle","Title");
            GUITitle.maxVisibleLines = 3;
            GUITitle.rectTransform.anchorMin = new Vector2(0, 1);
            GUITitle.rectTransform.anchorMax = new Vector2(0, 1);
            GUITitle.m_minFontSize = 32;
            var fit = GUITitle.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            GUIUploader= VidPanel.AddSubTitle("VidUpload","");; 
            GUIView= VidPanel.AddRoll("VidRating","1B dislikes",ResourceManager.GetSprite("NowWatching.view"));
            GUIDes= VidPanel.AddHeading("VidDes","Description");;
            GUIDes.maxVisibleLines = 5;
            GUIDes.verticalAlignment = VerticalAlignmentOptions.Baseline;
            
            ResetVidPanel(true);
            InfoPanel = MenuPanel.Create("VidInfo",pageContent,false);
            InfoPanel.Background.gameObject.SetActive(false);
            GUIUrl = InfoPanel.AddTitle("VidUrl", "Video information will be shown here");
            GUIResolved = InfoPanel.AddSubTitle("Resolved", "");
            GUIResolved.maxVisibleLines = 3;
            GUIError = InfoPanel.AddSubTitle("Resolved", "");
            GUIError.maxVisibleLines = 3;

            page.OnOpen += () =>
            {
                IsUIOpen = true;
                UpdateInfo();
            };
            page.OnClose += () =>
            {
                IsUIOpen = false;
            };
        }

        public static void UpdateInfo()
        {
            if(!IsUIOpen) return;
            MelonLogger.Msg("Update Info called");
            if (!GUIUrl || !GUIResolved) return;
        
            if (NowWatching.Mod.LastPlay == null) return;
            
            GUIUrl.gameObject.SetActive(Mod.LastPlay.Url!="");
            GUIUrl.text = $"URL: {Mod.LastPlay.Url}";
            GUIResolved.gameObject.SetActive(Mod.LastPlay.ResolvedUrl!=""&&Mod.LastPlay.Url!=Mod.LastPlay.ResolvedUrl);
            GUIResolved.text = $"Resolved: {Mod.LastPlay.ResolvedUrl}";
            GUIError.gameObject.SetActive(Mod.LastPlay.Error!="");
            GUIError.text = $"ERROR: {Mod.LastPlay.Error}";

            if (Mod.LastPlay.DlData!=null&&!Mod.LastPlay.YtdlFailed)
            {
                UpdateVidPanel();
            }
            else
            {
                new Task(() => { Mod.UpdateMetaData(Mod.LastPlay);}).Start();
                
            }
            
            
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
                MelonLogger.Msg($"Icon {resourceName}");
                ResourceManager.LoadSprite("NowWatching", resourceName, ms.ToArray());
            }
        }

        public static void ResetVidPanel(bool startup = false)
        {
            if(startup) VidPanel.GameObject.SetActive(false);
            GUITitle.text = "Loading";
            GUIUploader.text = "";
            GUIView.text = "";
            GUIDes.text = "";
        }

        private static string[] JsonCheckingKey =
        {
            "title", "channel","view_count","description"
        };
        public static void UpdateVidPanel()
        {
            if(Mod.LastPlay.DlData==null) return;
            bool anyHasValue = false;
            var d = Mod.LastPlay.DlData;
            
            GetValue(GUITitle,"title");
            GetValue(GUIUploader,"channel");
            GetValueRow(GUIView,"view_count","{0} Views");
            GetValue(GUIDes,"description");

            if (d.TryGetValue("thumbnails", out JToken t))
            {
                //MelonLogger.Msg($"thumb {t.ToArray()}");
                var a = t.ToArray();
                for (int i = a.Length-1; i >= 0; i--)
                {
                    var urlToken = a[i].SelectToken("url");
                    if(urlToken==null) continue;
                    string url = urlToken.ToString();
                    MelonLogger.Msg(url);
                    if (!url.EndsWith("webp"))
                    {
                        MelonCoroutines.Start(DownloadImage(url,VidPanel.Picture));
                        break;
                    }
                }
            }

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
            void GetValueRow(MenuRow text,string key,string format = "{0}")
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
            VidPanel.GameObject.SetActive(anyHasValue);

        }

        private static string lastImageUrl;
        private static Texture lastRequest;
        static IEnumerator  DownloadImage(string MediaUrl,RawImage rawImage)
        {
            if ((lastImageUrl) == MediaUrl && lastRequest != null)
            {
                //request = lastRequest;
                rawImage.texture = lastRequest;
                yield break;
            }
            UnityWebRequest request;
            
            lastImageUrl = MediaUrl; 
            var handler = new DownloadHandlerTexture(false);
            request = UnityWebRequestTexture.GetTexture(MediaUrl);
            request.downloadHandler = handler;
            //lastRequest = request;
            yield return request.SendWebRequest();
                

                if (request.isNetworkError || request.isHttpError)
                    Debug.Log(request.error);
                else
                    try
                    {
                        if (handler.texture)
                        {
                            lastRequest = handler.texture;
                            rawImage.texture = lastRequest;
                        }
                        //lastRequest = ((DownloadHandlerTexture) request.downloadHandler).texture;
                        rawImage.texture = lastRequest;
                    }
                    catch(Exception e)
                    {
                        MelonLogger.Error($"IMGERR {e}");
                    }

        } 
    }
    
    
}