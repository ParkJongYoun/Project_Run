using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Monster Race")]
[assembly: AssemblyDescription("Monster Race 웹 게임 실행기")]
[assembly: AssemblyProduct("Monster Race")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace ProjectRun.Launcher
{
    internal static class PlayLauncher
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool noBrowser = Array.IndexOf(args, "--no-browser") >= 0;
            try
            {
                Application.EnableVisualStyles();
                string root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                if (!File.Exists(Path.Combine(root, "index.html")))
                    throw new FileNotFoundException("게임 파일을 찾을 수 없습니다.\nPlay.exe를 웹 빌드 폴더 안에서 실행해 주세요.");

                string identity;
                using (var hash = SHA256.Create())
                    identity = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(root.ToUpperInvariant()))).Replace("-", "");

                bool ownsMutex;
                using (var mutex = new Mutex(true, @"Local\ProjectRun.Play." + identity, out ownsMutex))
                {
                    if (!ownsMutex)
                    {
                        // Another launch may still be starting its listener.
                        for (int attempt = 0; attempt < 20; attempt++)
                        {
                            string existing = FindRunningServer(identity);
                            if (existing != null)
                            {
                                if (!noBrowser) OpenBrowser(existing);
                                return 0;
                            }
                            Thread.Sleep(250);
                        }
                        throw new IOException("게임 실행기가 준비 중입니다. 잠시 후 다시 실행해 주세요.");
                    }
                    try
                    {
                        using (var server = new LocalGameServer(root, identity))
                        using (var tray = new LauncherContext(server))
                        {
                            server.Start();
                            if (!noBrowser) OpenBrowser(server.Url);
                            Application.Run(tray);
                        }
                    }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                if (!noBrowser)
                    MessageBox.Show(exception.Message, "Monster Race 실행 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static string FindRunningServer(string identity)
        {
            for (int port = 18767; port <= 18776; port++)
            {
                string url = "http://127.0.0.1:" + port + "/";
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url + "__project_run_launcher__");
                    request.Proxy = null;
                    request.Timeout = 200;
                    request.ReadWriteTimeout = 200;
                    using (var response = request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                        if (reader.ReadToEnd() == identity) return url;
                }
                catch (WebException) { }
            }
            return null;
        }

        internal static void OpenBrowser(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    internal sealed class LauncherContext : ApplicationContext
    {
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu;
        private readonly Icon icon;

        internal LauncherContext(LocalGameServer server)
        {
            icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            menu = new ContextMenuStrip();
            menu.Items.Add("게임 열기", null, delegate { TryOpen(server.Url); });
            menu.Items.Add("종료", null, delegate { ExitThread(); });
            tray = new NotifyIcon {
                Icon = icon, Text = "Monster Race - 게임 열기 / 종료",
                ContextMenuStrip = menu, Visible = true
            };
            tray.DoubleClick += delegate { TryOpen(server.Url); };
        }

        private static void TryOpen(string url)
        {
            try { PlayLauncher.OpenBrowser(url); }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Monster Race", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tray.Visible = false; tray.Dispose(); menu.Dispose(); icon.Dispose(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class LocalGameServer : IDisposable
    {
        private readonly string root;
        private readonly string identity;
        private TcpListener listener;
        private volatile bool stopping;
        internal string Url { get; private set; }

        internal LocalGameServer(string directory, string serverIdentity)
        {
            root = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            identity = serverIdentity;
        }

        internal void Start()
        {
            for (int port = 18767; port <= 18776; port++)
            {
                var candidate = new TcpListener(IPAddress.Loopback, port);
                candidate.Server.ExclusiveAddressUse = true;
                try { candidate.Start(); listener = candidate; Url = "http://127.0.0.1:" + port + "/"; break; }
                catch (SocketException) { candidate.Stop(); }
            }
            if (listener == null) throw new IOException("게임 실행에 사용할 로컬 포트가 없습니다. 다른 실행기를 종료하고 다시 시도해 주세요.");
            var thread = new Thread(AcceptRequests) { IsBackground = true, Name = "Monster Race local server" };
            thread.Start();
        }

        private void AcceptRequests()
        {
            while (!stopping)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { Serve(client); });
                }
                catch (SocketException) { if (!stopping) Thread.Sleep(50); }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 30000;
                    using (var stream = client.GetStream())
                    {
                        string request = ReadLine(stream);
                        if (String.IsNullOrEmpty(request)) return;
                        string[] parts = request.Split(' ');
                        if (parts.Length != 3) { SendText(stream, "400 Bad Request", "Bad request", false); return; }
                        bool head = parts[0] == "HEAD";
                        string range = null;
                        for (int count = 0; ; count++)
                        {
                            string header = ReadLine(stream);
                            if (String.IsNullOrEmpty(header)) break;
                            if (count >= 64) throw new IOException("Too many request headers.");
                            if (header.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) range = header.Substring(6).Trim();
                        }
                        if (parts[0] != "GET" && !head) { SendText(stream, "405 Method Not Allowed", "GET and HEAD only", head); return; }
                        string relative = Uri.UnescapeDataString(parts[1].Split('?')[0]).TrimStart('/');
                        if (relative == "__project_run_launcher__") { SendText(stream, "200 OK", identity, head); return; }
                        if (relative.Length == 0) relative = "index.html";
                        if (relative.IndexOf(':') >= 0 || relative.IndexOf('\\') >= 0 || relative.IndexOf('\0') >= 0)
                        { SendText(stream, "404 Not Found", "Not found", head); return; }
                        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        { SendText(stream, "404 Not Found", "Not found", head); return; }
                        // Check the normalized path as well: Build/../Play.exe is not web content.
                        string resolved = path.Substring(root.Length).Replace('\\', '/');
                        bool webFile = resolved == "index.html" || resolved.StartsWith("Build/", StringComparison.Ordinal)
                            || resolved.StartsWith("StreamingAssets/", StringComparison.Ordinal)
                            || resolved.StartsWith("TemplateData/", StringComparison.Ordinal);
                        if (!webFile || !File.Exists(path))
                        { SendText(stream, "404 Not Found", "Not found", head); return; }
                        using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            long start = 0, end = file.Length - 1;
                            string status = "200 OK", extra = "";
                            if (range != null)
                            {
                                if (!TryRange(range, file.Length, out start, out end))
                                {
                                    WriteHeader(stream, "416 Range Not Satisfiable", "text/plain", 0, "Content-Range: bytes */" + file.Length + "\r\n");
                                    return;
                                }
                                status = "206 Partial Content";
                                extra = "Content-Range: bytes " + start + "-" + end + "/" + file.Length + "\r\n";
                            }
                            string contentPath = path;
                            if (path.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
                            { extra += "Content-Encoding: br\r\n"; contentPath = path.Substring(0, path.Length - 3); }
                            else if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                            { extra += "Content-Encoding: gzip\r\n"; contentPath = path.Substring(0, path.Length - 3); }
                            long remaining = Math.Max(0, end - start + 1);
                            WriteHeader(stream, status, ContentType(contentPath), remaining, extra);
                            if (head) return;
                            file.Position = start;
                            byte[] buffer = new byte[65536];
                            while (remaining > 0 && !stopping)
                            {
                                int read = file.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                                if (read == 0) break;
                                stream.Write(buffer, 0, read);
                                remaining -= read;
                            }
                        }
                    }
                }
                // Browsers routinely cancel range requests when a video or page closes.
                catch (IOException) { }
                catch (SocketException) { }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static string ReadLine(Stream stream)
        {
            var line = new StringBuilder();
            for (int i = 0; i < 8192; i++)
            {
                int value = stream.ReadByte();
                if (value < 0 || value == '\n') return line.ToString().TrimEnd('\r');
                line.Append((char)value);
            }
            throw new IOException("Request line too long.");
        }

        private static bool TryRange(string value, long length, out long start, out long end)
        {
            start = 0; end = length - 1;
            var match = Regex.Match(value, @"^bytes=(\d*)-(\d*)$", RegexOptions.CultureInvariant);
            if (!match.Success || length == 0) return false;
            string first = match.Groups[1].Value, last = match.Groups[2].Value;
            long number;
            if (first.Length == 0)
            {
                if (!Int64.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number <= 0) return false;
                start = Math.Max(0, length - number);
            }
            else
            {
                if (!Int64.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out start)) return false;
                if (last.Length != 0)
                {
                    if (!Int64.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out number)) return false;
                    end = Math.Min(end, number);
                }
            }
            return start <= end && start < length;
        }

        private static void SendText(Stream stream, string status, string text, bool head)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            WriteHeader(stream, status, "text/plain; charset=utf-8", bytes.Length, "");
            if (!head) stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteHeader(Stream stream, string status, string type, long length, string extra)
        {
            string header = "HTTP/1.1 " + status + "\r\nContent-Type: " + type + "\r\nContent-Length: " + length
                + "\r\nAccept-Ranges: bytes\r\nCache-Control: no-cache\r\nCross-Origin-Opener-Policy: same-origin"
                + "\r\nCross-Origin-Embedder-Policy: require-corp\r\n" + extra + "Connection: close\r\n\r\n";
            byte[] bytes = Encoding.ASCII.GetBytes(header);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string ContentType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8";
                case ".js": return "application/javascript";
                case ".wasm": return "application/wasm";
                case ".json": return "application/json";
                case ".css": return "text/css";
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
                case ".mp4": return "video/mp4";
                case ".webm": return "video/webm";
                default: return "application/octet-stream";
            }
        }

        public void Dispose()
        {
            stopping = true;
            if (listener != null) listener.Stop();
        }
    }
}
