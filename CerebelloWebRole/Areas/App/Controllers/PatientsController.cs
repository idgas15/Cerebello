﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Cerebello.Model;
using CerebelloWebRole.Areas.App.Models;
using CerebelloWebRole.Code;
using JetBrains.Annotations;

namespace CerebelloWebRole.Areas.App.Controllers
{
    public class PatientsController : DoctorController
    {
        private class SessionEvent
        {
            public int Id { get; set; }

            /// <summary>
            /// Date and time expressed in local practice time-zone.
            /// </summary>
            public DateTime LocalDate { get; set; }
        }

        private static PatientViewModel GetViewModel(PracticeController controller, [NotNull] Patient patient, bool includeSessions, bool includeFutureAppointments, bool includeAddressData = true)
        {
            if (patient == null) throw new ArgumentNullException("patient");

            // Person, address, and patient basic properties.
            var viewModel = new PatientViewModel();

            FillPersonViewModel(controller.DbPractice, patient.Person, viewModel);

            viewModel.Id = patient.Id;
            viewModel.Code = patient.Code.HasValue ? patient.Code.Value.ToString("D6") : "000000";
            viewModel.PatientId = patient.Id;
            viewModel.PersonId = patient.Person.Id;
            viewModel.Observations = patient.Person.Observations;

            if (includeAddressData)
            {
                var address = patient.Person.Addresses.SingleOrDefault();
                viewModel.Address = address != null ? GetAddressViewModel(address) : new AddressViewModel();
            }

            // Other (more complex) properties.

            if (includeFutureAppointments)
            {
                // gets a textual date. The input date must be LOCAL
                Func<DateTime, string> getRelativeDate = s =>
                {
                    var result = s.ToShortDateString();
                    result += ", " + DateTimeHelper.GetFormattedTime(s);
                    result += ", " +
                              DateTimeHelper.ConvertToRelative(s, controller.GetPracticeLocalNow(),
                                                               DateTimeHelper.RelativeDateOptions.IncludeSuffixes |
                                                               DateTimeHelper.RelativeDateOptions.IncludePrefixes |
                                                               DateTimeHelper.RelativeDateOptions.ReplaceToday |
                                                               DateTimeHelper.RelativeDateOptions.ReplaceYesterdayAndTomorrow);

                    return result;
                };

                // get appointments scheduled for the future
                var utcNow = controller.GetUtcNow();

                var appointments = patient.Appointments
                    .Where(
                        a => a.DoctorId == patient.DoctorId
                             && a.Start > utcNow)
                    .ToList();

                viewModel.FutureAppointments = (from a in appointments
                                                select new AppointmentViewModel
                                                {
                                                    PatientId = a.PatientId,
                                                    PatientName = a.PatientId != default(int) ? a.Patient.Person.FullName : null,
                                                    LocalDateTime = ConvertToLocalDateTime(controller.DbPractice, a.Start),
                                                    LocalDateTimeSpelled = getRelativeDate(ConvertToLocalDateTime(controller.DbPractice, a.Start))
                                                }).ToList();
            }

            if (includeSessions)
            {
                var sessions = GetSessionViewModels(controller.DbPractice, patient, null);

                viewModel.Sessions = sessions;
            }

            return viewModel;
        }

        public static AddressViewModel GetAddressViewModel(Address address)
        {
            if (address == null)
                return null;

            return new AddressViewModel
                       {
                           CEP = address.CEP,
                           City = address.City,
                           Complement = address.Complement,
                           Neighborhood = address.Neighborhood,
                           StateProvince = address.StateProvince,
                           Street = address.Street,
                       };
        }

        public static void FillPersonViewModel(Practice practice, Person person, PersonViewModel viewModel)
        {
            viewModel.BirthPlace = person.BirthPlace;
            viewModel.FullName = person.FullName;
            viewModel.Gender = person.Gender;
            viewModel.MaritalStatus = person.MaritalStatus;
            viewModel.Ethnicity = person.Ethnicity;
            viewModel.Schooling = person.Schooling;
            viewModel.FatherName = person.FatherName;
            viewModel.FatherProfession = person.FatherProfession;
            viewModel.MotherName = person.MotherName;
            viewModel.MotherProfession = person.MotherProfession;
            viewModel.DateOfBirth = ConvertToLocalDateTime(practice, person.DateOfBirth);
            viewModel.Profissao = person.Profession;
            viewModel.Cpf = person.CPF;
            viewModel.Rg = person.RG;
            viewModel.Email = person.Email;
            viewModel.PhoneCell = person.PhoneCell;
            viewModel.PhoneLand = person.PhoneLand;
        }

