﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceComparisonMVCAdmin.Models;
using PriceComparisonMVCAdmin.Models.DTOs.Request.Seller;
using PriceComparisonMVCAdmin.Models.Request.Seller;
using PriceComparisonMVCAdmin.Models.Response.Seller;
using PriceComparisonMVCAdmin.Models.Seller;
using PriceComparisonMVCAdmin.Services;
using System.Security.Claims;

namespace PriceComparisonMVCAdmin.Controllers
{
    [Authorize(Policy = "SellerRights")]
    public class SellerController : BaseController
    {
        private readonly IApiService _apiService;

        public SellerController(IApiService apiService) : base(apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            SellerResponseModel? seller = null;

            try
            {
                seller = await _apiService.GetAsync<SellerResponseModel>($"api/Seller/getByUserId/{userId}");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Не вдалося отримати дані продавця";
                //redirect to error page or access deni
            }

            return View(seller);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var sellerResponse = await _apiService.GetAsync<SellerResponseModel>($"api/Seller/{id}");
            if (sellerResponse == null)
            {
                return NotFound();
            }

            var model = new SellerEditViewModel
            {
                Id = sellerResponse.Id,
                StoreName = sellerResponse.StoreName,
                WebsiteUrl = sellerResponse.WebsiteUrl,
                CurrentLogoImageUrl = sellerResponse.LogoImageUrl,
                PublishPriceList = sellerResponse.PublishPriceList
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SellerEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var originalSeller = await _apiService.GetAsync<SellerResponseModel>($"api/Seller/{model.Id}");
            if (originalSeller == null)
            {
                return NotFound();
            }

            var updateModel = new SellerUpdateRequestModel
            {
                Id = originalSeller.Id,
                ApiKey = originalSeller.ApiKey, //dont update
                StoreName = model.StoreName,
                WebsiteUrl = model.WebsiteUrl,
                IsActive = originalSeller.IsActive, //dont update
                AccoundBalance = originalSeller.AccoundBalance, //dont update
                UserId = originalSeller.UserId,
                DeleteCurrentLogoImage = model.NewLogoImage != null, //if new image is uploaded, delete the old one
                NewLogoImage = model.NewLogoImage,
                PublishPriceList = model.PublishPriceList
            };

            try
            {
                var response = await _apiService.SendAsync<SellerUpdateRequestModel, GeneralApiResponseModel>(
                    HttpMethod.Put,
                    "api/Seller/update",
                    updateModel,
                    useMultipartFormData: true
                );
                return RedirectToAction("Settings");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Оновлення не вдалося";
                return View(model);
            }
        }
        public async Task<IActionResult> Settings()
        {
            var userId = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            SellerResponseModel? seller = null;

            try
            {
                seller = await _apiService.GetAsync<SellerResponseModel>($"api/Seller/getByUserId/{userId}");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Не вдалося отримати дані продавця";
                //redirect to error page or access deni
            }

            return View(seller);
        }

        [HttpGet]
        public IActionResult UploadPrice()
        {
            return View(new PriceListViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> UploadPrice(PriceListViewModel model)
        {
            if (model.PriceListFile == null || model.PriceListFile.Length == 0)
            {
                model.IsSuccess = false;
                model.Message = "Будь ласка, виберіть файл для завантаження.";
                return View(model);
            }

            try
            {
                var response = await _apiService.SendAsync<SellerProductXmlRequestModel, GeneralApiResponseModel>(
                    HttpMethod.Post,
                    "api/SellerProductDetails/upload-file",
                    new SellerProductXmlRequestModel { PriceXML = model.PriceListFile },  // Об’єкт з файлом
                    useMultipartFormData: true
                );

                if (response.ReturnCode == "Ok" || response.ReturnCode == "SUCCESS")
                {
                    model.IsSuccess = true;
                    model.Message = "Файл успішно завантажено!";
                }
                else
                {
                    model.IsSuccess = false;
                    model.Message = $"Сталася помилка: {response.Message}";
                }
            }
            catch (Exception ex)
            {
                model.IsSuccess = false;
                model.Message = $"Виникла помилка: {ex.Message}";
            }

            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> Statistics()
        {
            var model = new ProductReferenceStatisticsViewModel
            {
                StartDate = DateTime.Today.AddMonths(-1),
                EndDate = DateTime.Today,
            };

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                var seller = await _apiService.GetAsync<SellerResponseModel>($"api/Seller/getByUserId/{userId}");

                var requestModel = new ProductSellerReferenceClickStaisticRequestModel
                {
                    SellerId = seller.Id,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate
                };

                try
                {
                    var response = await _apiService
                        .PostAsync<ProductSellerReferenceClickStaisticRequestModel, List<ProductSellerReferenceClickResponseModel>>(
                            "/api/ProductReferenceClick/statistic",
                            requestModel);

                    model.Results = response ?? new List<ProductSellerReferenceClickResponseModel>();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Помилка отримання статистики: {ex.Message}");
                }
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Statistics(ProductReferenceStatisticsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                var seller = await _apiService.GetAsync<SellerResponseModel>($"api/Seller/getByUserId/{userId}");

                var requestModel = new ProductSellerReferenceClickStaisticRequestModel
                {
                    SellerId = seller.Id,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate
                };

                try
                {
                    var response = await _apiService
                        .PostAsync<ProductSellerReferenceClickStaisticRequestModel,
                                   List<ProductSellerReferenceClickResponseModel>>(
                            "/api/ProductReferenceClick/statistic",
                            requestModel);

                    model.Results = response ?? new List<ProductSellerReferenceClickResponseModel>();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Помилка отримання статистики: {ex.Message}");
                }
            }
            return View(model);
        }
    }
}
