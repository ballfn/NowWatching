using System;
using System.Diagnostics;
using System.IO;
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
using VRC; 
using BuildInfo = NowWatching.BuildInfo;
using Debug = UnityEngine.Debug;


[assembly: MelonInfo(typeof(Mod), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author)]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonColor(ConsoleColor.DarkGreen)]

namespace NowWatching
{
    public static class BuildInfo
    {
        public const string Name = "NowShowing";
        public const string Author = "ballfun";
        public const string Version = "0.0.1";
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
            string dbAss = "";
            foreach (var v in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                dbAss += $"{v} | ";
            }
            Logger.Msg(dbAss);
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
                MelonLogger.Error("Failed to write native exe and no installed file found");
                MelonDebug.Msg(ex.ToString());
            }
            catch (Exception e)
            {
                MelonLogger.Error("other error "+e);
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

        public static string JsonCache;
        public static async void UpdateMetaData(VidLog vidData)
        {
            if (vidData == null || vidData.Url == String.Empty) return;
            try
            {
                if (vidData.DlData == null&&!vidData.YtdlFailed)
                {
                    var j = await GetMetadata(vidData.Url);
                    //await GetMetadata(vidData);
                    if (j.Length > 1)
                    {
                        MelonLogger.Msg($"JSON {j.Substring(0, Math.Min(j.Length, 500))}");
                        try
                        {
                            vidData.DlData = JObject.Parse(j); 
                            //WatchUI.UpdateVidPanel();
                        }
                        catch(Exception e) { vidData.YtdlFailed = true; }
                    }else{ vidData.YtdlFailed = true; }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Something went wrong with YTDL {e.ToString()}");
            }
            
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
                    MelonLogger.Msg("DL data found");
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

        MelonLogger.Msg("ExitCode: {0}", process.ExitCode);
        process.Close();
        return json;
        }
    }
}

