﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Xml;
using DotNetNuke.Application;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Modules.Dashboard.Components.Portals;
using DotNetNuke.Services.Search;
using DotNetNuke.Services.Search.Controllers;
using DotNetNuke.Services.Search.Entities;
using DotNetNuke.Services.Search.Internals;
using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;


namespace Nevoweb.DNN.NBrightBuy.Providers
{
    /// <summary>
    /// Implement DNN search Index
    /// </summary>
    public class DnnIdxProvider : Components.Interfaces.SchedulerInterface 
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="portalfinfo"></param>
        /// <returns></returns>
        public override string DoWork(int portalId)
        {

            try
            {
                var objCtrl = new NBrightBuyController();

                // check if we have NBS in this portal by looking for default settings.
                var nbssetting = objCtrl.GetByGuidKey(portalId, -1, "SETTINGS", "NBrightBuySettings");
                if (nbssetting != null)
                {
                    var storeSettings = new StoreSettings(portalId);
                    var pluginData = new PluginData(portalId); // get plugin data to see if this scheduler is active on this portal 
                    var plugin = pluginData.GetPluginByCtrl("dnnsearchindex");
                    if (plugin != null && plugin.GetXmlPropertyBool("genxml/checkbox/active"))
                    {
                        // The NBS scheduler is normally set to run hourly, therefore if we only want a process to run daily we need the logic this function.
                        // To to this we keep a last run flag on the sceduler settings
                        var setting = objCtrl.GetByGuidKey(portalId, -1, "DNNIDXSCHEDULER", "DNNIDXSCHEDULER");
                        if (setting == null)
                        {
                            setting = new NBrightInfo(true);
                            setting.ItemID = -1;
                            setting.PortalId = portalId;
                            setting.TypeCode = "DNNIDXSCHEDULER";
                            setting.GUIDKey = "DNNIDXSCHEDULER";
                            setting.ModuleId = -1;
                            setting.XMLData = "<genxml></genxml>";
                        }


                        var lastrun = setting.GetXmlPropertyRaw("genxml/lastrun");
                        var lastrundate = DateTime.Now.AddYears(-99);
                        if (Utils.IsDate(lastrun)) lastrundate = Convert.ToDateTime(lastrun);

                        var rtnmsg = DoProductIdx(portalId, lastrundate, storeSettings.DebugMode);
                        setting.SetXmlProperty("genxml/lastrun", DateTime.Now.ToString("s"), TypeCode.DateTime);
                        objCtrl.Update(setting);
                        if (rtnmsg != "") return rtnmsg;

                    }
                }

                return " - NBS-DNNIDX scheduler OK ";
            }
            catch (Exception ex)
            {
                return " - NBS-DNNIDX scheduler FAIL: " + ex.ToString() + " : ";
            }

        }

        private String DoProductIdx(DotNetNuke.Entities.Portals.PortalInfo portal, DateTime lastrun, Boolean debug)
        {
            if (debug)
            {
                InternalSearchController.Instance.DeleteAllDocuments(portal.PortalID, SearchHelper.Instance.GetSearchTypeByName("tab").SearchTypeId);
            }
            var searchDocs = new List<SearchDocument>();
            var culturecodeList = DnnUtils.GetCultureCodeList(portal.PortalID);
            var storeSettings = new StoreSettings(portal.PortalID);
            foreach (var lang in culturecodeList)
            {
                var strContent = "";
                // select all products
                var objCtrl = new NBrightBuyController();
                var strFilter = " and NB1.ModifiedDate > convert(datetime,'" + lastrun.ToString("s") + "') ";
                if (debug) strFilter = "";
                var l = objCtrl.GetList(portal.PortalID, -1, "PRD", strFilter);

                foreach (var p in l)
                {
                    var prodData = new ProductData(p.ItemID, portalId, lang);

                    strContent = prodData.Info.GetXmlProperty("genxml/textbox/txtproductref") + " : " + prodData.SEODescription + " " + prodData.SEOName + " " + prodData.SEOTagwords + " " + prodData.SEOTitle;

                    if (strContent != "")
                    {
                        var tags = new List<String>();
                        tags.Add("nbsproduct");

                        //Get the description string
                        string strDescription = HtmlUtils.Shorten(HtmlUtils.Clean(strContent, false), 100, "...");
                        var searchDoc = new SearchDocument();
                        // Assigns as a Search key the SearchItems' 
                        searchDoc.UniqueKey = prodData.Info.ItemID.ToString("");
                        searchDoc.QueryString = "ref=" + prodData.Info.GetXmlProperty("genxml/textbox/txtproductref");
                        searchDoc.Title = prodData.ProductName;
                        searchDoc.Body = strContent;
                        searchDoc.Description = strDescription;
                        if (debug)
                            searchDoc.ModifiedTimeUtc = DateTime.Now.Date;
                        else
                            searchDoc.ModifiedTimeUtc = prodData.Info.ModifiedDate;
                        searchDoc.AuthorUserId = 1;
                        searchDoc.TabId = storeSettings.ProductDetailTabId;
                        searchDoc.PortalId = portal.PortalID;
                        searchDoc.SearchTypeId = SearchHelper.Instance.GetSearchTypeByName("tab").SearchTypeId;
                        searchDoc.CultureCode = lang;
                        searchDoc.Tags = tags;
                        //Add Module MetaData
                        searchDoc.ModuleDefId = 0;
                        searchDoc.ModuleId = 0;

                        searchDocs.Add(searchDoc);
                    }
                }
            }

            //Index
            InternalSearchController.Instance.AddSearchDocuments(searchDocs);
            InternalSearchController.Instance.Commit();


            return " - NBS-DNNIDX scheduler ACTIVATED ";

        }



    }
}
