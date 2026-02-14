using System;
using System.IO;
using Middleware.Core.Parsers;
using Middleware.Core.Services;

namespace Middleware.Core.Services
{
    public class FileMonitoringService
    {
        private readonly string _watchFolder;
        private readonly LisSenderService _lisSender;
        private readonly string _lisUrl;

        public FileMonitoringService(
            string watchFolder,
            LisSenderService lisSender,
            string lisUrl)
        {
            _watchFolder = watchFolder;
            _lisSender = lisSender;
            _lisUrl = lisUrl;
        }

        public void Start()
        {
            var watcher = new FileSystemWatcher(_watchFolder);
            watcher.Filter = "*.*";
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.Created += OnFileCreated;
            watcher.EnableRaisingEvents = true;

            Console.WriteLine($"[Middleware] Monitoring folder: {_watchFolder}");
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            _ = ProcessFileAsync(e);
        }

        private async Task ProcessFileAsync(FileSystemEventArgs e)
        {
            try
            {
                if (!File.Exists(e.FullPath))
                    return;

                Console.WriteLine($"[Middleware] File detected: {e.Name}");

                await Task.Delay(500);

                string rawMessage = await File.ReadAllTextAsync(e.FullPath);

                var fileName = Path.GetFileName(e.FullPath) ?? string.Empty;

                var parser = ParserFactory.GetParser(fileName, rawMessage);

                var processor = new AnalyzerMessageProcessor(parser);
                string hl7Message = processor.Process(rawMessage);

                await _lisSender.SendAsync(hl7Message, _lisUrl);

                Console.WriteLine($"[Middleware] File processed and sent: {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Middleware] Error processing file {e.Name}: {ex.Message}");
            }
        }
    }
}