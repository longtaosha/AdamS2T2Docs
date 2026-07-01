using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AdamS2T2Docs
{
    public class AzureOpenAiProofreader : IProofreader
    {
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _deployment;
        private readonly string _apiVersion;
        private readonly HttpClient _httpClient;

        private readonly string _promptFile =
            @"prompts/openai-proofread-prompt.txt";

        private readonly string _fallbackPromptFile =
            @"prompts/qwen-proofread-prompt.txt";

        private readonly string _contextFile =
            @"prompts/openai-proofread-context.txt";

        private readonly string _fallbackContextFile =
            @"prompts/qwen-proofread-context.txt";

        public string ProviderName { get { return "Azure OpenAI"; } }

        public AzureOpenAiProofreader(string apiKey, string endpoint, string deployment, string apiVersion)
        {
            _apiKey = apiKey ?? "";
            _endpoint = (endpoint ?? "").Trim();
            _deployment = (deployment ?? "").Trim();
            _apiVersion = string.IsNullOrWhiteSpace(apiVersion) ? "2024-10-21" : apiVersion;

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
        }

        public async Task<ProofreadResult> ProofreadAsync(string text, string context = "")
        {
            if (string.IsNullOrWhiteSpace(text))
                return CreatePassthroughResult(text, 1.0);

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
            if (string.IsNullOrWhiteSpace(coreText))
                return CreatePassthroughResult(text, 1.0);

            string requestUrl = "";
            string validationMessage = "";
            bool isConfigurationValid = !string.IsNullOrWhiteSpace(_apiKey) &&
                TryBuildChatCompletionsUrl(out requestUrl, out validationMessage);

            if (!isConfigurationValid)
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                    validationMessage = "azureOpenAiApiKey is empty.";

                LogError("Configuration skipped: " + validationMessage);
                return CreatePassthroughResult(text, 0.0);
            }

            string contextBlock = string.IsNullOrWhiteSpace(context)
                ? "No prior context."
                : context;

            string promptTemplate = "";
            string customContext = "";

            try
            {
                promptTemplate = ReadFirstExistingFile(_promptFile, _fallbackPromptFile);
                customContext = ReadFirstExistingFile(_contextFile, _fallbackContextFile);
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    "logs/openaiPromptLoadErrors.txt",
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

            File.WriteAllText("logs/last-openai-prompt.txt", prompt);

            var body = new
            {
                messages = new object[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0
            };

            try
            {
                string json = JsonConvert.SerializeObject(body);

                using (var request = new HttpRequestMessage(HttpMethod.Post, requestUrl))
                {
                    request.Headers.Add("api-key", _apiKey);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    using (var response = await _httpClient.SendAsync(request))
                    {
                        string responseText = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            LogError("HTTP " + (int)response.StatusCode + "\n" + responseText);
                            return CreatePassthroughResult(text, 0.0);
                        }

                        JObject obj = JObject.Parse(responseText);
                        string result = obj["choices"]?[0]?["message"]?["content"]?.ToString();

                        if (string.IsNullOrWhiteSpace(result))
                            return CreatePassthroughResult(text, 0.0);

                        result = result.Trim();

                        if (!string.IsNullOrWhiteSpace(context) && !string.IsNullOrWhiteSpace(coreText))
                        {
                            bool tooLong = result.Length > Math.Max(coreText.Length * 3, coreText.Length + 80);

                            string contextTail = context.Length > 50
                                ? context.Substring(context.Length - 50)
                                : context;

                            bool containsContextTail = result.Contains(contextTail);

                            if (tooLong || containsContextTail)
                                result = coreText;
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
            catch (Exception ex)
            {
                LogError("Request failed for " + DescribeRequestUrl(requestUrl) + "\n" + ex);
                return CreatePassthroughResult(text, 0.0);
            }
        }

        private bool TryBuildChatCompletionsUrl(out string requestUrl, out string validationMessage)
        {
            requestUrl = "";
            validationMessage = "";

            if (string.IsNullOrWhiteSpace(_endpoint))
            {
                validationMessage = "azureOpenAiEndpoint is empty.";
                return false;
            }

            string endpoint = _endpoint.Trim();
            if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = "https://" + endpoint;
            }

            Uri endpointUri;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out endpointUri) ||
                string.IsNullOrWhiteSpace(endpointUri.Host))
            {
                validationMessage = "azureOpenAiEndpoint is not a valid absolute URL: " + _endpoint;
                return false;
            }

            string endpointText = endpointUri.ToString();
            if (endpointUri.AbsolutePath.IndexOf("/chat/completions", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                requestUrl = EnsureApiVersion(endpointText);
                return true;
            }

            if (string.IsNullOrWhiteSpace(_deployment))
            {
                validationMessage = "azureOpenAiDeployment is empty.";
                return false;
            }

            string origin = endpointUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            string escapedDeployment = Uri.EscapeDataString(_deployment);
            string escapedApiVersion = Uri.EscapeDataString(_apiVersion);
            requestUrl = origin + "/openai/deployments/" + escapedDeployment +
                         "/chat/completions?api-version=" + escapedApiVersion;
            return true;
        }

        private string EnsureApiVersion(string requestUrl)
        {
            if (requestUrl.IndexOf("api-version=", StringComparison.OrdinalIgnoreCase) >= 0)
                return requestUrl;

            string separator = requestUrl.Contains("?") ? "&" : "?";
            return requestUrl + separator + "api-version=" + Uri.EscapeDataString(_apiVersion);
        }

        private string DescribeRequestUrl(string requestUrl)
        {
            Uri uri;
            if (Uri.TryCreate(requestUrl, UriKind.Absolute, out uri))
                return uri.GetLeftPart(UriPartial.Path);

            return requestUrl;
        }

        private string ReadFirstExistingFile(string primaryFile, string fallbackFile)
        {
            if (File.Exists(primaryFile))
                return File.ReadAllText(primaryFile);

            if (File.Exists(fallbackFile))
                return File.ReadAllText(fallbackFile);

            return "";
        }

        private ProofreadResult CreatePassthroughResult(string text, double confidence)
        {
            return new ProofreadResult
            {
                CorrectedText = text,
                Confidence = confidence,
                NeedMoreContext = false
            };
        }

        private void LogError(string message)
        {
            Directory.CreateDirectory("logs");
            File.AppendAllText(
                "logs/openai-proofread-errors.txt",
                DateTime.Now + "\n" + message + "\n\n");
        }
    }
}
