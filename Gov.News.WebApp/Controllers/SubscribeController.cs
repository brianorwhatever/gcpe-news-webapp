using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gov.News.Api;
using Gov.News.Api.Models;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Gov.News.Website.Models.Subscribe;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    [ResponseCache(NoStore = true, Duration = 0)]
    public class SubscribeController : Shared.BaseController
    {
        public SubscribeController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        public async Task<SubscribeViewModel> LoadSubscribeViewModel(bool loadTags = true, bool loadNewsletters = true, bool loadServices = true)
        {
            var model = new SubscribeViewModel();
            await LoadAsync(model);
            model.Sectors = await Repository.GetSectorsAsync();

            try
            {
                if (loadTags)
                {
                    model.Tags = await Repository.ApiClient.Subscribe.SubscriptionItemsAsync("tags", Repository.APIVersion);
                }
            }
            catch (Exception) { }

            if (loadServices) model.Services = await Repository.GetServicesAsync();
            if (loadNewsletters) model.Newsletters = await Repository.GetNewslettersAsync();

            // As per Michael's instructions we're hard-coding these values and not fetching them from a table in a DB
            var emergencies = new List<KeyValuePair2>();
            emergencies.Add(new KeyValuePair2("alerts", "Emergency Alerts"));
            //emergencies.Add(new KeyValuePair<string, string>("campaigns", "Campaigns"));
            model.Emergencies = emergencies;
            return model;
        }

        // Having these parameters will make ASP MVC create (auto magically) them
        // using the request query parameters
        public async Task<ActionResult> Index(string[] ministries, string[] sectors, string[] tags, string[] services, string[] emergency, string[] newsletters, string display)
        {
            bool servicesSelected = services.Any();
            if (servicesSelected || newsletters.Any())
            {
                // do not show Ministries and Sectors
                ministries = null;
                sectors = null;
                emergency = null;
                if (servicesSelected) newsletters = null;
                else services = null;
            }
            else
            {
                newsletters = null;
                services = null;
            }

            if ((ministries?.Length ?? 0) == 0 && (sectors?.Length ?? 0) == 0 && (services?.Length ?? 0) == 0 && (emergency?.Length ?? 0) == 0 && (newsletters?.Length ?? 0) == 0)
            {
                if ((tags?.Length ?? 0) == 0)
                {
                    if (!string.IsNullOrEmpty(display))
                        return StatusCode(400);
                }
                else
                {
                    if (string.IsNullOrEmpty(display))
                    {
                        display = "selected";
                    }
                }
            }
            
            SubscribeViewModel model = await LoadSubscribeViewModel(ministries != null, newsletters != null, services != null);

            model.Selection = new SelectionModel { Ministries = ministries, Sectors = sectors, Tags = tags, Services = services, NewsAsItHappens = true, Emergency = emergency, Newsletters = newsletters };

            model.Display = display;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateReferer]
        public async Task<ActionResult> Index(SelectionModel options, bool? AllNews)
        {
            var model = new ContentViewModel()
            {
                Title = "Subscribe",
                Subtitle = "Confirm your subscription",
                Html = new HtmlString(
                    "<p>Thank you for subscribing to updates from the Government of B.C.  A confirmation email has been sent from noreply.newsondemand@gov.bc.ca.</p>" +
                    "<p><b>Click on the link in the email</b> to activate your subscription.</p>"
                )
            };

            await LoadAsync(model);

            AllNews = AllNews ?? options.Ministries != null && options.Ministries.Count() == model.Ministries.Count();

            SubscriberInfo info = GetSubscriberInfo(options, AllNews.Value);

            await Repository.ApiClient.Subscribe.CreateNewsOnDemandEmailSubscriptionWithPreferencesAsync(Repository.APIVersion, info);

            return View("ContentView", model);
        }

        public async Task<ActionResult> Manage(string token)
        {
            SubscriberInfo info = null;

            var guid = UrlTokenDecode(token);

            if (guid.HasValue)
            {
                info = await Repository.ApiClient.Subscribe.ConfirmUpdateCreateSubscriptionAsync(guid.Value, Repository.APIVersion);

                if (info == null)
                    return Redirect(new Uri(new Uri (Configuration["NewsHostUri"]), "subscribe").ToString());
            }

            SubscribeViewModel model = await LoadSubscribeViewModel();

            model.Selection = new SelectionModel();
            if (info != null)
            {
                model.Selection.EmailAddress = info.EmailAddress;
                if (info.SubscribedCategories != null)
                { // we have a valid token
                    model.Token = guid;

                    model.Selection.NewsAsItHappens = info.IsAsItHappens.Value;
                    model.Selection.NewsDailyDigest = info.IsDailyDigest.Value;

                    IList<string> lists;
                    if (info.IsAllNews.Value)
                    {
                        model.Selection.Ministries = model.Ministries.Select(m => m.Index.Key);
                        model.Selection.Sectors = model.Sectors.Select(m => m.Index.Key);
                        model.Selection.Tags = model.Tags?.Select(m => m.Key);
                    }
                    else
                    {
                        model.Selection.Ministries = info.SubscribedCategories.TryGetValue("ministries", out lists) ? lists : new string[0];
                        model.Selection.Sectors = info.SubscribedCategories.TryGetValue("sectors", out lists) ? lists : new string[0];
                        model.Selection.Tags = info.SubscribedCategories.TryGetValue("tags", out lists) ? lists : new string[0];
                    }

                    model.Selection.Services = info.SubscribedCategories.TryGetValue("services", out lists) ? lists : new string[0];

                    model.Selection.Emergency = info.SubscribedCategories.TryGetValue("emergency", out lists) ? lists : new string[0];

                    model.Selection.Newsletters = info.SubscribedCategories.TryGetValue("newsletters", out lists) ? lists : new string[0];

                    model.Selection.MediaDistributionLists = info.SubscribedCategories.TryGetValue("media-distribution-lists", out lists) ? string.Join(";", lists) : null;
                }
            }

            return View(model);
        }

        internal Guid? UrlTokenDecode(string input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            int length = input.Length;

            if (length < 1)
                return null;

            int padLength = (int)input[length - 1] - (int)'0';

            if (padLength < 0 || padLength > 10)
                return null;

            char[] base64Chars = new char[length - 1 + padLength];

            for (int i = 0; i < length - 1; i++)
            {
                char c = input[i];

                switch (c)
                {
                    case '-':
                        base64Chars[i] = '+';
                        break;

                    case '_':
                        base64Chars[i] = '/';
                        break;

                    default:
                        base64Chars[i] = c;
                        break;
                }
            }

            for (int i = length - 1; i < base64Chars.Length; i++)
            {
                base64Chars[i] = '=';
            }

            var token = Convert.FromBase64CharArray(base64Chars, 0, base64Chars.Length);

            if (token?.Length != 16)
                return null;

            return new Guid(token);
        }

        private static SubscriberInfo GetSubscriberInfo(SelectionModel options, bool isAllNews)
        {
            return new SubscriberInfo()
            {
                EmailAddress = options.EmailAddress,
                IsAdminRegistration = false,
                IsAllNews = isAllNews,
                IsAsItHappens = options.NewsAsItHappens,
                IsDailyDigest = options.NewsDailyDigest,

                //TODO: Include in SelectionModel
                NotifyIfNewCategories = true,

                SubscribedCategories = new Dictionary<string, IList<string>>()
                {
                    { "ministries", options.Ministries?.ToArray()},
                    { "sectors", options.Sectors?.ToArray() },
                    { "tags", options.Tags?.ToArray() },
                    { "services", options.Services?.ToArray() },
                    { "emergency", options.Emergency?.ToArray() },
                    { "newsletters", options.Newsletters?.ToArray() },
                    { "media-distribution-lists", options.MediaDistributionLists?.Split(';') }
                }
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateReferer]
        public async Task<string> Save(Guid token, SelectionModel options, bool allNews)
        {
            SubscriberInfo info = GetSubscriberInfo(options, allNews);

            await Repository.ApiClient.Subscribe.UpdateNewsOnDemandEmailSubscriptionWithPreferencesAsync(token, Repository.APIVersion, info);

            return "Your changes have been successfully saved";
        }

        public async Task<ActionResult> Renew(string EmailAddress)
        {
            if (!string.IsNullOrEmpty(EmailAddress))
            {
                await Repository.ApiClient.Subscribe.ManageNewsOnDemandEmailSubscriptionAsync(EmailAddress, Repository.APIVersion);
            }
            var model = new ContentViewModel()
            {
                Title = "Manage Subscription",
                Subtitle = "Subscription link renewal",
                Html = new HtmlString(
                    "<p>A new confirmation email has been sent from noreply.newsondemand@gov.bc.ca.</p>" +
                    "<p><b>Click on the renewed link in the email</b> to manage your subscription.</p>"
                )
            };

            await LoadAsync(model);

            return View("ContentView", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateReferer]
        public async Task<ActionResult> Manage(Guid? token, string EmailAddress)
        { // Unsubscribe or Renew
            if (!token.HasValue)
            {
                return await Renew(EmailAddress);
            }
            var model = new ContentViewModel()
            {
                Title = "Unsubscribed",
                Subtitle = ""
            };

            bool? validGuid = await Repository.ApiClient.Subscribe.CheckEmailActivationTokenAsync(token.Value, Repository.APIVersion);
            if (validGuid != true)
            {
                model.Html = new HtmlString(
                    "<p>This link has expired, or an error occurred</p>"
                );
            }
            else
            {
                bool? success = await Repository.ApiClient.Subscribe.UnsubscribeSubscriberAsync(token.Value, Repository.APIVersion);

                model.Html = new HtmlString(
                    success == true ? "<p>You have unsubscribed from BC News On Demand.</p>" : "<p>An error occurred.</p>"
                );
            }

            await LoadAsync(model);

            return View("ContentView", model);
        }
    }
}