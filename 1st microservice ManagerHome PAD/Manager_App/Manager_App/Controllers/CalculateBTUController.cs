﻿using Microsoft.AspNetCore.Mvc;
using Manager_App.Models;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using Manager_App.Services;

namespace Manager_App.Controllers
{
    public class CalculateBTUController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ConditionerService _conditionerService; // Сервис для работы с кондиционерами

        public CalculateBTUController(IHttpClientFactory httpClientFactory, ConditionerService conditionerService)
        {
            _httpClientFactory = httpClientFactory;
            _conditionerService = conditionerService; // Инициализируем сервис
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Создаем начальный ViewModel для отображения формы
            var viewModel = new BTUCalculatorViewModel
            {
                RequestModel = new BTURequestModel(),
                ConditionerModels = new List<ConditionerModel>()
            };

            // Возвращаем представление BTUCalculator с ViewModel
            return View("BTUCalculator", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> CalculateBtuValue([FromBody] BTURequestModel requestModel)
        {
            // Проверяем валидность модели
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState); // Возвращаем ошибки валидации
            }

            // Создаем HTTP-клиент
            var client = _httpClientFactory.CreateClient();
            var url = "https://localhost:7182/api/BTUCalculator"; // URL вашего микросервиса

            // Сериализуем модель в JSON
            var jsonContent = JsonConvert.SerializeObject(requestModel);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                // Отправляем POST-запрос на микросервис
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    // Десериализуем ответ в модель BTUResponseModel
                    var responseString = await response.Content.ReadAsStringAsync();
                    var responseModel = JsonConvert.DeserializeObject<BTUResponseModel>(responseString);

                    var result = new
                    {
                        CalculatedPowerBTU = responseModel.CalculatedPowerBTU,
                        RecommendedRangeBTU = new
                        {
                            Lower = responseModel.RecommendedRangeBTU.Lower,
                            Upper = responseModel.RecommendedRangeBTU.Upper
                        },
                        CalculatedPowerKW = responseModel.CalculatedPowerKW,
                        RecommendedRangeKW = new
                        {
                            Lower = responseModel.RecommendedRangeKW.Lower,
                            Upper = responseModel.RecommendedRangeKW.Upper
                        }
                    };

                    return Json(result); // Возвращаем результат в формате JSON
                }
                else
                {
                    return StatusCode((int)response.StatusCode, "Ошибка при расчете BTU. Пожалуйста, попробуйте снова.");
                }
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(500, "Ошибка при отправке запроса. Пожалуйста, проверьте соединение и попробуйте снова.");
            }
        }



        [HttpPost]
        public async Task<IActionResult> UpdateConditioners()
        {
            var client = _httpClientFactory.CreateClient();
            var url = "http://localhost:5000/api/scrape"; // URL Python микросервиса

            try
            {
                // Получаем текущую хэш-сумму из базы данных
                var currentHash = await _conditionerService.GetConditionerHashAsync();
                // Отправляем запрос на парсинг данных
                var response = await client.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    // Получаем новый хэш-сумму и список кондиционеров
                    var responseString = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);
                    var newHash = data["hash"].ToString(); // Предположим, что хэш-сумма приходит в поле "hash"
                    var conditioners = JsonConvert.DeserializeObject<List<ConditionerModel>>(data["conditioners"].ToString());

                    // Проверка на совпадение хэш-сумм
                    if (currentHash == newHash)
                    {
                        return Ok("No updates needed. Data is up to date.");
                    }

                    // Сохраняем кондиционеров в БД
                    foreach (var conditioner in conditioners)
                    {
                        conditioner.Hash = newHash; // Сохраняем новую хэш-сумму
                        await _conditionerService.AddConditionerAsync(conditioner);
                    }

                    return Ok("Conditioners updated successfully.");
                }
                else
                {
                    return StatusCode((int)response.StatusCode, "Failed to update conditioners.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error.");
            }
        }

    }
}