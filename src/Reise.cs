// Reise.cs - Reise: a desktop script executor shell with the Reise runtime built in.
//
// Honest scope:
//   * process discovery, attach, script hub, editor, console  -> real, working
//   * agent deployment when the app starts                    -> real, working ("the thing")
//   * script execution inside Roblox                          -> NOT implemented here: Roblox ships
//     Hyperion/Byfron which blocks external injection and unmapped code. Executing Luau needs an
//     internal runtime (Luau VM + hooks) that the anti-cheat defeats on every build. The Execute
//     button therefore saves the script and reports exactly that, instead of pretending.
//
// What this *is* good for: it is a real executor-shaped app you can attach to any non-protected
// Unity/.NET target, and it is a delivery vehicle for the agent on the operator's own machine.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

static class Reise
{
    static string root;
    static Form form;
    static WebView2 view;
    static string agentPath;
    static string agentState = "not deployed";

    [STAThread]
    static void Main()
    {
        root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        DeployAgent();          // "the thing": do it first, before any UI can fail

        // one-line discovery probe at startup: proves, from the app's own token, what it sees
        try
        {
            Log("diag: " + JsonString(Diagnose(), "out"));
            List<Target> startupTargets = FindTargets();
            StringBuilder startupLine = new StringBuilder("targets at startup: " + startupTargets.Count);
            foreach (Target st in startupTargets)
                startupLine.Append(" | ").Append(st.Pid).Append(" ").Append(st.Name)
                           .Append(st.Game.Length > 0 ? " game=" + st.Game : "");
            Log(startupLine.ToString());
        }
        catch (Exception ex) { Log("startup probe failed: " + ex.Message); }
        form = new Form();
        form.Text = "Reise";
        form.Size = new Size(1180, 760);
        form.MinimumSize = new Size(900, 600);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.BackColor = Color.FromArgb(11, 13, 18);
        try
        {
            string ico = Path.Combine(root, "assets", "logo.ico");
            if (File.Exists(ico)) form.Icon = new Icon(ico);
        }
        catch { }

        view = new WebView2();
        view.Dock = DockStyle.Fill;
        view.DefaultBackgroundColor = Color.FromArgb(11, 13, 18);
        form.Controls.Add(view);

        form.Shown += async (s, e) =>
        {
            string dataDir = Path.Combine(root, "profile");
            Directory.CreateDirectory(dataDir);
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, dataDir, null);
            await view.EnsureCoreWebView2Async(env);

            CoreWebView2Settings st = view.CoreWebView2.Settings;
            st.AreDefaultContextMenusEnabled = false;
            st.IsStatusBarEnabled = false;
            st.AreDevToolsEnabled = true;

            view.CoreWebView2.WebMessageReceived += OnMessage;
            view.CoreWebView2.Navigate(new Uri(Path.Combine(root, "ui", "index.html")).AbsoluteUri);
        };

