using HelperUtilties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace NUWM.Servers.Core.Uptime
{
    [Serializable]
    sealed class UptimePool
    {
        string setin = MainApp.dirs[0];
        public List<UptimeManager> POOL { get; set; }

        public static UptimePool Current;
        public void Initialize()
        {
            Current = this;
            POOL = new List<UptimeManager>();
            var f = setin + "/urls.txt";
            if (File.Exists(f))
            {
                var fl = File.OpenText(f);
                while (!fl.EndOfStream)
                {
                    var line = fl.ReadLine();
                    if (line.StartsWith('#')) continue;
                    var fg = new UptimeManager(line);
                    POOL.Add(fg);
                    fg.Manage();
                }
            }
        }
        public void AddObject(string url) => POOL.Add(new UptimeManager(url));
        public void DeleteObject(string key) => POOL.Remove(POOL.Where(x => x.Key == key)?.First());

        public StatsInfo GetStatus(string key, string port = null)
        {
            var f = MainApp.dirs[1] + "/" + key + "_" + port + "/uptime_" + TimeChron.GetRealTime().ToString("dd.MM.yyyy") + ".txt";
            StatsInfo list = new StatsInfo();

            if (File.Exists(f))
            {
                var r = File.OpenText(f);
                while (!r.EndOfStream)
                {
                    var line = r.ReadLine();
                    list.StatList.Add(StatsInfo.Parse(line));
                }
            }
            foreach (var i in POOL.Where(x => x.Key.Contains(key) && x.Key.Contains(port)).First().Stats)
            {
                list.StatList.Add(StatsInfo.Parse(i));
            }
            return list;

        }
        public class StatsInfo
        {
            public static InlineState Parse(string val)
            {
                var sp = val.Split("|");

                return new InlineState()
                {
                    Time = sp[0].Trim(' '),
                    State = sp[1].Trim(' '),
                    ElapsedMs = int.Parse(sp[2].Replace("ms", "").Trim(' '))
                };
            }
            DateTime Start, End;
            [JsonProperty("sdate")]
            public string StartDate { get { return Start.ToString("hh:mm:ss - dd.MM.yyyy"); } set { Start = DateTime.Parse(value); } }
            [JsonProperty("edate")]
            public string EndDate { get { return Start.ToString("hh:mm:ss - dd.MM.yyyy"); } set { End = DateTime.Parse(value); } }
            [JsonProperty("stat_list")]
            public List<InlineState> StatList { get; set; }
            public StatsInfo() => StatList = new List<InlineState>();
            public class InlineState
            {
                [JsonProperty("elms")]
                public int ElapsedMs { get; set; }
                [JsonProperty("state")]
                public string State { get; set; }
                [JsonProperty("intime")]
                public string Time { get; set; }
            }
        }
    }
    [Serializable]
    sealed class UptimeScheduler : BaseScheduler
    {
        private string uri;
        public UptimeScheduler(string uri)
        {
            this.uri = uri;
            base.SetDelay(new TimeSpan(0, 1, 0));
            base.CurrentWorkHandler = new WorkHandler(OnUptimeCheck);
            Schedule_Timer();
        }
        public void SetNewDelay(TimeSpan timeSpan)
        {
            base.StopTimer();
            base.SetDelay(timeSpan);
            base.Schedule_Timer();
        }
        public void OnUptimeCheck() => UptimePool.Current.POOL.Where(x => x.Key == uri)?.First()?.Manage();
    }
    [Serializable]
    sealed class SaverScheduler : BaseScheduler
    {
        private string uri;
        public SaverScheduler(string uri)
        {
            this.uri = uri;
            base.SetDelay(new TimeSpan(1, 0, 0));
            base.CurrentWorkHandler = new WorkHandler(OnSaving);
            Schedule_Timer();
        }
        public void SetNewDelay(TimeSpan timeSpan)
        {
            base.StopTimer();
            base.SetDelay(timeSpan);
            base.Schedule_Timer();
        }
        public void OnSaving() => UptimePool.Current.POOL.Where(x => x.Key == uri)?.First()?.Save();

    }
    [Serializable]
    sealed class UptimeManager
    {
        public string Key { get; set; }
        public List<string> Stats { get; set; }
        Uri CurrentUri;
        SaverScheduler CurrentSaver;
        UptimeScheduler CurrentScheduler;
        public UptimeManager(string full_url)
        {
            Key = full_url;
            CurrentUri = new Uri(full_url);
            CurrentScheduler = new UptimeScheduler(CurrentUri.OriginalString);
            CurrentSaver = new SaverScheduler(CurrentUri.OriginalString);
            Stats = new List<string>();
        }
        public void Manage()
        {
            var time = TimeChron.GetRealTime().ToString("hh:mm:ss");
            System.Diagnostics.Stopwatch st = new System.Diagnostics.Stopwatch();
            st.Start();
            bool isopen = Tools.IsPortOpen(CurrentUri.Host, CurrentUri.Port, new TimeSpan(0, 0, 5));
            st.Stop();

            Stats.Add(string.Format("{0} | {1} | {2}ms", time, (isopen ? "OK" : "DOWN"), st.ElapsedMilliseconds));
        }

        public void Save()
        {
            if (Stats != null)
            {
                try
                {
                    string dir = Path.Combine(MainApp.dirs[1], CurrentUri.Host + "_" + CurrentUri.Port);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, "uptime_" + TimeChron.GetRealTime().ToString("dd.MM.yyyy") + ".txt");
                    bool fileNew = !File.Exists(file);
                    var fs = File.Open(file, FileMode.Append);
                    string all = (fs.Length > 0 ? "\n" : "") + string.Join('\n', Stats);
                    var buff = Encoding.UTF8.GetBytes(all);
                    fs.Write(buff, 0, buff.Length);
                    fs.Close();
                    if (!fileNew) Stats.Clear();
                }
                catch (Exception ex) { Logger.Errors.Add(ex); }
            }
        }
    }
    sealed class Tools
    {
        public static double ConvertBytesToMegabytes(long bytes) => (bytes / 1024f) / 1024f;
        public static bool IsPortOpen(string host, int port, TimeSpan timeout)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(timeout))
                        return false;
                    client.EndConnect(result);
                }
            }
            catch (SocketException)
            {
                Logger.Errors.Add(new Exception(
string.Format("No internet connection on {0}",
TimeChron.GetRealTime().ToString("hh:mm:ss - dd.MM.yyyy")))); return false;
            }
            catch { return false; }
            return true;
        }
    }
}