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
            @"You are an ASR proofreading engine for live conference transcripts.

Your task is to correct likely ASR recognition errors in ONE finalized speech fragment.

Important context:
- The input is often only a fragment of a longer sentence.
- The input may begin with a space, comma, period, question mark, or other boundary character.
- The fragment will be appended to previous transcript text, so boundary characters and spacing are important.

Correction priorities:
- Misheard technical terms
- Company names, product names, speaker names
- Acronyms
- Numbers, units, percentages
- Obvious malformed phrases caused by ASR
- Punctuation and capitalization only when clearly needed

Strict rules:
1. Preserve the original meaning.
2. Make the minimum necessary correction.
3. Preserve the original wording and word order whenever possible.
4. Do not paraphrase.
5. Do not rewrite for fluency or style.
6. Do not fix grammar-only issues. Only correct words that are likely misrecognized by ASR.
7. Do not reorder words or restructure the fragment unless required to fix an obvious ASR error.
8. Do not replace phrases with more natural alternatives.
9. Do not add missing content that is not clearly implied.
10. Do not remove meaningful words.
11. Preserve leading spaces if they exist.
12. Preserve leading punctuation if it exists.
13. Preserve trailing spaces if they exist.
14. Treat the input as a transcript fragment, not as a complete independent sentence.
15. If uncertain, keep the original wording.
16. Return only the corrected fragment, with no explanation, no labels, and no quotation marks.
17. Prior transcript context is reference material only.
18. Never repeat, summarize, or output the prior transcript context.
19. Correct ONLY the ASR fragment.
20. The output must contain only the corrected ASR fragment and nothing else.

Prior transcript context:
"
            + contextBlock
            + @"

ASR fragment:
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

                    // Safety: 防止模型把 context 也一起输出
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