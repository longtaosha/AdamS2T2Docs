using CSCore;
using CSCore.Codecs.WAV;
using CSCore.SoundIn;
using CSCore.SoundOut;
using CSCore.Streams;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Sinks.File;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Documents;
using System.Windows.Forms;

namespace AdamS2T2Docs
{

    public partial class Form1 : Form
    {
        private string apikeyFilePath = "apikey.json";
        private string appid; 
        private string apiKey;
        private int typeEffect; 
        private string googleDocsID;
        private string copyWordgglID;
        private string language;
        private string filter_cn;

        private string qwenApiKey;
        private string qwenBaseUrl;
        private string qwenModel;
        private string azureOpenAiApiKey;
        private string azureOpenAiEndpoint;
        private string azureOpenAiDeployment;
        private string azureOpenAiApiVersion;
        private string azureSpeechKey;
        private string azureSpeechRegion;
        private string azureSpeechEndpoint;
        private string azureSpeechLanguage;
        private int azureSpeechVadMs;
        private string proofreadProvider = "qwen";
        private string sttProvider = "xfyun";
        private bool _isApplyingSttProviderSelection;
        private bool _isRecordingSessionActive;
        private bool _isStoppingRecordingSession;
        private bool isAiProofreadEnabled = true;
        private IProofreader aiProofreader;
        private ISttEngine sttEngine;
        private readonly SemaphoreSlim _aiSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _googleDirectSemaphore = new SemaphoreSlim(1, 1);

        private string _proofreadContext = "";
        private const int ProofreadContextMaxChars = 300;

        private string _googleDocsProofreadBuffer = "";
        private const int GoogleDocsProofreadMinWords = 12;

        private string _currentBusyStage = "Idle";
        private DateTime _currentBusyStageStart = DateTime.MinValue;
        private readonly object _busyStageLock = new object();
        private readonly LinkedList<string> _pendingGoogleDocsQueue = new LinkedList<string>();
        private readonly object _pendingGoogleDocsQueueLock = new object();
        private bool _isGoogleDocsWorkerRunning = false;
        private DateTime _lastFinalSegmentTime = DateTime.MinValue;
        private int googleDocsIdleFlushSeconds = 6;
        private int googleDocsAppendTimeoutSeconds = 20;
        private bool useGoogleDocsQueue = true;
        private bool filterChineseFinalText = false;
        private DateTime _lastFinalArrivalTime = DateTime.MinValue;
        private bool _pendingCommaAfterPause = false;

        private string googleDocsIDForFakeTyping;
        private ConnectToGoogle connectToGoogle;
        private string connectSqlString;
        //private MySqlConnection mySqlConnection;
        //private MySqlConnection gSqlConnection;

        private int i = 0;
        private int segidForTimer = 1;
        private int lastSegid = 0;
        private string currentSpeaker = "0"; 
        private bool readFromMysqlSucceeded = false;
        private bool isToWrite = true;
        private bool isPushedToGoogle = false;
        private int dataSize = 0;
        private bool isFirstAppend = true;
        private bool isToNewLine = true;
        private bool isToNewLineForGoogle = true;
        private bool isNamed = false;
        private bool noAudio = true;
        private bool isOutputWordToUI = false;

        private bool isToStreamText = false; 

        private WasapiCapture soundIn = new WasapiLoopbackCapture();
        private SoundInSource soundInSource;
        private IWaveSource convertedSource;
        private WaveWriter waveWriter;
        private TimeSpan timeSpan = new TimeSpan(0, 0, 1);
        private static readonly System.Timers.Timer aTimer;
        private static System.Diagnostics.Stopwatch myWatch;
        private System.Timers.Timer gTimer;
        private bool isGoogleError = false;
        private bool isOutputGoogleJustFinalResult = true;


        public Form1()
        {

            // Configure Serilog to write log messages to a file
            Log.Logger = new LoggerConfiguration()
                 .MinimumLevel.Information() // Set the minimum log level
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Log.Information("Application startup: Logging configuration executed.");
            //Log.Error("log error test"); 


            try
            {
                InitializeComponent();
                loadApiKey();
                InitializeSttEngine();
                ApplySttProviderSelection();
                connectToGoogle = new ConnectToGoogle(googleDocsID, typeEffect, copyWordgglID);
                //connectToGoogleForFakeTyping = new ConnectToGoogle(googleDocsIDForFakeTyping);               

                InitializeProofreader();
                ApplyProofreadProviderSelection();

                //connectSqlString = "SERVER= 115.28.210.53; DATABASE= s2t2docsscript;" +
                //  "USER= root; port=3306; PASSWORD= @f22bBfb@; SslMode = none";


                mySystemSoundCapture();
                soundIn.DataAvailable += SoundIn_DataAvailable;
                soundIn.Stopped += OnSoundInStopped;
                connectToGoogle.OnTextCopied += outputWordToUI;

                System.Windows.Forms.Timer monitorTimer = new System.Windows.Forms.Timer();
                monitorTimer.Interval = 1000;
                monitorTimer.Tick += (s, e) =>
                {
                    string stage;
                    DateTime start;
                    int queueCount;

                    lock (_busyStageLock)
                    {
                        stage = _currentBusyStage;
                        start = _currentBusyStageStart;
                    }

                    lock (_pendingGoogleDocsQueueLock)
                    {
                        queueCount = _pendingGoogleDocsQueue.Count;
                    }

                    // status display
                    if (stage == "Idle")
                    {
                        labelStatus.Text = "Idle | Q=" + queueCount;
                    }
                    else
                    {
                        var seconds = (DateTime.Now - start).TotalSeconds;

                        labelStatus.Text =
                            "Busy: " + stage + " " +
                            seconds.ToString("0.0") + "s" +
                            " | Q=" + queueCount;
                    }

                    // auto restart worker if queue exists
                    if (useGoogleDocsQueue && queueCount > 0 && !_isGoogleDocsWorkerRunning)
                    {
                        _ = ProcessGoogleDocsQueueAsync();
                    }

                    // idle flush:
                    // if no new final seg for several seconds,
                    // flush remaining docs buffer into queue
                    if (!string.IsNullOrWhiteSpace(_googleDocsProofreadBuffer) &&
                        _lastFinalSegmentTime != DateTime.MinValue &&
                        (DateTime.Now - _lastFinalSegmentTime).TotalSeconds >= googleDocsIdleFlushSeconds)
                    {
                        string docsRawText = _googleDocsProofreadBuffer;
                        _googleDocsProofreadBuffer = "";

                        if (useGoogleDocsQueue)
                        {
                            lock (_pendingGoogleDocsQueueLock)
                            {
                                _pendingGoogleDocsQueue.AddLast(docsRawText);

                                File.AppendAllText(
                                    "logs/googleQueue.txt",
                                    DateTime.Now +
                                    " IDLE_FLUSH_ENQUEUE len=" +
                                    docsRawText.Length +
                                    " queue=" +
                                    _pendingGoogleDocsQueue.Count +
                                    "\n");
                            }

                            _ = ProcessGoogleDocsQueueAsync();
                        }
                        else
                        {
                            File.AppendAllText(
                                "logs/googleQueue.txt",
                                DateTime.Now +
                                " IDLE_FLUSH_DIRECT len=" +
                                docsRawText.Length +
                                "\n");

                            _ = SendDocsTextDirectlyAsync(docsRawText);
                        }

                        // prevent repeated idle flush
                        _lastFinalSegmentTime = DateTime.MinValue;
                    }
                };

                monitorTimer.Start();

            }
            catch (Exception ex)
            { 
                // Log an error message with exception details
                Log.Error(ex, "An error occurred during {OperationName}", "Starting");
                MessageBox.Show(
                    GetUserVisibleExceptionMessage(ex),
                    "Application Startup Error");

            }
     

        }

