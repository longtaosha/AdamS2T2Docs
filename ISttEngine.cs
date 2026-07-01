using System;
using System.Threading.Tasks;

namespace AdamS2T2Docs
{
    public enum SttConnectionState
    {
        Closed,
        Connecting,
        Open,
        Closing,
        Error
    }

    public class SttTranscriptEventArgs : EventArgs
    {
        public bool IsFinal { get; set; }
        public string Text { get; set; } = "";
        public string SegmentId { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string SpeakerId { get; set; } = "";
    }

    public class SttStatusEventArgs : EventArgs
    {
        public SttConnectionState State { get; set; }
        public string Message { get; set; } = "";
        public bool IsRawProviderMessage { get; set; }
        public Exception Exception { get; set; }
    }

    public interface ISttEngine : IDisposable
    {
        string ProviderName { get; }
        SttConnectionState State { get; }
        event EventHandler<SttTranscriptEventArgs> TranscriptReceived;
        event EventHandler<SttStatusEventArgs> StatusChanged;
        Task StartAsync(string language);
        Task StopAsync();
        void WriteAudio(byte[] buffer, int count);
    }
}
