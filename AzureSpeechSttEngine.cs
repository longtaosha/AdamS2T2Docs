using Newtonsoft.Json;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AdamS2T2Docs
{
    public class AzureSpeechSttEngine : ISttEngine
    {
        private readonly string _subscriptionKey;
        private readonly string _region;
        private readonly string _endpoint;
        private readonly string _configuredLanguage;
        private readonly int _vadMs;
        private readonly object _audioLock = new object();

        private SpeechRecognizer _recognizer;
        private PushAudioInputStream _pushStream;
        private AudioConfig _audioConfig;
        private bool _acceptingAudio;
        private bool _disposed;
        private string _lastPartialText = "";
        private string _lastFinalResultId = "";
        private string _activeLanguage = "";
        private int _activeVadMs;

        public string ProviderName { get { return "Azure Speech"; } }
        public SttConnectionState State { get; private set; } = SttConnectionState.Closed;

        public event EventHandler<SttTranscriptEventArgs> TranscriptReceived;
        public event EventHandler<SttStatusEventArgs> StatusChanged;

        public AzureSpeechSttEngine(
            string subscriptionKey,
            string region,
            string endpoint,
            string configuredLanguage,
            int vadMs)
        {
            _subscriptionKey = subscriptionKey;
            _region = region;
            _endpoint = endpoint;
            _configuredLanguage = configuredLanguage;
            _vadMs = vadMs;
        }

        public async Task StartAsync(string language)
        {
            await StopAsync().ConfigureAwait(false);
            EnsureConfigured();
            EnsureNativeRuntimeAvailable();

            SpeechConfig speechConfig = CreateSpeechConfig();
            _activeLanguage = ResolveLanguage(language);
            _activeVadMs = ResolveVadMs(_vadMs);
            speechConfig.SpeechRecognitionLanguage = _activeLanguage;

            if (_activeVadMs > 0)
            {
                speechConfig.SetProperty(
                    PropertyId.Speech_SegmentationSilenceTimeoutMs,
                    _activeVadMs.ToString());
            }

            AudioStreamFormat audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            _pushStream = AudioInputStream.CreatePushStream(audioFormat);
            _audioConfig = AudioConfig.FromStreamInput(_pushStream);
            _recognizer = new SpeechRecognizer(speechConfig, _audioConfig);
            AttachEvents(_recognizer);

            _lastPartialText = "";
            _lastFinalResultId = "";
            _acceptingAudio = true;

            SetState(SttConnectionState.Connecting, "Starting Azure Speech recognition");
            Task startTask = _recognizer.StartContinuousRecognitionAsync();
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
            Task completedTask = await Task.WhenAny(startTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                _acceptingAudio = false;
                TimeoutException timeoutException = new TimeoutException(
                    "Azure Speech recognition start timed out after 15 seconds. Check the Azure region/endpoint, key, and network access.");

                SetState(SttConnectionState.Error, timeoutException.Message, timeoutException);
                throw timeoutException;
            }

            await startTask.ConfigureAwait(false);

            if (State == SttConnectionState.Connecting)
                SetState(SttConnectionState.Open, "Azure Speech recognition started");
        }

        public async Task StopAsync()
        {
            SpeechRecognizer recognizer = _recognizer;

            if (recognizer == null)
            {
                _acceptingAudio = false;
                CloseAudioObjects();
                if (State != SttConnectionState.Closed)
                    SetState(SttConnectionState.Closed, "Azure Speech recognition closed");
                return;
            }

            _recognizer = null;
            _acceptingAudio = false;
            SetState(SttConnectionState.Closing, "Stopping Azure Speech recognition");

            try
            {
                lock (_audioLock)
                {
                    if (_pushStream != null)
                        _pushStream.Close();
                }

                await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            finally
            {
                DetachEvents(recognizer);
                recognizer.Dispose();
                CloseAudioObjects();
            _lastPartialText = "";
            _lastFinalResultId = "";
            _activeLanguage = "";
            _activeVadMs = 0;
            SetState(SttConnectionState.Closed, "Azure Speech recognition closed");
        }
        }

        public void WriteAudio(byte[] buffer, int count)
        {
            if (!_acceptingAudio || buffer == null || count <= 0)
                return;

            try
            {
                byte[] audio = new byte[count];
                Buffer.BlockCopy(buffer, 0, audio, 0, count);

                lock (_audioLock)
                {
                    if (_acceptingAudio && _pushStream != null)
                        _pushStream.Write(audio);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                WriteLog("logs/azure-stt-errors.txt", ex.ToString());
                SetState(SttConnectionState.Error, "Azure Speech audio send error", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                Task stopTask = StopAsync();
                if (!stopTask.Wait(TimeSpan.FromSeconds(3)))
                {
                    WriteLog(
                        "logs/azure-stt-errors.txt",
                        "Azure Speech dispose timed out while stopping recognition.");
                }
            }
            catch (Exception ex)
            {
                WriteLog("logs/azure-stt-errors.txt", ex.ToString());
            }
            finally
            {
                CloseAudioObjects();
            }
        }

        private SpeechConfig CreateSpeechConfig()
        {
            if (!string.IsNullOrWhiteSpace(_endpoint))
                return SpeechConfig.FromEndpoint(new Uri(_endpoint), _subscriptionKey);

            return SpeechConfig.FromSubscription(_subscriptionKey, _region);
        }

        private void EnsureConfigured()
        {
            if (!Environment.Is64BitProcess)
                throw new InvalidOperationException(
                    "Azure Speech SDK requires a 64-bit process. Build and run the app as x64.");

            if (string.IsNullOrWhiteSpace(_subscriptionKey))
                throw new InvalidOperationException("Azure Speech subscription key is not configured.");

            if (string.IsNullOrWhiteSpace(_endpoint) && string.IsNullOrWhiteSpace(_region))
                throw new InvalidOperationException("Azure Speech region or endpoint is not configured.");

            ResolveVadMs(_vadMs);
        }

        private void EnsureNativeRuntimeAvailable()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string systemDir = Environment.SystemDirectory;
            List<string> missing = new List<string>();

            string[] speechNativeDlls =
            {
                "Microsoft.CognitiveServices.Speech.core.dll",
                "Microsoft.CognitiveServices.Speech.extension.audio.sys.dll",
                "Microsoft.CognitiveServices.Speech.extension.codec.dll",
                "Microsoft.CognitiveServices.Speech.extension.kws.dll",
                "Microsoft.CognitiveServices.Speech.extension.kws.ort.dll"
            };

            foreach (string dllName in speechNativeDlls)
            {
                if (!File.Exists(Path.Combine(appDir, dllName)))
                    missing.Add(dllName);
            }

            string[] vcRuntimeDlls =
            {
                "MSVCP140.dll",
                "MSVCP140_CODECVT_IDS.dll",
                "VCRUNTIME140.dll",
                "VCRUNTIME140_1.dll"
            };

            foreach (string dllName in vcRuntimeDlls)
            {
                if (!File.Exists(Path.Combine(appDir, dllName)) &&
                    !File.Exists(Path.Combine(systemDir, dllName)))
                {
                    missing.Add(dllName);
                }
            }

            if (missing.Count == 0)
                return;

            throw new InvalidOperationException(
                "Azure Speech native runtime is incomplete. Missing: " +
                string.Join(", ", missing) +
                "\n\nFor a release zip, include all Microsoft.CognitiveServices.Speech*.dll files from the Release folder. " +
                "On the target machine, install Microsoft Visual C++ 2015-2022 Redistributable (x64): " +
                "https://aka.ms/vs/17/release/vc_redist.x64.exe");
        }

        private string ResolveLanguage(string requestedLanguage)
        {
            if (!string.IsNullOrWhiteSpace(_configuredLanguage))
                return _configuredLanguage.Trim();

            if (string.IsNullOrWhiteSpace(requestedLanguage))
                return "en-US";

            string value = requestedLanguage.Trim();
            string normalized = value.ToLowerInvariant();

            if (normalized == "cn" || normalized == "zh" || normalized == "zh-cn")
                return "zh-CN";

            if (normalized == "ko" || normalized == "kr" || normalized == "ko-kr")
                return "ko-KR";

            if (normalized == "es" || normalized == "es-es")
                return "es-ES";

            if (normalized == "en" || normalized == "en-us")
                return "en-US";

            return value;
        }

        private int ResolveVadMs(int vadMs)
        {
            if (vadMs <= 0)
                return 0;

            if (vadMs < 100 || vadMs > 5000)
            {
                throw new InvalidOperationException(
                    "Azure Speech vad must be between 100 and 5000 milliseconds.");
            }

            return vadMs;
        }

        private void AttachEvents(SpeechRecognizer recognizer)
        {
            recognizer.Recognizing += OnRecognizing;
            recognizer.Recognized += OnRecognized;
            recognizer.Canceled += OnCanceled;
            recognizer.SessionStarted += OnSessionStarted;
            recognizer.SessionStopped += OnSessionStopped;
        }

        private void DetachEvents(SpeechRecognizer recognizer)
        {
            recognizer.Recognizing -= OnRecognizing;
            recognizer.Recognized -= OnRecognized;
            recognizer.Canceled -= OnCanceled;
            recognizer.SessionStarted -= OnSessionStarted;
            recognizer.SessionStopped -= OnSessionStopped;
        }

        private void OnRecognizing(object sender, SpeechRecognitionEventArgs e)
        {
            if (e.Result == null || e.Result.Reason != ResultReason.RecognizingSpeech)
                return;

            string text = e.Result.Text;
            if (string.IsNullOrWhiteSpace(text) || text == _lastPartialText)
                return;

            _lastPartialText = text;

            TranscriptReceived?.Invoke(this, new SttTranscriptEventArgs
            {
                IsFinal = false,
                Text = text,
                SegmentId = e.Result.ResultId ?? ""
            });
        }

        private void OnRecognized(object sender, SpeechRecognitionEventArgs e)
        {
            if (e.Result == null)
                return;

            if (e.Result.Reason == ResultReason.NoMatch)
            {
                WriteLog("logs/azure-stt.txt", "NoMatch");
                return;
            }

            if (e.Result.Reason != ResultReason.RecognizedSpeech)
                return;

            string text = e.Result.Text;
            string resultId = e.Result.ResultId ?? "";

            if (string.IsNullOrWhiteSpace(text) || resultId == _lastFinalResultId)
                return;

            _lastFinalResultId = resultId;
            _lastPartialText = "";

            TranscriptReceived?.Invoke(this, new SttTranscriptEventArgs
            {
                IsFinal = true,
                Text = text,
                SegmentId = resultId
            });
        }

        private void OnCanceled(object sender, SpeechRecognitionCanceledEventArgs e)
        {
            string message = "Azure Speech canceled: " + e.Reason;

            if (!string.IsNullOrWhiteSpace(e.ErrorDetails))
                message = message + " - " + e.ErrorDetails;

            WriteLog("logs/azure-stt-errors.txt", message);
            SetState(SttConnectionState.Error, message);
        }

        private void OnSessionStarted(object sender, SessionEventArgs e)
        {
            RaiseRawProviderMessage(BuildProviderMessage("azure_stt_session_started", e.SessionId));
            SetState(SttConnectionState.Open, "Azure Speech session started");
        }

        private void OnSessionStopped(object sender, SessionEventArgs e)
        {
            if (State != SttConnectionState.Closing)
                SetState(SttConnectionState.Closed, "Azure Speech session stopped");
        }

        private void CloseAudioObjects()
        {
            lock (_audioLock)
            {
                if (_audioConfig != null)
                {
                    _audioConfig.Dispose();
                    _audioConfig = null;
                }

                if (_pushStream != null)
                {
                    _pushStream.Dispose();
                    _pushStream = null;
                }
            }
        }

        private void SetState(SttConnectionState state, string message, Exception exception = null)
        {
            State = state;
            RaiseStatus(state, message, exception, false);
        }

        private void RaiseRawProviderMessage(string message)
        {
            RaiseStatus(State, message, null, true);
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
                Exception = exception,
                IsRawProviderMessage = isRawProviderMessage
            });
        }

        private string BuildProviderMessage(string eventName, string sessionId)
        {
            return JsonConvert.SerializeObject(new
            {
                provider = "azure",
                @event = eventName,
                sessionId = sessionId ?? "",
                language = _activeLanguage,
                vadMs = _activeVadMs > 0 ? _activeVadMs.ToString() : "default",
                segmentationProperty = _activeVadMs > 0 ? "Speech_SegmentationSilenceTimeoutMs" : "",
                endpoint = string.IsNullOrWhiteSpace(_endpoint) ? "" : _endpoint,
                region = string.IsNullOrWhiteSpace(_region) ? "" : _region,
                audioFormat = "PCM 16000 Hz 16-bit mono",
                process = Environment.Is64BitProcess ? "x64" : "x86"
            });
        }

        private void WriteLog(string path, string message)
        {
            Directory.CreateDirectory("logs");
            File.AppendAllText(path, DateTime.Now + " " + message + "\n");
        }
    }
}