        private void SoundIn_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            //throw new NotImplementedException();

        }
        private void OnSoundInStopped(object sender, RecordingStoppedEventArgs e)
        {
            //throw new NotImplementedException();
            //label1.Invoke(new Action(() => { label1.Text = "Sound In Source stopped"; }));
        }

        private void mySystemSoundCapture()
        {
            if (soundIn.RecordingState == RecordingState.Stopped) soundIn.Initialize();// get ready for caputering the system sound 
            soundInSource = new SoundInSource(soundIn) { FillWithZeros = false };//copy the captured sound
            soundInSource.DataAvailable += SoundInSource_DataAvailable;
            //convert the captured sound
            convertedSource = soundInSource
            .ChangeSampleRate(16000)
            .ToSampleSource()
            .ToWaveSource(16);
            //convert channel
            convertedSource = convertedSource.ToMono();


        }



        private void SoundInSource_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            if (sttEngine == null || sttEngine.State != SttConnectionState.Open)
                return;

            byte[] buffer = new byte[1280];//40ms
            int read;

            while ((read = convertedSource.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (sttEngine == null || sttEngine.State != SttConnectionState.Open)
                {
                    label1.Invoke(new Action(() => { label1.Text = "speech engine is closed"; }));
                    return;
                }

                sttEngine.WriteAudio(buffer, read);
            }
        }

        private async void OnSttTranscriptReceived(object sender, SttTranscriptEventArgs e)
        {
            try
            {
                string[] result =
                {
                    e.IsFinal ? "0" : "1",
                    e.Text,
                    e.SegmentId,
                    e.EndTime,
                    e.SpeakerId
                };

                if (result[1] != null)
                {
                        // index 0 is Type, 0 = final result, 1 = mid-processing result
                        // index 1 is final result
                        // index 2 is seg_id
                        // index 3 is end time
                        // index 4 is roleNum
                        if (result[0].Equals("0") && result[1] != null)
                        {
                            /*if (currentSpeaker != result[4] && result[4]!=null)
                            {
                                result[1] = "."+"\n"+">> "+result[4] +": "+ result[1].Substring(1);
                            }

                            currentSpeaker = result[4]; */

                            string finalText = result[1];

                            if (language == "cn")
                            {
                                finalText = NormalizeChinesePunctuation(finalText);

                                if (filterChineseFinalText && ContainsChinese(finalText))
                                {
                                    string originalFinalText = finalText;

                                    finalText = RemoveChineseTextAndNormalizePunctuation(finalText);

                                    bool hasContinuousEnglish =
                                        HasContinuousEnglishPhrase(finalText, 5);

                                    File.AppendAllText("logs/chineseFiltered.txt",
                                        DateTime.Now + "\nFILTERED_CN_FINAL:\n" +
                                        "RAW : " + originalFinalText + "\n" +
                                        "KEPT: " + finalText + "\n" +
                                        "CONTINUOUS_5: " + hasContinuousEnglish + "\n");

                                    if (string.IsNullOrWhiteSpace(finalText) ||
                                        !hasContinuousEnglish)
                                    {
                                        File.AppendAllText("logs/chineseFiltered.txt",
                                            "FILTER_DROP\n\n");

                                        return;
                                    }

                                    File.AppendAllText("logs/chineseFiltered.txt",
                                        "FILTER_KEEP\n\n");
                                }
                            }

                            finalText = EnsureFinalSegmentBoundarySpacing(
                                _proofreadContext,
                                finalText);

                            // pause-comma logic:
                            // if long pause and neither side has punctuation,
                            // prepend comma to current segment
                            if (_lastFinalArrivalTime != DateTime.MinValue)
                            {
                                double pauseMs =
                                    (DateTime.Now - _lastFinalArrivalTime)
                                    .TotalMilliseconds;

                                if (pauseMs > 2500 &&
                                    !_pendingCommaAfterPause &&
                                    !EndsWithBoundaryPunctuation(_proofreadContext) &&
                                    !StartsWithBoundaryPunctuation(finalText))
                                {
                                    finalText = ", " + finalText.TrimStart();

                                    File.AppendAllText(
                                        "logs/pauseComma.txt",
                                        DateTime.Now +
                                        " pause=" +
                                        pauseMs.ToString("0") +
                                        "ms ADD_COMMA\n");
                                }
                            }

                            _lastFinalArrivalTime = DateTime.Now;
                            _lastFinalSegmentTime = DateTime.Now;
                            int wordCount = CountWordsForProofread(finalText);
                          

                            File.AppendAllText("logs/ai-proofread.txt", DateTime.Now + "\nRAW_UI: " + finalText + "\n\n");

                            // UI path: show raw final text directly.
                            // AI proofreading is only used later in ProcessGoogleDocsQueueAsync()
                            // for Google Docs output.

                            richTextBox1?.Invoke(new Action(() =>
                            {
                                richTextBox1.SelectedText = finalText;
                                richTextBox1.DeselectAll();
                            }));

                   
                            isToNewLineForGoogle = isToNewLine;
                            isToNewLine = true;

                            _proofreadContext += finalText;

                            if (_proofreadContext.Length > ProofreadContextMaxChars)
                            {
                                _proofreadContext = _proofreadContext.Substring(
                                    _proofreadContext.Length - ProofreadContextMaxChars);
                            }
                           

                            _googleDocsProofreadBuffer += finalText;

                            int docsBufferWordCount = CountWordsForProofread(_googleDocsProofreadBuffer);

                            if (docsBufferWordCount >= GoogleDocsProofreadMinWords)
                            {
                                string docsRawText = _googleDocsProofreadBuffer;
                                string docsText = docsRawText;
                                _googleDocsProofreadBuffer = "";

                                if (useGoogleDocsQueue)
                                {
                                    lock (_pendingGoogleDocsQueueLock)
                                    {
                                        _pendingGoogleDocsQueue.AddLast(docsRawText);

                                        File.AppendAllText(
                                            "logs/googleQueue.txt",
                                            DateTime.Now +
                                            " ENQUEUE len=" +
                                            docsRawText.Length +
                                            " queue=" +
                                            _pendingGoogleDocsQueue.Count +
                                            "\n");
                                    }

                                    _ = ProcessGoogleDocsQueueAsync();
                                }
                                else
                                {
                                    File.AppendAllText(
                                        "logs/googleQueue.txt",
                                        DateTime.Now +
                                        " DIRECT len=" +
                                        docsRawText.Length +
                                        "\n");

                                    _ = SendDocsTextDirectlyAsync(docsRawText);
                                }
                               
                            }
                           
                        }
                        else if (result[0].Equals("1") && result[1] != null)
                        {
                            richTextBox1.Invoke(new Action(() =>
                            {

                                if (isFirstAppend == true)
                                {
                                    richTextBox1.AppendText(result[1]);
                                    isFirstAppend = false;
                                    label1.Invoke(new Action(() => { label1.Text = "first appended "; }));
                                    isToNewLine = true;
                                }
                                else
                                {
                                    richTextBox1.SelectedText = result[1];
                                    label1.Invoke(new Action(() => { label1.Text = "not first appended " + dataSize; }));

                                }

                                if (richTextBox1.Text.Length >= result[1].Length)
                                {
                                    richTextBox1.Select(richTextBox1.Text.Length - result[1].Length, result[1].Length);
                                }

                                //label1.Invoke(new Action(() => { label1.Text = "the last length is " + lastTextLength + " + result length is: " + result[1].Length+" = Text Length is: " + richTextBox1.Text.Length +" SlectedText Length is:"+richTextBox1.SelectedText.Length; }));
                            }));

                            var elapsedMs = myWatch.ElapsedMilliseconds;

                            //File.AppendAllText("logs/resultFromXunfei.txt", "seg_id: " + result[2] + " type = " + result[0] + "- " + " :" + "is1st=" +isToNewLine +": " + result[1] + "\n");

                            //string line = result[1].Replace("'", "\\'");
                            //string query = "INSERT INTO `rawscript` (`seg_id`, `type`, `line`, `newlinemark`) VALUES (" +
                            //result[2] + ", " + 1 + ", '" + line + "' ," + isToNewLine + " );";
                            // if (mySqlConnection.State == System.Data.ConnectionState.Closed) mySqlConnection.Open();
                            //MySqlCommand adamCommand = new MySqlCommand(query, mySqlConnection);
                            //adamCommand.ExecuteNonQuery();
                            //mySqlConnection.Close();
                            isToNewLineForGoogle = isToNewLine;
                            isToNewLine = false;

                            if (!isGoogleError)
                            {
                                if (!isOutputGoogleJustFinalResult)
                                {
                                    try
                                    {
                                        await connectToGoogle.connectOnlyWaitFinalAsync(1, result[1], isToNewLineForGoogle);
                                    }
                                    catch (Exception eGoogle)
                                    {
                                        isGoogleError = true;
                                        File.AppendAllText("logs/googleErrors.txt", DateTime.Now.ToString() + "\n"
                                            + eGoogle.Message.ToString() + eGoogle.StackTrace + "\n");
                                        MessageBox.Show(eGoogle.Message.ToString() + eGoogle.StackTrace);

                                    }
                                }
                            }

                            //await connectToGoogle.connectReplaceTextAsync(1, result[1], result[2]);                      
                            //await connectToGoogle.connectNewAsync(1, result[1]);
                            //await connectToGoogle.connectAsync(1, result[1]);
                        }
                        else
                        {
                            label1.Invoke(new Action(() => { label1.Text = " the result 1 is no null"; }));
                        }
                }
            }
            catch (Exception ex)
            {
                // Log the exception and handle it as needed
                Log.Error(ex, "An error occurred during speech transcript receiving");              
                // Optionally, display an error message to the user
                MessageBox.Show("An error occurred during speech transcript receiving. Please contact support.");
            }
        }

