using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Il2CppSystem.Text.RegularExpressions;
using UnityEngine;
using MelonLoader;
using Newtonsoft.Json.Linq;
using NowWatching;
using UnityEngine.Networking;
using UnityEngine.UI;
using VRC; 
using BuildInfo = NowWatching.BuildInfo;


[assembly: MelonInfo(typeof(Mod), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author)]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonColor(ConsoleColor.DarkGreen)]
//[assembly: MelonOptionalDependencies("ReMod.Core")]
namespace NowWatching
{
    public static class BuildInfo
    {
        public const string Name = "NowWatching";
        public const string Author = "ballfun";
        public const string Version = "0.2.0";
    }

    public class Mod : MelonMod
    {
        internal static MelonLogger.Instance Logger;
        public static VidLog LastPlay;
        private const string YTDLExe = "yt-dlp_x86.exe";
        static string YTDLPath = "VRChat_Data/Plugins/" + YTDLExe;

        #region SETTINGS

        public static MelonPreferences_Category MyPreferenceCategory;
        public static MelonPreferences_Entry<bool> AutoFetch;
        public static MelonPreferences_Entry<bool> ShowResolved;
        public static MelonPreferences_Entry<bool> ClearSceneChange;
        public static MelonPreferences_Entry<bool> ShowDebug;

        #endregion
        public override void OnApplicationStart()
        {
            Logger = LoggerInstance; 
            
            try
            {
                using var resourceStream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream( typeof(Mod),"yt-dlp_x86.exe");
                if (resourceStream == null) throw new Exception("WTF");
                using var fileStream = File.Open(YTDLPath, FileMode.Create, FileAccess.Write);
                resourceStream.CopyTo(fileStream);
            }
            catch (IOException ex)
            {
                MelonLogger.Warning("Failed to write native exe");
            }
            HarmonyInstance.Patch(
                AccessTools.Method(typeof(VRC.LogFile), "Enqueue"),
                postfix: new HarmonyMethod(typeof(Mod), nameof(PatchLog)));

            
            WatchUI.Initialize();
            MyPreferenceCategory = MelonPreferences.CreateCategory("NowWatching");
            AutoFetch = MyPreferenceCategory.CreateEntry("AutoFetch", true,"Auto Fetch","Auto fetch data when a video is played");
            ClearSceneChange = MyPreferenceCategory.CreateEntry("ClearSceneChange", true,"Clear after world change","Clear active video on world change");
            ShowResolved = MyPreferenceCategory.CreateEntry("ShowResolved", false,"Show Resolved","!THIS MAY LEAK IP! show resolved copy button");
            ShowDebug = MyPreferenceCategory.CreateEntry("ShowDebug", false,"Show Debug","Show debug UI",true);
            ShowDebug.OnValueChanged += (b, b1) => WatchUI.UpdateSettings();
            ShowResolved.OnValueChanged += (b, b1) => WatchUI.UpdateSettings();
        }


        public static void PatchLog(Il2CppSystem.DateTime __0, LogType __1, string __2, string __3)
        {
            string line = Regex.Replace(__3, "<.*?>", string.Empty);
            if (!line.StartsWith("[Video Playback] ") && !line.StartsWith("[VRCX] ")) return;
            WatchLog.Log(__0, __1, __2, __3, ref LastPlay);

        }

        public override void OnUpdate()
        {
            if (LastPlay != null && (WatchUI.IsUIOpen || AutoFetch.Value) && LastPlay.UpdateNeeded)
            {
                WatchUI.UpdateInfo();
                LastPlay.UpdateNeeded = false;
                if (!LastPlay.ThumbnailFetched)
                {
                    GetThumbnail(ref LastPlay);
                }
            }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (ClearSceneChange.Value)
            {
                if (LastPlay != null&&LastPlay.Url!="") ClearLastPlay();
            }
        }

