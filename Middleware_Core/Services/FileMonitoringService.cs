using System;
using System.IO;
using System.Threading.Tasks;
using Middleware_Core.Parsers;
using Middleware_Core.Queue;

namespace Middleware_Core.Services
{
    public class FileMonitoring
    {
        private readonly string _watchFolder;
        private readonly LisSenderService _lisSender;
        private readonly string _lisUrl;
        private readonly IncomingMessageQueue _queue;
        private FileSystemWatcher? _watcher;

        public FileMonitoring(string watchFolder, LisSenderService lisSender, string lisUrl)
        public FileMonitoring(string watchFolder, IncomingMessageQueue queue)
        {
            _watchFolder = watchFolder;
            _lisSender = lisSender;
            _lisUrl = lisUrl;
            _queue = queue;
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
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                await Task.Delay(500); // Esperar que el archivo termine de escribirse

                await Task.Delay(500);
                var rawMessage = await File.ReadAllTextAsync(e.FullPath);

                var parser = ParserFactory.GetParser(e.Name, rawMessage);
                var results = parser.Parse(rawMessage);

                foreach (var result in results)
                {
                    await _lisSender.SendAsync(result, _lisUrl);
                    Console.WriteLine($"✔ Sent: {result.SampleId} - {result.TestCode}");
                }
                await _queue.EnqueueAsync(new IncomingMessage("File", rawMessage, e.Name, DateTime.UtcNow));
                Console.WriteLine($"[FILE] Enqueued message from {e.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileMonitoring ERROR] {ex.Message}");
            }
        }
    }
}
