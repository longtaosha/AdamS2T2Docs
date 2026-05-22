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
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Documents;
using System.Windows.Forms;
using WebSocket4Net;

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
        private bool isAiProofreadEnabled = true;
        private QwenProofreader qwenProofreader;
        private readonly SemaphoreSlim _aiSemaphore = new SemaphoreSlim(1, 1);
        private string _proofreadContext = "";
        private const int ProofreadContextMaxChars = 300;
        private string _pendingShortFinalText = "";
        private const int ShortFragmentWordThreshold = 5;
        private string _googleDocsProofreadBuffer = "";
        private const int GoogleDocsProofreadMinWords = 12;

        private string _currentBusyStage = "Idle";
        private DateTime _currentBusyStageStart = DateTime.MinValue;
        private readonly object _busyStageLock = new object();
        private readonly LinkedList<string> _pendingGoogleDocsQueue = new LinkedList<string>();
        private readonly object _pendingGoogleDocsQueueLock = new object();
        private bool _isGoogleDocsWorkerRunning = false;

        private string googleDocsIDForFakeTyping;
        private static string uri;
        private bool isWebsocketServing = false;
        private bool isWebsocketOpened = false;
        private XunFeiEncription xunFeiEncription;
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
        private byte[] endCodeBytes = Encoding.ASCII.GetBytes("{\"end\": true}");

        private bool isToStreamText = false; 

        private WebSocket webSocket;
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
                xunFeiEncription = new XunFeiEncription(appid, apiKey);
                connectToGoogle = new ConnectToGoogle(googleDocsID, typeEffect, copyWordgglID);
                //connectToGoogleForFakeTyping = new ConnectToGoogle(googleDocsIDForFakeTyping);               

                qwenProofreader = new QwenProofreader(
                    qwenApiKey,
                    qwenBaseUrl,
                    qwenModel);

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
                    if (queueCount > 0 && !_isGoogleDocsWorkerRunning)
                    {
                        _ = ProcessGoogleDocsQueueAsync();
                    }
                };
                monitorTimer.Start();

            }
            catch (Exception ex)
            { 
                // Log an error message with exception details
                Log.Error(ex, "An error occurred during {OperationName}", "Starting");

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

        // Summary:
        // when the data received from Socekct is byte[], the event will be actived 
        private void OnWebSocketDataReceived(object sender, DataReceivedEventArgs e)
        {
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


            if (webSocket.State == WebSocketState.Open)
            {
                //byte[] buffer = new byte[convertedSource.WaveFormat.BytesPerSecond/2];//=16,000 (Byptes) = 0.5 second, 16 bit depth is 2 Byte depth

                byte[] buffer = new byte[1280];//40ms


                int read;
                //label1.Invoke(new Action(() => { label1.Text = "Sound In Source available" + i; }));
                //i++;
                //if (i > 2000) i = 0;

                while ((read = convertedSource.Read(buffer, 0, buffer.Length)) > 0)
                {

                    if (webSocket.State == WebSocketState.Open)
                    {
                        //webSocket.Send(buffer, 0, read);
                        //byte[] b = new byte[1280];
                        //webSocket.Send(b,0,1280);
                        try
                        {
                            webSocket.Send(buffer, 0, read);
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText("logs/websocket-send-errors.txt",
                                DateTime.Now + "\n" +
                                ex.Message + "\n" +
                                ex.StackTrace + "\n\n");

                            label1.Invoke(new Action(() => { label1.Text = "websocket send error"; }));
                            return;
                        }
                    }
                    else
                    {
                        label1.Invoke(new Action(() => { label1.Text = "socket is closed"; }));
                        return;
                    }

                }
                //webSocket.Send(endCodeBytes, 0, endCodeBytes.Length);

            }
            else return;
        }

        private void OnWebSocketError(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            string message = "You did not enter a server name. Cancel this operation?";
            string caption = "Error Detected in Input";
            MessageBoxButtons buttons = MessageBoxButtons.YesNo;
            DialogResult result;
            result = MessageBox.Show(e.Exception.ToString(), caption, buttons);
        }



        private void OnWebSocketOpened(object sender, EventArgs e)
        {
            isWebsocketServing = true;
            isWebsocketOpened = true;

        }

        private void OnWebSocketClosed(object sender, EventArgs e)
        {
            isWebsocketServing = false;
            isWebsocketOpened = false;


        }

        private async void OnWebSocketMessageReceived(object sender, MessageReceivedEventArgs e)
        {

            try
            {
                dynamic msg = JsonConvert.DeserializeObject(e.Message);
                var dataWithQuotes = msg.data;//ws is now a string with data content but with quotes 
                                              //richTextBox1.Invoke(new Action(() => { richTextBox1.AppendText (e.Message); }));

               

                if (msg.action == "started" || msg.action == "error" && isWebsocketServing == true)
                {
                    //mySystemSoundCapture();
                    richTextBox1.Invoke(new Action(() => { richTextBox1.AppendText(e.Message); }));
                }


                var midData = JsonConvert.SerializeObject(dataWithQuotes); // data now is a string without escape symbols
              
                dynamic dataWithoutQuotes = JsonConvert.DeserializeObject(midData);
                //File.AppendAllText("logs/datafordebug.txt", dataWithoutQuotes + "\n");

                if (dataWithQuotes == null)
                {
                    return;
                }


                if (dataWithoutQuotes != null)
                {
                    XfyunData deserializeXfyunData = JsonConvert.DeserializeObject<XfyunData>(dataWithoutQuotes);
                    if (deserializeXfyunData == null)
                    {
                        richTextBox1.Invoke(new Action(() => { richTextBox1.AppendText(" deserializeXfyunData is empty" + "\n"); }));
                        return;
                    }
                    else
                    {
                        ResultAnalysis resultAnalysis = new ResultAnalysis();
                        string[] result = resultAnalysis.returnResult(deserializeXfyunData);


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
                            int wordCount = CountWordsForProofread(finalText);
                            // 短 fragment：先暂存，等待下一段
                            /* *****
                            if (wordCount > 0 &&
                                wordCount < ShortFragmentWordThreshold &&
                                string.IsNullOrWhiteSpace(_pendingShortFinalText))
                            {
                                _pendingShortFinalText = finalText;
                                return;
                            }
                            *******/
                            // pending short
                            /*
                            if (!string.IsNullOrWhiteSpace(_pendingShortFinalText))
                            {
                                string pendingText = _pendingShortFinalText;
                                _pendingShortFinalText = "";

                                string lookaheadContext = _proofreadContext + pendingText + finalText;

                                File.AppendAllText("logs/qwen-proofread.txt",
                                    DateTime.Now + "\nRAW_PENDING: " + pendingText + "\nLOOKAHEAD: " + finalText + "\n");

                                ProofreadResult pendingProof = null;
                                if (isAiProofreadEnabled && qwenProofreader != null)
                                {
                                    await _aiSemaphore.WaitAsync();
                                    try
                                    {                                        
                                        pendingProof = await qwenProofreader.ProofreadAsync(pendingText, lookaheadContext);
                                        pendingText = pendingProof.CorrectedText;
                                    }
                                    finally
                                    {
                                        _aiSemaphore.Release();
                                    }
                                }

                                File.AppendAllText("logs/qwen-proofread.txt",
                                "AI_PENDING : " + pendingText + "\n" +
                                "CONF_PENDING: " + (pendingProof != null ? pendingProof.Confidence.ToString() : "N/A") + "\n" +
                                "NEED_MORE_PENDING: " + (pendingProof != null ? pendingProof.NeedMoreContext.ToString() : "N/A") + "\n\n");

                                richTextBox1?.Invoke(new Action(() =>
                                {
                                    richTextBox1.SelectedText = pendingText;
                                    richTextBox1.DeselectAll();
                                }));

                                _proofreadContext += pendingText;

                                if (_proofreadContext.Length > ProofreadContextMaxChars)
                                {
                                    _proofreadContext = _proofreadContext.Substring(
                                        _proofreadContext.Length - ProofreadContextMaxChars);
                                }

                                if (!isGoogleError && isOutputGoogleJustFinalResult)
                                {
                                    await connectToGoogle.connectFinalResultAsync(0, pendingText);
                                }
                            }
                            // end of pending short
                            */

                            File.AppendAllText("logs/qwen-proofread.txt", DateTime.Now + "\nRAW: " + finalText + "\n");

                            ProofreadResult proof = null;

                            if (isAiProofreadEnabled && qwenProofreader != null)
                            {
                                await _aiSemaphore.WaitAsync();
                                try
                                {
                                    proof = await qwenProofreader.ProofreadAsync(
                                        finalText,
                                        _proofreadContext);

                                    finalText = proof.CorrectedText;
                                }
                                finally
                                {
                                    _aiSemaphore.Release();
                                }
                            }


                            File.AppendAllText("logs/qwen-proofread.txt", "AI : " + finalText + "\n\n");

                            richTextBox1?.Invoke(new Action(() =>
                            {
                                //richTextBox1.SelectedText = "";
                                //richTextBox1.AppendText(result[1]);
                                //richTextBox1.SelectedText = result[4]+": "+result[1];//testing speaker indentification
                                richTextBox1.SelectedText = finalText;
                                richTextBox1.DeselectAll();
                            }));

                            //File.AppendAllText("logs/resultFromXunfei.txt", "seg_id: "+result[2]+" type = " + result[0] + ": " + "is1st= " + isToNewLine +": "+result[1]+"\n\n");
                            //File.AppendAllText("logs/finalResultCNandEN.txt", result[1]+"\n");
                            //File.AppendAllText("logs/finalResultCNandEN.txt", dataWithoutQuotes + "\n");
                            //string line = result[1].Replace("'", "\\'");
                            //string query = "INSERT INTO `rawscript` (`seg_id`, `type`, `line`, `newlinemark`) VALUES (" +
                            //result[2] + ", " + 0 + ", '" + line + "' ," + isToNewLine+ " );";
                            //string query2 = "INSERT INTO `finalresult` (`line`) VALUES (" + "'" + line + "'" + " );";
                            //if (mySqlConnection.State == System.Data.ConnectionState.Closed) mySqlConnection.Open();
                            //MySqlCommand adamCommand = new MySqlCommand(query, mySqlConnection);
                            //adamCommand.ExecuteNonQuery();
                            isToNewLineForGoogle = isToNewLine;
                            isToNewLine = true;

                            _proofreadContext += finalText;

                            if (_proofreadContext.Length > ProofreadContextMaxChars)
                            {
                                _proofreadContext = _proofreadContext.Substring(
                                    _proofreadContext.Length - ProofreadContextMaxChars);
                            }


                            /*if (!isGoogleError)
                            {
                                if (isOutputGoogleJustFinalResult)
                                {
                                    await connectToGoogle.connectFinalResultAsync(0, finalText);
                                }
                                else
                                {
                                    try
                                    {
                                        await connectToGoogle.connectOnlyWaitFinalAsync(0, finalText, isToNewLineForGoogle);
                                    }
                                    catch (Exception eGoogle)
                                    {
                                        isGoogleError = true;
                                        File.AppendAllText("logs/googleErrors.txt", DateTime.Now.ToString() + "\n"
                                            + eGoogle.Message.ToString() + eGoogle.StackTrace + "\n");
                                        MessageBox.Show(eGoogle.Message.ToString() + eGoogle.StackTrace);

                                    }
                                }
                            }*/

                            _googleDocsProofreadBuffer += finalText;

                            int docsBufferWordCount = CountWordsForProofread(_googleDocsProofreadBuffer);

                            if (docsBufferWordCount >= GoogleDocsProofreadMinWords)
                            {
                                string docsRawText = _googleDocsProofreadBuffer;
                                string docsText = docsRawText;
                                _googleDocsProofreadBuffer = "";

                                lock (_pendingGoogleDocsQueueLock)
                                {
                                    _pendingGoogleDocsQueue.AddLast(docsRawText);

                                    File.AppendAllText(
                                        "logs/googleQueue.txt",
                                        DateTime.Now +
                                        " ENQUEUE len=" + docsRawText.Length +
                                        " queue=" + _pendingGoogleDocsQueue.Count + "\n");
                                }

                                _ = ProcessGoogleDocsQueueAsync();
                                /*
// Temporarily disabled:
// AI proofreading + Google Docs writing will be moved to a background worker.

                            ProofreadResult docsProof = null;

                            if (isAiProofreadEnabled && qwenProofreader != null)
                            {
                                await _aiSemaphore.WaitAsync();
                                try
                                {
                                    try
                                    {
                                        docsProof = await qwenProofreader.ProofreadAsync(
                                            docsText,
                                            _proofreadContext);
                                    }
                                    finally
                                    {
                                        ClearBusyStage("Qwen Proofread");
                                    }
                                }
                                finally
                                {
                                    _aiSemaphore.Release();
                                }

                                docsText = docsProof.CorrectedText;
                            }

                            File.AppendAllText("logs/qwen-proofread-docs-buffer.txt",
                            DateTime.Now + "\n" +
                            "DOCS_RAW: " + docsRawText + "\n" +
                            "DOCS_AI : " + docsText + "\n\n");

                            if (!isGoogleError)
                            {
                                try
                                {
                                    SetBusyStage("Google Docs Append");
                                    try
                                    {
                                        await connectToGoogle.connectFinalResultAsync(0, docsText);
                                    }
                                    finally
                                    {
                                        ClearBusyStage("Google Docs Append");
                                    }
                                }
                                catch (TaskCanceledException ex)
                                {
                                    _googleDocsProofreadBuffer = docsText + _googleDocsProofreadBuffer;

                                    File.AppendAllText("logs/googleErrors.txt",
                                        DateTime.Now + "\nGoogle Docs timeout/canceled, requeued docsText:\n" +
                                        docsText + "\n" +
                                        ex.Message + "\n" + ex.StackTrace + "\n\n");
                                }
                                catch (Exception ex)
                                {
                                    //isGoogleError = true;

                                    File.AppendAllText("logs/googleErrors.txt",
                                        DateTime.Now + "\nGoogle Docs error:\n" +
                                        ex.Message + "\n" + ex.StackTrace + "\n\n");
                                }
                            }
                                */
                            }

                            //mySqlConnection.Close();



                            //await connectToGoogle.connectNewAsync(0, result[1]);
                            //await connectToGoogle.connectReplaceTextAsync(0, result[1], result[2]);
                            //connectToGoogle.connectFinalResultAsync(0, result[1]);
                            //await connectToGoogle.connectAsync(0, result[1]);
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
            }
            catch (Exception ex)
            {
                // Log the exception and handle it as needed
                Log.Error(ex, "An error occurred during websocket message receiving");              
                // Optionally, display an error message to the user
                MessageBox.Show("An error occurred during websocket message receiving. Please contact support.");
            }
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

                    if (isAiProofreadEnabled && qwenProofreader != null)
                    {
                        await _aiSemaphore.WaitAsync();
                        try
                        {
                            SetBusyStage("Qwen Proofread");
                            try
                            {
                                docsProof = await qwenProofreader.ProofreadAsync(
                                    docsText,
                                    _proofreadContext);
                            }
                            finally
                            {
                                ClearBusyStage("Qwen Proofread");
                            }
                        }
                        finally
                        {
                            _aiSemaphore.Release();
                        }

                        docsText = docsProof.CorrectedText;
                    }

                    File.AppendAllText("logs/qwen-proofread-docs-buffer.txt",
                        DateTime.Now + "\n" +
                        "DOCS_RAW: " + docsRawText + "\n" +
                        "DOCS_AI : " + docsText + "\n\n");

                    try
                    {
                        SetBusyStage("Google Docs Append");
                        try
                        {
                            Task googleTask = connectToGoogle.connectFinalResultAsync(0, docsText);
                            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

                            Task completedTask = await Task.WhenAny(googleTask, timeoutTask);

                            if (completedTask == timeoutTask)
                            {
                                throw new TimeoutException("Google Docs append timed out after 5 seconds.");
                            }

                            await googleTask;
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

            }
            catch (Exception e)
            {
                //
                
            }

        }

        private void startRecord_Click(object sender, EventArgs e)
        {

            try
            {

                uri = xunFeiEncription.getUri();
                if (language == "cn" && uri != null)
                { 
                    uri = uri.Replace("lang=en", "lang=cn");  
                }

                if (language == "ko" && uri != null)
                {
                    uri = uri.Replace("lang=en", "lang=ko");
                }

                if (language == "es" && uri != null)
                {
                    uri = uri.Replace("lang=en", "lang=es");
                }

                //webSocket = new WebSocket(uri);
                // Explicitly set the security protocol to TLS 1.2
                // to avoid error "System.Security.Authentication.AuthenticationException: A Call to SSPI failed, see inner exception" after Microsoft
                // updated its .NET framework
                webSocket = new WebSocket(uri, sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls);

                webSocket.Opened += OnWebSocketOpened;
                webSocket.DataReceived += OnWebSocketDataReceived;
                webSocket.MessageReceived += OnWebSocketMessageReceived;
                webSocket.Closed += OnWebSocketClosed;
                webSocket.Error += OnWebSocketError;

                //languageGroupBox.Visible = true;

                if (soundIn.RecordingState == RecordingState.Stopped)
                {
                    soundIn.Start();
                    label1.Invoke(new Action(() => { label1.Text = "recordingstate is stopped"; }));
                    //System.Threading.Thread.Sleep(2000);
                }
                if (webSocket.State != WebSocketState.Open)
                {
                    Task taskA = Task.Run(() => webSocket.Open());
                    taskA.Wait();

                    label1.Invoke(new Action(() => { label1.Text = "recordingstate is stopped and websocket opened"; }));
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
                isWebsocketServing = true;
                timer1.Enabled = true;
                myWatch = System.Diagnostics.Stopwatch.StartNew();
                isGoogleError = false;
                //setgTimer();

            } catch (Exception ex)
            {
                // Log the exception and handle it as needed
                Log.Error(ex, "An error occurred during button click");
                // Optionally, display an error message to the user
                MessageBox.Show("An error occurred. Please contact support.");
            }



        }

        private void stopRecord_Click(object sender, EventArgs e)
        {
           
            if (soundIn.RecordingState == RecordingState.Recording)
            {
                label1.Invoke(new Action(() => { label1.Text = "recording state is Recording"; }));
                soundIn.Stop(); 
            }

            if (webSocket.State == WebSocketState.Open && webSocket.Handshaked)
            {
               Task taskA = Task.Run(() => webSocket.Close());
                Task taskB = Task.Run(() => webSocket.Dispose());
               taskA.Wait();
                taskB.Wait();
            }
                
            isWebsocketServing = false;  
            
            audioSourceGroupBox.Visible = false;
            stopRecordButton.Enabled = false;
            startRecordButton.Enabled = true;
            timer1.Enabled = false;
            //stopTimer();


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
                if (webSocket != null)
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        webSocket.Send(endCodeBytes, 0, endCodeBytes.Length);
                        webSocket.Close();
                        webSocket.Dispose();
                    }
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
            if (webSocket.State == WebSocketState.Closed)
            {
   
            }

            if (soundIn.RecordingState == RecordingState.Stopped)
            {
                soundIn.Start();
            }

                if (richTextBox1.GetLineFromCharIndex(richTextBox1.TextLength) > 20) richTextBox1.Text = ""; 

            labelTime.Text = String.Format(@"{0:hh\:mm\:ss}", timeSpan);
            label2.Text = "is audio playing: " + AudioPlayChecker.IsAudioPlaying(AudioPlayChecker.GetDefaultRenderDevice());
            label1.Text = webSocket.State.ToString(); 
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

        
     /*   private async void gTimedEvent(object sender, ElapsedEventArgs e)
        {
            if (isPushedToGoogle == false)
            {
                isPushedToGoogle = true;
                string query = "SELECT * FROM `rawscript` WHERE `seg_id` = " + segidForTimer;
                if (gSqlConnection.State == System.Data.ConnectionState.Closed) gSqlConnection.Open();
                MySqlCommand gCommand = new MySqlCommand(query, gSqlConnection);
                gCommand.ExecuteNonQuery();
                MySqlDataReader gReader = gCommand.ExecuteReader();
                int seg_id = 0;
                int type = 0;
                string line = "";
                bool isToNewLine = true;
                if (gReader.Read())
                {
                    seg_id = gReader.GetInt16("seg_id");
                    type = gReader.GetInt16("type");
                    line = gReader.GetString("line");
                    isToNewLine = gReader.GetBoolean("newlinemark");
                    readFromMysqlSucceeded = true;
                }
                else
                {
                    readFromMysqlSucceeded = false;
                    isPushedToGoogle = false;
                    gReader.Close();
                    gCommand.Dispose();
                    return;
                }

                gReader.Close();
                gCommand.Dispose();

                if (readFromMysqlSucceeded && ((segidForTimer - lastSegid == 1) || segidForTimer == 0))
                {
                    lastSegid = segidForTimer;
                    if (isToNewLine == true)
                    {
                        await connectToGoogle.connectTimerAsync(seg_id, isToNewLine, type, "  --");
                        segidForTimer++;
                        isPushedToGoogle = false;
                        return;
                    }
                    else
                    {
                        switch (type)
                        {
                            case 1:
                                {
                                    if (isToWrite == true)
                                    {
                                        await connectToGoogle.connectTimerAsync(seg_id, isToNewLine, type, line);
                                        File.AppendAllText("logs/callFrom-gTimer.txt", segidForTimer + ".vs." + seg_id + ": " + isToNewLine + " type=" + type + ": " + line + "\n");
                                        segidForTimer++;
                                        isPushedToGoogle = false;
                                    }
                                }
                                break;
                            case 0:
                                {

                                    await connectToGoogle.connectTimerAsync(seg_id, isToNewLine, type, line);
                                    File.AppendAllText("logs/callFrom-gTimer.txt", segidForTimer + ".vs." + seg_id + ": " + isToNewLine + " type=" + type + ": " + line + "\n");
                                    if (await connectToGoogle.checkNamedRangeDeleteUpdateAsync())
                                    {
                                        isToWrite = true;
                                        segidForTimer++;
                                        isPushedToGoogle = false;
                                    }
                                    else
                                    {
                                        isToWrite = false;
                                        isPushedToGoogle = false;
                                        return;
                                    }
                                }
                                break;
                        }
                    }

                }
            }
            else return;



            /*if (line.Length > 0)
            {
                if (isToWrite == true)
                {
                    File.AppendAllText("logs/gTimerRead.txt", seg_id + " - " + type + " : " + line + "\n");
                    isToWrite = false;
                    await connectToGoogle.connectTimerAsync(seg_id, type, line);
                }

                if (segidForTimer == 0)
                {
                    if (await connectToGoogle.checkUpdate("  --" + line, (int)myWatch.ElapsedMilliseconds))
                    {
                        segidForTimer++;
                        isToWrite = true;
                        File.AppendAllText("logs/callForCheckFrom-gTimer.txt", "  --" + "\n");
                    }

                }
                else
                {
                    query = "SELECT * FROM `rawscript` WHERE `seg_id` = " + (segidForTimer - 1);
                    if (gSqlConnection.State == System.Data.ConnectionState.Closed) gSqlConnection.Open();
                    gCommand = new MySqlCommand(query, gSqlConnection);
                    gCommand.ExecuteNonQuery();
                    gReader = gCommand.ExecuteReader();
                    if (gReader.Read())
                    {
                        line = gReader.GetString("line");
                    }
                    gReader.Close();
                    gCommand.Dispose();

                    if (await connectToGoogle.checkUpdate(line, (int)myWatch.ElapsedMilliseconds))
                    {
                        segidForTimer++;
                        isToWrite = true;
                        File.AppendAllText("logs/callForCheckFrom-gTimer.txt", line + "\n");
                    }
                    else
                    {
                        File.AppendAllText("logs/callForCheckFrom-gTimerfailed.txt", line + "\n");
                    }

                }
            }
        }
    */

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

        private void cnenRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (webSocket != null)
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    Task taskA = Task.Run(()=> webSocket.Send(endCodeBytes, 0, endCodeBytes.Length));
                    Task taskB = Task.Run(()=> webSocket.Close());
                    Task taskC = Task.Run(()=> webSocket.Dispose());
                    taskA.Wait();
                    taskB.Wait();
                    taskC.Wait();
                    uri = xunFeiEncription.getUri().Replace("lang=en", "lang=cn");
                    webSocket = new WebSocket(uri);
                    webSocket.Opened += OnWebSocketOpened;
                    webSocket.DataReceived += OnWebSocketDataReceived;
                    webSocket.MessageReceived += OnWebSocketMessageReceived;
                    webSocket.Closed += OnWebSocketClosed;
                    webSocket.Error += OnWebSocketError;
                    taskA = Task.Run(() => webSocket.Open());
                    taskA.Wait();
                }
            }

            

        }

        private void enRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (webSocket != null)
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    Task taskA = Task.Run(() => webSocket.Send(endCodeBytes, 0, endCodeBytes.Length));
                    Task taskB = Task.Run(() => webSocket.Close());
                    Task taskC = Task.Run(() => webSocket.Dispose());
                    taskA.Wait();
                    taskB.Wait();
                    taskC.Wait();
                    uri = xunFeiEncription.getUri();
                    webSocket = new WebSocket(uri);
                    webSocket.Opened += OnWebSocketOpened;
                    webSocket.DataReceived += OnWebSocketDataReceived;
                    webSocket.MessageReceived += OnWebSocketMessageReceived;
                    webSocket.Closed += OnWebSocketClosed;
                    webSocket.Error += OnWebSocketError;
                    taskA = Task.Run(() => webSocket.Open());
                    taskA.Wait();
                }
            }

            
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
    }
}
