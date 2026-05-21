using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdamS2T2Docs
{
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

        public async Task<string> ProofreadAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_baseUrl))
                return text;

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
6. Do not reorder words or restructure the fragment unless required to fix an obvious ASR error.
7. Do not replace phrases with more natural alternatives.
8. Do not add missing content that is not clearly implied.
9. Do not remove meaningful words.
10. Preserve leading spaces if they exist.
11. Preserve leading punctuation if it exists.
12. Preserve trailing spaces if they exist.
13. Treat the input as a transcript fragment, not as a complete independent sentence.
14. If uncertain, keep the original wording.
15. Return only the corrected fragment, with no explanation, no labels, and no quotation marks.

ASR fragment:
" + text;

            var body = new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.1
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
                        return text;

                    JObject obj = JObject.Parse(responseText);
                    string result = obj["choices"]?[0]?["message"]?["content"]?.ToString();

                    return string.IsNullOrWhiteSpace(result) ? text : result;
                }
            }
        }
    }
}