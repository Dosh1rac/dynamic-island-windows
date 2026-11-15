using System;
using System.Collections.ObjectModel;
using System.IO;
using DynamicIsland.Models;

namespace DynamicIsland.Services
{
    public interface IDropZoneService
    {
        ObservableCollection<DroppedFileInfo> Files { get; }
        event EventHandler<DroppedFileInfo>? FileAdded;

        void AddFiles(string[] filePaths);
        void RemoveFile(DroppedFileInfo file);
        void Clear();
    }

    public class DropZoneService : IDropZoneService
    {
        public ObservableCollection<DroppedFileInfo> Files { get; } = new();
        public event EventHandler<DroppedFileInfo>? FileAdded;

        public void AddFiles(string[] filePaths)
        {
            foreach (var path in filePaths)
            {
                if (!File.Exists(path) && !Directory.Exists(path)) continue;

                string sizeStr = "Папка";
                if (File.Exists(path))
                {
                    long bytes = new FileInfo(path).Length;
                    sizeStr = FormatBytes(bytes);
                }

                var item = new DroppedFileInfo
                {
                    FilePath = path,
                    FormattedSize = sizeStr
                };

                Files.Insert(0, item);
                while (Files.Count > 10)
                {
                    Files.RemoveAt(Files.Count - 1);
                }

                FileAdded?.Invoke(this, item);
            }
        }

        public void RemoveFile(DroppedFileInfo file)
        {
            Files.Remove(file);
        }

        public void Clear()
        {
            Files.Clear();
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int i = 0;
            double dbl = bytes;
            while (dbl >= 1024 && i < suffixes.Length - 1)
            {
                dbl /= 1024;
                i++;
            }
            return $"{dbl:0.1} {suffixes[i]}";
        }
    }
}
