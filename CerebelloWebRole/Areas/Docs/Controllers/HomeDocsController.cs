﻿using System.Web.Mvc;
using CerebelloWebRole.Code.Controllers;

namespace CerebelloWebRole.Areas.Docs.Controllers
{
    public class HomeDocsController : RootController
    {
        //
        // GET: /Docs/HomeDocs/

        public ActionResult Index()
        {
            return this.View();
        }

        public ActionResult CreatingAnAccount()
        {
            return this.View();
        }
    }
}
