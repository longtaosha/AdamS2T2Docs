using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdamS2T2Docs
{
    public class ProofreadResult
    {
        public string CorrectedText { get; set; } = "";
        public double Confidence { get; set; } = 1.0;
        public bool NeedMoreContext { get; set; } = false;
    }
    public class QwenProofreader
    {
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly HttpClient _httpClient;

        private readonly string _promptFile =
            @"prompts/qwen-proofread-prompt.txt";

        private readonly string _contextFile =
            @"prompts/qwen-proofread-context.txt";

        public QwenProofreader(string apiKey, string baseUrl, string model)
        {
            _apiKey = apiKey ?? "";
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _model = string.IsNullOrWhiteSpace(model) ? "qwen-plus" : model;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
        }        
       
        public async Task<ProofreadResult> ProofreadAsync(string text, string context = "")

        {
            if (string.IsNullOrWhiteSpace(text))
                return new ProofreadResult
                {
                    CorrectedText = text,
                    Confidence = 1.0,
                    NeedMoreContext = false
                };
            string leadingBoundary = "";
            string trailingBoundary = "";

            int start = 0;
            while (start < text.Length &&
                   (char.IsWhiteSpace(text[start]) ||
                    text[start] == '.' ||
                    text[start] == ',' ||
                    text[start] == '?' ||
                    text[start] == '!' ||
                    text[start] == ';' ||
                    text[start] == ':'))
            {
                leadingBoundary += text[start];
                start++;
            }

            int end = text.Length - 1;
            while (end >= start &&
                   (char.IsWhiteSpace(text[end]) ||
                    text[end] == '.' ||
                    text[end] == ',' ||
                    text[end] == '?' ||
                    text[end] == '!' ||
                    text[end] == ';' ||
                    text[end] == ':'))
            {
                trailingBoundary = text[end] + trailingBoundary;
                end--;
            }

            string coreText = text.Substring(start, end - start + 1);


            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_baseUrl))
                return new ProofreadResult
                {
                    CorrectedText = text,
                    Confidence = 1.0,
                    NeedMoreContext = false
                };

            string contextBlock = string.IsNullOrWhiteSpace(context)
    ? "No prior context."
    : context;

            string promptTemplate = "";
            string customContext = "";

            try
            {
                if (File.Exists(_promptFile))
                    promptTemplate = File.ReadAllText(_promptFile);

                if (File.Exists(_contextFile))
                    customContext = File.ReadAllText(_contextFile);
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    "logs/qwenPromptLoadErrors.txt",
                    DateTime.Now +
                    "\nPrompt/context load error:\n" +
                    ex.ToString() +
                    "\n\n");
            }

            string prompt =
                promptTemplate +
                "\n\nCustom domain context:\n" +
                customContext +
                "\n\nPrior transcript context:\n" +
                contextBlock +
                "\n\nASR fragment:\n" +
                coreText;
            File.WriteAllText(
    "logs/last-qwen-prompt.txt",
    prompt);

            var body = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0
            };

            string json = JsonConvert.SerializeObject(body);

            using (var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/chat/completions"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await _httpClient.SendAsync(request))
                {
                    string responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                        return new ProofreadResult
                        {
                            CorrectedText = text,
                            Confidence = 0.0,
                            NeedMoreContext = false
                        };

                    JObject obj = JObject.Parse(responseText);

                    string result = obj["choices"]?[0]?["message"]?["content"]?.ToString();

                    if (string.IsNullOrWhiteSpace(result))
                        return new ProofreadResult
                        {
                            CorrectedText = text,
                            Confidence = 0.0,
                            NeedMoreContext = false
                        };

                    result = result.Trim();

                    // 防止模型把 context 也一起输出
                    if (!string.IsNullOrWhiteSpace(context) && !string.IsNullOrWhiteSpace(coreText))
                    {
                        bool tooLong = result.Length > Math.Max(coreText.Length * 3, coreText.Length + 80);

                        string contextTail = context.Length > 50
                            ? context.Substring(context.Length - 50)
                            : context;

                        bool containsContextTail = result.Contains(contextTail);

                        if (tooLong || containsContextTail)
                        {
                            result = coreText;
                        }
                    }

                    return new ProofreadResult
                    {
                        CorrectedText = leadingBoundary + result + trailingBoundary,
                        Confidence = 1.0,
                        NeedMoreContext = false
                    };
                }
            }
        }
    }
}