        static void ClearLastPlay()
        {
            LastPlay = new VidLog();
            WatchUI.UpdateInfo();
        }
        //*****************************FETCHING DATA*****************************//
        private static bool isUpdatingMeta;
        public static async void UpdateMetaData(VidLog vidData)
        {
            if(isUpdatingMeta) return;
            if (vidData == null || vidData.Url == String.Empty) return;
            isUpdatingMeta = true;
            try
            {
                if (vidData.DlData == null&&!vidData.YtdlFailed)
                {
                    var j = await GetMetadata(vidData.Url);
                    //await GetMetadata(vidData);
                    if (j.Length > 1)
                    {
                        //MelonLogger.Msg($"JSON {j.Substring(0, Math.Min(j.Length, 500))}");
                        try
                        {
                            vidData.DlData = JObject.Parse(j); 
                            
                            //WatchUI.UpdateVidPanel();
                        }
                        catch(Exception e) { vidData.YtdlFailed = true; }
                    }else{ vidData.YtdlFailed = true; }

                    vidData.UpdateNeeded = true;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Something went wrong with YTDL {e.ToString()}");
            }
            isUpdatingMeta = false;
        }

        public static async Task<string> GetMetadata(string vidData)
        {
           //var fetched = await ProcessAsyncHelper.RunProcessAsync(AppContext.BaseDirectory+"\\"+YTDLPath.Replace('/','\\'),$"-j {vidData}",100);
           //MelonLogger.Msg($"[Fetched] {fetched}");
           var processInfo = new ProcessStartInfo(AppContext.BaseDirectory+"\\"+YTDLPath.Replace('/','\\'),
            $"-j {vidData}");

        processInfo.CreateNoWindow = true;
        processInfo.UseShellExecute = false;
        processInfo.RedirectStandardError = true;
        processInfo.RedirectStandardOutput = true;
        string json = "";
        var process = Process.Start(processInfo);
        process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (e != null)
            {
                string d = e.Data;
                if (d!=null&&d.StartsWith("{") && d.EndsWith("}"))
                {
                    json = d;
                }
            }
        };
        process.BeginOutputReadLine();

        process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (e != null)
            {
                if(e.Data!=null && e.Data.Length>1) MelonLogger.Warning("YTDL Error>>" + e.Data);
            }
        };
        process.BeginErrorReadLine();

        process.WaitForExit();

        //MelonLogger.Msg("ExitCode: {0}", process.ExitCode);
        process.Close();
        return json;
        }

        static void GetThumbnail(ref VidLog log)
        {
            if(log?.DlData == null) return;
            MelonCoroutines.Start(DownloadImage(log));
            
        }
        static IEnumerator  DownloadImage(VidLog log)
        {
            if (log.ThumbnailFetched)
            {
                yield break;
            }
            if (log.DlData.TryGetValue("thumbnails", out JToken t))
            {
                var a = t.ToArray();
                for (int i = a.Length-1; i >= 0; i--)
                {
                    var urlToken = a[i].SelectToken("url");
                    if(urlToken==null) continue;
                    string url = urlToken.ToString();
                    if (!url.EndsWith("webp"))
                    {
                        UnityWebRequest request;
                        log.ThumbnailFetched = true;
                        var handler = new DownloadHandlerTexture(false);
                        request = UnityWebRequestTexture.GetTexture(url);
                        request.downloadHandler = handler;
                        yield return request.SendWebRequest();


                        if (request.isNetworkError || request.isHttpError)
                        {
                            if(!request.error.Contains("404 Not Found")) MelonLogger.Warning($"Thumbnail[{i}] failed:{request.error}");
                        }
                        else
                            try
                            {
                                if (handler.texture)
                                {
                                    log.Thumbnail = handler.texture;
                                    
                                    log.UpdateNeeded = true;
                                    break;
                                }
                            }
                            catch(Exception e)
                            {
                                MelonLogger.Error($"IMGERR {e}");
                            }
                    }
                }
            }
        } 
    }
    
    
}

