﻿using DNTPersianUtils.Core;
using GanjooRazor.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Ganjoor;
using RMuseum.Models.Ganjoor.ViewModels;
using RMuseum.Models.GanjoorAudio.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GanjooRazor.Pages
{
    [IgnoreAntiforgeryToken(Order = 1001)]
    public partial class IndexModel : LoginPartialEnabledPageModel
    {
        /// <summary>
        /// configration file reader (appsettings.json)
        /// </summary>
        private readonly IConfiguration Configuration;

        /// <summary>
        /// memory cache
        /// </summary>
        private readonly IMemoryCache _memoryCache;

        // <summary>
        /// aggressive cache
        /// </summary>
        public bool AggressiveCacheEnabled
        {
            get
            {
                try
                {
                    return bool.Parse(Configuration["AggressiveCacheEnabled"]);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="httpClient"></param>
        /// <param name="memoryCache"></param>
        public IndexModel(IConfiguration configuration,
            HttpClient httpClient,
            IMemoryCache memoryCache
            ) : base(httpClient)
        {
            Configuration = configuration;
            _memoryCache = memoryCache;
        }



        /// <summary>
        /// last error
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// banner
        /// </summary>
        public GanjoorSiteBannerViewModel Banner { get; set; }

        

        public _CommentPartialModel GetCommentModel(GanjoorCommentSummaryViewModel comment)
        {
            return new _CommentPartialModel()
            {
                Comment = comment,
                Error = "",
                InReplyTo = null,
                LoggedIn = LoggedIn,
                DivSuffix = ""
            };
        }

        public _AudioPlayerPartialModel GetRecitationsModel(PublicRecitationViewModel[] recitations)
        {
            return new _AudioPlayerPartialModel()
            {
                LoggedIn = LoggedIn,
                Recitations = recitations,
                ShowAllRecitaions = ShowAllRecitaions
            };
        }

        public async Task<ActionResult> OnPostReply(string replyCommentText, int refPoemId, int refCommentId)
        {
            return await OnPostComment(replyCommentText, refPoemId, refCommentId, -1);
        }



        /// <summary>
        /// comment
        /// </summary>
        /// <param name="comment"></param>
        /// <param name="poemId"></param>
        /// <param name="inReplytoId"></param>
        /// <param name="coupletIndex"></param>
        /// <returns></returns>
        public async Task<ActionResult> OnPostComment(string comment, int poemId, int inReplytoId, int coupletIndex)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {

                    var stringContent = new StringContent(
                        JsonConvert.SerializeObject
                        (
                            new GanjoorCommentPostViewModel()
                            {
                                HtmlComment = comment,
                                InReplyToId = inReplytoId == 0 ? null : inReplytoId,
                                PoemId = poemId,
                                CoupletIndex = coupletIndex == -1 ? null : coupletIndex
                            }
                        ),
                        Encoding.UTF8, "application/json");
                    var response = await secureClient.PostAsync($"{APIRoot.Url}/api/ganjoor/comment", stringContent);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        GanjoorCommentSummaryViewModel resComment = JsonConvert.DeserializeObject<GanjoorCommentSummaryViewModel>(await response.Content.ReadAsStringAsync());
                        resComment.MyComment = true;

                        return new PartialViewResult()
                        {
                            ViewName = "_CommentPartial",
                            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                            {
                                Model = new _CommentPartialModel()
                                {
                                    Comment = resComment,
                                    Error = "",
                                    InReplyTo = inReplytoId == 0 ? null : new GanjoorCommentSummaryViewModel(),
                                    LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"])
                                }
                            }
                        };
                    }
                    else
                    {
                        return new PartialViewResult()
                        {
                            ViewName = "_CommentPartial",
                            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                            {
                                Model = new _CommentPartialModel()
                                {
                                    Comment = null,
                                    Error = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()),
                                    InReplyTo = null,
                                    LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"])
                                }
                            }
                        };
                    }
                }
                else
                {
                    return new PartialViewResult()
                    {
                        ViewName = "_CommentPartial",
                        ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                        {
                            Model = new _CommentPartialModel()
                            {
                                Comment = null,
                                Error = "لطفا از گنجور خارج و مجددا به آن وارد شوید.",
                                InReplyTo = null
                            }
                        }
                    };
                }
            }
        }

        /// <summary>
        /// delete my comment
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnDeleteMyComment(int id)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/ganjoor/comment?id={id}");

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                }
                else
                {
                    return new BadRequestObjectResult("لطفا از گنجور خارج و مجددا به آن وارد شوید.");
                }
            }
            return new JsonResult(true);
        }

        /// <summary>
        /// edit my comment
        /// </summary>
        /// <param name="id"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        public async Task<IActionResult> OnPutMyComment(int id, string comment)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.PutAsync($"{APIRoot.Url}/api/ganjoor/comment/{id}", new StringContent(JsonConvert.SerializeObject(comment), Encoding.UTF8, "application/json"));
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                }
                else
                {
                    return new BadRequestObjectResult("لطفا از گنجور خارج و مجددا به آن وارد شوید.");
                }
            }
            return new JsonResult(true);
        }

        /// <summary>
        /// is home page
        /// </summary>
        public bool IsHomePage { get; set; }
        /// <summary>
        /// Poets
        /// </summary>
        public List<GanjoorPoetViewModel> Poets { get; set; }

        /// <summary>
        /// Corresponding Ganojoor Page
        /// </summary>
        public GanjoorPageCompleteViewModel GanjoorPage { get; set; }

        /// <summary>
        /// is poet page
        /// </summary>
        public bool IsPoetPage { get; set; }

        /// <summary>
        /// is category page
        /// </summary>
        public bool IsCatPage { get; set; }

        /// <summary>
        /// is poem page
        /// </summary>
        public bool IsPoemPage { get; set; }

        /// <summary>
        /// canonical url
        /// </summary>
        public string CanonicalUrl
        {
            get
            {
                return $"{Configuration["SiteUrl"]}{GanjoorPage.FullUrl}";
            }
        }

        /// <summary>
        /// can edit
        /// </summary>
        public bool CanEdit { get; set; }

        /// <summary>
        /// keep history
        /// </summary>
        public bool KeepHistory { get; set; }

        /// <summary>
        /// pinterest url
        /// </summary>
        public string PinterestUrl { get; set; }

        /// <summary>
        /// show all recitations
        /// </summary>
        public bool ShowAllRecitaions { get; set; }

        /// <summary>
        /// prepare poem except
        /// </summary>
        /// <param name="poem"></param>
        private void _preparePoemExcerpt(GanjoorPoemSummaryViewModel poem)
        {
            if (poem == null || poem.Excerpt == null)
            {
                return;
            }
            if (poem.Excerpt.Length > 100)
            {
                poem.Excerpt = poem.Excerpt.Substring(0, 50);
                int n = poem.Excerpt.LastIndexOf(' ');
                if (n >= 0)
                {
                    poem.Excerpt = poem.Excerpt.Substring(0, n) + " ...";
                }
                else
                {
                    poem.Excerpt += "...";
                }
            }
        }

        /// <summary>
        /// specify which comments belong to current user
        /// </summary>
        private void _markMyComments()
        {
            if (GanjoorPage == null)
            {
                return;
            }
            if (GanjoorPage.Poem == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(Request.Cookies["UserId"]))
            {
                return;
            }
            if (!Guid.TryParse(Request.Cookies["UserId"], out Guid userId))
            {
                return;
            }
            if (userId == Guid.Empty)
            {
                return;
            }
            foreach (GanjoorCommentSummaryViewModel comment in GanjoorPage.Poem.Comments)
            {
                comment.MyComment = comment.UserId == userId;
                _markMyReplies(comment, userId);
            }
        }

        private void _markMyReplies(GanjoorCommentSummaryViewModel parent, Guid userId)
        {
            foreach (var reply in parent.Replies)
            {
                reply.MyComment = reply.UserId == userId;
                _markMyReplies(reply, userId);
            }
        }

        /// <summary>
        /// get audio description
        /// </summary>
        /// <param name="recitation"></param>
        /// <param name="contributionLink"></param>
        /// <returns></returns>
        public string getAudioDesc(PublicRecitationViewModel recitation, bool contributionLink = false)
        {
            string audiodesc = "به خوانش ";
            if (!string.IsNullOrEmpty(recitation.AudioArtistUrl))
            {
                audiodesc += $"<a href='{recitation.AudioArtistUrl}'>{recitation.AudioArtist}</a>";
            }
            else
            {
                audiodesc += $"{recitation.AudioArtist}";
            }

            if (!string.IsNullOrEmpty(recitation.AudioSrc))
            {
                if (!string.IsNullOrEmpty(recitation.AudioSrcUrl))
                {
                    audiodesc += $" <a href='{recitation.AudioSrcUrl}'>{recitation.AudioSrc}</a>";
                }
                else
                {
                    audiodesc += $" {recitation.AudioSrc}";
                }
            }

            audiodesc += $" <small><a href='/AudioClip/?a={recitation.Id}' onclick='wpopen(this.href); return false' class='comments-link' title='دریافت'>(دریافت)</a></small>";

            if (contributionLink)
            {
                audiodesc += "<br /> <small>می‌خواهید شما بخوانید؟ <a href='http://ava.ganjoor.net/about/'>اینجا</a> را ببینید.</small>";
            }

            return audiodesc;
        }

        private async Task<bool> preparePoets()
        {
            var cacheKey = $"/api/ganjoor/poets";
            if (!_memoryCache.TryGetValue(cacheKey, out List<GanjoorPoetViewModel> poets))
            {
                var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/poets");
                if (!response.IsSuccessStatusCode)
                {
                    LastError = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                    return false;
                }
                poets = JArray.Parse(await response.Content.ReadAsStringAsync()).ToObject<List<GanjoorPoetViewModel>>();
                if (AggressiveCacheEnabled)
                {
                    _memoryCache.Set(cacheKey, poets);
                }
            }

            Poets = poets;
            return true;
        }

        public async Task<IActionResult> OnGetPoetInformationAsync(int id)
        {
            if (id == 0)
                return new OkObjectResult(null);
            var cacheKey = $"/api/ganjoor/poet/{id}";
            if (!_memoryCache.TryGetValue(cacheKey, out GanjoorPoetCompleteViewModel poet))
            {
                var poetResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/poet/{id}");
                if (!poetResponse.IsSuccessStatusCode)
                {
                    return BadRequest(JsonConvert.DeserializeObject<string>(await poetResponse.Content.ReadAsStringAsync()));
                }
                poet = JObject.Parse(await poetResponse.Content.ReadAsStringAsync()).ToObject<GanjoorPoetCompleteViewModel>();
                if(AggressiveCacheEnabled)
                {
                    _memoryCache.Set(cacheKey, poet);
                }
            }
            return new OkObjectResult(poet);
        }

        private async Task<bool> _PreparePoetGroups()
        {
            var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/centuries");
            if (!response.IsSuccessStatusCode)
            {
                LastError = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                return false;
            }
            PoetGroups = JArray.Parse(await response.Content.ReadAsStringAsync()).ToObject<List<GanjoorCenturyViewModel>>();
            return true;
        }

        /// <summary>
        /// valid only for home page
        /// </summary>
        public List<GanjoorCenturyViewModel> PoetGroups { get; set; }

        /// <summary>
        /// html language
        /// </summary>
        public string HtmlLanguage { get; set; } = "fa-IR";

        public string NextUrl { get; set; }

        public string NextTitle { get; set; }

        public string PreviousUrl { get; set; }

        public string PreviousTitle { get; set; }

        private void _prepareNextPre()
        {
            switch(GanjoorPage.GanjoorPageType)
            {
                case GanjoorPageType.PoemPage:
                    {
                        if(GanjoorPage.Poem.Next != null)
                        {
                            NextUrl = GanjoorPage.PoetOrCat.Cat.FullUrl + "/" + GanjoorPage.Poem.Next.UrlSlug;
                            NextTitle = GanjoorPage.Poem.Next.Title + ": " + GanjoorPage.Poem.Next.Excerpt;
                        }
                        else
                        if(GanjoorPage.Poem.MixedModeOrder > 0 
                            && GanjoorPage.Poem.Category.Cat.Children.Where(c => c.MixedModeOrder == 0 || c.MixedModeOrder > GanjoorPage.Poem.MixedModeOrder).Any())
                        {
                            var nextCat = GanjoorPage.Poem.Category.Cat.Children.Where(c => c.MixedModeOrder == 0 || c.MixedModeOrder > GanjoorPage.Poem.MixedModeOrder).OrderBy(c => c.MixedModeOrder).First();
                            NextUrl = nextCat.FullUrl;
                            NextTitle = nextCat.Title;
                        }
                        else
                        if(GanjoorPage.Poem.Category.Cat.Next != null)
                        {
                            NextUrl = GanjoorPage.Poem.Category.Cat.Next.FullUrl;
                            NextTitle = GanjoorPage.Poem.Category.Cat.Next.Title;
                        }

                        if (GanjoorPage.Poem.Previous != null)
                        {
                            PreviousUrl = GanjoorPage.PoetOrCat.Cat.FullUrl + "/" + GanjoorPage.Poem.Previous.UrlSlug;
                            PreviousTitle = GanjoorPage.Poem.Previous.Title + ": " + GanjoorPage.Poem.Previous.Excerpt;
                        }
                        else
                        if (GanjoorPage.Poem.MixedModeOrder > 0
                            && GanjoorPage.Poem.Category.Cat.Children.Where(c => c.MixedModeOrder != 0 && c.MixedModeOrder < GanjoorPage.Poem.MixedModeOrder).Any())
                        {
                            var prevCat = GanjoorPage.Poem.Category.Cat.Children.Where(c => c.MixedModeOrder != 0 && c.MixedModeOrder < GanjoorPage.Poem.MixedModeOrder).OrderByDescending(c => c.MixedModeOrder).First();
                            PreviousUrl = prevCat.FullUrl;
                            PreviousTitle = prevCat.Title;
                        }
                        else
                        if (GanjoorPage.Poem.Category.Cat.Previous != null)
                        {
                            PreviousUrl = GanjoorPage.Poem.Category.Cat.Previous.FullUrl;
                            PreviousTitle = GanjoorPage.Poem.Category.Cat.Previous.Title;
                        }
                    }
                    break;
                case GanjoorPageType.CatPage:
                    {
                        if(GanjoorPage.PoetOrCat.Cat.Next != null)
                        {
                            NextUrl = GanjoorPage.PoetOrCat.Cat.Next.FullUrl;
                            NextTitle = GanjoorPage.PoetOrCat.Cat.Next.Title;
                        }
                        if(GanjoorPage.PoetOrCat.Cat.Previous != null)
                        {
                            PreviousUrl = GanjoorPage.PoetOrCat.Cat.Previous.FullUrl;
                            PreviousTitle = GanjoorPage.PoetOrCat.Cat.Previous.Title;
                        }
                    }
                    break;
            }
        }

        public List<GanjoorPoemSection> SectionsWithRelated { get; set; }

        public List<GanjoorPoemSection> SectionsWithMetreAndRhymes { get; set; }
        private void _prepareRelatedSecions()
        {
            SectionsWithRelated = new List<GanjoorPoemSection>();
            SectionsWithMetreAndRhymes = new List<GanjoorPoemSection>();
            foreach (var section in GanjoorPage.Poem.Sections)
            {
                if(section.Top6RelatedSections.Length > 0)
                {
                    SectionsWithRelated.Add(section);
                }
                if(section.SectionType == PoemSectionType.WholePoem && section.GanjoorMetre != null && !string.IsNullOrEmpty(section.RhymeLetters))
                {
                    SectionsWithMetreAndRhymes.Add(section);
                }
            }
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> OnGetAsync()
        {
            if(bool.Parse(Configuration["MaintenanceMode"]))
            {
                return StatusCode(503);
            }
            LastError = "";
            LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"]);
            CanEdit = Request.Cookies["CanEdit"] == "True";
            KeepHistory = Request.Cookies["KeepHistory"] == "True";
            IsPoetPage = false;
            IsCatPage = false;
            IsPoemPage = false;
            IsHomePage = Request.Path == "/";
            PinterestUrl = Request.Query["pinterest_url"];
            ShowAllRecitaions = Request.Query["allaudio"] == "1";
            ViewData["GoogleAnalyticsCode"] = Configuration["GoogleAnalyticsCode"];
            
            GoogleBreadCrumbList breadCrumbList = new GoogleBreadCrumbList();
            Banner = null;

            if (!string.IsNullOrEmpty(Request.Query["p"]))
            {
                var pageUrlResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/pageurl?id={Request.Query["p"]}");
                if (!pageUrlResponse.IsSuccessStatusCode)
                {
                    LastError = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
                    return Page();
                }
                var pageUrl = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
                return Redirect(pageUrl);
            }

            if (Request.Path.ToString().IndexOf("index.php") != -1)
            {
                return Redirect($"{Request.Path.ToString().Replace("index.php", "search")}{Request.QueryString}");
            }

            await preparePoets();

            if (!IsHomePage)
            {
                var pageQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/page?url={Request.Path}");
                if (!pageQuery.IsSuccessStatusCode)
                {
                    if (pageQuery.StatusCode == HttpStatusCode.NotFound)
                    {
                        var redirectQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/redirecturl?url={Request.Path}");
                        if(redirectQuery.IsSuccessStatusCode)
                        {
                            var redirectUrl = JsonConvert.DeserializeObject<string>(await redirectQuery.Content.ReadAsStringAsync());
                            return Redirect(redirectUrl);
                        }
                        return NotFound();
                    }
                }
                if (!pageQuery.IsSuccessStatusCode)
                {
                    LastError = JsonConvert.DeserializeObject<string>(await pageQuery.Content.ReadAsStringAsync());
                    return Page();
                }
                GanjoorPage = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<GanjoorPageCompleteViewModel>();
                if(!string.IsNullOrEmpty(GanjoorPage.HtmlText))
                {
                    GanjoorPage.HtmlText = GanjoorPage.HtmlText.Replace("https://ganjoor.net/", "/").Replace("http://ganjoor.net/", "/");
                }
                switch (GanjoorPage.GanjoorPageType)
                {
                    case GanjoorPageType.PoemPage:
                        _markMyComments();
                        _preparePoemExcerpt(GanjoorPage.Poem.Next);
                        _preparePoemExcerpt(GanjoorPage.Poem.Previous);
                        GanjoorPage.PoetOrCat = GanjoorPage.Poem.Category;
                        _prepareNextPre();
                        _prepareRelatedSecions();
                        IsPoemPage = true;
                        
                        break;
                    case GanjoorPageType.PoetPage:
                        IsPoetPage = true;
                        break;
                    case GanjoorPageType.CatPage:
                        _prepareNextPre();
                        IsCatPage = true;
                        break;
                }

                if (IsPoemPage)
                {
                    HtmlLanguage = string.IsNullOrEmpty(GanjoorPage.Poem.Language) ? "fa-IR" : GanjoorPage.Poem.Language;
                    var bannerQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/banners/random");
                    if (!bannerQuery.IsSuccessStatusCode)
                    {
                        LastError = JsonConvert.DeserializeObject<string>(await bannerQuery.Content.ReadAsStringAsync());
                        return Page();
                    }
                    string bannerResponse = await bannerQuery.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(bannerResponse))
                    {
                        Banner = JObject.Parse(bannerResponse).ToObject<GanjoorSiteBannerViewModel>();
                    }
                }
            }

            if (IsHomePage)
            {
                ViewData["Title"] = "گنجور";
                await _PreparePoetGroups();
            }
            else
            if (IsPoetPage)
            {
                ViewData["Title"] = $"گنجور » {GanjoorPage.PoetOrCat.Poet.Name}";
                breadCrumbList.AddItem(GanjoorPage.PoetOrCat.Poet.Name, GanjoorPage.PoetOrCat.Cat.FullUrl, $"{APIRoot.InternetUrl + GanjoorPage.PoetOrCat.Poet.ImageUrl}");
            }
            else
            if (IsCatPage)
            {
                string title = $"گنجور » ";
                bool poetCat = true;
                foreach (var gran in GanjoorPage.PoetOrCat.Cat.Ancestors)
                {
                    title += $"{gran.Title} » ";
                    breadCrumbList.AddItem(gran.Title, gran.FullUrl, poetCat ? $"{APIRoot.InternetUrl + GanjoorPage.PoetOrCat.Poet.ImageUrl}" : "https://i.ganjoor.net/cat.png");
                    poetCat = false;
                }
                breadCrumbList.AddItem(GanjoorPage.PoetOrCat.Cat.Title, GanjoorPage.PoetOrCat.Cat.FullUrl, "https://i.ganjoor.net/cat.png");
                title += GanjoorPage.PoetOrCat.Cat.Title;
                ViewData["Title"] = title;
            }
            else
            if (IsPoemPage)
            {
                ViewData["Title"] = $"گنجور » {GanjoorPage.Poem.FullTitle}";
                bool poetCat = true;
                foreach (var gran in GanjoorPage.Poem.Category.Cat.Ancestors)
                {
                    breadCrumbList.AddItem(gran.Title, gran.FullUrl, poetCat ? $"{APIRoot.InternetUrl + GanjoorPage.PoetOrCat.Poet.ImageUrl}" : "https://i.ganjoor.net/cat.png");
                    poetCat = false;
                }
                breadCrumbList.AddItem(GanjoorPage.PoetOrCat.Cat.Title, GanjoorPage.PoetOrCat.Cat.FullUrl, "https://i.ganjoor.net/cat.png");
                breadCrumbList.AddItem(GanjoorPage.Poem.Title, GanjoorPage.Poem.FullUrl, "https://i.ganjoor.net/poem.png");


            }
            else
            {
                if (GanjoorPage.PoetOrCat != null)
                {
                    bool poetCat = true;
                    string fullTitle = "گنجور » ";
                    if (GanjoorPage.PoetOrCat.Cat.Ancestors.Count == 0)
                    {
                        fullTitle += $"{GanjoorPage.PoetOrCat.Poet.Name} » ";
                    }
                    else
                        foreach (var gran in GanjoorPage.PoetOrCat.Cat.Ancestors)
                        {
                            breadCrumbList.AddItem(gran.Title, gran.FullUrl, poetCat ? $"{APIRoot.InternetUrl + GanjoorPage.PoetOrCat.Poet.ImageUrl}" : "https://i.ganjoor.net/cat.png");
                            poetCat = false;
                            fullTitle += $"{gran.Title} » ";
                        }
                    ViewData["Title"] = $"{fullTitle}{GanjoorPage.Title}";
                    breadCrumbList.AddItem(GanjoorPage.PoetOrCat.Poet.Name, GanjoorPage.PoetOrCat.Cat.FullUrl, $"{APIRoot.InternetUrl + GanjoorPage.PoetOrCat.Poet.ImageUrl}");
                }
                else
                {
                    ViewData["Title"] = $"گنجور » {GanjoorPage.FullTitle}";


                    switch (GanjoorPage.UrlSlug)
                    {
                        case "hashieha":
                            await _GenerateHashiehaHtmlText();
                            break;
                        case "vazn":
                            await _GenerateVaznHtmlText();
                            break;
                        case "simi":
                            await _GenerateSimiHtmlText();
                            break;
                    }
                }
                breadCrumbList.AddItem(GanjoorPage.Title, GanjoorPage.FullUrl, "https://i.ganjoor.net/cat.png");
            }


            ViewData["BrearCrumpList"] = breadCrumbList.ToString();


            return Page();
        }


        public async Task<ActionResult> OnGetBNumPartialAsync(int poemId, int coupletIndex)
        {
            var responseCoupletComments = await _httpClient.GetAsync($"{APIRoot.Url}/api/ganjoor/poem/{poemId}/comments?coupletIndex={coupletIndex}");
            if (!responseCoupletComments.IsSuccessStatusCode)
            {
                return BadRequest(JsonConvert.DeserializeObject<string>(await responseCoupletComments.Content.ReadAsStringAsync()));
            }
            var comments = JArray.Parse(await responseCoupletComments.Content.ReadAsStringAsync()).ToObject<List<GanjoorCommentSummaryViewModel>>();

            var responseCoupletNumbers = await _httpClient.GetAsync($"{APIRoot.Url}/api/numberings/couplet/{poemId}/{coupletIndex}");
            if (!responseCoupletNumbers.IsSuccessStatusCode)
            {
                return BadRequest(JsonConvert.DeserializeObject<string>(await responseCoupletNumbers.Content.ReadAsStringAsync()));
            }
            var numbers = JArray.Parse(await responseCoupletNumbers.Content.ReadAsStringAsync()).ToObject<List<GanjoorCoupletNumberViewModel>>();

            bool isBookmarked = false;

            if (!string.IsNullOrEmpty(Request.Cookies["UserId"]))
            {
                if (Guid.TryParse(Request.Cookies["UserId"], out Guid userId))
                    if (userId != Guid.Empty)
                    {
                        foreach (GanjoorCommentSummaryViewModel comment in comments)
                        {
                            comment.MyComment = comment.UserId == userId;
                            _markMyReplies(comment, userId);
                        }

                        using (HttpClient secureClient = new HttpClient())
                        {
                            if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                            {
                                HttpResponseMessage responseIsBookmarked = await secureClient.GetAsync($"{APIRoot.Url}/api/ganjoor/bookmark/{poemId}/{coupletIndex}");
                                if (!responseIsBookmarked.IsSuccessStatusCode)
                                {
                                    return BadRequest(JsonConvert.DeserializeObject<string>(await responseIsBookmarked.Content.ReadAsStringAsync()));
                                }

                                isBookmarked = JsonConvert.DeserializeObject<bool>(await responseIsBookmarked.Content.ReadAsStringAsync());
                            }
                        }
                    }
            }

            return new PartialViewResult()
            {
                ViewName = "_BNumPartial",
                ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                {
                    Model = new _BNumPartialModel()
                    {
                        PoemId = poemId,
                        CoupletIndex = coupletIndex,
                        LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"]),
                        Comments = comments,
                        IsBookmarked = isBookmarked,
                        Numbers = numbers
                    }
                }
            };
        }

        public async Task<IActionResult> OnPostSwitchBookmarkAsync(int poemId, int coupletIndex)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/ganjoor/bookmark/switch/{poemId}/{coupletIndex}", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    var res = JsonConvert.DeserializeObject<bool>(await response.Content.ReadAsStringAsync());
                    return new OkObjectResult(res);
                }
                else
                {
                    return new BadRequestObjectResult("لطفا از گنجور خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<IActionResult> OnGetIsCoupletBookmarkedAsync(int poemId, int coupletIndex)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.GetAsync($"{APIRoot.Url}/api/ganjoor/bookmark/{poemId}/{coupletIndex}");
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    var res = JsonConvert.DeserializeObject<bool>(await response.Content.ReadAsStringAsync());
                    return new OkObjectResult(res);
                }
            }
            return new OkObjectResult(false);
        }

        public async Task<IActionResult> OnGetUserUpvotedRecitationsAsync(int poemId)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.GetAsync($"{APIRoot.Url}/api/ganjoor/poem/{poemId}/recitations/upvotes");
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    var res = JsonConvert.DeserializeObject<int[]>(await response.Content.ReadAsStringAsync());
                    return new OkObjectResult(res);
                }
            }
            return new BadRequestObjectResult("لطفا از گنجور خارج و مجددا به آن وارد شوید.");
        }

        public async Task<ActionResult> OnPostSwitchRecitationUpVoteAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PutAsync(
                        $"{APIRoot.Url}/api/audio/vote/switch/{id}", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    var res = JsonConvert.DeserializeObject<bool>(await response.Content.ReadAsStringAsync());
                    return new OkObjectResult(res);
                }
                else
                {
                    return new BadRequestObjectResult("لطفا از گنجور خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<ActionResult> OnPostAddToMyHistoryAsync(int poemId)
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/tracking", new StringContent(JsonConvert.SerializeObject(poemId), Encoding.UTF8, "application/json"));
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    var res = JsonConvert.DeserializeObject<bool>(await response.Content.ReadAsStringAsync());
                    if(res == false)
                    {
                        if (Request.Cookies["KeepHistory"] != null)
                        {
                            Response.Cookies.Delete("KeepHistory");
                        }
                        var cookieOption = new CookieOptions()
                        {
                            Expires = DateTime.Now.AddDays(365),
                        };
                        Response.Cookies.Append("KeepHistory", $"{false}", cookieOption);
                    }
                    return new OkObjectResult(res);
                }
                else
                {
                    return new BadRequestObjectResult("لطفا از گنجور خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<IActionResult> OnGetCheckIfHasNotificationsAsync()
        {
            using (HttpClient secureClient = new HttpClient())
            {
                if (await GanjoorSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.GetAsync($"{APIRoot.Url}/api/notifications/unread/count");
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    var res = JsonConvert.DeserializeObject<int>(await response.Content.ReadAsStringAsync());
                    if (res == 0)
                        return new OkObjectResult("");
                    return new OkObjectResult(res.ToString().ToPersianNumbers());
                }
            }
            return new BadRequestObjectResult("لطفا از گنجور خارج و مجددا به آن وارد شوید.");
        }
    }
}