        public static List<SessionViewModel> GetSessionViewModels(Practice practice, Patient patient, DateTimeInterval? filterUtcInterval)
        {
            var eventDates = new List<DateTime>();

            var utcDateFilterStart = filterUtcInterval.HasValue ? filterUtcInterval.Value.Start : (DateTime?)null;
            var utcDateFilterEnd = filterUtcInterval.HasValue ? filterUtcInterval.Value.End : (DateTime?)null;

            // anamneses
            var anamneses = filterUtcInterval.HasValue
                ? patient.Anamneses.Where(x => x.MedicalRecordDate >= utcDateFilterStart && x.MedicalRecordDate < utcDateFilterEnd)
                : patient.Anamneses;
            var anamnesesByDate =
                (from avm in
                     (from r in anamneses
                      select new SessionEvent
                                 {
                                     LocalDate = ConvertToLocalDateTime(practice, r.MedicalRecordDate),
                                     Id = r.Id
                                 })
                 group avm by avm.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(anamnesesByDate.Keys);

            // physical examinations
            var physicalExaminations = filterUtcInterval.HasValue
                ? patient.PhysicalExaminations.Where(x => x.MedicalRecordDate >= utcDateFilterStart && x.MedicalRecordDate < utcDateFilterEnd)
                : patient.PhysicalExaminations;
            var physicalExaminationsByDate =
                (from pe in
                     (from r in physicalExaminations
                      select new SessionEvent
                      {
                          LocalDate = ConvertToLocalDateTime(practice, r.MedicalRecordDate),
                          Id = r.Id
                      })
                 group pe by pe.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(physicalExaminationsByDate.Keys);

            // diagnostic hipotheses
            var diagnosticHypotheses = filterUtcInterval.HasValue
                ? patient.DiagnosticHypotheses.Where(x => x.MedicalRecordDate >= utcDateFilterStart && x.MedicalRecordDate < utcDateFilterEnd)
                : patient.DiagnosticHypotheses;
            var diagnosticHypothesesByDate =
                (from pe in
                     (from r in diagnosticHypotheses
                      select new SessionEvent
                      {
                          LocalDate = ConvertToLocalDateTime(practice, r.MedicalRecordDate),
                          Id = r.Id
                      })
                 group pe by pe.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(diagnosticHypothesesByDate.Keys);

            // receipts
            var receipts = filterUtcInterval.HasValue
                ? patient.Receipts.Where(x => x.IssuanceDate >= utcDateFilterStart && x.IssuanceDate < utcDateFilterEnd)
                : patient.Receipts;
            var receiptsByDate =
                (from rvm in
                     (from r in receipts
                      select new SessionEvent
                                 {
                                     LocalDate = ConvertToLocalDateTime(practice, r.IssuanceDate),
                                     Id = r.Id
                                 })
                 group rvm by rvm.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(receiptsByDate.Keys);

            // certificates
            var certificates = filterUtcInterval.HasValue
                ? patient.MedicalCertificates.Where(x => x.IssuanceDate >= utcDateFilterStart && x.IssuanceDate < utcDateFilterEnd)
                : patient.MedicalCertificates;
            var certificatesByDate =
                (from cvm in
                     (from c in certificates
                      select new SessionEvent
                                 {
                                     LocalDate = ConvertToLocalDateTime(practice, c.IssuanceDate),
                                     Id = c.Id
                                 })
                 group cvm by cvm.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(certificatesByDate.Keys);

            // exam requests
            var examRequests = filterUtcInterval.HasValue
                ? patient.ExaminationRequests.Where(x => x.RequestDate >= utcDateFilterStart && x.RequestDate < utcDateFilterEnd)
                : patient.ExaminationRequests;
            var examRequestsByDate =
                (from ervm in
                     (from c in examRequests
                      select new SessionEvent
                                 {
                                     LocalDate = ConvertToLocalDateTime(practice, c.RequestDate),
                                     Id = c.Id
                                 })
                 group ervm by ervm.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(examRequestsByDate.Keys);

            // exam results
            var examResults = filterUtcInterval.HasValue
                ? patient.ExaminationResults.Where(x => x.ReceiveDate >= utcDateFilterStart && x.ReceiveDate < utcDateFilterEnd)
                : patient.ExaminationResults;
            var examResultsByDate =
                (from ervm in
                     (from c in examResults
                      select new SessionEvent
                                 {
                                     LocalDate = ConvertToLocalDateTime(practice, c.ReceiveDate),
                                     Id = c.Id
                                 })
                 group ervm by ervm.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(examResultsByDate.Keys);

            // diagnosis
            var diagnosis = filterUtcInterval.HasValue
                ? patient.Diagnoses.Where(x => x.MedicalRecordDate >= utcDateFilterStart && x.MedicalRecordDate < utcDateFilterEnd)
                : patient.Diagnoses;
            var diagnosisByDate =
                (from dvm in
                     (from d in diagnosis
                      select new SessionEvent
                                 {
                                     LocalDate = ConvertToLocalDateTime(practice, d.MedicalRecordDate),
                                     Id = d.Id
                                 })
                 group dvm by dvm.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(diagnosisByDate.Keys);

            // patientFiles
            var patientFiles = filterUtcInterval.HasValue
                ? patient.PatientFileGroups.Where(x => x.ReceiveDate >= utcDateFilterStart && x.ReceiveDate < utcDateFilterEnd)
                : patient.PatientFileGroups;
            var patientFilesByDate =
                (from dvm in
                     (from d in patientFiles
                      select new SessionEvent
                      {
                          LocalDate = ConvertToLocalDateTime(practice, d.ReceiveDate),
                          Id = d.Id
                      })
                 group dvm by dvm.LocalDate.Date
                     into g
                     select g).ToDictionary(g => g.Key, g => g.ToList());

            eventDates.AddRange(patientFilesByDate.Keys);

            // discover what dates have events
            eventDates = eventDates.Distinct().OrderBy(dt => dt).ToList();

            // creating sessions
            var sessions = eventDates.Select(
                eventDate => new SessionViewModel
                                 {
                                     PatientId = patient.Id,
                                     Date = eventDate,
                                     AnamneseIds =
                                         anamnesesByDate.ContainsKey(eventDate)
                                             ? anamnesesByDate[eventDate].Select(a => a.Id).ToList()
                                             : new List<int>(),
                                     PhysicalExaminationIds =
                                        physicalExaminationsByDate.ContainsKey(eventDate)
                                            ? physicalExaminationsByDate[eventDate].Select(a => a.Id).ToList()
                                    : new List<int>(),
                                     DiagnosticHipothesesId =
                                     diagnosticHypothesesByDate.ContainsKey(eventDate)
                                     ? diagnosticHypothesesByDate[eventDate].Select(a => a.Id).ToList()
                                     : new List<int>(),
                                     ReceiptIds =
                                         receiptsByDate.ContainsKey(eventDate)
                                             ? receiptsByDate[eventDate].Select(v => v.Id).ToList()
                                             : new List<int>(),
                                     MedicalCertificateIds =
                                         certificatesByDate.ContainsKey(eventDate)
                                             ? certificatesByDate[eventDate].Select(c => c.Id).ToList()
                                             : new List<int>(),
                                     ExaminationRequestIds =
                                         examRequestsByDate.ContainsKey(eventDate)
                                             ? examRequestsByDate[eventDate].Select(v => v.Id).ToList()
                                             : new List<int>(),
                                     ExaminationResultIds =
                                         examResultsByDate.ContainsKey(eventDate)
                                             ? examResultsByDate[eventDate].Select(v => v.Id).ToList()
                                             : new List<int>(),
                                     DiagnosisIds =
                                         diagnosisByDate.ContainsKey(eventDate)
                                             ? diagnosisByDate[eventDate].Select(v => v.Id).ToList()
                                             : new List<int>(),
                                     PatientFiles =
                                         patientFilesByDate.ContainsKey(eventDate)
                                             ? patientFilesByDate[eventDate].Select(v => v.Id).ToList()
                                             : new List<int>()
                                 }).ToList();

            return sessions;
        }

        //
        // GET: /App/Patients/

        [CanAlternateUser]
        public ActionResult Index()
        {
            var model =
                new PatientsIndexViewModel
                    {
                        LastRegisteredPatients =
                            (from p in
                                 (from Patient patient in this.db.Patients
                                  where patient.DoctorId == this.Doctor.Id
                                  orderby patient.Person.CreatedOn descending
                                  select patient).Take(Constants.RECENTLY_REGISTERED_LIST_MAXSIZE).ToList()
                             select
                                 new PatientViewModel
                                     {
                                         Id = p.Id,
                                         DateOfBirth =
                                             ConvertToLocalDateTime(this.DbPractice, p.Person.DateOfBirth),
                                         FullName = p.Person.FullName
                                     }).ToList(),
                        TotalPatientsCount = this.db.Patients.Count(p => p.DoctorId == this.Doctor.Id)
                    };

            // The view must know about the patients limit.
            this.ViewBag.PatientsLimit = this.DbPractice.AccountContract.PatientsLimit;
            this.ViewBag.PatientsCount = this.db.Patients.Count(p => p.PracticeId == this.DbPractice.Id);

            return this.View(model);
        }

        public ActionResult Details(int id)
        {
            var patient = this.db.Patients.SingleOrDefault(p => p.Id == id && p.DoctorId == this.Doctor.Id);

            if (patient == null)
                return new StatusCodeResult(HttpStatusCode.NotFound, "Patient not found.");

            // Only the doctor and the patient can see the medical records.
            var canAccessMedicalRecords = this.DbUser.Id == patient.Doctor.Users.Single().Id;
            this.ViewBag.CanAccessMedicalRecords = canAccessMedicalRecords;

            // Creating the view-model object.
            var model = GetViewModel(this, patient, canAccessMedicalRecords, true);

            this.ViewBag.RecordDate = this.GetPracticeLocalNow().Date;

            return this.View(model);
        }

        [HttpGet]
        public ActionResult Create()
        {
            return this.Edit((int?)null);
        }

        [HttpPost]
        public ActionResult Create(PatientViewModel viewModel)
        {
            return this.Edit(viewModel);
        }

        [HttpGet]
        public ActionResult Edit(int? id)
        {
            PatientViewModel viewModel = null;

            if (id.HasValue)
            {
                // editing an existing patient
                var patient = this.db.Patients.First(p => p.Id == id);
                viewModel = GetViewModel(this, patient, false, false);

                ViewBag.Title = "Alterando paciente: " + viewModel.FullName;
            }
            else
            {
                viewModel = new PatientViewModel();
                // the suggested next patient code for the practice. Patient code is not unique for each doctor, it's unique for each each practice
                var currentLastCode = this.db.Patients.Max(p => p.Code);
                if (!currentLastCode.HasValue)
                    currentLastCode = 0;
                viewModel.Code = (currentLastCode + 1).Value.ToString("D6");

                // if this account has a patient limit, then we should tell the user
                var patientLimit = this.DbPractice.AccountContract.PatientsLimit;

                if (patientLimit != null)
                {
                    var patientCount = this.db.Patients.Count(p => p.PracticeId == this.DbPractice.Id);
                    if (patientCount + 1 > patientLimit)
                    {
                        this.ModelState.Clear();
                        this.ModelState.AddModelError(
                            "PatientsLimit",
                            "Não é possível adicionar mais pacientes, pois já foi atingido o limite de {0} pacientes de sua conta.",
                            patientLimit);
                    }
                }

                // adding new patient
                this.ViewBag.Title = "Novo paciente";
            }

            return View("Edit", viewModel);
        }

        [HttpPost]
        public ActionResult Edit(PatientViewModel formModel)
        {
            // if this account has a patient limit, then we should tell the user if he/she blows up the limit
            var patientLimit = this.DbPractice.AccountContract.PatientsLimit;

            // verify patient limit
            if (patientLimit != null)
            {
                var patientCount = this.db.Patients.Count(p => p.PracticeId == this.DbPractice.Id);
                if (patientCount + 1 > patientLimit)
                {
                    this.ModelState.Clear();
                    this.ModelState.AddModelError(
                        "PatientsLimit",
                        "Não é possível adicionar mais pacientes, pois já foi atingido o limite de {0} pacientes de sua conta.",
                        patientLimit);
                }
            }

            // Verify if the patient code is valid
            if (formModel.Code != null)
            {
                var patientCodeAsInt = default(int);
                int.TryParse(formModel.Code, out patientCodeAsInt);
                if (patientCodeAsInt != default(int) && this.db.Patients.Any(p => p.Code == patientCodeAsInt))
                    this.ModelState.AddModelError<PatientViewModel>(
                        model => model.Code, "O código do paciente informado pertence a outro paciente");
            }

            if (ModelState.IsValid)
            {
                var isEditing = formModel.Id != null;

                Patient patient;
                if (isEditing)
                    patient = this.db.Patients.First(p => p.Id == formModel.Id);
                else
                {
                    patient = new Patient
                                  {
                                      Person = new Person { PracticeId = this.DbUser.PracticeId, },
                                      PracticeId = this.DbUser.PracticeId,
                                  };
                    this.db.Patients.AddObject(patient);
                }

                patient.Doctor = this.Doctor;

                patient.IsBackedUp = false;
                Debug.Assert(formModel.Code != null, "formModel.Code != null");
                patient.Code = int.Parse(formModel.Code);
                patient.Person.BirthPlace = formModel.BirthPlace;
                patient.Person.CPF = formModel.Cpf;
                patient.Person.RG = formModel.Rg;
                patient.Person.Ethnicity = (int?)formModel.Ethnicity;
                patient.Person.Schooling = (int?)formModel.Schooling;
                patient.Person.FatherName = formModel.FatherName;
                patient.Person.FatherProfession = formModel.FatherProfession;
                patient.Person.MotherName = formModel.MotherName;
                patient.Person.MotherProfession = formModel.MotherProfession;
                patient.Person.CreatedOn = this.GetUtcNow();
                Debug.Assert(formModel.DateOfBirth != null, "formModel.DateOfBirth != null");
                patient.Person.DateOfBirth = ConvertToUtcDateTime(this.DbPractice, formModel.DateOfBirth.Value);
                patient.Person.FullName = formModel.FullName;
                patient.Person.Gender = (short)formModel.Gender;
                patient.Person.MaritalStatus = formModel.MaritalStatus;
                patient.Person.Observations = formModel.Observations;
                patient.Person.Profession = formModel.Profissao;
                patient.Person.Email = !string.IsNullOrEmpty(formModel.Email) ? formModel.Email.ToLower() : null;
                patient.Person.EmailGravatarHash = GravatarHelper.GetGravatarHash(formModel.Email);
                patient.Person.PhoneLand = formModel.PhoneLand;
                patient.Person.PhoneCell = formModel.PhoneCell;

                // handle patient address
                if (!patient.Person.Addresses.Any())
                    patient.Person.Addresses.Add(new Address { PracticeId = this.DbUser.PracticeId });

                var patientAddress = patient.Person.Addresses.First();
                patientAddress.CEP = formModel.Address.CEP;
                patientAddress.City = formModel.Address.City;
                patientAddress.Complement = formModel.Address.Complement;
                patientAddress.Neighborhood = formModel.Address.Neighborhood;
                patientAddress.StateProvince = formModel.Address.StateProvince;
                patientAddress.Street = formModel.Address.Street;

                this.db.SaveChanges();
                return this.RedirectToAction("Details", new { id = patient.Id });
            }

            return this.View("Edit", formModel);
        }

        /// <summary>
        /// Deletes a patient
        /// </summary>
        /// <remarks>
        /// Requiriments:
        ///     - Should delete the patient along with the following associations:
        ///         - Anamneses
        /// </remarks>
        [HttpGet]
        public JsonResult Delete(int id)
        {
            try
            {
                var patient = this.db.Patients.First(m => m.Id == id);

                // delete anamneses manually
                var anamneses = patient.Anamneses.ToList();
                while (anamneses.Any())
                {
                    var anamnese = anamneses.First();
                    this.db.Anamnese.DeleteObject(anamnese);
                    anamneses.Remove(anamnese);
                }

                // delete diagnostic hipotheses manually
                var diagnosticHypotheses = patient.DiagnosticHypotheses.ToList();
                while (diagnosticHypotheses.Any())
                {
                    var diagnosticHypothesis = diagnosticHypotheses.First();
                    this.db.DiagnosticHypotheses.DeleteObject(diagnosticHypothesis);
                    diagnosticHypotheses.Remove(diagnosticHypothesis);
                }

                // delete receipts manually
                var receipts = patient.Receipts.ToList();
                while (receipts.Any())
                {
                    var receipt = receipts.First();
                    this.db.Receipts.DeleteObject(receipt);
                    receipts.Remove(receipt);
                }

                // delete physical exams requests manually
                var physicalExams = patient.PhysicalExaminations.ToList();
                while (physicalExams.Any())
                {
                    var physicalExam = physicalExams.First();
                    this.db.PhysicalExaminations.DeleteObject(physicalExam);
                    physicalExams.Remove(physicalExam);
                }

                // delete exam requests manually
                var examRequests = patient.ExaminationRequests.ToList();
                while (examRequests.Any())
                {
                    var examRequest = examRequests.First();
                    this.db.ExaminationRequests.DeleteObject(examRequest);
                    examRequests.Remove(examRequest);
                }

                // delete exam results manually
                var examResults = patient.ExaminationResults.ToList();
                while (examResults.Any())
                {
                    var examResult = examResults.First();
                    this.db.ExaminationResults.DeleteObject(examResult);
                    examResults.Remove(examResult);
                }

                // delete medical certificates manually
                var certificates = patient.MedicalCertificates.ToList();
                while (certificates.Any())
                {
                    var certificate = certificates.First();

                    // deletes fields within the certificate manually
                    while (certificate.Fields.Any())
                    {
                        var field = certificate.Fields.First();
                        this.db.MedicalCertificateFields.DeleteObject(field);
                    }

                    this.db.MedicalCertificates.DeleteObject(certificate);
                    certificates.Remove(certificate);
                }

                // delete diagnosis manually
                var diagnosis = patient.Diagnoses.ToList();
                while (diagnosis.Any())
                {
                    var diag = diagnosis.First();
                    this.db.Diagnoses.DeleteObject(diag);
                    diagnosis.Remove(diag);
                }

                // delete appointments manually
                var appointments = patient.Appointments.ToList();
                while (appointments.Any())
                {
                    var appointment = appointments.First();
                    this.db.Appointments.DeleteObject(appointment);
                    appointments.Remove(appointment);
                }

                // delete files manually
                var patientFiles = patient.PatientFiles.ToList();
                while (patientFiles.Any())
                {
                    var patientFile = patientFiles.First();
                    var file = patientFile.FileMetadata;

                    var storage = new WindowsAzureBlobStorageManager();
                    storage.DeleteFileFromStorage(Constants.AZURE_STORAGE_PATIENT_FILES_CONTAINER_NAME, file.SourceFileName);

                    this.db.PatientFiles.DeleteObject(patientFile);
                    this.db.FileMetadatas.DeleteObject(file);

                    patientFiles.Remove(patientFile);
                }

                // delete file groups manually
                var patientFileGroups = patient.PatientFileGroups.ToList();
                while (patientFileGroups.Any())
                {
                    var patientFileGroup = patientFileGroups.First();
                    this.db.PatientFileGroups.DeleteObject(patientFileGroup);
                    patientFileGroups.Remove(patientFileGroup);
                }

                this.db.Patients.DeleteObject(patient);
                this.db.SaveChanges();
                return this.Json(new JsonDeleteMessage { success = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return this.Json(new JsonDeleteMessage { success = false, text = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult LookupPatients(string term, int pageSize, int? pageIndex)
        {
            if (pageIndex == null)
                throw new ArgumentNullException("pageIndex");

            var baseQuery = this.db.Patients.Include("Person").Where(l => l.DoctorId == this.Doctor.Id);
            if (!string.IsNullOrEmpty(term))
                baseQuery = baseQuery.Where(l => l.Person.FullName.Contains(term));

            var rows = (from p in baseQuery.OrderBy(p => p.Person.FullName).Skip((pageIndex.Value - 1) * pageSize).Take(pageSize).ToList()
                        select new
                        {
                            Id = p.Id,
                            Value = p.Person.FullName,
                            InsuranceId = p.LastUsedHealthInsuranceId,
                            InsuranceName = this.db.HealthInsurances.Where(hi => hi.Id == p.LastUsedHealthInsuranceId).Select(hi => hi.Name),
                        }).ToList();

            var result = new AutocompleteJsonResult()
            {
                Rows = new System.Collections.ArrayList(rows),
                Count = baseQuery.Count()
            };

            return this.Json(result, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Searchs for patients
        /// </summary>
        /// <remarks>
        /// Requirements:
        ///     - Should return a slice of the existing records matching the criteria corresponding to the specified 'Page' 
        ///     - Should return a result correspoding to all records when no search term is provided (respecting pagination)
        ///     - In the result, the 'Count' property should consider all records
        ///     - In the result, the list should bring only results corresponding to the specified page
        /// </remarks>
        public ActionResult Search(SearchModel searchModel)
        {
            var model = new SearchViewModel<PatientViewModel>();

            var query = from patient in this.db.Patients.Include("Person")
                        where patient.DoctorId == this.Doctor.Id
                        select patient;

            if (!string.IsNullOrEmpty(searchModel.Term))
                query = from patient in query where patient.Person.FullName.Contains(searchModel.Term) select patient;

            // 1-based page index
            var pageIndex = searchModel.Page;
            var pageSize = Constants.GRID_PAGE_SIZE;

            model.Count = query.Count();
            model.Objects = (from p in query
                             select new PatientViewModel()
                             {
                                 Id = p.Id,
                                 // Note: this date is coming from the DB in Utc format, and must be converted to local time.
                                 DateOfBirth = p.Person.DateOfBirth,
                                 FullName = p.Person.FullName
                             })
                             .OrderBy(p => p.FullName)
                             .Skip((pageIndex - 1) * pageSize)
                             .Take(pageSize)
                             .ToList();

            // Converting all dates from Utc to local practice time-zone.
            foreach (var eachPatientViewModel in model.Objects)
                eachPatientViewModel.DateOfBirth = ConvertToLocalDateTime(this.DbPractice, eachPatientViewModel.DateOfBirth);

            return this.View(model);
        }

        [SelfPermission]
        public ActionResult AddMedicalRecords(int id, int? y, int? m, int? d)
        {
            var patient = this.db.Patients.SingleOrDefault(p => p.Id == id && p.DoctorId == this.Doctor.Id);

            if (patient == null)
                return new StatusCodeResult(HttpStatusCode.NotFound, "Patient not found");

            // Only the doctor and the patient can see the medical records.
            var canAccessMedicalRecords = this.DbUser.Id == patient.Doctor.Users.Single().Id;
            this.ViewBag.CanAccessMedicalRecords = canAccessMedicalRecords;

            var localDateFilter = DateTimeHelper.CreateDate(y, m, d) ?? this.GetPracticeLocalNow().Date;
            var utcDateFilter = ConvertToUtcDateTime(this.DbPractice, localDateFilter);

            // Creating the view-model object.
            var model = GetViewModel(this, patient, false, false, false);
            model.Sessions = GetSessionViewModels(this.DbPractice, patient, DateTimeInterval.FromDateAndDays(utcDateFilter, 1));

            this.ViewBag.RecordDate = localDateFilter;

            return this.View(model);
        }

        [HttpGet]
        public ActionResult GetDatesWithMedicalRecords(int patientId, int year, int month)
        {
            var localFirst = new DateTime(year, month, 1);
            var localLast = localFirst.AddMonths(1);

            var patient = this.db.Patients.SingleOrDefault(p => p.Id == patientId);

            if (patient == null)
                return new StatusCodeResult(HttpStatusCode.NotFound);

            var utcFirst = ConvertToUtcDateTime(this.DbPractice, localFirst);
            var utcLast = ConvertToUtcDateTime(this.DbPractice, localLast);

            var result = GetSessionViewModels(this.DbPractice, patient, new DateTimeInterval(utcFirst, utcLast))
                .Select(s => s.Date.ToString("'d'dd_MM_yyyy"))
                .Distinct().ToArray();

            return this.Json(result, JsonRequestBehavior.AllowGet);
        }
    }
}