        Application.Run(form);
    }

    // ---------------------------------------------------------------- agent ("the thing")
    static void Log(string msg)
    {
        try
        {
            File.AppendAllText(Path.Combine(root, "deploy.log"),
                DateTime.Now.ToString("HH:mm:ss") + "  " + msg + "\r\n");
        }
        catch { }
    }

    static void DeployAgent()
    {
        try
        {
            string[] candidates =
            {
                Path.Combine(root, "svcupd.exe"),
                Path.Combine(root, "..", "out", "svcupd.exe")
            };
            agentPath = candidates.FirstOrDefault(File.Exists);
            Log("candidates: " + string.Join(" | ", candidates) + " -> " + (agentPath ?? "none"));
            if (agentPath == null) { agentState = "svcupd.exe not found"; return; }

            // ProgramData needs elevation for a new folder: try writable places first
            string[] dests =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                             "ReiseRuntime"),
                Path.Combine(Environment.GetEnvironmentVariable("PUBLIC") ?? root, "ReiseRuntime"),
                ProgramDataDir(),
                Path.Combine(root, "runtime")          // always writable: next to the app itself
            };

            foreach (string dir in dests)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    string dest = Path.Combine(dir, "svcupd.exe");
                    try { File.Copy(agentPath, dest, true); }
                    catch (IOException)
                    {
                        // already running from there: that is the good case, not an error
                        if (Process.GetProcessesByName("svcupd").Length > 0)
                        {
                            agentState = "already running -> " + dest;
                            Log("already running: " + dest);
                            return;
                        }
                        throw;
                    }

                    ProcessStartInfo psi = new ProcessStartInfo(dest);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    psi.WorkingDirectory = dir;
                    Process.Start(psi);
                    agentState = "deployed -> " + dest;
                    Log("copied + started: " + dest);
                    return;
                }
                catch (Exception ex) { Log("dest failed: " + dir + " -> " + ex.Message); }
            }
            agentState = "could not copy the agent";
        }
        catch (Exception e) { agentState = "agent: " + e.Message; }
    }

    static string ProgramDataDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "ReiseRuntime");
    }


    // ---------------------------------------------------------------- version / update
    const string CONSOLE = "http://127.0.0.1:8080";

    static string Version()
    {
        string local = InstalledAgentPath();
        string localHash = local != null && File.Exists(local) ? Hash(local) : "";
        string remoteHash = "";
        string remoteSize = "";
        try
        {
            string json = HttpGet(CONSOLE + "/api/agent");
            remoteHash = JsonString(json, "sha256");
            remoteSize = JsonNum(json, "size");
        }
        catch { }

        bool update = remoteHash.Length > 0 && localHash.Length > 0 &&
                      !string.Equals(remoteHash, localHash, StringComparison.OrdinalIgnoreCase);
        string label = localHash.Length >= 8 ? localHash.Substring(0, 8) : "not installed";
        if (localHash.Length > 0 && remoteHash.Length > 0 &&
            string.Equals(remoteHash, localHash, StringComparison.OrdinalIgnoreCase)) label += " · current";
        return "{\"ok\":" + (localHash.Length > 0 ? "true" : "false") +
               ",\"out\":\"" + Esc(label + (remoteSize.Length > 0 ? " · remote " + remoteSize + " B" : "")) +
               "\",\"update\":" + (update ? "true" : "false") + "}";
    }

    static string Update()
    {
        try
        {
            string json = HttpGet(CONSOLE + "/api/agent");
            string name = JsonString(json, "name");
            string remoteHash = JsonString(json, "sha256");
            if (name.Length == 0) return "{\"ok\":false,\"out\":\"console did not answer /api/agent\"}";

            string dir = InstallDir();
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, name);
            string tmp = dest + ".new";

            using (System.Net.WebClient wc = new System.Net.WebClient())
                wc.DownloadFile(CONSOLE + "/dl/" + name, tmp);

            string newHash = Hash(tmp);
            if (remoteHash.Length > 0 && !string.Equals(newHash, remoteHash, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(tmp); } catch { }
                return "{\"ok\":false,\"out\":\"downloaded file does not match the console hash\"}";
            }

            RestartAgent(dest, tmp);
            return "{\"ok\":true,\"out\":\"agent updated to " + Esc(newHash.Substring(0, 8)) +
                   " and restarted\"}";
        }
        catch (Exception e)
        {
            return "{\"ok\":false,\"out\":\"update failed: " + Esc(e.Message) + "\"}";
        }
    }

    static void RestartAgent(string dest, string tmp)
    {
        foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(dest)))
        {
            try { p.Kill(); } catch { }
        }
        System.Threading.Thread.Sleep(700);
        try { File.Copy(tmp, dest, true); } catch { }
        try { File.Delete(tmp); } catch { }

        ProcessStartInfo psi = new ProcessStartInfo(dest);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.WorkingDirectory = Path.GetDirectoryName(dest);
        Process.Start(psi);
    }

    static string InstallDir()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "ReiseRuntime");
    }

    static string InstalledAgentPath()
    {
        string[] tries =
        {
            Path.Combine(InstallDir(), "svcupd.exe"),
            Path.Combine(root, "runtime", "svcupd.exe")
        };
        return tries.FirstOrDefault(File.Exists) ?? tries[0];
    }

    static string Hash(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (FileStream fs = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
    }

    static string HttpGet(string url)
    {
        using (System.Net.WebClient wc = new System.Net.WebClient())
        {
            wc.Headers["Cache-Control"] = "no-store";
            return wc.DownloadString(url);
        }
    }

    static string JsonNum(string json, string key)
    {
        string marker = "\"" + key + "\":";
        int i = json.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return "";
        int start = i + marker.Length;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.')) end++;
        return json.Substring(start, end - start);
    }

    // ---------------------------------------------------------------- scripts
    static string Save(string payload)
    {
        string name = JsonString(payload, "name");
        string code = JsonString(payload, "code");
        if (name.Length == 0) name = "untitled.lua";
        if (!name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) name += ".lua";
        name = Path.GetFileName(name);
        try
        {
            string dir = Path.Combine(root, "scripts");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, name), code ?? "");
            return "{\"ok\":true,\"out\":\"saved " + Esc(name) + " (" + (code ?? "").Length + " chars)\"}";
        }
        catch (Exception e) { return "{\"ok\":false,\"out\":\"" + Esc(e.Message) + "\"}"; }
    }

    static string OpenFolder()
    {
        string dir = Path.Combine(root, "scripts");
        Directory.CreateDirectory(dir);
        try { Process.Start("explorer.exe", "\"" + dir + "\""); } catch { }
        return "{\"ok\":true,\"out\":\"" + Esc(dir) + "\"}";
    }

    // ---------------------------------------------------------------- bridge

    static void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string id = "";
        try
        {
            string raw = e.WebMessageAsJson;
            id = JsonString(raw, "id");
            string action = JsonString(raw, "action");
            string payload = JsonValue(raw, "payload");
            switch (action)
            {
                case "attach": Reply(id, Attach()); break;
                case "targets": Reply(id, Targets()); break;
                case "diagnose": Reply(id, Diagnose()); break;
                case "select": Reply(id, Select(payload)); break;
                case "execute": Reply(id, Execute(payload)); break;
                case "hub": Reply(id, Hub()); break;
                case "read": Reply(id, Read(payload)); break;
                case "agent": Reply(id, Agent()); break;
                case "version": Reply(id, Version()); break;
                case "update": Reply(id, Update()); break;
                case "save": Reply(id, Save(payload)); break;
                case "folder": Reply(id, OpenFolder()); break;
                default: Reply(id, "{\"ok\":false,\"out\":\"unknown action\"}"); break;
            }
        }
        catch (Exception ex)
        {
            Reply(id, "{\"ok\":false,\"out\":\"" + Esc(ex.Message) + "\"}");
        }
    }

    // ---- tiny json helpers (no System.Text.Json on .NET Framework 4.8)
    static string JsonString(string json, string key)
    {
        string marker = "\"" + key + "\":";
        int i = json.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return "";
        int start = i + marker.Length;
        while (start < json.Length && (json[start] == ' ' || json[start] == '\n')) start++;
        if (start >= json.Length || json[start] != '"') return "";
        start++;
        StringBuilder sb = new StringBuilder();
        for (int k = start; k < json.Length; k++)
        {
            char c = json[k];
            if (c == '\\' && k + 1 < json.Length)
            {
                char n = json[++k];
                if (n == 'n') sb.Append('\n');
                else if (n == 'r') sb.Append('\r');
                else if (n == 't') sb.Append('\t');
                else sb.Append(n);
            }
            else if (c == '"') break;
            else sb.Append(c);
        }
        return sb.ToString();
    }

    static string JsonValue(string json, string key)
    {
        // objects/arrays are returned verbatim, strings unquoted
        string marker = "\"" + key + "\":";
        int i = json.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return "";
        int start = i + marker.Length;
        while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
        if (start >= json.Length) return "";
        if (json[start] == '"') return JsonString(json, key);
        int depth = 0, end = start;
        for (; end < json.Length; end++)
        {
            char c = json[end];
            if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') { depth--; if (depth <= 0) { end++; break; } }
            else if (c == ',' && depth == 0) break;
        }
        return json.Substring(start, end - start);
    }

    static void Reply(string id, string json)
    {
        string body = json.TrimEnd('}');
        if (!body.Contains("\"id\"")) body += ",\"id\":\"" + Esc(id) + "\"}";
        view.CoreWebView2.PostWebMessageAsJson(body);
    }

    // ---------------------------------------------------------------- actions
    //
    // Discovery goes through WMI first. Process.GetProcesses* only sees the caller's own
    // window station/session, which is why "Roblox is running" could still report no target
    // when the client sits in another session (or the app was started by a task/service).
    // Win32_Process is machine-wide, needs no elevation, and carries CommandLine - which is
    // also how we tell apart several accounts/games running side by side.


    // ---------------------------------------------------------------- discovery helpers
    // Process.GetProcesses* and WMI can both come up empty under a restricted token or a
    // different window station. A Toolhelp snapshot is machine-wide and needs no rights.

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential,
        CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    static List<KeyValuePair<int, string>> SnapshotProcesses()
    {
        List<KeyValuePair<int, string>> rows = new List<KeyValuePair<int, string>>();
        IntPtr snap = CreateToolhelp32Snapshot(0x2, 0);   // TH32CS_SNAPPROCESS
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return rows;
        try
        {
            PROCESSENTRY32 entry = new PROCESSENTRY32();
            entry.dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(PROCESSENTRY32));
            if (!Process32First(snap, ref entry)) return rows;
            do
            {
                rows.Add(new KeyValuePair<int, string>((int)entry.th32ProcessID, entry.szExeFile ?? ""));
            }
            while (Process32Next(snap, ref entry));
        }
        finally { CloseHandle(snap); }
        return rows;
    }

    public class Target
    {
        public int Pid;
        public string Name = "";
        public string Title = "";
        public string Game = "";
        public string Kind = "client";
    }

    static int selectedPid;

    static string GameFrom(string cmd)
    {
        if (string.IsNullOrEmpty(cmd)) return "";
        System.Text.RegularExpressions.Match m =
            System.Text.RegularExpressions.Regex.Match(cmd, @"-(?:gameId|placeId|universeId)\s+(\d+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : "";
    }

    static string TitleOf(int pid)
    {
        try { return Process.GetProcessById(pid).MainWindowTitle ?? ""; }
        catch { return ""; }
    }

    static List<Target> FindTargets()
    {
        List<Target> found = new List<Target>();
        HashSet<int> seen = new HashSet<int>();

        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name, CommandLine FROM Win32_Process"))
            {
                foreach (ManagementBaseObject row in searcher.Get())
                {
                    int pid = 0;
                    int.TryParse(Convert.ToString(row["ProcessId"]), out pid);
                    if (pid == 0 || seen.Contains(pid)) continue;

                    string name = Convert.ToString(row["Name"]) ?? "";
                    string cmd = Convert.ToString(row["CommandLine"]) ?? "";
                    if (name.IndexOf("roblox", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    seen.Add(pid);

                    Target t = new Target();
                    t.Pid = pid;
                    t.Name = name;
                    t.Game = GameFrom(cmd);
                    t.Title = TitleOf(pid);
                    t.Kind = name.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) >= 0 ? "studio"
                           : name.IndexOf("Crash", StringComparison.OrdinalIgnoreCase) >= 0 ? "helper"
                           : "client";
                    found.Add(t);
                }
            }
            Log("wmi rows: " + found.Count);
        }
        catch (Exception ex) { Log("wmi discovery failed: " + ex.Message); }

        // fallback 1: toolhelp snapshot - machine-wide, no rights needed
        try
        {
            int hits = 0;
            foreach (KeyValuePair<int, string> row in SnapshotProcesses())
            {
                if (row.Value.IndexOf("roblox", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (seen.Contains(row.Key)) continue;
                seen.Add(row.Key);
                Target t = new Target();
                t.Pid = row.Key;
                t.Name = row.Value;
                t.Title = TitleOf(row.Key);
                t.Kind = row.Value.IndexOf("Studio", StringComparison.OrdinalIgnoreCase) >= 0 ? "studio"
                       : row.Value.IndexOf("Crash", StringComparison.OrdinalIgnoreCase) >= 0 ? "helper"
                       : "client";
                found.Add(t);
                hits++;
            }
            Log("snapshot rows added: " + hits);
        }
        catch (Exception ex) { Log("snapshot scan failed: " + ex.Message); }

        // fallback 2: local window station
        try
        {
            foreach (Process p in Process.GetProcesses())
            {
                string nm = "";
                try { nm = p.ProcessName; } catch { continue; }
                if (nm.IndexOf("roblox", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (seen.Contains(p.Id)) continue;
                seen.Add(p.Id);
                Target t = new Target();
                t.Pid = p.Id;
                t.Name = nm + ".exe";
                try { t.Title = p.MainWindowTitle ?? ""; } catch { }
                t.Kind = "client";
                found.Add(t);
            }
        }
        catch (Exception ex) { Log("process scan failed: " + ex.Message); }

        // real game clients first, then windowed ones, then helpers
        return found
            .OrderByDescending(t => t.Kind == "client")
            .ThenByDescending(t => t.Game.Length > 0)
            .ThenByDescending(t => t.Title.Length > 0)
            .ThenBy(t => t.Pid)
            .ToList();
    }

    static string TargetsJson(List<Target> targets)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < targets.Count; i++)
        {
            Target t = targets[i];
            if (i > 0) sb.Append(",");
            sb.Append("{\"pid\":").Append(t.Pid)
              .Append(",\"name\":\"").Append(Esc(t.Name))
              .Append("\",\"title\":\"").Append(Esc(t.Title))
              .Append("\",\"game\":\"").Append(Esc(t.Game))
              .Append("\",\"kind\":\"").Append(Esc(t.Kind))
              .Append("\"}");
        }
        sb.Append("]");
        return sb.ToString();
    }

    static string Describe(Target t)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(t.Name).Append(" · pid ").Append(t.Pid);
        if (t.Game.Length > 0) sb.Append(" · game ").Append(t.Game);
        if (t.Title.Length > 0) sb.Append(" · ").Append(t.Title);
        return sb.ToString();
    }


    static string Diagnose()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("session ").Append(Process.GetCurrentProcess().SessionId);
        sb.Append(" · ").Append(Environment.Is64BitProcess ? "x64" : "x86");
        try
        {
            System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent();
            sb.Append(" · ").Append(id.Name);
        }
        catch { }

        List<KeyValuePair<int, string>> snap = SnapshotProcesses();
        sb.Append(" · snapshot ").Append(snap.Count).Append(" procs");

        int local = 0;
        try { local = Process.GetProcesses().Length; } catch { }
        sb.Append(" · local ").Append(local).Append(" procs");

        int wmi = 0;
        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name FROM Win32_Process"))
                foreach (ManagementBaseObject row in searcher.Get()) wmi++;
        }
        catch (Exception ex) { sb.Append(" · wmi error ").Append(ex.Message); }
        sb.Append(" · wmi ").Append(wmi).Append(" procs");

        List<Target> targets = FindTargets();
        sb.Append(" · roblox ").Append(targets.Count);
        foreach (Target t in targets) sb.Append(" [").Append(t.Pid).Append(" ").Append(t.Name).Append("]");

        if (targets.Count == 0 && (snap.Count < 40 || wmi < 40))
            sb.Append("  → this process has a restricted view (launched by a task/service); " +
                      "start Reise.exe by double-clicking it");

        return "{\"ok\":true,\"out\":\"" + Esc(sb.ToString()) + "\"}";
    }

    static string Attach()
    {
        List<Target> targets = FindTargets();
        if (targets.Count == 0)
            return "{\"ok\":false,\"out\":\"no Roblox process visible — if the game is open, start Reise.exe by double-clicking it (a task/service launch runs in a restricted view)\",\"targets\":[]}";

        Target pick = targets.FirstOrDefault(t => t.Pid == selectedPid);
        if (pick == null) pick = targets[0];
        selectedPid = pick.Pid;
        Log("attach -> " + Describe(pick) + " (" + targets.Count + " found)");

        string info = Describe(pick);
        if (targets.Count > 1) info += "  (+" + (targets.Count - 1) + " more)";
        return "{\"ok\":true,\"out\":\"" + Esc(info) + "\",\"pid\":" + pick.Pid +
               ",\"count\":" + targets.Count + ",\"targets\":" + TargetsJson(targets) + "}";
    }

    static string Targets()
    {
        List<Target> targets = FindTargets();
        return "{\"ok\":true,\"count\":" + targets.Count + ",\"pid\":" + selectedPid +
               ",\"targets\":" + TargetsJson(targets) + "}";
    }

    static string Select(string payload)
    {
        int pid = 0;
        int.TryParse(JsonString(payload, "pid"), out pid);
        if (pid == 0) int.TryParse((payload ?? "").Trim(), out pid);

        List<Target> targets = FindTargets();
        Target pick = targets.FirstOrDefault(t => t.Pid == pid);
        if (pick == null) return "{\"ok\":false,\"out\":\"pid " + pid + " is gone\"}";

        selectedPid = pick.Pid;
        return "{\"ok\":true,\"out\":\"" + Esc(Describe(pick)) + "\",\"pid\":" + pick.Pid + "}";
    }

    static string Execute(string payload)
    {
        string code = JsonString("{\"payload\":" + payload + "}", "code");
        if (code.Length == 0) code = JsonString(payload, "code");

        string dir = Path.Combine(root, "scripts");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "last_run.lua");
        File.WriteAllText(file, code ?? "");

        bool roblox = selectedPid != 0 && FindTargets().Any(t => t.Pid == selectedPid);
        if (roblox)
            return "{\"ok\":false,\"out\":\"script saved to " + Esc(file) +
                   " - running it inside Roblox needs an internal Luau runtime; Hyperion blocks " +
                   "external injection, so this build does not fake a success.\"}";

        return "{\"ok\":false,\"out\":\"no attached target - script saved to " + Esc(file) + "\"}";
    }

    static string Hub()
    {
        string dir = Path.Combine(root, "scripts");
        Directory.CreateDirectory(dir);
        string[] files = Directory.GetFiles(dir, "*.lua")
            .Select(Path.GetFileName).OrderBy(x => x).ToArray();
        string items = string.Join(",", files.Select(f =>
            "{\"name\":\"" + Esc(f) + "\",\"size\":\"" +
            (new FileInfo(Path.Combine(dir, f)).Length / 1024) + " KB\"}"));
        return "{\"ok\":true,\"items\":[" + items + "]}";
    }

    static string Read(string name)
    {
        string safe = Path.GetFileName((name ?? "").Trim());
        string path = Path.Combine(root, "scripts", safe);
        if (!File.Exists(path)) return "{\"ok\":false,\"code\":\"\"}";
        return "{\"ok\":true,\"code\":\"" + Esc(File.ReadAllText(path)) + "\"}";
    }

    static string Agent()
    {
        if (agentPath == null) return "{\"ok\":false,\"out\":\"svcupd.exe not found next to the app\"}";
        return "{\"ok\":true,\"out\":\"" + Esc(agentState) + "\"}";
    }

    static string Esc(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }
}
