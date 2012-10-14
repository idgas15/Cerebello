﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CerebelloWebRole.Areas.App.Models
{
    public class AnamneseViewModel
    {
        public AnamneseViewModel()
        {
            this.Symptoms = new List<SymptomViewModel>();
        }

        public int? Id { get; set; }

        [Required]
        public int? PatientId { get; set; }

        // Propriedade não está sendo usada?
        //public DateTime? Date { get; set; }

        [Display(Name="Notas")]
        public string Text { get; set; }

        [Display(Name="Sintomas")]
        public List<SymptomViewModel> Symptoms { get; set; }
    }
}