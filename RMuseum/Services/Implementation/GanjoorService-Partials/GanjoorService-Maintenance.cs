﻿using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Ganjoor;
using RMuseum.Services.Implementation.ImportedFromDesktopGanjoor;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Models.Generic.Db;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IGanjoorService implementation
    /// </summary>
    public partial class GanjoorService : IGanjoorService
    {
        private async Task _FindCategoryPoemsRhythmsInternal(int catId, bool retag, string rhythm)
        {
            using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
            {
                LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                var job = (await jobProgressServiceEF.NewJob($"FindCategoryPoemsRhythms Cat {catId}", "Query data")).Result;
                try
                {
                    var metres = await context.GanjoorMetres.OrderBy(m => m.Rhythm).AsNoTracking().ToArrayAsync();
                    var rhythms = metres.Select(m => m.Rhythm).ToArray();

                    GanjoorMetre preDeterminedMetre = string.IsNullOrEmpty(rhythm) ? null : metres.Where(m => m.Rhythm == rhythm).Single();

                    var poems = await context.GanjoorPoems.Where(p => p.CatId == catId).ToListAsync();

                    int i = 0;
                    using (HttpClient httpClient = new HttpClient())
                    {
                        foreach (var poem in poems)
                        {
                            if (retag || poem.GanjoorMetreId == null)
                            {
                                await jobProgressServiceEF.UpdateJob(job.Id, i++);
                                if (preDeterminedMetre == null)
                                {
                                    var res = await _FindPoemRhythm(poem.Id, context, httpClient, rhythms);
                                    if (!string.IsNullOrEmpty(res.Result))
                                    {
                                        poem.GanjoorMetreId = metres.Where(m => m.Rhythm == res.Result).Single().Id;
                                        context.GanjoorPoems.Update(poem);
                                        await context.SaveChangesAsync();
                                    }
                                }
                                else
                                {
                                    poem.GanjoorMetreId = preDeterminedMetre.Id;
                                    context.GanjoorPoems.Update(poem);
                                    await context.SaveChangesAsync();
                                }
                            }
                        }
                    }
                    await jobProgressServiceEF.UpdateJob(job.Id, 99);

                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                }
                catch (Exception exp)
                {
                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                }
            }
        }

        /// <summary>
        /// find category poem rhymes
        /// </summary>
        /// <param name="catId"></param>
        /// <param name="retag"></param>
        /// <param name="rhythm"></param>
        /// <returns></returns>
        public RServiceResult<bool> FindCategoryPoemsRhythms(int catId, bool retag, string rhythm = "")
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
                        (
                        async token =>
                        {
                            await _FindCategoryPoemsRhythmsInternal(catId, retag, rhythm);
                        });

            return new RServiceResult<bool>(true);
        }

        private async Task _GeneratingSubCatsTOC(Guid userId, RMuseumDbContext context, LongRunningJobProgressServiceEF jobProgressServiceEF, RLongRunningJobStatus job, int catId)
        {
            foreach (var cat in await context.GanjoorCategories.AsNoTracking().Where(c => c.ParentId == catId).ToListAsync())
            {
                await jobProgressServiceEF.UpdateJob(job.Id, cat.Id);
                var page = await context.GanjoorPages.Where(p => p.FullUrl == cat.FullUrl).SingleAsync();

                context.GanjoorPageSnapshots.Add
                           (
                           new GanjoorPageSnapshot()
                           {
                               GanjoorPageId = page.Id,
                               MadeObsoleteByUserId = userId,
                               HtmlText = page.HtmlText,
                               Note = "تولید گروهی فهرستهای زیربخشها",
                               RecordDate = DateTime.Now
                           }
                           );

                page.HtmlText = (await _GenerateTableOfContents(context, cat.Id, GanjoorTOC.TitlesAndFirstVerse)).Result;
                context.GanjoorPages.Update(page);
                await context.SaveChangesAsync();

                await _GeneratingSubCatsTOC(userId, context, jobProgressServiceEF, job, cat.Id);
            }
        }

        /// <summary>
        /// start generating sub cats TOC
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="catId"></param>
        /// <returns></returns>
        public RServiceResult<bool> StartGeneratingSubCatsTOC(Guid userId, int catId)
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
                        (
                        async token =>
                        {
                            using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                            {
                                LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                var job = (await jobProgressServiceEF.NewJob($"GeneratingSubCatsTOC {catId}", "Query data")).Result;
                                try
                                {
                                    await _GeneratingSubCatsTOC(userId, context, jobProgressServiceEF, job, catId);
                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                }
                                catch (Exception exp)
                                {
                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                }

                            }
                        });

            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// generate category TOC
        /// </summary>
        /// <param name="catId"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task<RServiceResult<string>> GenerateTableOfContents(int catId, GanjoorTOC options)
        {
            return await _GenerateTableOfContents(_context, catId, options);
        }


        private async Task<RServiceResult<string>> _GenerateTableOfContents(RMuseumDbContext context, int catId, GanjoorTOC options)
        {
            string html = "";
            var subCats = await context.GanjoorCategories.Where(c => c.ParentId == catId).OrderBy(c => c.Id).AsNoTracking().ToArrayAsync();

            foreach (var cat in subCats)
            {
                html += $"<p><a href=\"{cat.FullUrl}\">{cat.Title}</a></p>{Environment.NewLine}";
            }

            var poems = await context.GanjoorPoems.Where(p => p.CatId == catId).OrderBy(p => p.Id).AsNoTracking().ToArrayAsync();

            if (poems.Length > 0)
            {
                if (options == GanjoorTOC.Analyse)
                {
                    if (poems.Where(p => !string.IsNullOrEmpty(p.RhymeLetters)).Count() * 100 / poems.Length > 50)
                    {
                        options = GanjoorTOC.AlphabeticWithFirstVerse;
                    }
                    else
                    {
                        options = GanjoorTOC.TitlesAndFirstVerse;
                    }
                }
            }

            if
                (
                options == GanjoorTOC.AlphabeticWithFirstCouplet
                ||
                options == GanjoorTOC.AlphabeticWithFirstVerse
                ||
                options == GanjoorTOC.AlphabeticWithSecondVerse
                )
            {
                var taggedPoems = poems.Where(p => !string.IsNullOrEmpty(p.RhymeLetters)).ToArray();
                if (taggedPoems.Length > 0)
                {
                    html += $"<p>فهرست شعرها به ترتیب آخر حرف قافیه گردآوری شده است. برای پیدا کردن یک شعر کافی است حرف آخر قافیهٔ آن را در نظر بگیرید تا بتوانید آن  را پیدا کنید.</p>{Environment.NewLine}";
                    var randomPoem = taggedPoems[new Random(DateTime.Now.Millisecond).Next(taggedPoems.Length)];
                    var randomPoemVerses = await context.GanjoorVerses.AsNoTracking().Where(p => p.PoemId == randomPoem.Id).OrderBy(v => v.VOrder).ToArrayAsync();
                    if (randomPoemVerses.Length > 2)
                    {
                        html += $"<p>مثلاً برای پیدا کردن شعری که مصرع <em>{randomPoemVerses[1].Text}</em> مصرع دوم یکی از بیتهای آن است باید شعرهایی را نگاه کنید که آخر حرف قافیهٔ آنها «<em><a href=\"#{ GPersianTextSync.UniquelyFarglisize(randomPoem.RhymeLetters.Substring(randomPoem.RhymeLetters.Length - 1)) }\">{randomPoem.RhymeLetters.Substring(randomPoem.RhymeLetters.Length - 1)}</a></em>» است.</p>{Environment.NewLine}";
                    }

                    html += $"<h3><a id=\"index\">دسترسی سریع به حروف</a></h3>{Environment.NewLine}";
                    html += $"<p>{Environment.NewLine}";
                    string lastChar = "";
                    List<string> visitedLastChart = new List<string>();
                    foreach (var poem in taggedPoems)
                    {
                        string poemLastChar = poem.RhymeLetters.Substring(poem.RhymeLetters.Length - 1);
                        if (poemLastChar != lastChar)
                        {
                            if (visitedLastChart.IndexOf(poemLastChar) == -1)
                            {
                                if (lastChar != "")
                                {
                                    html += " | ";
                                }
                                html += $"<a href=\"#{GPersianTextSync.UniquelyFarglisize(poemLastChar)}\">{poemLastChar}</a>";
                                lastChar = poemLastChar;

                                visitedLastChart.Add(poemLastChar);
                            }
                        }
                    }
                    html += $"</p>{Environment.NewLine}";
                }
            }

            string last = "";
            List<string> visitedLast = new List<string>();
            foreach (var poem in poems)
            {
                if
                (
                options == GanjoorTOC.AlphabeticWithFirstCouplet
                ||
                options == GanjoorTOC.AlphabeticWithFirstVerse
                ||
                options == GanjoorTOC.AlphabeticWithSecondVerse
                )
                {

                    if (!string.IsNullOrEmpty(poem.RhymeLetters))
                    {
                        string poemLast = poem.RhymeLetters.Substring(poem.RhymeLetters.Length - 1);
                        if (poemLast != last)
                        {
                            if (visitedLast.IndexOf(poemLast) == -1)
                            {
                                html += $"<h3><a href=\"#index\" id=\"{GPersianTextSync.UniquelyFarglisize(poemLast)}\">{poemLast}</a></h3>{Environment.NewLine}";
                                last = poemLast;
                                visitedLast.Add(poemLast);
                            }
                        }
                    }
                }

                html += $"<p><a href=\"{poem.FullUrl}\">{poem.Title}</a>";

                var verses = await context.GanjoorVerses.AsNoTracking().Where(p => p.PoemId == poem.Id).OrderBy(v => v.VOrder).ToArrayAsync();

                if (verses.Length > 0)
                {
                    if (options == GanjoorTOC.TitlesAndFirstVerse || options == GanjoorTOC.AlphabeticWithFirstVerse)
                    {
                        html += $": {verses[0].Text}";
                    }
                    else
                    if (options == GanjoorTOC.AlphabeticWithSecondVerse || options == GanjoorTOC.TitlesAndSecondVerse)
                    {
                        if (verses.Length > 1)
                        {
                            html += $": {verses[1].Text}";
                        }
                        else
                        {
                            html += $": {verses[0].Text}";
                        }
                    }
                    else
                    if (options == GanjoorTOC.AlphabeticWithFirstCouplet || options == GanjoorTOC.TitlesAndFirstCouplet)
                    {
                        if (verses.Length > 1)
                        {
                            html += $": {verses[0].Text} - {verses[1].Text}";
                        }
                        else
                        {
                            html += $": {verses[0].Text}";
                        }
                    }
                    else
                    if (options == GanjoorTOC.TitlesAndFirstCenteredVerse)
                    {
                        if (verses.Where(v => v.VersePosition == VersePosition.CenteredVerse1).Any())
                        {
                            html += $": {verses.Where(v => v.VersePosition == VersePosition.CenteredVerse1).First().Text}";
                        }
                        else
                        {
                            html += $": {verses[0].Text}";
                        }
                    }
                    else
                    if (options == GanjoorTOC.TitlesAndFirstCenteredCouplet)
                    {
                        if (
                            verses.Where(v => v.VersePosition == VersePosition.CenteredVerse1).Any()
                            &&
                            verses.Where(v => v.VersePosition == VersePosition.CenteredVerse2).Any()
                            )
                        {
                            html += $": {verses.Where(v => v.VersePosition == VersePosition.CenteredVerse1).First().Text} - {verses.Where(v => v.VersePosition == VersePosition.CenteredVerse2).First().Text}";
                        }
                        else
                        {
                            html += $": {verses[0].Text}";
                        }
                    }
                }


                html += $"</p>{Environment.NewLine}";
            }
            return new RServiceResult<string>(html);
        }



        /// <summary>
        /// make plain text
        /// </summary>
        /// <param name="verses"></param>
        /// <returns></returns>
        private static string PreparePlainText(List<GanjoorVerse> verses)
        {
            string plainText = "";
            foreach (GanjoorVerse verse in verses)
            {
                plainText += $"{LanguageUtils.MakeTextSearchable(verse.Text)}{Environment.NewLine}";//replace zwnj with space
            }
            return plainText.Trim();
        }

        /// <summary>
        /// separate verses in poem.PlainText with  Environment.NewLine instead of SPACE
        /// </summary>
        /// <returns></returns>
        public RServiceResult<bool> RegerneratePoemsPlainText()
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
            (
            async token =>
            {
                using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                {
                    LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                    var job = (await jobProgressServiceEF.NewJob("RegerneratePoemsPlainText", "Query data")).Result;

                    try
                    {
                        var poems = await context.GanjoorPoems.ToArrayAsync();

                        await jobProgressServiceEF.UpdateJob(job.Id, 0, $"Updating PlainText/Poem Html");

                        int percent = 0;
                        for (int i = 0; i < poems.Length; i++)
                        {
                            if (i * 100 / poems.Length > percent)
                            {
                                percent++;
                                await jobProgressServiceEF.UpdateJob(job.Id, percent);
                            }

                            var poem = poems[i];

                            var verses = await context.GanjoorVerses.AsNoTracking().Where(v => v.PoemId == poem.Id).OrderBy(v => v.VOrder).ToListAsync();

                            poem.PlainText = PreparePlainText(verses);
                            poem.HtmlText = PrepareHtmlText(verses);
                        }

                        await jobProgressServiceEF.UpdateJob(job.Id, 0, $"Finalizing PlainText/Poem Html");

                        context.GanjoorPoems.UpdateRange(poems);

                        await context.SaveChangesAsync();

                        await jobProgressServiceEF.UpdateJob(job.Id, 50, $"Updating pages HTML");

                        await context.Database.ExecuteSqlRawAsync("UPDATE p SET p.HtmlText = (SELECT poem.HtmlText FROM GanjoorPoems poem WHERE poem.Id = p.Id) FROM GanjoorPages p WHERE p.GanjoorPageType = 3 ");

                        await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                    }
                    catch (Exception exp)
                    {
                        await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                    }

                }
            }
            );

            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// examine site pages for broken links
        /// </summary>
        /// <returns></returns>
        public RServiceResult<bool> HealthCheckContents()
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
            (
            async token =>
            {
                using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                {
                    LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                    var job = (await jobProgressServiceEF.NewJob("HealthCheckContents", "Query data")).Result;

                    try
                    {
                        var pages = await context.GanjoorPages.ToArrayAsync();

                        await jobProgressServiceEF.UpdateJob(job.Id, 0, $"Examining Pages");

                        var previousErrors = await context.GanjoorHealthCheckErrors.ToArrayAsync();
                        context.RemoveRange(previousErrors);
                        await context.SaveChangesAsync();
                        int percent = 0;
                        for (int i = 0; i < pages.Length; i++)
                        {
                            if (i * 100 / pages.Length > percent)
                            {
                                percent++;
                                await jobProgressServiceEF.UpdateJob(job.Id, percent);
                            }

                            var hrefs = pages[i].HtmlText.Split(new[] { "href=\"" }, StringSplitOptions.RemoveEmptyEntries).Where(o => o.StartsWith("http")).Select(o => o.Substring(0, o.IndexOf("\"")));

                            foreach (string url in hrefs)
                            {
                                if (url == "https://ganjoor.net" || url == "https://ganjoor.net/" || url.IndexOf("https://ganjoor.net/vazn/?") == 0 || url.IndexOf("https://ganjoor.net/simi/?v") == 0)
                                    continue;
                                if (url.IndexOf("http://ganjoor.net") == 0)
                                {
                                    context.GanjoorHealthCheckErrors.Add
                                    (
                                        new GanjoorHealthCheckError()
                                        {
                                            ReferrerPageUrl = pages[i].FullUrl,
                                            TargetUrl = url,
                                            BrokenLink = false,
                                            MulipleTargets = false
                                        }
                                     );

                                    await context.SaveChangesAsync();
                                }
                                else
                                if (url.IndexOf("https://ganjoor.net") == 0)
                                {
                                    var testUrl = url.Substring("https://ganjoor.net".Length);
                                    if (testUrl[testUrl.Length - 1] == '/')
                                        testUrl = testUrl.Substring(0, testUrl.Length - 1);
                                    var pageCount = await context.GanjoorPages.Where(p => p.FullUrl == testUrl).CountAsync();
                                    if (pageCount != 1)
                                    {
                                        context.GanjoorHealthCheckErrors.Add
                                     (
                                         new GanjoorHealthCheckError()
                                         {
                                             ReferrerPageUrl = pages[i].FullUrl,
                                             TargetUrl = url,
                                             BrokenLink = pageCount == 0,
                                             MulipleTargets = pageCount != 0
                                         }
                                      );

                                        await context.SaveChangesAsync();
                                    }
                                }
                            }
                        }

                        await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                    }
                    catch (Exception exp)
                    {
                        await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                    }

                }
            }
            );

            return new RServiceResult<bool>(true);
        }

        private async Task<string> _ProcessCommentHtmlLinks(string commentText, RMuseumDbContext context)
        {
            if(commentText.IndexOf("href=") == -1 && commentText.IndexOf("http") != -1)
            {
                commentText = _Linkify(commentText);
            }
            int index = commentText.IndexOf("href=");
            while (index != -1)
            {
                index += "href=\"".Length;
                commentText = commentText.Replace("'", "\"");
                if (commentText.IndexOf("\"", index) != -1)
                {
                    int closeIndex = commentText.IndexOf("\"", index);
                    if (closeIndex == -1)
                    {
                        continue;
                    }
                    string url = commentText.Substring(index, closeIndex - index);
                    closeIndex = commentText.IndexOf(">", index);
                    if (closeIndex != -1 && commentText.IndexOf("</a>", closeIndex) != -1)
                    {
                        closeIndex += ">".Length;
                        string urlText = commentText.Substring(closeIndex, commentText.IndexOf("</a>", closeIndex) - closeIndex);
                        if (urlText == url)
                        {
                            bool textFixed = false;
                            if (urlText.IndexOf("http://ganjoor.net") == 0 || urlText.IndexOf("https://ganjoor.net") == 0)
                            {
                                urlText = urlText.Replace("http://ganjoor.net", "").Replace("https://ganjoor.net", "");
                                int coupletNumber = -1;
                                if(urlText.IndexOf("#bn") != -1)
                                {
                                    int coupletStartIndex = urlText.IndexOf("#bn") + "#bn".Length;
                                    if(int.TryParse(urlText.Substring(coupletStartIndex), out coupletNumber))
                                    {
                                        urlText = urlText.Substring(0, urlText.IndexOf("#bn"));
                                    }
                                }
                                if (urlText.Length > 0 && urlText[urlText.Length - 1] == '/')
                                    urlText = urlText.Substring(0, urlText.Length - 1);
                                var page = await context.GanjoorPages.AsNoTracking().Where(p => p.FullUrl == urlText).FirstOrDefaultAsync();
                                if (page != null)
                                {
                                    if(coupletNumber != -1)
                                    {
                                        string coupletSummary = "";
                                        int coupletIndex = coupletNumber - 1;
                                        var verses = await _context.GanjoorVerses.Where(v => v.PoemId == page.Id).OrderBy(v => v.VOrder).ToListAsync();
                                        int cIndex = -1;
                                        for (int i = 0; i < verses.Count; i++)
                                        {
                                            if (verses[i].VersePosition != VersePosition.Left && verses[i].VersePosition != VersePosition.CenteredVerse2)
                                                cIndex++;
                                            if (cIndex == coupletIndex)
                                            {
                                                coupletSummary = verses[i].Text;
                                                if (verses[i].VersePosition == VersePosition.Right)
                                                {
                                                    if (i < verses.Count - 1)
                                                    {
                                                        coupletSummary += $" {verses[i + 1].Text}";
                                                    }
                                                }
                                                if (verses[i].VersePosition == VersePosition.CenteredVerse1)
                                                {
                                                    if (i < verses.Count - 1)
                                                    {
                                                        if (verses[i + 1].VersePosition == VersePosition.CenteredVerse2)
                                                        {
                                                            coupletSummary += $" {verses[i + 1].Text}";
                                                        }
                                                    }
                                                }
                                                break;
                                            }
                                        }
                                        if (!string.IsNullOrEmpty(coupletSummary))
                                        {
                                            coupletSummary = _CutSummary(coupletSummary);
                                            commentText = commentText.Substring(0, closeIndex) + page.FullTitle + " » " + coupletSummary + commentText.Substring(commentText.IndexOf("</a>", closeIndex));
                                            textFixed = true;
                                        }
                                        else
                                            coupletNumber = -1;
                                    }
                                    if(coupletNumber == -1)
                                    {
                                        commentText = commentText.Substring(0, closeIndex) + page.FullTitle + commentText.Substring(commentText.IndexOf("</a>", closeIndex));
                                        textFixed = true;
                                    }
                                }
                                
                            }
                            if (!textFixed)
                                commentText = commentText.Substring(0, closeIndex) + "پیوند به وبگاه بیرونی" + commentText.Substring(commentText.IndexOf("</a>", closeIndex));
                        }
                    }
                }
                index = commentText.IndexOf("href=\"", index);
            }
            return commentText;
        }

        /// <summary>
        /// examine comments for long links
        /// </summary>
        /// <returns></returns>
        public RServiceResult<bool> FindAndFixLongUrlsInComments()
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
            (
            async token =>
            {
                using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                {
                    LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                    var job = (await jobProgressServiceEF.NewJob("FindAndFixLongUrlsInComments", "Query data")).Result;

                    try
                    {
                        var comments = await context.GanjoorComments.Where(c => c.HtmlComment.Contains("href=")).ToArrayAsync();

                        await jobProgressServiceEF.UpdateJob(job.Id, 0, $"Examining {comments.Length} Comments");

                        int percent = 0;
                        for (int i = 0; i < comments.Length; i++)
                        {
                            if (i * 100 / comments.Length > percent)
                            {
                                percent++;
                                await jobProgressServiceEF.UpdateJob(job.Id, percent);
                            }

                            var comment = comments[i];

                            string commentText = await _ProcessCommentHtmlLinks(comment.HtmlComment, context);

                            if(commentText != comment.HtmlComment)
                            {
                                comment.HtmlComment = commentText;
                                context.Update(comment);
                                await context.SaveChangesAsync();
                            }
                        }

                        await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                    }
                    catch (Exception exp)
                    {
                        await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                    }

                }
            }
            );

            return new RServiceResult<bool>(true);
        }
    }
}