        private bool EndsWithBoundaryPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            char c = text[text.Length - 1];

            return c == '.' ||
                   c == '?' ||
                   c == '!' ||
                   c == ',' ||
                   c == ':' ||
                   c == ';' ||
                   c == '—' ||
                   c == '\n' ||
                   c == '\r';
        }

        private bool StartsWithBoundaryPunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            string trimmed = text.TrimStart();

            if (string.IsNullOrEmpty(trimmed))
                return true;

            char c = trimmed[0];

            return c == '.' ||
                   c == '?' ||
                   c == '!' ||
                   c == ',' ||
                   c == ':' ||
                   c == ';' ||
                   c == '—' ||
                   c == '\n' ||
                   c == '\r';
        }

        private string EnsureFinalSegmentBoundarySpacing(string priorText, string currentText)
        {
            if (string.IsNullOrEmpty(priorText) || string.IsNullOrEmpty(currentText))
                return currentText;

            if (char.IsWhiteSpace(currentText[0]))
                return currentText;

            char previous = priorText[priorText.Length - 1];
            char current = currentText[0];

            if (char.IsLetterOrDigit(previous) && char.IsLetterOrDigit(current))
                return " " + currentText;

            return currentText;
        }

        private int CountWordsForProofread(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            string cleaned = text.Trim();

            char[] separators = new char[]
            {
        ' ', '\t', '\r', '\n',
        '.', ',', '?', '!', ';', ':',
        '，', '。', '？', '！', '；', '：'
            };

            string[] words = cleaned.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            return words.Length;
        }
        private bool HasContinuousEnglishPhrase(
        string text,
        int minWords = 5)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var matches =
                System.Text.RegularExpressions.Regex.Matches(
                    text,
                    @"[A-Za-z]+(?:['\-][A-Za-z]+)?");

            int consecutive = 0;

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                consecutive++;

                if (consecutive >= minWords)
                    return true;
            }

            return false;
        }
        private bool ContainsChinese(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                if (c >= '\u4e00' && c <= '\u9fff')
                    return true;
            }

            return false;
        }
        private string NormalizeChinesePunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string result = text;

            result = result.Replace("，", ", ")
                           .Replace("。", ". ")
                           .Replace("？", "? ")
                           .Replace("！", "! ")
                           .Replace("；", "; ")
                           .Replace("：", ": ")
                           .Replace("（", "(")
                           .Replace("）", ")")
                           .Replace("“", "\"")
                           .Replace("”", "\"")
                           .Replace("‘", "'")
                           .Replace("’", "'")
                           .Replace("、", ", ");

            // collapse accidental double spaces
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"\s+",
                " ");

            return result;
        }
        private string RemoveChineseTextAndNormalizePunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string result = NormalizeChinesePunctuation(text);

            // Remove Chinese characters only
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"[\u4e00-\u9fff]+",
                " ");

            // Normalize spaces
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"\s+",
                " ");

            return result;
        }
        private void SetBusyStage(string stage)
        {
            lock (_busyStageLock)
            {
                _currentBusyStage = stage;
                _currentBusyStageStart = DateTime.Now;
            }

            File.AppendAllText("logs/runtime-stage.txt",
                DateTime.Now + " ENTER " + stage + "\n");
        }

        private void ClearBusyStage(string stage)
        {
            TimeSpan elapsed;

            lock (_busyStageLock)
            {
                elapsed = DateTime.Now - _currentBusyStageStart;
                _currentBusyStage = "Idle";
                _currentBusyStageStart = DateTime.MinValue;
            }

            File.AppendAllText("logs/runtime-stage.txt",
                DateTime.Now + " EXIT " + stage + " elapsed=" + elapsed.TotalMilliseconds.ToString("0") + "ms\n");
        }

        private void InitializeSttEngine()
        {
            Directory.CreateDirectory("logs");

            string provider = NormalizeSttProvider(sttProvider);
            ValidateSttProviderConfig(provider);
            sttProvider = provider;

            if (sttEngine != null)
            {
                sttEngine.TranscriptReceived -= OnSttTranscriptReceived;
                sttEngine.StatusChanged -= OnSttStatusChanged;
                sttEngine.Dispose();
                sttEngine = null;
            }

            if (provider == "xfyun")
            {
                sttEngine = new XfyunSttEngine(appid, apiKey);
            }
            else if (provider == "azure")
            {
                sttEngine = new AzureSpeechSttEngine(
                    azureSpeechKey,
                    azureSpeechRegion,
                    azureSpeechEndpoint,
                    azureSpeechLanguage,
                    azureSpeechVadMs);
            }
            else
            {
                sttEngine = new XfyunSttEngine(appid, apiKey);
                provider = "xfyun";
            }

            sttProvider = provider;
            sttEngine.TranscriptReceived += OnSttTranscriptReceived;
            sttEngine.StatusChanged += OnSttStatusChanged;

            File.AppendAllText("logs/stt-provider.txt",
                DateTime.Now + " provider=" + sttProvider +
                " name=" + sttEngine.ProviderName + "\n");
        }

        private string NormalizeSttProvider(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
                return "xfyun";

            provider = provider.Trim().ToLowerInvariant();

            if (provider == "xunfei" || provider == "iflytek" ||
                provider == "xfyun" || provider == "xun_fei")
            {
                return "xfyun";
            }

            if (provider == "azure" || provider == "azurestt" ||
                provider == "azure_stt" || provider == "azure-speech" ||
                provider == "azurespeech")
            {
                return "azure";
            }

            return provider;
        }

        private void ValidateSttProviderConfig(string provider)
        {
            if (provider == "xfyun")
            {
                if (string.IsNullOrWhiteSpace(appid) ||
                    string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException(
                        "XFYun STT is selected, but appid or apiKey is missing. Check apikey.json, or set sttProvider to azure.");
                }

                return;
            }

            if (provider == "azure")
            {
                if (string.IsNullOrWhiteSpace(azureSpeechKey))
                {
                    throw new InvalidOperationException(
                        "Azure STT is selected, but azureSpeechKey is missing. Check apikey.json.");
                }

                if (string.IsNullOrWhiteSpace(azureSpeechEndpoint) &&
                    string.IsNullOrWhiteSpace(azureSpeechRegion))
                {
                    throw new InvalidOperationException(
                        "Azure STT is selected, but azureSpeechRegion or azureSpeechEndpoint is missing. Check apikey.json.");
                }
            }
        }

        private void OnSttStatusChanged(object sender, SttStatusEventArgs e)
        {
            if (e.IsRawProviderMessage && !string.IsNullOrWhiteSpace(e.Message))
            {
                richTextBox1.BeginInvoke(new Action(() =>
                {
                    richTextBox1.AppendText(e.Message + "\n");
                }));
            }

            string statusText = e.State.ToString();
            if (!string.IsNullOrWhiteSpace(e.Message))
                statusText = statusText + ": " + e.Message;

            if (label1 != null && !label1.IsDisposed)
                label1.BeginInvoke(new Action(() => { label1.Text = statusText; }));

            if (e.Exception != null)
            {
                File.AppendAllText("logs/stt-errors.txt",
                    DateTime.Now + "\n" +
                    e.Message + "\n" +
                    e.Exception + "\n\n");

                MessageBox.Show(
                    e.Exception.Message,
                    "Speech Engine Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void InitializeProofreader()
        {
            Directory.CreateDirectory("logs");
            string provider = NormalizeProofreadProvider(proofreadProvider);

            if (provider == "none")
            {
                aiProofreader = null;
            }
            else if (provider == "openai")
            {
                aiProofreader = new AzureOpenAiProofreader(
                    azureOpenAiApiKey,
                    azureOpenAiEndpoint,
                    azureOpenAiDeployment,
                    azureOpenAiApiVersion);
            }
            else
            {
                aiProofreader = new QwenProofreader(
                    qwenApiKey,
                    qwenBaseUrl,
                    qwenModel);
            }

            proofreadProvider = provider;

            File.AppendAllText("logs/proofread-provider.txt",
                DateTime.Now + " provider=" + proofreadProvider +
                " name=" + (aiProofreader == null ? "No proofreading" : aiProofreader.ProviderName) + "\n");
        }

        private string NormalizeProofreadProvider(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
                return "qwen";

            provider = provider.Trim().ToLowerInvariant();

            if (provider == "none" || provider == "no" ||
                provider == "off" || provider == "disabled" ||
                provider == "raw" || provider == "no proofreading" ||
                provider == "noproofreading" || provider == "no_proofreading" ||
                provider == "no pr." || provider == "no pr" ||
                provider == "nopr" || provider == "no_pr")
            {
                return "none";
            }

            if (provider == "azure" || provider == "azureopenai" ||
                provider == "azure_openai" || provider == "openai")
            {
                return "openai";
            }

            return "qwen";
        }

        private void ApplySttProviderSelection()
        {
            if (sttProviderComboBox == null)
                return;

            string provider = NormalizeSttProvider(sttProvider);
            string selectedItem = provider == "azure" ? "Azure" : "XFYun";

            _isApplyingSttProviderSelection = true;
            try
            {
                if (sttProviderComboBox.Items.Contains(selectedItem))
                    sttProviderComboBox.SelectedItem = selectedItem;
            }
            finally
            {
                _isApplyingSttProviderSelection = false;
            }
        }

        private void ApplyProofreadProviderSelection()
        {
            if (proofreadProviderComboBox == null)
                return;

            string provider = NormalizeProofreadProvider(proofreadProvider);
            string selectedItem = "Qwen";
            if (provider == "openai")
                selectedItem = "OpenAI";
            else if (provider == "none")
                selectedItem = "No PR.";

            if (proofreadProviderComboBox.Items.Contains(selectedItem))
                proofreadProviderComboBox.SelectedItem = selectedItem;
        }

        private string ReadJsonString(dynamic json, string propertyName)
        {
            try
            {
                JToken token = json[propertyName];
                if (token == null || token.Type == JTokenType.Null)
                    return "";

                return token.ToString();
            }
            catch
            {
                return "";
            }
        }

        private string ReadFirstJsonString(dynamic json, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                string value = ReadJsonString(json, propertyName);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private int ReadFirstJsonInt(dynamic json, params string[] propertyNames)
        {
            string value = ReadFirstJsonString(json, propertyNames);
            int result;

            if (int.TryParse(value, out result))
                return result;

            return 0;
        }

        private void proofreadProviderComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (proofreadProviderComboBox.SelectedItem == null)
                return;

            string selectedProvider = proofreadProviderComboBox.SelectedItem.ToString();
            string normalizedProvider = NormalizeProofreadProvider(selectedProvider);

            if (normalizedProvider == proofreadProvider &&
                (normalizedProvider == "none" || aiProofreader != null))
            {
                return;
            }

            proofreadProvider = normalizedProvider;
            InitializeProofreader();
        }

        private void sttProviderComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isApplyingSttProviderSelection || sttProviderComboBox.SelectedItem == null)
                return;

            string selectedProvider = sttProviderComboBox.SelectedItem.ToString();
            string normalizedProvider = NormalizeSttProvider(selectedProvider);

            if (normalizedProvider == sttProvider && sttEngine != null)
                return;

            bool isRecording =
                _isRecordingSessionActive ||
                _isStoppingRecordingSession;

            bool isEngineBusy =
                sttEngine != null &&
                sttEngine.State != SttConnectionState.Closed &&
                sttEngine.State != SttConnectionState.Error;

            if (isRecording || isEngineBusy)
            {
                MessageBox.Show(
                    "Stop recording before switching STT engine.",
                    "STT Engine",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                ApplySttProviderSelection();
                return;
            }

            string previousProvider = sttProvider;
            sttProvider = normalizedProvider;

            try
            {
                InitializeSttEngine();
                ApplySttProviderSelection();
                label1.Text = "STT engine: " + sttEngine.ProviderName;
            }
            catch (Exception ex)
            {
                sttProvider = previousProvider;
                ApplySttProviderSelection();

                Exception displayException = UnwrapException(ex);
                Log.Error(displayException, "Failed to switch STT provider");
                MessageBox.Show(
                    GetUserVisibleExceptionMessage(displayException),
                    "STT Engine Switch Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private string GetProofreadDocsLogLabel(IProofreader proofreader)
        {
            if (proofreader == null)
                return "DOCS_NO_AI";

            string providerName = proofreader.ProviderName ?? "";
            if (providerName.Equals("Qwen", StringComparison.OrdinalIgnoreCase))
                return "DOCS_QWE";

            if (providerName.IndexOf("OpenAI", StringComparison.OrdinalIgnoreCase) >= 0)
                return "DOCS_AZO";

            return "DOCS_AI";
        }

        private bool IsProofreadSuccessful(ProofreadResult proofreadResult)
        {
            return proofreadResult != null && proofreadResult.Confidence > 0.0;
        }

        private void AppendProofreadDocsBufferLog(
            bool isDirect,
            string rawText,
            string docsText,
            IProofreader proofreader,
            ProofreadResult proofreadResult)
        {
            string suffix = isDirect ? "_DIRECT" : "";
            string providerLabel = GetProofreadDocsLogLabel(proofreader) + suffix;
            bool proofreadSkipped = proofreader == null;
            bool proofreadSucceeded = IsProofreadSuccessful(proofreadResult);

            string logText =
                DateTime.Now + "\n" +
                "DOCS_RAW" + suffix + ": " + rawText + "\n" +
                providerLabel + ": " + ((proofreadSucceeded || proofreadSkipped) ? docsText : "") + "\n";

            if (!proofreadSucceeded && !proofreadSkipped)
            {
                logText +=
                    "DOCS_AI_STATUS" + suffix + ": FAILED_OR_SKIPPED\n" +
                    "DOCS_FALLBACK_RAW_TO_GOOGLE" + suffix + ": " + rawText + "\n";
            }

            File.AppendAllText("logs/ai-proofread-docs-buffer.txt", logText + "\n");
        }

        private void LogProofreadCallError(IProofreader proofreader, bool isDirect, Exception ex)
        {
            Directory.CreateDirectory("logs");
            File.AppendAllText(
                "logs/ai-proofread-errors.txt",
                DateTime.Now + "\n" +
                "provider=" + (proofreader == null ? "" : proofreader.ProviderName) +
                " direct=" + isDirect + "\n" +
                ex + "\n\n");
        }

        private async Task SendDocsTextDirectlyAsync(string docsRawText)
        {
            if (string.IsNullOrWhiteSpace(docsRawText))
                return;

            await _googleDirectSemaphore.WaitAsync();

            try
            {
                string docsText = docsRawText;
                ProofreadResult docsProof = null;

                IProofreader proofreader = aiProofreader;
                if (isAiProofreadEnabled && proofreader != null)
                {
                    await _aiSemaphore.WaitAsync();
                    try
                    {
                        string proofreadStage = proofreader.ProviderName + " Proofread Direct";
                        SetBusyStage(proofreadStage);
                        try
                        {
                            try
                            {
                                docsProof = await proofreader.ProofreadAsync(
                                    docsText,
                                    _proofreadContext);
                            }
                            catch (Exception ex)
                            {
                                docsProof = null;
                                LogProofreadCallError(proofreader, true, ex);
                            }
                        }
                        finally
                        {
                            ClearBusyStage(proofreadStage);
                        }
                    }
                    finally
                    {
                        _aiSemaphore.Release();
                    }

                    if (IsProofreadSuccessful(docsProof))
                        docsText = docsProof.CorrectedText;
                }

                AppendProofreadDocsBufferLog(true, docsRawText, docsText, proofreader, docsProof);

                SetBusyStage("Google Docs Direct Append");
                try
                {
                    await connectToGoogle.connectFinalResultAsync(0, docsText);

                    File.AppendAllText(
                        "logs/googleAppendSuccess.txt",
                        DateTime.Now + "\n" +
                        "GOOGLE_APPEND_DIRECT:\n" +
                        docsText + "\n\n");
                }
                finally
                {
                    ClearBusyStage("Google Docs Direct Append");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("logs/googleErrors.txt",
                    DateTime.Now + "\nGoogle Docs direct write failed:\n" +
                    docsRawText + "\n" +
                    ex.Message + "\n" + ex.StackTrace + "\n\n");
            }
            finally
            {
                _googleDirectSemaphore.Release();
            }
        }

        private async Task ProcessGoogleDocsQueueAsync()
        {
            if (_isGoogleDocsWorkerRunning)
                return;

            _isGoogleDocsWorkerRunning = true;

            try
            {
                while (true)
                {
                    string docsRawText = null;

                    lock (_pendingGoogleDocsQueueLock)
                    {
                        if (_pendingGoogleDocsQueue.Count > 0)
                        {
                            docsRawText = _pendingGoogleDocsQueue.First.Value;
                            _pendingGoogleDocsQueue.RemoveFirst();

                            File.AppendAllText(
                                "logs/googleQueue.txt",
                                DateTime.Now +
                                " DEQUEUE len=" + docsRawText.Length +
                                " queue=" + _pendingGoogleDocsQueue.Count + "\n");
                        }
                    }

                    if (string.IsNullOrWhiteSpace(docsRawText))
                        break;

                    string docsText = docsRawText;
                    ProofreadResult docsProof = null;

                    IProofreader proofreader = aiProofreader;
                    if (isAiProofreadEnabled && proofreader != null)
                    {
                        await _aiSemaphore.WaitAsync();
                        try
                        {
                            string proofreadStage = proofreader.ProviderName + " Proofread";
                            SetBusyStage(proofreadStage);
                            try
                            {
                                try
                                {
                                    docsProof = await proofreader.ProofreadAsync(
                                        docsText,
                                        _proofreadContext);
                                }
                                catch (Exception ex)
                                {
                                    docsProof = null;
                                    LogProofreadCallError(proofreader, false, ex);
                                }
                            }
                            finally
                            {
                                ClearBusyStage(proofreadStage);
                            }
                        }
                        finally
                        {
                            _aiSemaphore.Release();
                        }

                        if (IsProofreadSuccessful(docsProof))
                            docsText = docsProof.CorrectedText;
                    }

                    AppendProofreadDocsBufferLog(false, docsRawText, docsText, proofreader, docsProof);

                    try
                    {
                        SetBusyStage("Google Docs Append");
                        try
                        {
                            Task googleTask = connectToGoogle.connectFinalResultAsync(0, docsText);
                            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(googleDocsAppendTimeoutSeconds)); ;

                            Task completedTask = await Task.WhenAny(googleTask, timeoutTask);

                            if (completedTask == timeoutTask)
                            {
                                throw new TimeoutException(
                            "Google Docs append timed out after " +
                            googleDocsAppendTimeoutSeconds +
                            " seconds.");
                            }

                            await googleTask;

                            File.AppendAllText(
                                "logs/googleAppendSuccess.txt",
                                DateTime.Now + "\n" +
                                "GOOGLE_APPEND:\n" +
                                docsText + "\n\n");
                        }
                        finally
                        {
                            ClearBusyStage("Google Docs Append");
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (_pendingGoogleDocsQueueLock)
                        {
                            _pendingGoogleDocsQueue.AddFirst(docsRawText);
                        }

                        File.AppendAllText("logs/googleErrors.txt",
                            DateTime.Now + "\nGoogle Docs queue write failed, requeued to front:\n" +
                            docsRawText + "\n" +
                            ex.Message + "\n" + ex.StackTrace + "\n\n");

                        break;
                    }
                }
            }
            finally
            {
                _isGoogleDocsWorkerRunning = false;
            }
        }

        private async Task FlushPendingDocsBufferAsync()
        {
            // flush pending docs buffer
            if (!string.IsNullOrWhiteSpace(_googleDocsProofreadBuffer))
            {
                string docsRawText = _googleDocsProofreadBuffer;
                _googleDocsProofreadBuffer = "";

                if (useGoogleDocsQueue)
                {
                    lock (_pendingGoogleDocsQueueLock)
                    {
                        _pendingGoogleDocsQueue.AddLast(docsRawText);

                        File.AppendAllText(
                            "logs/googleQueue.txt",
                            DateTime.Now +
                            " STOP_FLUSH_ENQUEUE len=" +
                            docsRawText.Length +
                            " queue=" +
                            _pendingGoogleDocsQueue.Count +
                            "\n");
                    }
                }
                else
                {
                    File.AppendAllText(
                        "logs/googleQueue.txt",
                        DateTime.Now +
                        " STOP_FLUSH_DIRECT len=" +
                        docsRawText.Length +
                        "\n");

                    await SendDocsTextDirectlyAsync(docsRawText);
                }
            }

            // make sure worker starts
            if (useGoogleDocsQueue)
            {
                await ProcessGoogleDocsQueueAsync();
                // wait queue drain
                while (true)
                {
                    bool queueEmpty;
                    bool workerRunning;

                    lock (_pendingGoogleDocsQueueLock)
                    {
                        queueEmpty =
                            _pendingGoogleDocsQueue.Count == 0;
                    }

                    workerRunning = _isGoogleDocsWorkerRunning;

                    if (queueEmpty && !workerRunning)
                        break;

                    await Task.Delay(200);
                }
            }

            

            File.AppendAllText(
                "logs/googleQueue.txt",
                DateTime.Now +
                " STOP_FLUSH_COMPLETE\n");
        }
        private void loadApiKey()
        {
            try
            {
                string jsonFromFile;
                using (var reader = new StreamReader(apikeyFilePath))
                {
                    jsonFromFile = reader.ReadToEnd();
                }

                dynamic d = JObject.Parse(jsonFromFile);
                
                appid = d.appid;
                apiKey = d.apiKey;
                typeEffect = d.typeEffect; 
                googleDocsID = d.googleDocsID;
                copyWordgglID = d.copyWordgglID;
                language = d.language;
                filter_cn = d.filter_cn;
                qwenApiKey = d.qwenApiKey;
                qwenBaseUrl = d.qwenBaseUrl;
                qwenModel = d.qwenModel;
                azureOpenAiApiKey = ReadFirstJsonString(
                    d,
                    "azureOpenAiApiKey",
                    "azureOpenAIApiKey",
                    "openAiApiKey",
                    "openAIApiKey");
                azureOpenAiEndpoint = ReadFirstJsonString(
                    d,
                    "azureOpenAiEndpoint",
                    "azureOpenAIEndpoint",
                    "openAiEndpoint",
                    "openAIEndpoint");
                azureOpenAiDeployment = ReadFirstJsonString(
                    d,
                    "azureOpenAiDeployment",
                    "azureOpenAIDeployment",
                    "azureOpenAiModel",
                    "azureOpenAIModel",
                    "openAiDeployment",
                    "openAIDeployment");
                azureOpenAiApiVersion = ReadFirstJsonString(
                    d,
                    "azureOpenAiApiVersion",
                    "azureOpenAIApiVersion",
                    "openAiApiVersion",
                    "openAIApiVersion");
                azureSpeechKey = ReadFirstJsonString(
                    d,
                    "azureSpeechKey",
                    "azureSttKey",
                    "azureSpeechApiKey",
                    "azureSpeechSubscriptionKey");
                azureSpeechRegion = ReadFirstJsonString(
                    d,
                    "azureSpeechRegion",
                    "azureSttRegion",
                    "speechRegion");
                azureSpeechEndpoint = ReadFirstJsonString(
                    d,
                    "azureSpeechEndpoint",
                    "azureSttEndpoint",
                    "speechEndpoint");
                azureSpeechLanguage = ReadFirstJsonString(
                    d,
                    "azureSpeechLanguage",
                    "azureSttLanguage",
                    "azureSpeechRecognitionLanguage",
                    "speechRecognitionLanguage");
                azureSpeechVadMs = ReadFirstJsonInt(
                    d,
                    "vad",
                    "azureVad",
                    "azureVadMs",
                    "azureSpeechVad",
                    "azureSpeechVadMs",
                    "azureSttVad",
                    "azureSttVadMs",
                    "speechSegmentationSilenceTimeoutMs",
                    "azureSpeechSegmentationSilenceTimeoutMs");
                proofreadProvider = ReadFirstJsonString(d, "proofreadProvider", "aiProofreadProvider");
                if (string.IsNullOrWhiteSpace(proofreadProvider))
                    proofreadProvider = "qwen";
                sttProvider = ReadFirstJsonString(d, "sttProvider", "speechProvider", "asrProvider");
                if (string.IsNullOrWhiteSpace(sttProvider))
                    sttProvider = "xfyun";
                try
                {
                    if (d.useGoogleDocsQueue != null)
                        useGoogleDocsQueue = (bool)d.useGoogleDocsQueue;
                }
                catch
                {
                    useGoogleDocsQueue = true;
                }

                try
                {
                    if (d.googleDocsIdleFlushSeconds != null)
                        googleDocsIdleFlushSeconds = (int)d.googleDocsIdleFlushSeconds;

                    if (d.googleDocsAppendTimeoutSeconds != null)
                        googleDocsAppendTimeoutSeconds = (int)d.googleDocsAppendTimeoutSeconds;
                }
                catch
                {
                    googleDocsIdleFlushSeconds = 6;
                    googleDocsAppendTimeoutSeconds = 20;
                }
                try
                {
                    if (d.filterChineseFinalText != null)
                        filterChineseFinalText = (bool)d.filterChineseFinalText;
                }
                catch
                {
                    filterChineseFinalText = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load apikey.json");
                throw new InvalidOperationException(
                    "Failed to load apikey.json. Check the JSON syntax and required settings. " +
                    ex.Message,
                    ex);
            }

        }

        private async void startRecord_Click(object sender, EventArgs e)
        {

            try
            {
                //languageGroupBox.Visible = true;

                if (soundIn.RecordingState == RecordingState.Stopped)
                {
                    soundIn.Start();
                    label1.Text = "recordingstate is stopped";
                    //System.Threading.Thread.Sleep(2000);
                }
                if (sttEngine == null)
                    InitializeSttEngine();

                if (sttEngine.State != SttConnectionState.Open)
                {
                    await sttEngine.StartAsync(language);

                    label1.Text = "recordingstate is stopped and speech engine opened";
                }

                //mySqlConnection = new MySqlConnection(connectSqlString);
                //mySqlConnection.Open();

                //gSqlConnection = new MySqlConnection(connectSqlString);
                //gSqlConnection.Open();

                //string query = "TRUNCATE table rawscript";
                //string query2 = "TRUNCATE table finalresult";
                //MySqlCommand adamCommand = new MySqlCommand(query, mySqlConnection);
                //adamCommand.ExecuteNonQuery();
                //adamCommand.Dispose();


                startRecordButton.Enabled = false;
                stopRecordButton.Enabled = true;
                _isRecordingSessionActive = true;
                sttProviderComboBox.Enabled = false;
                timer1.Enabled = true;
                myWatch = System.Diagnostics.Stopwatch.StartNew();
                isGoogleError = false;
                //setgTimer();

            } catch (Exception ex)
            {
                // Log the exception and handle it as needed
                Exception displayException = UnwrapException(ex);
                Log.Error(displayException, "An error occurred during button click");
                // Optionally, display an error message to the user
                MessageBox.Show(
                    GetUserVisibleExceptionMessage(displayException),
                    "Start Recording Error");
            }



        }

        private async void stopRecord_Click(object sender, EventArgs e)
        {
            if (_isStoppingRecordingSession)
                return;

            _isStoppingRecordingSession = true;
            stopRecordButton.Enabled = false;
            labelStatus.Text = "Stopping...";

            try
            {
                if (soundIn.RecordingState == RecordingState.Recording)
                {
                    label1.Invoke(new Action(() => { label1.Text = "recording state is Recording"; }));
                    soundIn.Stop();
                }

                if (sttEngine != null)
                    await sttEngine.StopAsync();

                _isRecordingSessionActive = false;
                labelStatus.Text = "Stopping: flushing docs queue...";

                try
                {
                    await FlushPendingDocsBufferAsync();
                }
                catch (Exception ex)
                {
                    File.AppendAllText("logs/googleErrors.txt",
                        DateTime.Now + "\nStop flush failed:\n" +
                        ex.Message + "\n" + ex.StackTrace + "\n\n");
                }
            }
            catch (Exception ex)
            {
                Exception displayException = UnwrapException(ex);
                Log.Error(displayException, "An error occurred during stop recording");
                MessageBox.Show(
                    GetUserVisibleExceptionMessage(displayException),
                    "Stop Recording Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _isRecordingSessionActive = false;
                _isStoppingRecordingSession = false;
                audioSourceGroupBox.Visible = false;
                stopRecordButton.Enabled = false;
                startRecordButton.Enabled = true;
                sttProviderComboBox.Enabled = true;
                timer1.Enabled = false;
            }
        }


        private void audioSourceButton_Click(object sender, EventArgs e)
        {
            audioSourceGroupBox.Visible = true;
        }

        private void audioFromMicCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void audioFromSysCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void audioFromMicCheckBox_Click(object sender, EventArgs e)
        {
            audioFromSysCheckBox.Checked = false;
            System.Threading.Thread.Sleep(200);
            audioSourceGroupBox.Visible = false;
        }

        private void audioFromSysCheckBox_Click(object sender, EventArgs e)
        {
            audioFromMicCheckBox.Checked = false;
            System.Threading.Thread.Sleep(200);
            audioSourceGroupBox.Visible = false;
        }

        private void Form1_Click(object sender, EventArgs e)
        {
            audioSourceGroupBox.Visible = false;
        }

        private void richTextBox1_Click(object sender, EventArgs e)
        {
            audioSourceGroupBox.Visible = false;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                // Dispose of resources and close connections here
                richTextBox1.Dispose();
                if (sttEngine != null)
                {
                    sttEngine.StopAsync().GetAwaiter().GetResult();
                    sttEngine.Dispose();
                }
                // Dispose of other resources as needed
            }
            finally
            {
                // Close and flush the log
                Log.CloseAndFlush();
            }
            this.Dispose();

        }

        private void label2_Click(object sender, EventArgs e)
        {
            label2.Text = "is audio playing: " + AudioPlayChecker.IsAudioPlaying(AudioPlayChecker.GetDefaultRenderDevice()); 
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (soundIn.RecordingState == RecordingState.Stopped)
            {
                soundIn.Start();
            }

                if (richTextBox1.GetLineFromCharIndex(richTextBox1.TextLength) > 20) richTextBox1.Text = ""; 

            labelTime.Text = String.Format(@"{0:hh\:mm\:ss}", timeSpan);
            label2.Text = "is audio playing: " + AudioPlayChecker.IsAudioPlaying(AudioPlayChecker.GetDefaultRenderDevice());
            label1.Text = sttEngine == null ? "No speech engine" : sttEngine.State.ToString(); 
            timeSpan = timeSpan.Add(new TimeSpan(0, 0, 1));

        }

        private void setgTimer(int ms)
        {
            gTimer = new System.Timers.Timer(ms);
            // Hook up the Elapsed event for the timer. 
            //gTimer.Elapsed += gTimedEventFinalResultSplitWord;
            gTimer.Elapsed += gTimedEventCopyWord; 
            gTimer.AutoReset = true;
            gTimer.Enabled = true;
        }

        private void stopgTimer()
        {
            if (gTimer != null)
            {
                gTimer.Stop();
                gTimer.Dispose();
            }
        }

        private  void gTimedEventCopyWord(object sender, ElapsedEventArgs e)
        {

           
            if (connectToGoogle.isWordCopyIsDone)
            {
                StartWordCopyTask();
                isOutputWordToUI = false; 
            }

            if (connectToGoogle.copiedWordFromDoc != null && isOutputWordToUI !=false)
            {
                //outputWordToUI();
                isOutputWordToUI = true; 


            }

        }        
     
        private void labelTime_Click(object sender, EventArgs e)
        {

        }

        private void realDocsButton_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://docs.google.com/document/d/"+googleDocsID);
        }

 

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void radioButtonJustFinal_CheckedChanged(object sender, EventArgs e)
        {
            isOutputGoogleJustFinalResult = true;
        }

        private void radioButtonWordByWord_CheckedChanged(object sender, EventArgs e)
        {
            isOutputGoogleJustFinalResult = false;
        }

        private async void RestartSttEngineIfRunning()
        {
            if (sttEngine == null || sttEngine.State != SttConnectionState.Open)
                return;

            try
            {
                await sttEngine.StopAsync();
                await sttEngine.StartAsync(language);
            }
            catch (Exception ex)
            {
                File.AppendAllText("logs/stt-errors.txt",
                    DateTime.Now + "\nRestart failed:\n" +
                    ex.Message + "\n" + ex.StackTrace + "\n\n");
                MessageBox.Show(ex.Message, "Speech Engine Restart Error");
            }
        }

        private void cnenRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            language = "cn";
            RestartSttEngineIfRunning();

        }

        private void enRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            language = "en";
            RestartSttEngineIfRunning();
        }

        private void buttonWordCopy_Click(object sender, EventArgs e)
        {
            //int docLength = 0;
            //docLength = await connectToGoogle.wordCopy(docLength);
            buttonWordCopy.Enabled = false;
            buttonStopWordCopy.Enabled = true;
            StartWordCopyTask();
            //outputWordToUI();
            setgTimer(10000);
        }

        private void buttonStopWordCopy_Click(object sender, EventArgs e)
        {
            stopgTimer();
            buttonWordCopy.Enabled = true; 
            buttonStopWordCopy.Enabled = false;
            //outputWordToUI();
            StopWordCopyTask(); 
            
        }
        // Start the word copy task
        private async void StartWordCopyTask()
        {            
           connectToGoogle.isWordCopyShouldStop = false;

            if (!isGoogleError)
            {
                try
                {
                    bool isFilterCN = false; 
                    if (filter_cn != null && filter_cn == "filter_cn_yes")
                    {
                        isFilterCN = true;
                    }
  
                    await connectToGoogle.wordCopyAndDelete(isToStreamText, isFilterCN);
                }
                catch (Google.GoogleApiException gae)
                {
                    if (gae.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        File.AppendAllText("logs/googleErrors.txt", "Bad Request Error in wordCopyAndDelete: " + gae.ToString() + "\n");
                    }
                    else
                    {
                        File.AppendAllText("logs/googleErrors.txt", "Google API Exception in wordCopyAndDelete: " + gae.ToString() + "\n");
                    }

                    // Handle or rethrow the exception as needed
                }

                catch (Exception eGoogle)
                {
                    isGoogleError = true;
                    File.AppendAllText("logs/googleErrors.txt", DateTime.Now.ToString() + "\n"
                        + eGoogle.Message.ToString() + eGoogle.StackTrace + "\n");
                    MessageBox.Show(eGoogle.Message.ToString() + eGoogle.StackTrace);
                }
            }
        }

        private void outputWordToUI (string text)
        {
            //output copied word to UI

            if (InvokeRequired)
            {
                Invoke(new Action<string>(outputWordToUI), text);
            }
            else
            {
                richTextBoxWordCopy.Text = text;
            }

        }

        // Stop the word copy task
        private void StopWordCopyTask()
        {
            // Set the ManualResetEvent
            connectToGoogle.isWordCopyShouldStop = true;
            connectToGoogle.isWordCopyIsDone = true; 
        }

        private void waitForWordCopyTest(int milliseconds)
        {
            var timer1 = new System.Windows.Forms.Timer();
            if (milliseconds == 0 || milliseconds < 0) return;

            // Console.WriteLine("start wait timer");
            timer1.Interval = milliseconds;
            timer1.Enabled = true;
            timer1.Start();

            timer1.Tick += (s, e) =>
            {
                timer1.Enabled = false;
                timer1.Stop();
                // Console.WriteLine("stop wait timer");
            };

            while (timer1.Enabled)
            {
                Application.DoEvents();
            }
        }

        private void buttonToStreamText_Click(object sender, EventArgs e)
        {
            isToStreamText = true;
            buttonToStreamText.Enabled = false;
            buttonStopStreamText.Enabled = true;

        }

       

        private void buttonStopStreamText_Click(object sender, EventArgs e)
        {

            isToStreamText = false;
            buttonToStreamText.Enabled = true;
            buttonStopStreamText.Enabled = false; 
        }

        private void buttonCaption_Click(object sender, EventArgs e)
        {
            // Attempt to retrieve the "FormCaption" instance
            FormCaption formCaption = Application.OpenForms["FormCaption"] as FormCaption;

            if (formCaption != null)
            {
                // If the form exists, show it
                formCaption.Show();
            }
            else
            {
                // If the form doesn't exist, create and show it
                formCaption = new FormCaption();
                formCaption.Show();
            }
        }

        private Exception UnwrapException(Exception ex)
        {
            AggregateException aggregate = ex as AggregateException;

            while (aggregate != null &&
                   aggregate.InnerExceptions.Count == 1 &&
                   aggregate.InnerException != null)
            {
                ex = aggregate.InnerException;
                aggregate = ex as AggregateException;
            }

            return ex;
        }

        private string GetUserVisibleExceptionMessage(Exception ex)
        {
            Exception displayException = UnwrapException(ex);

            if (displayException.InnerException != null)
            {
                return displayException.Message + "\n\n" +
                    displayException.InnerException.Message;
            }

            return displayException.Message;
        }
    }
}
