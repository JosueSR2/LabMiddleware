using System;
using System.IO;
using Middleware.Core.Parsers;
using Middleware.Core.Services;

namespace Middleware.Core.Services
{
    public class FileMonitoringService
    {
        private readonly string _watchFolder;
        private readonly AnalyzerMessageProcessor _processor;
        private readonly LisSenderService _lisSender;
        private readonly string _lisUrl;

        public FileMonitoringService(
            string watchFolder,
            AnalyzerMessageProcessor processor,
            LisSenderService lisSender,
            string lisUrl)
        {
            _watchFolder = watchFolder;
            _processor = processor;
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

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.WriteLine($"[Middleware] File detected: {e.Name}");

                await Task.Delay(500); // Esperar a que se termine de escribir

                string rawMessage = File.ReadAllText(e.FullPath);

                // Detectar automáticamente el parser
                var parser = ParserFactory.GetParser(e.Name, rawMessage);

                var processor = new AnalyzerMessageProcessor(parser);
                string hl7Message = processor.Process(rawMessage);

                await _lisSender.SendAsync(hl7Message, _lisUrl);

                Console.WriteLine($"[Middleware] File processed and sent: {e.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Middleware] Error processing file {e.Name}: {ex.Message}");
            }
        }
    }
}
