using System;
using System.IO;
using System.Threading.Tasks;
using Middleware_Core.Parsers;

namespace Middleware_Core.Services
{
    public class FileMonitoring
    {
        private readonly string _watchFolder;
        private readonly LisSenderService _lisSender;
        private readonly string _lisUrl;
        private FileSystemWatcher? _watcher;

        public FileMonitoring(string watchFolder, LisSenderService lisSender, string lisUrl)
        {
            _watchFolder = watchFolder;
            _lisSender = lisSender;
            _lisUrl = lisUrl;
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

                var rawMessage = await File.ReadAllTextAsync(e.FullPath);

                var parser = ParserFactory.GetParser(e.Name, rawMessage);
                var results = parser.Parse(rawMessage);

                foreach (var result in results)
                {
                    await _lisSender.SendAsync(result, _lisUrl);
                    Console.WriteLine($"✔ Sent: {result.SampleId} - {result.TestCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileMonitoring ERROR] {ex.Message}");
            }
        }
    }
}
