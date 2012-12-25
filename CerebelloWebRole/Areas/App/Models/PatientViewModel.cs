﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CerebelloWebRole.Areas.App.Models
{
    public class PatientViewModel : PersonViewModel
    {
        public PatientViewModel()
        {
            this.Sessions = new List<SessionViewModel>();
            this.FutureAppointments = new List<AppointmentViewModel>();
        }

        [Display(Name = "Convênio")]
        public int? HealthInsuranceId { get; set; }

        [Display(Name = "Convênio")]
        public String HealthInsuranceText { get; set; }

        [Display(Name = "Observações")]
        public String Observations { get; set; }

        public List<SessionViewModel> Sessions { get; set; }

        public List<AppointmentViewModel> FutureAppointments { get; set; }
    }
}