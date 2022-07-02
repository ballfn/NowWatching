using System;
using System.Reflection;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2CppSystem.Text.RegularExpressions;
using UnityEngine;
using MelonLoader;
using OpenBrowser;
using BuildInfo = OpenBrowser.BuildInfo;


[assembly: MelonInfo(typeof(Mod), BuildInfo.Name, BuildInfo.Version, BuildInfo.Author)]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonColor(ConsoleColor.DarkGreen)]
namespace OpenBrowser
{
    public static class BuildInfo
    {
        public const string Name = "OpenBroswer";
        public const string Author = "ballfun";
        public const string Version = "1.0.0";
    }

    public class Mod : MelonMod
    {
        internal static MelonLogger.Instance Logger;

        #region SETTINGS

        public static MelonPreferences_Category MyPreferenceCategory;
        
        public static MelonPreferences_Entry<bool> IsEnable;
        public static MelonPreferences_Entry<bool> TrustedOnly;

        #endregion
        public override void OnApplicationStart()
        {
            Logger = LoggerInstance;
            MyPreferenceCategory = MelonPreferences.CreateCategory("Open Browser");
            IsEnable = MyPreferenceCategory.CreateEntry("isEnable",true, "Enable Open Browser", "Enable opening browser");
            TrustedOnly = MyPreferenceCategory.CreateEntry("trustedOnly", true, "Trusted Only", "Only open browser for trusted URL");
            
            HarmonyInstance.Patch(
                AccessTools.Method(typeof(VRC.LogFile), "Enqueue"),
                postfix: new HarmonyMethod(typeof(Mod), nameof(PatchLog)));
        }


        public static void PatchLog(Il2CppSystem.DateTime __0, LogType __1, string __2, string __3)
        {
            if(!IsEnable.Value) return;
            string line = Regex.Replace(__3, "<.*?>", string.Empty);
            
            if (line.StartsWith("[Video Playback] Attempting to resolve URL"))
            {
                var url = line.Replace("[Video Playback] Attempting to resolve URL","").Trim().Split('\'');
                if(url[1].StartsWith("http://localhost/Temporary_Listen_Addresses/openURL/"))
                {
                    if(Uri.TryCreate(url[1].Replace("http://localhost/Temporary_Listen_Addresses/openURL/",""), UriKind.Absolute, out var uri)){
                        if(!uri.ToString().StartsWith("http")) return;
                        if (trustedURL.Any(x => uri.Host.EndsWith(x))||!TrustedOnly.Value)
                        {
                            Logger.Msg("Opening browser for " + uri.Host);
                            Application.OpenURL(uri.ToString());
                        }
                    }
                }
            }
            
        }
        //list based on https://github.com/YukiYukiVirtual/OpenBrowserServer/blob/master/setting.yaml
        private static string[] trustedURL = new[]
        {
            "alice-books.com",
            "amazon.co.jp",
            "avatarmuseum.jp",
            "bandcamp.com",
            "bilibili.com",
            "booth.pm",
            "camp-fire.jp",
            "ci-en.net",
            "circle.ms",
            "clip-studio.com",
            "discord.gg",
            "dlsite.com",
            "fanbox.cc",
            "fantia.jp",
            "github.com",
            "gitlab.com",
            "kotobukiya.co.jp",
            "linkco.re",
            "melonbooks.co.jp",
            "mixcloud.com",
            "mochi152.com",
            "nico.ms",
            "nicovideo.jp",
            "pictsquare.net",
            "pixiv.net",
            "seed.online",
            "ske.be",
            "skeb.jp",
            "sketchfab.com",
            "soundcloud.com",
            "steamcommunity.com",
            "steampowered.com",
            "streamlabs.com",
            "suzuri.jp",
            "techbookfest.org",
            "thebase.in",
            "tinami.com",
            "toranoana.jp",
            "tunecore.co.jp",
            "twitch.tv",
            "twitter.com",
            "unity.com",
            "v-market.work",
            "virtualcast.jp",
            "vrchat.com",
            "vrchat.net",
            "vroid.com",
            "youtu.be",
            "youtube.com",
            "yukiyukivirtual.github.io",
            "yukiyukivirtual.net",
        };
    }
}