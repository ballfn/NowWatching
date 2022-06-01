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
using ReMod.Core.VRChat;
using UnityEngine.Networking;
using UnityEngine.UI;
using VRC; 
using BuildInfo = NowWatching.BuildInfo;


[assembly: MelonInfo(typeof(Mod), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author)]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonColor(ConsoleColor.DarkGreen)]
namespace NowWatching
{
    public static class BuildInfo
    {
        public const string Name = "NowWatching";
        public const string Author = "ballfun";
        public const string Version = "0.1.0";
    }

    public class Mod : MelonMod
    {
        internal static MelonLogger.Instance Logger;
        public static VidLog LastPlay;
        private const string YTDLExe = "yt-dlp_x86.exe";
        static string YTDLPath = "VRChat_Data/Plugins/" + YTDLExe;
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
        }


        public static void PatchLog(Il2CppSystem.DateTime __0, LogType __1, string __2, string __3)
        {
            string line = Regex.Replace(__3, "<.*?>", string.Empty);
            if (!line.StartsWith("[Video Playback] ") && !line.StartsWith("[VRCX] ")) return;
            WatchLog.Log(__0, __1, __2, __3, ref LastPlay);

        }

        public override void OnUpdate()
        {
            if (LastPlay!=null&&WatchUI.IsUIOpen && LastPlay.UpdateNeeded)
            {
                WatchUI.UpdateInfo();
                LastPlay.UpdateNeeded = false;
                if (!LastPlay.ThumbnailFetched)
                {
                    GetThumbnail(ref LastPlay);
                }
            }
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
                        MelonCoroutines.Start(DownloadImage(url,log));
                        break;
                    }
                }
            }
            log.ThumbnailFetched = true;
        }
        static IEnumerator  DownloadImage(string url,VidLog log)
        {
            if (log.ThumbnailFetched)
            {
                yield break;
            }
            UnityWebRequest request;
            log.ThumbnailFetched = true;
            var handler = new DownloadHandlerTexture(false);
            request = UnityWebRequestTexture.GetTexture(url);
            request.downloadHandler = handler;
            yield return request.SendWebRequest();
                

            if (request.isNetworkError || request.isHttpError)
                MelonLogger.Warning(request.error);
            else
                try
                {
                    if (handler.texture)
                    {
                        log.Thumbnail = handler.texture;
                        log.UpdateNeeded = true;
                    }
                }
                catch(Exception e)
                {
                    MelonLogger.Error($"IMGERR {e}");
                }
        } 
    }
    
    
}

