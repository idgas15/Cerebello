﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;

namespace CerebelloWebRole.Areas.App.Models
{
    /// <summary>
    /// This is the GridModel used for displaying data within the CID lookup
    /// </summary>
    public class CidLookupGridModel
    {
        /// <summary>
        /// Code CID10
        /// </summary>
        [Display(Name = "CID-10")]
        public string Cid10Code { get; set; }

        /// <summary>
        /// Name of the condition
        /// </summary>
        [Display(Name = "Descrição")]
        public string Cid10Name { get; set; }
    }
}