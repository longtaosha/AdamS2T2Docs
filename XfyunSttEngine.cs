using Newtonsoft.Json;
using System;
using System.IO;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using WebSocket4Net;

namespace AdamS2T2Docs
{
    public class XfyunSttEngine : ISttEngine
    {
        private readonly XunFeiEncription _xunFeiEncription;
        private readonly byte[] _endCodeBytes = Encoding.ASCII.GetBytes("{\"end\": true}");
        private WebSocket _webSocket;
        private bool _isServing;

        public string ProviderName { get { return "XFYun"; } }
        public SttConnectionState State { get; private set; } = SttConnectionState.Closed;

        public event EventHandler<SttTranscriptEventArgs> TranscriptReceived;
        public event EventHandler<SttStatusEventArgs> StatusChanged;

        public XfyunSttEngine(string appid, string apiKey)
        {
            _xunFeiEncription = new XunFeiEncription(appid, apiKey);
        }

        public async Task StartAsync(string language)
        {
            await StopAsync().ConfigureAwait(false);

            string uri = BuildUri(language);

            _webSocket = new WebSocket(
                uri,
                sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls);

            _webSocket.DataReceived += OnWebSocketDataReceived;
            _webSocket.MessageReceived += OnWebSocketMessageReceived;
            _webSocket.Opened += OnWebSocketOpened;
            _webSocket.Closed += OnWebSocketClosed;
            _webSocket.Error += OnWebSocketError;

            SetState(SttConnectionState.Connecting, "Opening XFYun WebSocket");
            await Task.Run(() => _webSocket.Open()).ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            if (_webSocket == null)
            {
                SetState(SttConnectionState.Closed, "XFYun WebSocket closed");
                return;
            }

            WebSocket socket = _webSocket;
            _webSocket = null;
            _isServing = false;
            SetState(SttConnectionState.Closing, "Closing XFYun WebSocket");

            try
            {
                if (socket.State == WebSocketState.Open && socket.Handshaked)
                    await Task.Run(() => socket.Send(_endCodeBytes, 0, _endCodeBytes.Length)).ConfigureAwait(false);

                if (socket.State == WebSocketState.Open)
                    await Task.Run(() => socket.Close()).ConfigureAwait(false);
            }
            finally
            {
                socket.Dispose();
                SetState(SttConnectionState.Closed, "XFYun WebSocket closed");
            }
        }

        public void WriteAudio(byte[] buffer, int count)
        {
            WebSocket socket = _webSocket;

            if (socket == null || socket.State != WebSocketState.Open)
                return;

            try
            {
                socket.Send(buffer, 0, count);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory("logs");
                File.AppendAllText("logs/websocket-send-errors.txt",
                    DateTime.Now + "\n" +
                    ex.Message + "\n" +
                    ex.StackTrace + "\n\n");

                SetState(SttConnectionState.Error, "XFYun audio send error", ex);
            }
        }

        public void Dispose()
        {
            if (_webSocket != null)
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
        }

        private string BuildUri(string language)
        {
            string uri = _xunFeiEncription.getUri();

            if (language == "cn" && uri != null)
                return uri.Replace("lang=en", "lang=cn");

            if (language == "ko" && uri != null)
                return uri.Replace("lang=en", "lang=ko");

            if (language == "es" && uri != null)
                return uri.Replace("lang=en", "lang=es");

            return uri;
        }

        private void OnWebSocketDataReceived(object sender, DataReceivedEventArgs e)
        {
        }

        private void OnWebSocketOpened(object sender, EventArgs e)
        {
            _isServing = true;
            SetState(SttConnectionState.Open, "XFYun WebSocket opened");
        }

        private void OnWebSocketClosed(object sender, EventArgs e)
        {
            _isServing = false;
            SetState(SttConnectionState.Closed, "XFYun WebSocket closed");
        }

        private void OnWebSocketError(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            Directory.CreateDirectory("logs");
            File.AppendAllText(
                "logs/websocket-errors.txt",
                DateTime.Now + "\n" + e.Exception + "\n\n");

            SetState(SttConnectionState.Error, "XFYun WebSocket error", e.Exception);
        }

        private void OnWebSocketMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                dynamic msg = JsonConvert.DeserializeObject(e.Message);
                var dataWithQuotes = msg.data;

                if (msg.action == "started" || msg.action == "error" && _isServing)
                    RaiseStatus(State, e.Message, null, true);

                var midData = JsonConvert.SerializeObject(dataWithQuotes);
                dynamic dataWithoutQuotes = JsonConvert.DeserializeObject(midData);

                if (dataWithQuotes == null || dataWithoutQuotes == null)
                    return;

                XfyunData deserializeXfyunData =
                    JsonConvert.DeserializeObject<XfyunData>(dataWithoutQuotes);

                if (deserializeXfyunData == null)
                    return;

                ResultAnalysis resultAnalysis = new ResultAnalysis();
                string[] result = resultAnalysis.returnResult(deserializeXfyunData);

                if (result == null || result.Length < 2 || result[1] == null)
                    return;

                TranscriptReceived?.Invoke(this, new SttTranscriptEventArgs
                {
                    IsFinal = result[0] == "0",
                    Text = result[1],
                    SegmentId = result.Length > 2 ? result[2] : "",
                    EndTime = result.Length > 3 ? result[3] : "",
                    SpeakerId = result.Length > 4 ? result[4] : ""
                });
            }
            catch (Exception ex)
            {
                SetState(SttConnectionState.Error, "XFYun message parse error", ex);
            }
        }

        private void SetState(SttConnectionState state, string message, Exception exception = null)
        {
            State = state;
            RaiseStatus(state, message, exception, false);
        }

        private void RaiseStatus(
            SttConnectionState state,
            string message,
            Exception exception,
            bool isRawProviderMessage)
        {
            StatusChanged?.Invoke(this, new SttStatusEventArgs
            {
                State = state,
                Message = message,
                IsRawProviderMessage = isRawProviderMessage,
                Exception = exception
            });
        }
    }
}
