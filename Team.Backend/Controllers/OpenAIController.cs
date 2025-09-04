using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Team.Backend.Models.ViewModels;

namespace Team.Backend.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class OpenAIController : ControllerBase
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IConfiguration _configuration;

		public OpenAIController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
		{
			_httpClientFactory = httpClientFactory;
			_configuration = configuration;
		}

		// Generic proxy endpoint: POST api/OpenAI/chat
		[HttpPost("chat")]
		public async Task<IActionResult> Chat([FromBody] object payload)
		{
			var apiKey = _configuration["OpenAI:ApiKey"];
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				return StatusCode(500, new { success = false, error = "OpenAI API key not configured on server." });
			}

			var client = _httpClientFactory.CreateClient();
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

			var contentString = payload?.ToString() ?? "{}";
			var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
			{
				Content = new StringContent(contentString, Encoding.UTF8, "application/json")
			};

			try
			{
				var resp = await client.SendAsync(request);
				var text = await resp.Content.ReadAsStringAsync();
				return new ContentResult
				{
					StatusCode = (int)resp.StatusCode,
					Content = text,
					ContentType = "application/json"
				};
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { success = false, error = ex.Message });
			}
		}

		// POST: api/OpenAI/CustomerService
		[HttpPost("CustomerService")]
		[HttpPost("~/OpenAI/CustomerService")]
		public async Task<IActionResult> CustomerService([FromBody] ContextualChatRequest request)
		{
			try
			{
				var httpClient = _httpClientFactory.CreateClient();

				var apiKey = _configuration["OpenAI:ApiKey"];
				if (string.IsNullOrEmpty(apiKey))
				{
					return BadRequest(new { success = false, error = "OpenAI API Key 未設定" });
				}

				var apiUrl = "https://api.openai.com/v1/chat/completions";
				var systemMessage = new
				{
					role = "system",
					content = @"你是一位專業的線上客服專員小玉，工作在一個電商平台的後台管理系統。請用親切、專業的態度回答用戶問題。

回答原則：
1. 語調親切自然，使用繁體中文
2. 針對訂單、商品、配送、退換貨等電商相關問題提供專業建議
3. 如果遇到技術問題，表示會為用戶轉接技術支援
4. 保持耐心和同理心
5. 回答簡潔明瞭，避免過於冗長
6. 主動詢問是否還需要其他協助

請記住，你代表的是一個專業的電商平台客服團隊。"
				};

				var messages = new List<object> { systemMessage };
				messages.AddRange(request.Messages.Select(m => new { role = m.Role.ToLower(), content = m.Content }));

				var data = new
				{
					model = "gpt-3.5-turbo",
					messages = messages.ToArray(),
					max_tokens = 500,
					temperature = 0.7
				};

				var jsonContent = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
				httpClient.DefaultRequestHeaders.Accept.Clear();
				httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

				var response = await httpClient.PostAsync(apiUrl, jsonContent);
				if (response.IsSuccessStatusCode)
				{
					var responseContent = await response.Content.ReadAsStringAsync();
					var result = JsonConvert.DeserializeObject<CompletionViewModel>(responseContent);
					if (result?.Choices?.Count > 0)
					{
						return Ok(new { success = true, response = result.Choices[0].Message.Content });
					}
					else
					{
						return Ok(new { success = false, error = "API 回應格式異常" });
					}
				}
				else
				{
					var errorContent = await response.Content.ReadAsStringAsync();
					return StatusCode((int)response.StatusCode, new { success = false, error = $"API 錯誤: {response.StatusCode}, {errorContent}" });
				}
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { success = false, error = ex.Message });
			}
		}

		// POST: api/OpenAI/SendMessage
		[HttpPost("SendMessage")]
		public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
		{
			try
			{
				var httpClient = _httpClientFactory.CreateClient();

				var apiKey = _configuration["OpenAI:ApiKey"];
				if (string.IsNullOrEmpty(apiKey))
				{
					return BadRequest(new { success = false, error = "OpenAI API Key 未設定" });
				}

				var apiUrl = "https://api.openai.com/v1/chat/completions";
				var data = new
				{
					model = "gpt-3.5-turbo",
					messages = new[]
					{
						new { role = "user", content = request.Message }
					},
					max_tokens = 2048,
					temperature = 0.7
				};

				var jsonContent = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
				httpClient.DefaultRequestHeaders.Accept.Clear();
				httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

				var response = await httpClient.PostAsync(apiUrl, jsonContent);
				if (response.IsSuccessStatusCode)
				{
					var responseContent = await response.Content.ReadAsStringAsync();
					var result = JsonConvert.DeserializeObject<CompletionViewModel>(responseContent);
					if (result?.Choices?.Count > 0)
					{
						return Ok(new { success = true, reply = result.Choices[0].Message.Content });
					}
					else
					{
						return Ok(new { success = false, error = "API 回應格式異常" });
					}
				}
				else
				{
					var errorContent = await response.Content.ReadAsStringAsync();
					return StatusCode((int)response.StatusCode, new { success = false, error = $"API 錯誤: {response.StatusCode}, {errorContent}" });
				}
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { success = false, error = ex.Message });
			}
		}

		// POST: api/OpenAI/SendContextualMessage
		[HttpPost("SendContextualMessage")]
		public async Task<IActionResult> SendContextualMessage([FromBody] ContextualChatRequest request)
		{
			try
			{
				var httpClient = _httpClientFactory.CreateClient();
				var apiKey = _configuration["OpenAI:ApiKey"];
				if (string.IsNullOrEmpty(apiKey))
				{
					return BadRequest(new { success = false, error = "OpenAI API Key 未設定" });
				}

				var apiUrl = "https://api.openai.com/v1/chat/completions";
				var data = new
				{
					model = "gpt-3.5-turbo",
					messages = request.Messages.Select(m => new { role = m.Role.ToLower(), content = m.Content }).ToArray(),
					max_tokens = 1000,
					temperature = 0.7
				};

				var jsonContent = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
				httpClient.DefaultRequestHeaders.Accept.Clear();
				httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

				var response = await httpClient.PostAsync(apiUrl, jsonContent);
				if (response.IsSuccessStatusCode)
				{
					var responseContent = await response.Content.ReadAsStringAsync();
					var result = JsonConvert.DeserializeObject<CompletionViewModel>(responseContent);
					if (result?.Choices?.Count > 0)
					{
						return Ok(new { success = true, response = result.Choices[0].Message.Content });
					}
					else
					{
						return Ok(new { success = false, error = "API 回應格式異常" });
					}
				}
				else
				{
					var errorContent = await response.Content.ReadAsStringAsync();
					return StatusCode((int)response.StatusCode, new { success = false, error = $"API 錯誤: {response.StatusCode}, {errorContent}" });
				}
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { success = false, error = ex.Message });
			}
		}

		// 保留原有的 CallApi 方法作為範例
		[HttpGet("CallApi")]
		public async Task<IActionResult> CallApi()
		{
			var httpClient = _httpClientFactory.CreateClient();
			var apiKey = _configuration["OpenAI:ApiKey"] ?? "請在 appsettings.json 中設定 OpenAI:ApiKey";
			var apiUrl = "https://api.openai.com/v1/chat/completions";

			var data = new
			{
				model = "gpt-3.5-turbo",
				messages = new[]
				{
					new { role = "user", content = "為什麼歐美舉例程式變數名稱時, 喜歡用foo , bar來命名？能說說是什麼典故嗎？" }
				}
			};

			var jsonContent = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
			httpClient.DefaultRequestHeaders.Accept.Clear();
			httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

			var response = await httpClient.PostAsync(apiUrl, jsonContent);
			if (response.IsSuccessStatusCode)
			{
				var responseContent = await response.Content.ReadAsStringAsync();
				var result = JsonConvert.DeserializeObject<CompletionViewModel>(responseContent);
				return Ok(result);
			}
			else
			{
				return StatusCode((int)response.StatusCode);
			}
		}
	}

	public class ChatRequest
	{
		public string Message { get; set; } = string.Empty;
	}

	public class ContextualChatRequest
	{
		public List<ChatMessage> Messages { get; set; } = new();
	}

	public class ChatMessage
	{
		public string Role { get; set; } = string.Empty;
		public string Content { get; set; } = string.Empty;
	}
}
