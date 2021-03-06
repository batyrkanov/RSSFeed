﻿using AutoMapper;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RSSFeed.Service.Interfaces;
using RSSFeed.Service.Models;
using RSSFeed.Web.Controllers.Base;
using RSSFeed.Web.Models;
using RSSFeed.Web.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;
using RSSFeed.Web.Util.ApiClient;

namespace RSSFeed.Web.Controllers
{
    public class HomeController : BaseController
    {
        protected readonly IHubContext<NewsHub> _hubContext;
        public HomeController(IPostService postService, IChannelService channelService, ICategoryService categoryService, IMapper mapper, IHubContext<NewsHub> hubContext)
            : base(postService, channelService, categoryService, mapper) { _hubContext = hubContext; }

        public IActionResult Index(string query)
        {
            ViewBag.SearchQuery = (query ?? "");
            var channels = _channelService.GetChannels();
            ViewBag.Sources = channels.Count() > 0 
                ? new SelectList(channels, "Id", "Title")
                : new SelectList(new List<ChannelModel>());

            RecurringJob.AddOrUpdate(
                () => RunInBackground(),
                Cron.Hourly);
            
            return View();
        }

        // method to getting data to scrolling page
        public async Task<JsonResult> GetData(int pageNumber, string query, string source, int sort, string category)
        {
            var pageSize = 40;
            var postModels = await GetPosts(pageSize, pageNumber, sort, category, source, query);
            
            if (Guid.TryParse(source, out var sourceGuid) && category != "Все категории")
            {
                ViewBag.Sources = new SelectList(_channelService.GetChannels(), "Id", "Title", sourceGuid);
            }
            else
            {
                ViewBag.Sources = new SelectList(_channelService.GetChannels(), "Id", "Title");
            }

            return Json(new { postModels.Data, total = postModels.RecordsTotal, filtered = postModels.RecordsFiltered });
        }
        
        public JsonResult GetCategoriesBySource(Guid sourceId)
        {
            var categories = _categoryService.GetAllCategories(sourceId).ToList();
            categories.Insert(0, (new CategoryModel { Name = "Все категории" }));
            return Json(new SelectList(categories, "Name", "Name"));
        }

        public JsonResult PostSeen(string postId)
        {
            _postService.PostSeen(Guid.Parse(postId));
            return Json(new { data = "success" });
        }

        public async Task SendToApi()
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.BaseAddress = new Uri("https://fin.kg/api/create_article");
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                var serializerSettings = new JsonSerializerSettings();
                serializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                var posts = _postService.GetFinancialPosts().ToList();

                foreach (var post in posts)
                {
                    var postList = new List<PostModel>();
                    postList.Add(post);
                    var jsonData = JsonConvert.SerializeObject(postList, serializerSettings);
                
                    var httpContent = new StringContent(jsonData, Encoding.UTF8, "application/json");
                    await httpClient.PostAsync(httpClient.BaseAddress, httpContent);
                    post.IsNew = false;
                    _postService.SetPostIsNotNewYet(post.Id);
                    Thread.Sleep(1000);
                }
            }
        }
        public async Task RunInBackground()
        {
            // add channels, if not exist
            var channels = GetChannels();

            foreach (var channel in channels)
            {
                _channelService.AddChannel(channel);
            }

            var channelModels = _channelService.GetChannels();
            foreach (var channel in channelModels.ToList())
            {
                var feedItems = await _postService.FeedItems(channel);
                foreach (KeyValuePair<PostModel, CategoryModel> keyValuePair in feedItems)
                {
                    keyValuePair.Key.Title = Regex.Replace(keyValuePair.Key.Title, @"<[^>]*(>|$)|&nbsp;|&zwnj;|&raquo;|&laquo;|&mdash;", " ").Trim();
                    keyValuePair.Key.Body = Regex.Replace(keyValuePair.Key.Body, @"<[^>]*(>|$)|&nbsp;|&zwnj;|&raquo;|&laquo;|&mdash;", " ").Trim();
                    try
                    {
                        //add post
                        _postService.AddPost(keyValuePair.Key);
                        //add category
                        _categoryService.AddCategories(keyValuePair.Value, channel.Id);
                    }
                    catch (DbUpdateException)
                    {
                        continue;
                    }
                }
            }
            await SendToApi();
            await _hubContext.Clients.All.SendAsync("broadcastMessage",
                _postService.GetPosts().Count(x => x.IsNew));
        }
        
        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private IList<ChannelModel> GetChannels()
        {
            return new List<ChannelModel>
            {
                new ChannelModel
                {
                    Title = "K-News",
                    Url = "https://knews.kg/feed/",
                    Image = "https://knews.kg/wp-content/uploads/2016/02/logo.png"
                },
                new ChannelModel
                {
                    Title = "Habr",
                    Url = "http://habrahabr.ru/rss/",
                    Image = "https://hsto.org/storage/stuff/habr/habr_ru.png"
                },
                new ChannelModel
                {
                    Title = "24kg",
                    Url = "https://24.kg/rss/",
                    Image = "https://24.kg/assets/42adfee/images/logo.png"
                },
                //new ChannelModel
                //{
                //    Title = "Sputnik Бишкек",
                //    Url = "https://sputnik.kg/export/rss2/archive/index.xml",
                //    Image = "https://ru.sputnik.kg/i/logo.png"
                //},
                new ChannelModel
                {
                    Title = "Ru Sputnik Бишкек",
                    Url = "https://ru.sputnik.kg/export/rss2/archive/index.xml",
                    Image = "https://ru.sputnik.kg/i/logo.png"
                },
                new ChannelModel
                {
                    Title = "Kaktus Media",
                    Url = "https://kaktus.media/?rss",
                    Image = "https://kaktus.media/lenta4/static/img/logo.png?2"
                },
                new ChannelModel
                {
                    Title = "Kloop Ru",
                    Url = "https://kloop.kg/feed/",
                    Image = "https://kloop.kg/wp-content/uploads/2017/01/kloop_transparent_site.png"
                },
                //new ChannelModel
                //{
                //    Title = "Kloop Kg",
                //    Url = "https://ky.kloop.asia/feed/",
                //    Image = "https://kloop.kg/wp-content/uploads/2017/01/kloop_transparent_site.png"
                //},
                //new ChannelModel
                //{
                //    Title = "Azattyk Экономика",
                //    Url = "https://www.azattyk.org/api/zyooqeqgoo",
                //    Image = "https://www.azattyk.org/Content/responsive/RFE/ky-KG/img/logo-compact.svg"
                //}
            };
        }
    }
}
