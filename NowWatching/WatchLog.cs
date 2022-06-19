using Newtonsoft.Json.Linq;
using Il2CppSystem;
using Il2CppSystem.Text.RegularExpressions;
using UnityEngine;

namespace NowWatching
{
    public class WatchLog
    {
        public static void Log(Il2CppSystem.DateTime __0, LogType __1, string __2, string __3,ref VidLog lastPlay)
        {
            string line = Regex.Replace(__3, "<.*?>", string.Empty);
            if (!line.StartsWith("[Video Playback] ") && !line.StartsWith("[VRCX] ")) return;
            if (lastPlay == null) lastPlay = new VidLog();
            
            var log = LogPhaser.ParseLog(line);
            
            switch (log.type)
            {
                case LogData.Type.Unknown:
                   lastPlay.Error = log.data[0];
                   lastPlay.UpdateNeeded = true;
                    break;
                case LogData.Type.VidError:
                    lastPlay.Error = log.data[0];
                    lastPlay.UpdateNeeded = true;
                    break;
                case LogData.Type.VidResolve:
                    lastPlay.ResolvedUrl = log.data[3];
                    lastPlay.UpdateNeeded = true;
                    break;
                case LogData.Type.VRX:
                    lastPlay.Vrcx = log.DataDebug();
                    break;
                case LogData.Type.VidPlay:
                    var url = log.data[1];
                    if (url == lastPlay.Url)
                    {
                        lastPlay.Attempt++;
                    }
                    else
                    {
                        if(lastPlay.Url!="")lastPlay = new VidLog();
                        lastPlay.Url = url;
                        lastPlay.UpdateNeeded = true;
                    }
                    break;
            }
            /*Mod.Logger.Msg($"----Now Showing----\nURL:{lastPlay.Url}\nResolved:{lastPlay.ResolvedUrl.Substring(0, Math.Min(lastPlay.ResolvedUrl.Length, 100))}" +
                       $"\nrcx:{lastPlay.Vrcx}\nError:{lastPlay.Error}\n----------------------------------------------");*/
            
        }
    }
    public class LogPhaser
    {
        public static LogData ParseLog(string line)
        {
            LogData data = new LogData();
            switch (line)
                {
                    case string s when s.StartsWith("[Video Playback] Attempting to resolve URL"):
                        data.type = LogData.Type.VidPlay;;
                        data.data =line.Replace("[Video Playback] Attempting to resolve URL","").Trim().Split('\'');
                        break;
                    case string s when s.StartsWith("[Video Playback] URL"):
                        data.type = LogData.Type.VidResolve;;
                        data.data =line.Replace("[Video Playback] URL","").Trim().Split('\'');
                        break;
                    case string s when s.StartsWith("[Video Playback] ERROR:"):
                        data.type = LogData.Type.VidError;;
                        data.data =new []{line.Replace("[Video Playback] ERROR:","").Trim()};
                        break;
                    case string s when s.StartsWith("[Video Playback] WARNING:"):
                        //TODO: Implement this
                        break;
                    case string s when s.StartsWith("[VRCX]"):
                        data.type = LogData.Type.VRX;
                        data.data =line.Replace("[VRCX]","").Trim().Split(',');
                        break;
                    default:
                        data.type = LogData.Type.Unknown;;
                        data.data =new []{$"{line.Trim()}"};
                        break;
                }
            return data;
        }
    }
    public struct LogData
    {
        public enum Type
        {
            Unknown=0,VidPlay,VidError,VidResolve,VRX
        }

        public Type type;
        public string[] data;

        public string DataDebug()
        {
            string logO = "";
            foreach (var v in data)
            {
                logO += $"{v.Trim()}";
            }
            return logO;
        }
    }

    public class VidLog
    {
        public string Url="";
        public string ResolvedUrl="";
        public string Error="";
        public string Vrcx="";

        public bool UpdateNeeded;
        public JObject DlData=null;
        public Texture Thumbnail;
        public bool ThumbnailFetched;
        public bool YtdlFailed = false;
        public int Attempt;
    }
}