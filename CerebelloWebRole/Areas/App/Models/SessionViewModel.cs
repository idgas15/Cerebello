﻿using System;
using System.Collections.Generic;

namespace CerebelloWebRole.Areas.App.Models
{
    public class SessionViewModel
    {
        public SessionViewModel()
        {
            this.ReceiptIds = new List<int>();
            this.AnamneseIds = new List<int>();
        }

        public int PatientId { get; set; }

        public DateTime Date { get; set; }

        /// <summary>
        /// Receipt ids.
        /// </summary>
        public List<int> ReceiptIds { get; set; }

        /// <summary>
        /// Anamnese ids.
        /// </summary>
        public List<int> AnamneseIds { get; set; }

        /// <summary>
        /// Physical examination ids.
        /// </summary>
        public List<int> PhysicalExaminationIds { get; set; }

        /// <summary>
        /// Diagnostic hypotheses ids
        /// </summary>
        public List<int> DiagnosticHipothesesId { get; set; }

        /// <summary>
        /// Certificate ids.
        /// </summary>
        public List<int> MedicalCertificateIds { get; set; }

        /// <summary>
        /// Examination request ids.
        /// </summary>
        public List<int> ExaminationRequestIds { get; set; }

        /// <summary>
        /// Examination result ids.
        /// </summary>
        public List<int> ExaminationResultIds { get; set; }

        /// <summary>
        /// Diagnosis ids.
        /// </summary>
        public List<int> DiagnosisIds { get; set; }

        /// <summary>
        /// Patient file ids.
        /// </summary>
        public List<int> PatientFiles { get; set; }
    }
}