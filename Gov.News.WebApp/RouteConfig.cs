using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;

namespace Gov.News.Website
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(this IRouteBuilder routes)
        {
            //MIGRATION: Implement middleware to redirect users to lower case URLs
            //routes.LowercaseUrls = true;

            //routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            #region Constraints

            const string formats = "rss|atom";
            const string postKinds = "Stories|Releases|Factsheets|Updates";
            
            //Aug. 3/2018
            //const string categories = "Ministries|Sectors|Regions|Tags|Themes|Services";
            
            //The line above has been updated. During the News work in Fall 2017 (BC Gov news public web api) and move to OpenShift
            //Regions were not implemented in the application as they were never used. Therefore to prohibit the appliation throwing
            //errors on routes containing regions, it is being removed fromt he routes.

            //If regions are reimplmented in Repository.cs and the Web API, put them back in the route table.
            const string categories = "Ministries|Sectors|Tags|Themes|Services";

            IRouteConstraint yearConstraint = new RegexRouteConstraint(@"^\d{4}$");
            IRouteConstraint monthConstraint = new RegexRouteConstraint(@"^(\d{2})?$");
            IRouteConstraint formatConstraint = new OptionalRouteConstraint(new RegexRouteConstraint(formats));
            IRouteConstraint postKindConstraint = new OptionalRouteConstraint(new RegexRouteConstraint(postKinds));
            IRouteConstraint categoryControllerConstraint = new RegexRouteConstraint(categories);

            #endregion

            //context.MapRoute(
            //    "Newsroom_default",
            //    "Newsroom/{controller}/{action}/{id}",
            //    new { action = "Index", id = UrlParameter.Optional }
            //);

            //#region Files
            routes.MapRoute(
                name: "Newsroom-Files",
                template: "files/{*path}",
                defaults: new { controller = "Files", action = "Single" }
            );

            //#endregion

            #region Assets

            routes.MapRoute(
                name: "Newsroom-Assets-License",
                template: "assets/license",
                defaults: new { controller = "Assets", action = "License" }
            );

            routes.MapRoute(
                name: "Newsroom-Assets",
                template: "assets/{*path}",
                defaults: new { controller = "Assets", action = "Single" }
            );

            #endregion

            #region Default

            /* The Default controller supports the following URL patterns:
             * 
             *   index
             *   more-news
             *   (feed | embed [format])
             *   archive [year [month]]
             *   (top | feature) feed [format]
             *   biography
             *   
             */

            routes.MapRoute(
                name: "NewsroomReference",
                template: "{reference}",
                defaults: new { controller = "Default", action = "Reference" },
                constraints: new { reference = @"\d+"}
            );

            //index
            routes.MapRoute(
                name: "Newsroom",
                template: "",
                defaults: new { controller = "Default", action = "Index" }
            );

            routes.MapRoute(
                name: "Newsroom-Sitemap",
                template: "sitemap/html",
                defaults: new { controller = "Default", action = "Sitemap" }
            );

            routes.MapRoute(
                name: "Newsroom-SiteStatus",
                template: "SiteStatus",
                defaults: new { controller = "Default", action = "SiteStatus" }
            );

            routes.MapRoute(
                name: "CarouselImage",
                template: "CarouselImage/{slideId}",
                defaults: new { controller = "Default", action = "CarouselImage" }
            );

            //more-news
            routes.MapRoute(
                name: "Newsroom-MoreNews",
                template: "more-news",
                defaults: new { controller = "Default", action = "MoreNews" }
            );

            //archive [year [month]] 
            {
                routes.MapRoute(
                    name: "Newsroom-Archive",
                    template: "archive",
                    defaults: new { controller = "Default", action = "Archive" }
                );

                routes.MapRoute(
                    name: "Newsroom-Archive-Month",
                    template: "archive/{year}/{month?}",
                    defaults: new { controller = "Default", action = "Month" },
                    constraints: new { year = yearConstraint, month = monthConstraint }
                );
            }

            //feed | embed [format]
            routes.MapRoute(
                name: "Newsroom-Action",
                template: "{action}/{format?}",
                defaults: new { controller = "Default" },
                constraints: new { action = "Feed|Embed", format = formatConstraint }
            );

            //(top | feature) feed [format]
            routes.MapRoute(
                name: "Newsroom-Special-Action",
                template: "{action}/{type}/{format?}",
                defaults: new { controller = "Default" },
                constraints: new { action = "Top|Feature", format = formatConstraint }
            );

            #endregion

            //biographies key
            MapRouteOptional(
                routes: routes,
                name: "Newsroom[-Parent]-Biography",
                template: "ministries[/{parentKey}]/{key}/biography",
                defaults: new { controller = "Ministries", action = "Biography" },
                constraints: null
            );

            #region Ministries, Regions, Sectors, Tags, Themes

            /* The Categories controller supports the following URL patterns:
             * 
             *   category [parentKey] [postKind]
             *   category [parentKey] key [postKind]
             *   category [parentKey] key [postKind] more-news
             *   category [parentKey] key [postKind] (feed | embed [format])
             *   category [parentKey] key [postKind] archive [year [month]]
             * 
             */

            //category key [postKind] more-news
            MapRouteOptional(
                routes: routes,
                name: "Newsroom[-Parent]-Category[-Type]-More",
                template: "{category}[/{parentKey}]/{key}[/{postKind?}]/more-news",
                defaults: new { controller = "Category", action = "MoreNews" },
                constraints: new { category = categoryControllerConstraint, postKind = postKindConstraint }
            );

            MapRouteOptional(
               routes: routes,
               name: "Newsroom[-Parent]-Category[-Type]-MoreArticles",
               template: "{category}[/{parentKey}]/{key}[/{postKind?}]/morearticles",
               defaults: new { controller = "Category", action = "MoreArticles" },
               constraints: new { category = categoryControllerConstraint, postKind = postKindConstraint }
           );

            //category key [postKind] archive [year [month]]
            {
                //i.e. category key [postKind] archive
                MapRouteOptional(
                    routes: routes,
                    name: "Newsroom[-Parent]-Category[-Type]-Archive",
                    template: "{category}[/{parentKey}]/{key}[/{postKind?}]/archive",
                    defaults: new { controller = "Category", action = "Archive" },
                    constraints: new { category = categoryControllerConstraint, postKind = postKindConstraint }
                );

                //i.e. category key [postKind] archive year [month]
                MapRouteOptional(
                    routes: routes,
                    name: "Newsroom[-Parent]-Category[-Type]-Archive-Year",
                    template: "{category}[/{parentKey}]/{key}[/{postKind?}]/archive/{year}/{month?}",
                    defaults: new { controller = "Category", action = "Month" },
                    constraints: new { category = categoryControllerConstraint, year = yearConstraint, month = monthConstraint, postKind = postKindConstraint }
                );
            }

            //category [parent] key [postKind] (feed | embed [format])
            MapRouteOptional(
                routes: routes,
                name: "Newsroom[-Parent]-Category[-Type]-Action",
                template: "{category}[/{parentKey}]/{key}[/{postKind?}]/{action}/{format?}",
                defaults: new { controller = "Category" },
                constraints: new { category = categoryControllerConstraint, postKind = postKindConstraint, action = "Feed|Embed", format = formatConstraint }
            );

            //category [parent] key [postKind]
            MapRouteOptional(
                routes: routes,
                name: "Newsroom[-Parent]-Category-Type",
                template: "{category}[/{parentKey}]/{key}/{postKind?}",
                defaults: new { controller = "Category", action = "Details" },
                constraints: new { category = categoryControllerConstraint, postKind = postKindConstraint }
            );

            //TODO: Determine if [postKind] should be present
            //category [postKind]
            routes.MapRoute(
                name: "Newsroom-Category-Index-Type",
                template: "{category}/{postKind?}",
                defaults: new { controller = "Category", action = "Index" },
                constraints: new { category = categoryControllerConstraint, postKind = postKindConstraint }
            );


            #endregion

            //#region Tags

            ///* The Tags controller supports the following URL patterns:
            // * 
            // *   Tag key [postKind] (feed | embed) [format]
            // * 
            // */

            ////Tag key [postKind] (feed | embed) [format]
            //MapRouteOptional(
            //    context: context,
            //    name: "Newsroom-Tags[-Type]-Action",
            //    template: "Tags/{key}[/{postKind}]/{action}/{format}",
            //    defaults: new { controller = "Tags", format = UrlParameter.Optional },
            //    constraints: new { postKind = postKindConstraint, action = "Feed|Embed", format = formatConstraint }
            //);

            //#endregion

            #region Stories, Releases, Factsheets, Updates

            foreach (string postKind in postKinds.Split(","))
            {
                /* The Posts controller supports the following URL patterns:
                 * 
                 *   postKind
                 *   postKind more-news
                 *   postKind (feed | embed [format])
                 *   postKind archive [year [month]]
                 *   postKind key
                 * 
                 */

                //postKind
                routes.MapRoute(
                    name: "Newsroom-Type-Index" + "-" + postKind,
                    template: "{postKind}",
                    defaults: new { controller = "Posts", postKind = postKind.ToLower(), action = "Index" },
                    constraints: new { postKind = postKind }
                );

                //postKind more-news
                routes.MapRoute(
                    name: "Newsroom-Type-More" + "-" + postKind,
                    template: "{postKind}/more-news",
                    defaults: new { controller = "Posts", postKind = postKind.ToLower(), action = "MoreNews" },
                    constraints: new { postKind = postKind }
                );

                routes.MapRoute(
                    name: "Newsroom-Type-MoreArticles" + "-" + postKind,
                    template: "{postKind}/MoreArticles",
                    defaults: new { controller = "Posts", postKind = postKind.ToLower(), action = "MoreArticles" },
                    constraints: new { postKind = postKind }
                );


                //postKind (feed | embed [format])
                routes.MapRoute(
                    name: "Newsroom-Type-Stories-Action" + "-" + postKind,
                    template: "{postKind}/{action}/{format?}",
                    defaults: new { controller = "Posts", postKind = postKind.ToLower() },
                    constraints: new { postKind = postKind, action = "Feed|Embed" }
                );

                //postKind archive [year [month]]
                {
                    //i.e. postKind archive
                    routes.MapRoute(
                        name: "Newsroom-Type-Archive" + "-" + postKind,
                        template: "{postKind}/archive",
                        defaults: new { controller = "Posts", postKind = postKind.ToLower(), action = "Archive" },
                        constraints: new { postKind = postKind }
                    );

                    //i.e. postKind archive year [month]
                    routes.MapRoute(
                        name: "Newsroom-Type-Archive-Year" + "-" + postKind,
                        template: "{postKind}/archive/{year}/{month?}",
                        defaults: new { controller = "Posts", postKind = postKind.ToLower(), action = "Month" },
                        constraints: new { postKind = postKind, year = yearConstraint, month = monthConstraint }
                    );
                }

                //postKind key
                routes.MapRoute(
                    name: "Newsroom-Type-Details" + "-" + postKind,
                    template: "{postKind}/{key}",
                    defaults: new { controller = "Posts", postKind = postKind.ToLower(), action = "Details" },
                    constraints: new { postKind = postKind }
                );

                //return the image via our server.
                routes.MapRoute(
                    name: "Newsroom-Type-Details-Image" + "-" + postKind,
                    template: "{postKind}/{key}/image",
                    defaults: new { controller = "Posts", postKind = postKind.ToLower(), action = "Image" },
                    constraints: new { postKind = postKind }
                    );
            }
            #endregion

            ////controller minstryKey key
            //context.MapRoute(
            //    name: "Newsroom-Biography",
            //    template: "{controller}/{ministryKey}/{key}",
            //    defaults: new { action = "Details" },
            //    constraints: new { controller = categoryControllerConstraint }
            //);

            #region Office of the Premier - Speeches

            /* The Categories controller supports the following URL patterns:
            * 
            *   category [parentKey] [postKind]
            *   category [parentKey] key [postKind]
            *   category [parentKey] key [postKind] more-news
            *   category [parentKey] key [postKind] (feed | embed [format])
            *   category [parentKey] key [postKind] archive [year [month]]
            * 
            */

            //category key [postKind] more-news
            routes.MapRoute(
                name: "Newsroom-Premier-Speeches-More",
                template: "office-of-the-premier/speeches/more-news",
                defaults: new { controller = "Category", category = "tags", action = "MoreNews", key = "speeches" },
                constraints: new { category = categoryControllerConstraint }
            );

            //category key [postKind] archive [year [month]]
            {
                //i.e. category key [postKind] archive
                routes.MapRoute(
                    name: "Newsroom-Premier-Speeches-Archive",
                    template: "office-of-the-premier/speeches/archive",
                    defaults: new { controller = "Category", category = "tags", action = "Archive", key = "speeches" },
                    constraints: new { category = categoryControllerConstraint }
                );

                //i.e. category key [postKind] archive year [month]
                routes.MapRoute(
                    name: "Newsroom-Premier-Speeches-Archive-Year",
                    template: "office-of-the-premier/speeches/archive/{year}/{month?}",
                    defaults: new { controller = "Category", category = "tags", key = "speeches", action = "Month" },
                    constraints: new { year = yearConstraint, month = monthConstraint }
                );
            }

            //category key [postKind] (feed | embed [format])
            routes.MapRoute(
                name: "Newsroom-Premier-Speeches-Action",
                template: "office-of-the-premier/speeches/{action}/{format?}",
                defaults: new { controller = "Category", category = "tags", key = "speeches" },
                constraints: new { action = "Feed|Embed", format = formatConstraint, category = categoryControllerConstraint }
            );

            routes.MapRoute(
                name: "Newsroom-Premier-Speeches-Action-MoreArticles",
                template: "office-of-the-premier/speeches/morearticles",
                defaults: new { controller = "Category", category = "tags", key = "speeches", action = "MoreArticles" },
                constraints: new { category = categoryControllerConstraint }
            );

            routes.MapRoute(
                name: "Newsroom-Premier-Speeches",
                template: "office-of-the-premier/speeches",
                defaults: new { controller = "Category", category = "tags", key = "speeches", action = "Details" },
                constraints: new { category = categoryControllerConstraint }
            );

            #endregion

            #region Office of the Premier
            /* The Categories controller supports the following URL patterns:
             * 
             *   office-of-the-premier biography
             *   office-of-the-premier [postKind]
             *   office-of-the-premier [postKind] more-news
             *   office-of-the-premier [postKind] (feed | embed [format])
             *   office-of-the-premier [postKind] archive [year [month]]
             * 
             */

            MapRouteOptional(
                routes: routes,
                name: "Newsroom-Premier-Biography",
                template: "office-of-the-premier/biography",
                defaults: new { controller = "Ministries", action = "Biography", key = "office-of-the-premier" },
                constraints: null
            );

            //category key [postKind] more-news
            MapRouteOptional(
                routes: routes,
                name: "Newsroom-Premier[-Type]-More",
                template: "office-of-the-premier[/{postKind?}]/more-news",
                defaults: new { controller = "Category", category = "ministries", key = "office-of-the-premier", action = "MoreNews" },
                constraints: new { postKind = postKindConstraint }
            );

            MapRouteOptional(
                routes: routes,
                name: "Newsroom-Premier[-Type]-MoreArticles",
                template: "office-of-the-premier[/{postKind?}]/morearticles",
                defaults: new { controller = "Category", category = "ministries", key = "office-of-the-premier", action = "MoreArticles" },
                constraints: new { postKind = postKindConstraint }
            );

            //category key [postKind] archive [year [month]]
            {
                //i.e. category key [postKind] archive
                MapRouteOptional(
                    routes: routes,
                    name: "Newsroom-Premier[-Type]-Archive",
                    template: "office-of-the-premier[/{postKind?}]/archive",
                    defaults: new { controller = "Category", category = "ministries", key = "office-of-the-premier", action = "Archive" },
                    constraints: new { postKind = postKindConstraint }
                );

                //i.e. category key [postKind] archive year [month]
                MapRouteOptional(
                    routes: routes,
                    name: "Newsroom-Premier[-Type]-Archive-Year",
                    template: "office-of-the-premier[/{postKind?}]/archive/{year}/{month?}",
                    defaults: new { controller = "Category", category = "ministries", key = "office-of-the-premier", action = "Month" },
                    constraints: new { year = yearConstraint, month = monthConstraint, postKind = postKindConstraint }
                );
            }

            //category key [postKind] (feed | embed [format])
            MapRouteOptional(
                routes: routes,
                name: "Newsroom-Premier[-Type]-Action",
                template: "office-of-the-premier[/{postKind?}]/{action}/{format?}",
                defaults: new { controller = "Category", category = "ministries", key = "office-of-the-premier" },
                constraints: new { postKind = postKindConstraint, action = "Feed|Embed", format = formatConstraint }
            );

            MapRouteOptional(
                routes: routes,
                name: "Newsroom-Premier-Type",
                template: "office-of-the-premier/{postKind?}",
                defaults: new { controller = "Category", category = "ministries", key = "office-of-the-premier", action = "Details" },
                constraints: new { postKind = postKindConstraint }
            );

            #endregion

            routes.MapRoute(
                name: "Newsroom-Page",
                template: "{action}",
                defaults: new { controller = "Default" },
                constraints: new { action = "Connect|Search|Privacy|Live|Contact|Contacts|Error" }
            );

            routes.MapRoute(
                name: "Subscribe",
                template: "subscribe/{action}",
                defaults: new { controller = "Subscribe", action = "Index" },
                constraints: new { action = "Index|Manage|Save|Renew" }
            );

            //context.MapRoute(
            //    name: "Newsroom-Page",
            //    template: "{key}",
            //    defaults: new { controller = "Page", action = "Single" }
            //);

            //routes.MapRoute(
            //    name: "Newsroom-NotFound",
            //    template: "{*path}",
            //    defaults: new { controller = "Default", action = "NotFound" }
            //);

            //TODO: Apply only to Index controllers

            routes.MapRoute(
                name: "Newsletters",
                template: "newsletters",
                defaults: new { controller = "Newsletters", action = "Index" }
            );

            routes.MapRoute(
               name: "Editions",
               template: "newsletters/{newsletterKey}",
               defaults: new { controller = "Newsletters", action = "Editions" }
           );

            routes.MapRoute(
                name: "GetBinaryByGuid",
                    template: "newsletters/{type}/{guid}",
                    defaults: new { controller = "Newsletters", action = "GetBinaryByGuid" },
                constraints: new { type = "file|image" }
              );
            
            routes.MapRoute(
               name: "Edition",
               template: "newsletters/{newsletterKey}/{editionKey}",
               defaults: new { controller = "Newsletters", action = "Edition" }
            );

            routes.MapRoute(
                name: "GetArticle",
                template: "newsletters/{newsletterKey}/{editionKey}/{articleKey}",
                defaults: new { controller = "Newsletters", action = "GetArticle"}
            );

            routes.MapRoute(
                name: "Default",
                template: "{controller}/{action}",
                defaults: new { },
                constraints: new { action = "MoreArticles" }
            );

            routes.MapRoute(
                name: "NotFound",
                template: "{*path}",
                defaults: new { controller = "Default", action = "NotFound" }
                //TEST: Why was the namespaces parameter set?
                //namespaces: new[] { "Gov.News.Website.Controllers" }
            );
        }

        public static void MapRouteOptional(IRouteBuilder routes, string name, string template, object defaults = null, object constraints = null)
        {
            Regex reg = new Regex("\\[.*?\\]");
            MatchCollection nameMatches = reg.Matches(name);
            MatchCollection urlMatches = reg.Matches(template);

            for (int i = 0; i <= urlMatches.Count - 1; i++)
            {
                string newName = name;
                string newUrl = template;

                for (int j = i; j >= 0; j--)
                {
                    newName = reg.Replace(newName, "", 1, nameMatches[j].Index);
                    newUrl = reg.Replace(newUrl, "", 1, urlMatches[j].Index);

                    routes.MapRoute(
                        name: newName.Replace("[", "").Replace("]", ""),
                        template: newUrl.Replace("[", "").Replace("]", ""),
                        defaults: defaults,
                        constraints: constraints
                    );
                }
            }

            routes.MapRoute(
                name: name.Replace("[", "").Replace("]", ""),
                template: template.Replace("[", "").Replace("]", ""),
                defaults: defaults,
                constraints: constraints
            );
        }
    }
}