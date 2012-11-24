﻿using System;
using CerebelloWebRole.Models;

namespace CerebelloWebRole.WorkerRole.Code.Workers
{
    public class PatientEmailModel
    {
        public bool IsBodyHtml { get; set; }

        public DateTime AppointmentDate;

        public string PatientName { get; set; }

        public string PracticeUrlId { get; set; }

        public string PracticeName { get; set; }

        public string DoctorName { get; set; }

        public string PatientEmail { get; set; }

        public TypeGender PatientGender { get; set; }

        public TypeGender DoctorGender { get; set; }

        public string DoctorPhone { get; set; }

        public string DoctorEmail { get; set; }

        public string PracticeEmail { get; set; }

        public string PracticePhone { get; set; }
    }
}