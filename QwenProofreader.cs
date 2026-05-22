using System;
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

            string prompt =
@"You are an experienced human conference transcript proofreader.
Your job is to review ASR transcript text conservatively, 
like a professional human proofreader, correcting only clear recognition errors 
while preserving the speaker's original wording and speaking style.

Task:
Correct only likely ASR recognition errors in the current text.

Context:
The prior transcript is for reference only. Do not output it.

Rules:
1. Preserve meaning, wording, and word order as much as possible.
2. Do not rewrite for style or fluency.
3. Do not fix grammar-only issues unless caused by ASR.
4. If uncertain, keep the original text.
5. You may insert line breaks only at strong semantic or sentence boundaries when it improves readability.
6. Do not insert excessive or decorative line breaks.
7. Return only the corrected text, with no explanation.

Prior transcript context:
"
+ contextBlock
+ @"

Current ASR text:
"
+ coreText;

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