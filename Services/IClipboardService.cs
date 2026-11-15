using System;
using System.Collections.ObjectModel;
using System.Windows;
using DynamicIsland.Helpers;
using Clipboard = System.Windows.Clipboard;

namespace DynamicIsland.Services
{
    public interface IClipboardService : IDisposable
    {
        event EventHandler<string>? ItemCopied;
        ObservableCollection<string> History { get; }

        void Initialize(IntPtr windowHandle);
        void HandleClipboardUpdateMessage();
        void CopyToClipboard(string text);
        void ClearHistory();
    }

    public class ClipboardService : IClipboardService
    {
        private IntPtr _hwnd = IntPtr.Zero;
        private string _lastText = string.Empty;

        public ObservableCollection<string> History { get; } = new();
        public event EventHandler<string>? ItemCopied;

        public void Initialize(IntPtr windowHandle)
        {
            _hwnd = windowHandle;
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.AddClipboardFormatListener(_hwnd);
            }
        }

        public void HandleClipboardUpdateMessage()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText().Trim();
                    if (!string.IsNullOrWhiteSpace(text) && text != _lastText)
                    {
                        _lastText = text;

                        // Добавляем в начало истории
                        if (History.Contains(text))
                        {
                            History.Remove(text);
                        }
                        History.Insert(0, text);

                        while (History.Count > 10)
                        {
                            History.RemoveAt(History.Count - 1);
                        }

                        // Уведомление
                        string preview = text.Length > 35 ? text.Substring(0, 32) + "..." : text;
                        ItemCopied?.Invoke(this, preview);
                    }
                }
            }
            catch
            {
                // Буфер обмена может быть временно занят другим приложением
            }
        }

        public void CopyToClipboard(string text)
        {
            try
            {
                _lastText = text;
                Clipboard.SetText(text);
            }
            catch
            {
            }
        }

        public void ClearHistory()
        {
            History.Clear();
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.RemoveClipboardFormatListener(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
