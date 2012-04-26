﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Collections;

namespace CerebelloWebRole.Areas.App.Models
{
    public class CollectionEditorViewModel
    {
        /// <summary>
        /// Items
        /// </summary>
        public ArrayList Items { get; set; }

        /// <summary>
        /// The class name of the wrapper list
        /// </summary>
        public string ListClass { get; set; }

        /// <summary>
        /// Name of the view that must be called for each item
        /// </summary>
        public string ListParialViewName { get; set; }

        /// <summary>
        /// Id of the "add-another" link
        /// </summary>
        public string AddAnotherLinkId { get; set; }
    }
}