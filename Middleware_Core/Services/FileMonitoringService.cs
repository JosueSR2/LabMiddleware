using System;
using System.IO;
using System.Threading.Tasks;
using Middleware_Core.Parsers;
using Middleware_Core.Configuration;
using Middleware_Core.Queue;

namespace Middleware_Core.Services
{
    public class FileMonitoring
    {
        private readonly string _watchFolder;
        private readonly LisSenderService _lisSender;
        private readonly string _lisUrl;
        private readonly string _source;
        private readonly IncomingMessageQueue _queue;
        private FileSystemWatcher? _watcher;
        public FileMonitoring(string watchFolder, IncomingMessageQueue queue, string source = "File")
        {
            _watchFolder = watchFolder;
            _queue = queue;
            _source = source;
        }

        public FileMonitoring(AnalyzerProfile profile, IncomingMessageQueue queue)
        {
            _watchFolder = string.IsNullOrWhiteSpace(profile.WatchFolder) ? "./TestingResources" : profile.WatchFolder;
            _queue = queue;
            _source = profile.SourceMachine;
        }

        public void Start()
        {
            if (!Directory.Exists(_watchFolder))
            {
                Directory.CreateDirectory(_watchFolder);
            }

            _watcher = new FileSystemWatcher(_watchFolder)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _watcher.Created += OnFileCreated;

            Console.WriteLine($"📁 Watching folder: {_watchFolder}");
            Console.WriteLine($"📁 Watching folder: {_watchFolder} ({_source})");
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                await Task.Delay(500); // Esperar que el archivo termine de escribirse

                await Task.Delay(500);
                var rawMessage = await File.ReadAllTextAsync(e.FullPath);
                await _queue.EnqueueAsync(new IncomingMessage(_source, rawMessage, e.Name, DateTime.UtcNow));
                Console.WriteLine($"[FILE] Enqueued message from {e.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileMonitoring ERROR] {ex.Message}");
            }
        }
    }
}
