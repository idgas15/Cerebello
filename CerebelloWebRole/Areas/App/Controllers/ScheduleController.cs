﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using Cerebello.Model;
using CerebelloWebRole.Areas.App.Models;
using CerebelloWebRole.Code;

namespace CerebelloWebRole.Areas.App.Controllers
{
    public class ScheduleController : DoctorController
    {
        public JsonResult GetAppointments(int start, int end)
        {
            DateTime originUtc = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            var startAsDateTimeUtc = originUtc.AddSeconds(start);
            var endAsDateTimeUtc = originUtc.AddSeconds(end);

            var appointments =
                this.db.Appointments
                .Include("Patient")
                .Include("Patient.Person")
                .Where(a => a.DoctorId == this.Doctor.Id)
                .Where(a => a.Start >= startAsDateTimeUtc && a.End <= endAsDateTimeUtc)
                .ToList();

            return this.Json((from a in appointments
                              select new ScheduleEventViewModel()
                              {
                                  id = a.Id,
                                  start = ConvertToLocalDateTime(this.DbPractice, a.Start).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                  end = ConvertToLocalDateTime(this.DbPractice, a.End).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                  title = GetAppointmentText(a),
                                  className = GetAppointmentClass(a),
                              }).ToList(), JsonRequestBehavior.AllowGet);
        }

        public static string GetAppointmentText(Appointment a)
        {
            string result = null;
            switch ((TypeAppointment)a.Type)
            {
                case TypeAppointment.GenericAppointment:
                    result = a.Description;
                    break;
                case TypeAppointment.MedicalAppointment:
                    result = a.Patient.Person.FullName;
                    break;
                default:
                    throw new Exception("Unsupported appointment type.");
            }
            if (string.IsNullOrEmpty(result))
                throw new Exception("The appointment text cannot be null. This will trigger a client-side exception");

            return result;
        }

        private static string GetAppointmentClass(Appointment a)
        {
            var classes = new List<string>();
            if (a.Status == (int)TypeAppointmentStatus.NotAccomplished)
                classes.Add("not-accomplished");

            switch ((TypeAppointmentStatus)a.Status)
            {
                case TypeAppointmentStatus.Accomplished:
                    classes.Add("accomplished");
                    break;
                case TypeAppointmentStatus.NotAccomplished:
                    classes.Add("not-accomplished");
                    break;
                case TypeAppointmentStatus.Discarded:
                case TypeAppointmentStatus.Undefined:
                    break;
                default:
                    throw new Exception("Unsupported appointment status");
            }

            switch ((TypeAppointment)a.Type)
            {
                case TypeAppointment.GenericAppointment:
                    classes.Add("generic-appointment");
                    break;
                case TypeAppointment.MedicalAppointment:
                    classes.Add("medical-appointment");
                    break;
                default:
                    throw new Exception("Unsupported appointment type.");
            }

            return string.Join(" ", classes);
        }

        [CanAlternateUser]
        public ActionResult Index()
        {
            // verify min and max times
            List<string> minTimes = new List<string>();
            List<string> maxTimes = new List<string>();

            // If schedule of the doctor is missing, we must inform the user that the schedule must be configured before using the software.
            if (this.Doctor.CFG_Schedule == null)
            {
                return RedirectToAction("MissingConfigurations");
            }

            if (this.Doctor.CFG_Schedule.Sunday)
            {
                minTimes.Add(this.Doctor.CFG_Schedule.SundayWorkdayStartTime);
                maxTimes.Add(this.Doctor.CFG_Schedule.SundayWorkdayEndTime);
            }
            if (this.Doctor.CFG_Schedule.Monday)
            {
                minTimes.Add(this.Doctor.CFG_Schedule.MondayWorkdayStartTime);
                maxTimes.Add(this.Doctor.CFG_Schedule.MondayWorkdayEndTime);
            }
            if (this.Doctor.CFG_Schedule.Tuesday)
            {
                minTimes.Add(this.Doctor.CFG_Schedule.TuesdayWorkdayStartTime);
                maxTimes.Add(this.Doctor.CFG_Schedule.TuesdayWorkdayEndTime);
            }
            if (this.Doctor.CFG_Schedule.Wednesday)
            {
                minTimes.Add(this.Doctor.CFG_Schedule.WednesdayWorkdayStartTime);
                maxTimes.Add(this.Doctor.CFG_Schedule.WednesdayWorkdayEndTime);
            }
            if (this.Doctor.CFG_Schedule.Thursday)
            {
                minTimes.Add(this.Doctor.CFG_Schedule.ThursdayWorkdayStartTime);
                maxTimes.Add(this.Doctor.CFG_Schedule.ThursdayWorkdayEndTime);
            }
            if (this.Doctor.CFG_Schedule.Friday)
            {
                minTimes.Add(this.Doctor.CFG_Schedule.FridayWorkdayStartTime);
                maxTimes.Add(this.Doctor.CFG_Schedule.FridayWorkdayEndTime);
            }
            if (this.Doctor.CFG_Schedule.Saturday)
            {
                minTimes.Add(this.Doctor.CFG_Schedule.SaturdayWorkdayStartTime);
                maxTimes.Add(this.Doctor.CFG_Schedule.SaturdayWorkdayEndTime);
            }

            var minMinTime = minTimes.Min();
            var maxMaxTime = maxTimes.Max();

            var viewModel = new ScheduleViewModel
                {
                    SlotMinutes = this.Doctor.CFG_Schedule.AppointmentTime,
                    MinTime = minMinTime,
                    MaxTime = maxMaxTime,
                    Weekends = this.Doctor.CFG_Schedule.Saturday || this.Doctor.CFG_Schedule.Sunday,
                    DoctorId = this.Doctor.Id
                };

            return View(viewModel);
        }

        [HttpGet]
        public ActionResult Create(DateTime? date, string start, string end, int? patientId, bool? findNextAvailable)
        {
            // Note: remember that all DataTime parameter that comes from the client, are expressed in local practice time-zone.

            var localNow = this.GetPracticeLocalNow();

            if (date != null)
            {
                // The behavior for 'start' and 'end' parameters are different
                // depending on 'findNextAvailable' param, when 'date' is something:
                // - case false: start must have a valid time value, end is optional.
                if (findNextAvailable == true)
                {
                    // When 'start' is not specified, we set it to the begining of the day.
                    if (string.IsNullOrEmpty(start))
                        start = "00:00";
                }
                else if (string.IsNullOrEmpty(start))
                {
                    // If date has something, then start and end must also be null or empty.
                    this.ModelState.AddModelError<AppointmentViewModel>(
                        m => m.LocalDateTime,
                        "Ocorreu um erro nos parâmetros desta página.");

                    return this.View("Edit", new AppointmentViewModel());
                }
            }
            else
            {
                date = localNow;
                if (string.IsNullOrEmpty(start))
                    start = localNow.ToString("HH:mm");
            }

            var localDateAlone = date.Value.Date;

            //var slots = GetDaySlots(dateOnly, this.Doctor);
            var slotDuration = TimeSpan.FromMinutes(this.Doctor.CFG_Schedule.AppointmentTime);

            // Getting start date and time.
            var localStartTime =
                string.IsNullOrEmpty(start) ?
                localDateAlone :
                localDateAlone + DateTimeHelper.GetTimeSpan(start);

            // todo: just delete code or find a place for it?
            //FindNearestSlotStartTime(ref start, slots, ref startTime);

            // Getting end date and time.
            var localEndTime =
                string.IsNullOrEmpty(end) ?
                localStartTime + slotDuration :
                localDateAlone + DateTimeHelper.GetTimeSpan(end);

            if (localEndTime - localStartTime < slotDuration)
                localEndTime = localStartTime + slotDuration;

            // todo: just delete code or find a place for it?
            //FindNearestSlotEndTime(ref end, slots, ref endTime);

            // Find next available time slot.
            if (findNextAvailable == true)
            {
                var doctor = this.Doctor;

                // Determining the date and time to start scanning for a free time slot.
                var localStartingFrom = localStartTime;

                if (localNow > localStartingFrom)
                    localStartingFrom = localNow;

                // Finding the next available time slot, and setting the startTime and endTime.
                var slot = FindNextFreeTimeInPracticeLocalTime(this.db, doctor, localStartingFrom);
                localStartTime = slot.Item1;
                localEndTime = slot.Item2;
            }

            localDateAlone = localStartTime.Date;
            start = localStartTime.ToString("HH:mm");
            end = localEndTime.ToString("HH:mm");

            // Creating viewmodel.
            var viewModel = new AppointmentViewModel();

            var patient = this.db.Patients
                .Where(p => p.Id == patientId)
                .Select(p => new { p.Code, p.Person.FullName, p.LastUsedHealthInsuranceId })
                .FirstOrDefault();

            if (patient != null)
            {
                viewModel.PatientCode = patient.Code.HasValue ? patient.Code.Value.ToString("D6") : "000000";
                viewModel.PatientNameLookup = patient.FullName;
                viewModel.HealthInsuranceId = patient.LastUsedHealthInsuranceId;
                viewModel.HealthInsuranceName = this.db.HealthInsurances
                    .Where(hi => hi.Id == patient.LastUsedHealthInsuranceId)
                    .Select(hi => hi.Name)
                    .SingleOrDefault();
                viewModel.PatientId = patientId;
            }
            else
            {
                var currentLastCode = this.db.Patients.Max(p => p.Code);
                if (!currentLastCode.HasValue)
                    currentLastCode = 0;
                viewModel.PatientCode = (currentLastCode + 1).Value.ToString("D6");
            }

            viewModel.LocalDateTime = localDateAlone;
            viewModel.Start = start;
            viewModel.End = end;
            viewModel.DoctorId = this.Doctor.Id;
            viewModel.LocalDateTimeSpelled =
                DateTimeHelper.GetDayOfWeekAsString(localDateAlone) + ", "
                + DateTimeHelper.ConvertToRelative(
                    localDateAlone,
                    localNow,
                    DateTimeHelper.RelativeDateOptions.IncludePrefixes
                    | DateTimeHelper.RelativeDateOptions.IncludeSuffixes
                    | DateTimeHelper.RelativeDateOptions.ReplaceToday
                    | DateTimeHelper.RelativeDateOptions.ReplaceYesterdayAndTomorrow);

            this.DoDateAndTimeValidation(viewModel, localNow, null);

            this.ModelState.Clear();

            this.ViewBag.IsEditingOrCreating = 'C';

            return this.View("Edit", viewModel);
        }

        [Obsolete("This method is not used anywhere.", error: true)]
        private static void FindNearestSlotEndTime(ref string end, List<Tuple<DateTime, DateTime>> slots, ref DateTime endTime)
        {
            // slots.Min() dispara exceção quando slot é vazio. É necessário verificar
            if (slots != null && slots.Any())
            {
                var endTime2 = endTime;
                var min = slots.Min(s => (s.Item2 > endTime2 ? s.Item2 - endTime2 : endTime2 - s.Item2));
                var findMin = slots.First(s => (s.Item2 > endTime2 ? s.Item2 - endTime2 : endTime2 - s.Item2) == min);
                endTime = findMin.Item2;
            }

            end = endTime.ToString("HH:mm");
        }

        [Obsolete("This method is not used anywhere.", error: true)]
        private static void FindNearestSlotStartTime(ref string start, List<Tuple<DateTime, DateTime>> slots, ref DateTime startTime)
        {
            // slots.Min() dispara exceção quando slot é vazio. É necessário verificar
            if (slots != null && slots.Any())
            {
                var startTime2 = startTime;
                var min = slots.Min(s => (s.Item1 > startTime2 ? s.Item1 - startTime2 : startTime2 - s.Item1));
                var findMin = slots.First(s => (s.Item1 > startTime2 ? s.Item1 - startTime2 : startTime2 - s.Item1) == min);
                startTime = findMin.Item1;
            }

            start = startTime.ToString("HH:mm");
        }

        [HttpPost]
        public ActionResult Create(AppointmentViewModel formModel)
        {
            return this.Edit(formModel);
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var currentUserPracticeId = this.DbUser.PracticeId;

            var appointment = this.db.Appointments
                .Where(a => a.Id == id).FirstOrDefault(a => a.Doctor.Users.FirstOrDefault().PracticeId == currentUserPracticeId);

            if (appointment == null)
            {
                // todo: return new HttpNotFoundResult("A consulta/compromisso não existe.");
                return this.View("NotFound");
            }

            var localNow = this.GetPracticeLocalNow();

            var appointmentLocalStart = ConvertToLocalDateTime(this.DbPractice, appointment.Start);
            var appointmentLocalEnd = ConvertToLocalDateTime(this.DbPractice, appointment.End);

            var viewModel = new AppointmentViewModel()
                {
                    Id = appointment.Id,
                    LocalDateTime = appointmentLocalStart.Date,
                    Start = appointmentLocalStart.ToString("HH:mm"),
                    End = appointmentLocalEnd.ToString("HH:mm"),
                    DoctorId = appointment.DoctorId,
                    LocalDateTimeSpelled = DateTimeHelper.GetDayOfWeekAsString(appointmentLocalStart.Date) + ", "
                        + DateTimeHelper.ConvertToRelative(
                            appointmentLocalStart.Date,
                            localNow,
                            DateTimeHelper.RelativeDateOptions.IncludePrefixes
                                | DateTimeHelper.RelativeDateOptions.IncludeSuffixes
                                | DateTimeHelper.RelativeDateOptions.ReplaceToday
                                | DateTimeHelper.RelativeDateOptions.ReplaceYesterdayAndTomorrow),
                    HealthInsuranceId = appointment.HealthInsuranceId,
                    HealthInsuranceName = this.db.HealthInsurances
                        .Where(hi => hi.Id == appointment.HealthInsuranceId)
                        .Select(hi => hi.Name)
                        .SingleOrDefault(),
                    Status = appointment.Status,
                };

            DoDateAndTimeValidation(viewModel, localNow, id);

            switch ((TypeAppointment)appointment.Type)
            {
                case TypeAppointment.GenericAppointment:
                    viewModel.IsGenericAppointment = true;
                    viewModel.Description = appointment.Description;
                    break;
                case TypeAppointment.MedicalAppointment:
                    viewModel.IsGenericAppointment = false;
                    viewModel.PatientNameLookup = appointment.Patient.Person.FullName;
                    viewModel.PatientId = appointment.PatientId;
                    break;
                default:
                    throw new Exception("Unsupported appointment type.");
            }

            this.ViewBag.IsEditingOrCreating = 'E';

            return this.View("Edit", viewModel);
        }

        [HttpPost]
        public ActionResult Edit(AppointmentViewModel formModel)
        {
            // Custom model validation.
            if (formModel.IsGenericAppointment)
            {
                // This is a generic appointment, so we must clear validation for patient.
                this.ModelState.ClearPropertyErrors(() => formModel.PatientId);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientCode);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientName);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientNameLookup);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientGender);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientFirstAppointment);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientEmail);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientDateOfBirth);
                this.ModelState.ClearPropertyErrors(() => formModel.HealthInsuranceId);
                this.ModelState.ClearPropertyErrors(() => formModel.HealthInsuranceName);
            }
            else if (formModel.PatientFirstAppointment)
            {
                // This is a medical appointment, so we must clear validation for generic appointment.
                this.ModelState.ClearPropertyErrors(() => formModel.Description);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientId);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientNameLookup);
            }
            else
            {
                // This is a medical appointment, so we must clear validation for generic appointment.
                this.ModelState.ClearPropertyErrors(() => formModel.Description);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientCode);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientName);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientGender);
                this.ModelState.ClearPropertyErrors(() => formModel.PatientDateOfBirth);

                if (formModel.PatientId != null)
                {
                    var patient = this.db.Patients.FirstOrDefault(p => p.Id == formModel.PatientId);

                    if (patient == null)
                    {
                        this.ModelState.AddModelError<AppointmentViewModel>(
                            model => model.PatientNameLookup,
                            "O paciente informado não foi encontrado no banco de dados");
                    }
                    else if (patient.Person.FullName != formModel.PatientNameLookup)
                    {
                        this.ModelState.AddModelError<AppointmentViewModel>(
                            model => model.PatientNameLookup,
                            "O paciente informado foi encontrado mas o nome não coincide");
                    }
                }
            }

            // Verify if appoitment hours are consistent
            {
                DoDateAndTimeValidation(formModel, this.GetPracticeLocalNow(), formModel.Id);
            }

            // Verify if we're creating an appointment for the future with a Status set
            if (!string.IsNullOrEmpty(formModel.Start))
            {
                if (formModel.LocalDateTime + DateTimeHelper.GetTimeSpan(formModel.Start) > this.GetPracticeLocalNow())
                    if (formModel.Status != (int)TypeAppointmentStatus.Undefined)
                        ModelState.AddModelError<AppointmentViewModel>(
                            model => model.Status,
                            "Não é permitido determinar o Status para consultas agendadas para o futuro");
            }

            // Verify if the patient code is valid
            if (formModel.PatientFirstAppointment && formModel.PatientCode != null)
            {
                var patientCodeAsInt = default(int);
                int.TryParse(formModel.PatientCode, out patientCodeAsInt);
                if (patientCodeAsInt != default(int) && this.db.Patients.Any(p => p.Code == patientCodeAsInt))
                    this.ModelState.AddModelError<AppointmentViewModel>(
                        model => model.PatientCode, "O código do paciente informado pertence a outro paciente");
            }

            // Saving data if model is valid.
            if (this.ModelState.IsValid)
            {
                // Creating the appointment.
                Appointment appointment = null;

                if (formModel.Id == null)
                {
                    appointment = new Appointment
                        {
                            PracticeId = this.DbUser.PracticeId,
                            CreatedOn = this.UtcNowGetter(),
                            DoctorId = formModel.DoctorId,
                            CreatedById = this.DbUser.Id,
                        };
                    this.db.Appointments.AddObject(appointment);
                }
                else
                {
                    var currentUserPracticeId = this.DbUser.PracticeId;

                    appointment = this.db.Appointments
                        .Where(a => a.Id == formModel.Id).FirstOrDefault(a => a.Doctor.Users.FirstOrDefault().PracticeId == currentUserPracticeId);

                    // If the appointment does not exist, or does not belongs to the current practice,
                    // it should go to a view indicating that.
                    if (appointment == null)
                        return View("NotFound", formModel);
                }

                var appointmentStart = ConvertToUtcDateTime(this.DbPractice,
                    formModel.LocalDateTime + DateTimeHelper.GetTimeSpan(formModel.Start));

                var appointmentEnd = ConvertToUtcDateTime(this.DbPractice,
                    formModel.LocalDateTime + DateTimeHelper.GetTimeSpan(formModel.End));

                if (appointment.Start != appointmentStart)
                    appointment.Notified = false;

                appointment.Start = appointmentStart;
                appointment.End = appointmentEnd;

                appointment.Status = (int)formModel.Status;

                // Setting the appointment type and associated properties.
                // - generic appointment: has description, date and time interval
                // - medical appointment: has patient, date and time interval
                if (formModel.IsGenericAppointment)
                {
                    appointment.Description = formModel.Description;
                    appointment.Type = (int)TypeAppointment.GenericAppointment;
                }
                else
                {
                    appointment.Type = (int)TypeAppointment.MedicalAppointment;
                    appointment.HealthInsuranceId = formModel.HealthInsuranceId;

                    if (formModel.PatientFirstAppointment)
                    {
                        appointment.Patient = new Patient
                            {
                                Person =
                                    new Person
                                        {
                                            FullName = formModel.PatientName,
                                            Gender = (short)formModel.PatientGender,
                                            DateOfBirth =
                                                ConvertToUtcDateTime(this.DbPractice, formModel.PatientDateOfBirth.Value),
                                            PhoneCell = formModel.PatientPhoneCell,
                                            PhoneLand = formModel.PatientPhoneLand,
                                            CreatedOn = this.GetUtcNow(),
                                            PracticeId = this.DbUser.PracticeId,
                                            Email = formModel.PatientEmail,
                                            EmailGravatarHash = GravatarHelper.GetGravatarHash(formModel.PatientEmail)
                                        },
                                Code = int.Parse(formModel.PatientCode),
                                LastUsedHealthInsuranceId = formModel.HealthInsuranceId,
                                Doctor = this.Doctor,
                                PracticeId = this.DbUser.PracticeId,
                            };
                    }
                    else
                    {
                        var patient = this.db.Patients.Single(p => p.Id == formModel.PatientId);
                        patient.LastUsedHealthInsuranceId = formModel.HealthInsuranceId;
                        appointment.PatientId = formModel.PatientId.Value;
                    }
                }

                // Returning a JSON result, indicating what has happened.
                try
                {
                    this.db.SaveChanges();
                    return this.Json((dynamic)new { status = "success" }, JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    return this.Json((dynamic)new
                    {
                        status = "error",
                        text = "Não foi possível salvar a consulta. Erro inexperado",
                        details = ex.Message
                    }, JsonRequestBehavior.AllowGet);
                }
            }

            this.ViewBag.IsEditingOrCreating = this.RouteData.Values["action"].ToString()
                .ToLowerInvariant() == "edit" ? 'E' : 'C';

            return this.View("Edit", formModel);
        }

        /// <summary>
        /// Deletes an appointment
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public JsonResult Delete(int id)
        {
            var appointment = this.db.Appointments.First(m => m.Id == id);
            try
            {
                this.db.Appointments.DeleteObject(appointment);
                this.db.SaveChanges();
                return this.Json(new { success = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return this.Json(new { success = false, text = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult MissingConfigurations()
        {
            return View();
        }

        /// <summary>
        /// Returns the series of all day slots ordered.
        /// </summary>
        /// <param name="localDateTime"></param>
        /// <param name="doctor"></param>
        /// <returns></returns>
        private static IEnumerable<Tuple<DateTime, DateTime>> GetDaySlotsInLocalTime(DateTime localDateTime, Doctor doctor)
        {
            if (localDateTime.Kind != DateTimeKind.Unspecified)
                throw new ArgumentException("'localDateTime' must be expressed in local practice time-zone.", "localDateTime");

            string workdayStartTimeAsString = null;
            string workdayEndTimeAsString = null;
            string lunchStartTimeAsString = null;
            string lunchEndTimeAsString = null;

            if (doctor.CFG_Schedule == null)
                throw new Exception("You should have checked to see if 'doctor.CFG_Schedule' is null before calling this method.");

            switch (localDateTime.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    if (!doctor.CFG_Schedule.Sunday)
                        return new List<Tuple<DateTime, DateTime>>();
                    workdayStartTimeAsString = doctor.CFG_Schedule.SundayWorkdayStartTime;
                    workdayEndTimeAsString = doctor.CFG_Schedule.SundayWorkdayEndTime;
                    lunchStartTimeAsString = doctor.CFG_Schedule.SundayLunchStartTime;
                    lunchEndTimeAsString = doctor.CFG_Schedule.SundayLunchEndTime;
                    break;
                case DayOfWeek.Monday:
                    if (!doctor.CFG_Schedule.Monday)
                        return new List<Tuple<DateTime, DateTime>>();
                    workdayStartTimeAsString = doctor.CFG_Schedule.MondayWorkdayStartTime;
                    workdayEndTimeAsString = doctor.CFG_Schedule.MondayWorkdayEndTime;
                    lunchStartTimeAsString = doctor.CFG_Schedule.MondayLunchStartTime;
                    lunchEndTimeAsString = doctor.CFG_Schedule.MondayLunchEndTime;
                    break;
                case DayOfWeek.Tuesday:
                    if (!doctor.CFG_Schedule.Tuesday)
                        return new List<Tuple<DateTime, DateTime>>();
                    workdayStartTimeAsString = doctor.CFG_Schedule.TuesdayWorkdayStartTime;
                    workdayEndTimeAsString = doctor.CFG_Schedule.TuesdayWorkdayEndTime;
                    lunchStartTimeAsString = doctor.CFG_Schedule.TuesdayLunchStartTime;
                    lunchEndTimeAsString = doctor.CFG_Schedule.TuesdayLunchEndTime;
                    break;
                case DayOfWeek.Wednesday:
                    if (!doctor.CFG_Schedule.Wednesday)
                        return new List<Tuple<DateTime, DateTime>>();
                    workdayStartTimeAsString = doctor.CFG_Schedule.WednesdayWorkdayStartTime;
                    workdayEndTimeAsString = doctor.CFG_Schedule.WednesdayWorkdayEndTime;
                    lunchStartTimeAsString = doctor.CFG_Schedule.WednesdayLunchStartTime;
                    lunchEndTimeAsString = doctor.CFG_Schedule.WednesdayLunchEndTime;
                    break;
                case DayOfWeek.Thursday:
                    if (!doctor.CFG_Schedule.Thursday)
                        return new List<Tuple<DateTime, DateTime>>();
                    workdayStartTimeAsString = doctor.CFG_Schedule.ThursdayWorkdayStartTime;
                    workdayEndTimeAsString = doctor.CFG_Schedule.ThursdayWorkdayEndTime;
                    lunchStartTimeAsString = doctor.CFG_Schedule.ThursdayLunchStartTime;
                    lunchEndTimeAsString = doctor.CFG_Schedule.ThursdayLunchEndTime;
                    break;
                case DayOfWeek.Friday:
                    if (!doctor.CFG_Schedule.Friday)
                        return new List<Tuple<DateTime, DateTime>>();
                    workdayStartTimeAsString = doctor.CFG_Schedule.FridayWorkdayStartTime;
                    workdayEndTimeAsString = doctor.CFG_Schedule.FridayWorkdayEndTime;
                    lunchStartTimeAsString = doctor.CFG_Schedule.FridayLunchStartTime;
                    lunchEndTimeAsString = doctor.CFG_Schedule.FridayLunchEndTime;
                    break;
                case DayOfWeek.Saturday:
                    if (!doctor.CFG_Schedule.Saturday)
                        return new List<Tuple<DateTime, DateTime>>();
                    workdayStartTimeAsString = doctor.CFG_Schedule.SaturdayWorkdayStartTime;
                    workdayEndTimeAsString = doctor.CFG_Schedule.SaturdayWorkdayEndTime;
                    lunchStartTimeAsString = doctor.CFG_Schedule.SaturdayLunchStartTime;
                    lunchEndTimeAsString = doctor.CFG_Schedule.SaturdayLunchEndTime;
                    break;
            }

            DateTime todayBeginning = localDateTime.Date;
            var workdayStartTime = todayBeginning + DateTimeHelper.GetTimeSpan(workdayStartTimeAsString);
            var workdayEndTime = todayBeginning + DateTimeHelper.GetTimeSpan(workdayEndTimeAsString);
            var lunchStartTime = !string.IsNullOrEmpty(lunchStartTimeAsString) ? todayBeginning + DateTimeHelper.GetTimeSpan(lunchStartTimeAsString) : (DateTime?)null;
            var lunchEndTime = !string.IsNullOrEmpty(lunchEndTimeAsString) ? todayBeginning + DateTimeHelper.GetTimeSpan(lunchEndTimeAsString) : (DateTime?)null;

            // ok. Now with all the info we need, let' start building these slots

            var result = new List<Tuple<DateTime, DateTime>>();

            var time = workdayStartTime;
            var appointmentTimeSpan = new TimeSpan(0, doctor.CFG_Schedule.AppointmentTime, 0);

            while (true)
            {
                var timeEnd = time + appointmentTimeSpan;

                // the time computation depends on whether or not theres a lunch time configured

                // when there's lunch time
                if (lunchStartTime.HasValue)
                {
                    if ((time >= workdayStartTime && timeEnd <= lunchStartTime) || (time >= lunchEndTime && timeEnd <= workdayEndTime))
                    {
                        // in this case this span (time to timeEnd) is absolutely valid and we must add it to the slots
                        result.Add(new Tuple<DateTime, DateTime>(time, timeEnd));
                        time = time + appointmentTimeSpan;
                    }

                    else if (time >= workdayStartTime && timeEnd > lunchStartTime && timeEnd < workdayEndTime)
                    {
                        // this is an exception case in which the appointment would end in the middle of the lunch time
                        Debug.Assert(lunchEndTime != null, "lunchEndTime != null");

                        // the time is restarted after lunch
                        time = lunchEndTime.Value;
                    }
                    else
                        // in this case we're getting an inconsistent time and the time slots for the day are over
                        break;
                }
                // when there's no lunch time
                else
                {
                    if (time >= workdayStartTime && timeEnd <= workdayEndTime)
                    {
                        // in this case this span (time to timeEnd) is absolutely valid and we must add it to the slots
                        result.Add(new Tuple<DateTime, DateTime>(time, timeEnd));
                        time = time + appointmentTimeSpan;
                    }
                    // in this case we're getting an inconsistent time and the time slots for the day are over
                    else
                        break;
                }

            }

            return result;
        }

        [HttpGet]
        public JsonResult FindNextFreeTime(string date, string time)
        {
            var doctor = this.Doctor;

            var localNow = this.GetPracticeLocalNow();

            // Determining the date and time to start scanning for a free time slot.
            DateTime localStartingFrom = localNow;

            if (!string.IsNullOrEmpty(date))
            {
                // Note: as 'date' param is coming from the client, it is expressed in local practice time-zone.
                localStartingFrom = DateTime.Parse(date)
                    + (string.IsNullOrEmpty(time) ?
                        new TimeSpan(0, 0, 0) :
                        DateTimeHelper.GetTimeSpan(time));
            }

            if (localNow > localStartingFrom)
                localStartingFrom = localNow;

            var slot = this.FindNextFreeValidTimeInPracticeLocalTime(doctor, localNow, localStartingFrom);

            return this.Json(new
            {
                date = slot.Item1.ToString("dd/MM/yyyy"),
                start = slot.Item1.ToString("HH:mm"),
                end = slot.Item2.ToString("HH:mm"),
                dateSpelled = DateTimeHelper.GetDayOfWeekAsString(slot.Item1) + ", "
                + DateTimeHelper.ConvertToRelative(
                    slot.Item1,
                    localNow,
                    DateTimeHelper.RelativeDateOptions.IncludePrefixes
                    | DateTimeHelper.RelativeDateOptions.IncludeSuffixes
                    | DateTimeHelper.RelativeDateOptions.ReplaceToday
                    | DateTimeHelper.RelativeDateOptions.ReplaceYesterdayAndTomorrow)
            }, JsonRequestBehavior.AllowGet);
        }

        private Tuple<DateTime, DateTime> FindNextFreeValidTimeInPracticeLocalTime(Doctor doctor, DateTime localNow, DateTime localStartingFrom)
        {
            var localStartingFrom2 = localStartingFrom;

            while (true)
            {
                var slot = FindNextFreeTimeInPracticeLocalTime(this.db, doctor, localStartingFrom2);

                var vm = new AppointmentViewModel
                {
                    LocalDateTime = slot.Item1.Date,
                    Start = slot.Item1.ToString("HH:mm"),
                    End = slot.Item2.ToString("HH:mm"),
                };

                this.DoDateAndTimeValidation(vm, localNow, null);

                if (vm.DateAndTimeValidationState == DateAndTimeValidationState.Passed)
                {
                    return slot;
                }

                localStartingFrom2 = slot.Item2;
            }
        }

        /// <summary>
        /// Returns the next free time of a doctor, in local practice time-zone.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="doctor"></param>
        /// <param name="startingFromLocalTime">
        /// The date and time to start scanning for free times.
        /// This must be a time in the practice time-zone.
        /// </param>
        /// <returns></returns>
        public static Tuple<DateTime, DateTime> FindNextFreeTimeInPracticeLocalTime(
            CerebelloEntitiesAccessFilterWrapper db,
            Doctor doctor,
            DateTime startingFromLocalTime)
        {
            if (startingFromLocalTime.Kind != DateTimeKind.Unspecified)
                throw new ArgumentException("'startingFromLocalTime' must be expressed in local practice time-zone.", "startingFromLocalTime");

            var practice = doctor.Users.FirstOrDefault().Practice;

            var currentDateStartLocal = startingFromLocalTime.Date;
            while (true)
            {
                // getting a list of valid dates to look for slots
                // - days-off are invalid
                DateTime[] validLocalDates;
                {
                    var currentDateEndLocal = currentDateStartLocal.AddDays(30.0);

                    while (true)
                    {
                        var daysOffDates = db.CFG_DayOff
                            .Where(df => df.DoctorId == doctor.Id)
                            .Where(df => df.Date >= currentDateStartLocal && df.Date < currentDateEndLocal)
                            .Select(df => df.Date)
                            .ToArray();

                        validLocalDates = DateTimeHelper.Range(currentDateStartLocal, 30, d => d.AddDays(1.0)).ToArray();
                        validLocalDates = validLocalDates.Except(daysOffDates).ToArray();

                        if (validLocalDates.Length > 0)
                            break;

                        currentDateStartLocal = currentDateEndLocal;
                    }
                }

                // For each valid date, we look inside them for available slots.
                foreach (var eachValidLocalDate in validLocalDates)
                {
                    var currentDateStartUtc = ConvertToUtcDateTime(practice, eachValidLocalDate);
                    var currentDateEndUtc = ConvertToUtcDateTime(practice, eachValidLocalDate.AddDays(1.0).AddTicks(-1));

                    // take all appointments of that day
                    var appointments = db.Appointments
                        .Where(a => a.DoctorId == doctor.Id)
                        .Where(a => a.End >= currentDateStartUtc && a.Start <= currentDateEndUtc)
                        .OrderBy(a => a.Start)
                        .ToList();

                    var slots = GetDaySlotsInLocalTime(eachValidLocalDate, doctor)
                        .Where(s => s.Item1 >= startingFromLocalTime)
                        .Select(s => new
                        {
                            StartUtc = ConvertToUtcDateTime(practice, s.Item1),
                            EndUtc = ConvertToUtcDateTime(practice, s.Item2)
                        })
                        .ToList();

                    // Looking for available slots of time in the current day.
                    foreach (var slot in slots)
                    {
                        if (!IsTimeAvailableUtc(slot.StartUtc, slot.EndUtc, appointments))
                            continue;

                        return Tuple.Create(
                            ConvertToLocalDateTime(practice, slot.StartUtc),
                            ConvertToLocalDateTime(practice, slot.EndUtc));
                    };
                }
            }
        }

        [HttpGet]
        public JsonResult GetSpelledDate(string date)
        {
            DateTime dateParsed;
            if (DateTime.TryParse(date, out dateParsed))
                return this.Json(
                    new
                    {
                        success = true,
                        text = DateTimeHelper.GetDayOfWeekAsString(dateParsed) + ", "
                            + DateTimeHelper.ConvertToRelative(
                                dateParsed,
                                this.GetPracticeLocalNow(),
                                DateTimeHelper.RelativeDateOptions.IncludePrefixes
                                | DateTimeHelper.RelativeDateOptions.IncludeSuffixes
                                | DateTimeHelper.RelativeDateOptions.ReplaceToday
                                | DateTimeHelper.RelativeDateOptions.ReplaceYesterdayAndTomorrow)
                    },
                    JsonRequestBehavior.AllowGet);
            else
                return this.Json(new { success = false }, JsonRequestBehavior.AllowGet);
        }

        private static bool ValidateTime(
            CerebelloEntitiesAccessFilterWrapper db,
            Doctor doctor,
            DateTime localDate,
            string startTimeText,
            string endTimeText,
            ModelStateDictionary modelState,
            ModelStateDictionary inconsistencyMessages,
            DateTime localNow)
        {
            if (string.IsNullOrEmpty(startTimeText) || string.IsNullOrEmpty(endTimeText))
                return false;

            var hasError = false;

            var startRegexMatch = TimeDataTypeAttribute.Regex.Match(startTimeText);
            var endRegexMatch = TimeDataTypeAttribute.Regex.Match(endTimeText);

            var integerHourStart = int.Parse(startRegexMatch.Groups[1].Value) * 100 + int.Parse(startRegexMatch.Groups[2].Value);
            var integerHourEnd = int.Parse(endRegexMatch.Groups[1].Value) * 100 + int.Parse(endRegexMatch.Groups[2].Value);

            var monthAndDay = localDate.Month * 100 + localDate.Day;

            // Validation: cannot be holliday.
            var isHolliday = db.SYS_Holiday.Any(h => h.MonthAndDay == monthAndDay);
            if (isHolliday)
            {
                inconsistencyMessages.AddModelError<AppointmentViewModel>(
                    model => model.LocalDateTime,
                    "O campo '{0}' é inválido. Este dia é um feriado.");
                hasError = true;
            }

            //var doctor = this.Doctor;

            // Validation: cannot be day-off.
            var isDayOff = db.CFG_DayOff
                             .Where(d => d.DoctorId == doctor.Id).Any(d => d.Date == localDate.Date);

            if (isDayOff)
            {
                inconsistencyMessages.AddModelError<AppointmentViewModel>(
                    model => model.LocalDateTime,
                    "O campo '{0}' é inválido. Este dia está no intervalo de dias sem expediente do médico.");
                hasError = true;
            }

            // Validation: cannot set an appointment date to the past.
            var localStartDate = localDate.Date + DateTimeHelper.GetTimeSpan(startTimeText);
            if (localStartDate < localNow)
            {
                inconsistencyMessages.AddModelError<AppointmentViewModel>(
                    model => model.LocalDateTime,
                    "A data e hora indicadas estão no passado.");
                hasError = true;
            }

            // Validation: end time cannot be the same as start time.
            if (integerHourStart == integerHourEnd)
            {
                modelState.AddModelError<AppointmentViewModel>(
                    model => model.End,
                    "O campo '{0}' não pode ser igual ao horário de início.");
                hasError = true;
            }

            // Validation: end time must come after the start time.
            else if (integerHourStart > integerHourEnd)
            {
                modelState.AddModelError<AppointmentViewModel>(
                    model => model.End,
                    "O campo '{0}' não pode ser menor que o horário de início.");
                hasError = true;
            }

            Action<string, string, string, string> checkModelTimingError = (workdayStart, workdayEnd, lunchStart, lunchEnd) =>
            {
                if (string.IsNullOrEmpty(workdayStart) || string.IsNullOrEmpty(workdayEnd))
                {
                    modelState.AddModelError<AppointmentViewModel>(
                        model => model.LocalDateTime,
                        "O campo '{0}' é inválido. Não existem configurações de horário para esta data.");
                    hasError = true;
                }
                else
                {
                    // Verify the lunch time.
                    // lunch time can only be a problem when it's set, because it's optional
                    // when the start time is set, so it is the end time
                    // I'm checking both here but it could be just one
                    if (!string.IsNullOrEmpty(lunchStart) && !string.IsNullOrEmpty(lunchEnd))
                    {
                        var lunchStartInteger = GetTimeAsInteger(lunchStart);
                        var lunchEndInteger = GetTimeAsInteger(lunchEnd);

                        bool lunchIsAfter = integerHourStart < lunchStartInteger && integerHourEnd <= lunchStartInteger;
                        bool lunchIsBefore = integerHourStart >= lunchEndInteger && integerHourEnd > lunchEndInteger;

                        if (lunchIsAfter == lunchIsBefore)
                        {
                            inconsistencyMessages.AddModelError<AppointmentViewModel>(
                                model => model.LocalDateTime,
                                "A data e hora marcada está no horário de almoço do médico.");
                            hasError = true;
                        }
                    }

                    // Verify the work time.
                    {
                        var workdayStartInteger = GetTimeAsInteger(workdayStart);
                        var workdayEndInteger = GetTimeAsInteger(workdayEnd);

                        if (integerHourStart < workdayStartInteger)
                        {
                            modelState.AddModelError<AppointmentViewModel>(
                                model => model.Start,
                                "O campo '{0}' não é um horário válido devido às configurações de horário de trabalho.");
                            hasError = true;
                        }

                        if (integerHourEnd > workdayEndInteger)
                        {
                            modelState.AddModelError<AppointmentViewModel>(
                                model => model.End,
                                "O campo '{0}' não é um horário válido devido às configurações de horário de trabalho.");
                            hasError = true;
                        }
                    }
                }
            };

            switch (localDate.Date.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    checkModelTimingError(
                        doctor.CFG_Schedule.SundayWorkdayStartTime,
                        doctor.CFG_Schedule.SundayWorkdayEndTime,
                        doctor.CFG_Schedule.SundayLunchStartTime,
                        doctor.CFG_Schedule.SundayLunchEndTime);
                    break;
                case DayOfWeek.Monday:
                    checkModelTimingError(
                        doctor.CFG_Schedule.MondayWorkdayStartTime,
                        doctor.CFG_Schedule.MondayWorkdayEndTime,
                        doctor.CFG_Schedule.MondayLunchStartTime,
                        doctor.CFG_Schedule.MondayLunchEndTime);
                    break;
                case DayOfWeek.Tuesday:
                    checkModelTimingError(
                        doctor.CFG_Schedule.TuesdayWorkdayStartTime,
                        doctor.CFG_Schedule.TuesdayWorkdayEndTime,
                        doctor.CFG_Schedule.TuesdayLunchStartTime,
                        doctor.CFG_Schedule.TuesdayLunchEndTime);
                    break;
                case DayOfWeek.Wednesday:
                    checkModelTimingError(
                        doctor.CFG_Schedule.WednesdayWorkdayStartTime,
                        doctor.CFG_Schedule.WednesdayWorkdayEndTime,
                        doctor.CFG_Schedule.WednesdayLunchStartTime,
                        doctor.CFG_Schedule.WednesdayLunchEndTime);
                    break;
                case DayOfWeek.Thursday:
                    checkModelTimingError(
                        doctor.CFG_Schedule.ThursdayWorkdayStartTime,
                        doctor.CFG_Schedule.ThursdayWorkdayEndTime,
                        doctor.CFG_Schedule.ThursdayLunchStartTime,
                        doctor.CFG_Schedule.ThursdayLunchEndTime);
                    break;
                case DayOfWeek.Friday:
                    checkModelTimingError(
                        doctor.CFG_Schedule.FridayWorkdayStartTime,
                        doctor.CFG_Schedule.FridayWorkdayEndTime,
                        doctor.CFG_Schedule.FridayLunchStartTime,
                        doctor.CFG_Schedule.FridayLunchEndTime);
                    break;
                case DayOfWeek.Saturday:
                    checkModelTimingError(
                        doctor.CFG_Schedule.SaturdayWorkdayStartTime,
                        doctor.CFG_Schedule.SaturdayWorkdayEndTime,
                        doctor.CFG_Schedule.SaturdayLunchStartTime,
                        doctor.CFG_Schedule.SaturdayLunchEndTime);
                    break;
            }

            return !hasError;
        }

        /// <summary>
        /// Converts a string containing a time to an integer.
        /// e.g.: "13:15" -> 1315
        /// </summary>
        /// <param name="strTime"></param>
        /// <returns></returns>
        private static int GetTimeAsInteger(string strTime)
        {
            var match = TimeDataTypeAttribute.Regex.Match(strTime);
            int result = int.Parse(match.Groups[1].Value) * 100 + int.Parse(match.Groups[2].Value);
            return result;
        }

        public static bool IsTimeAvailableUtc(DateTime startTimeUtc, DateTime endTimeUtc, IEnumerable<Appointment> appointments, int? excludeAppointmentId = null)
        {
            if (startTimeUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("startTimeUtc must be UTC", "startTimeUtc");

            if (endTimeUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("endTimeUtc must be UTC", "endTimeUtc");

            // Not overlap condition:
            // The whole body of A is before the start of B   (a.start < b.start && a.end <= b.start)
            //   OR                                             ||
            // The whole body of A is after the end of B      (a.start >= b.end && a.end > b.end)
            var query = from a in appointments
                        where
                        !(
                               (a.Start < startTimeUtc && a.End <= startTimeUtc)
                                 ||
                               (a.Start >= endTimeUtc && a.End > endTimeUtc)
                        )
                        select a;

            // When moving the appointment to another date or time,
            // we must exclude it from the selection... you can
            // move it to the position where it is now.
            if (excludeAppointmentId != null)
                query = query.Where(a => a.Id != excludeAppointmentId);

            return !query.Any();
        }

        /// <summary>
        /// Verifies whether it's a valid time
        /// </summary>
        /// <param name="date"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="excludeAppointmentId"></param>
        /// <returns></returns>
        [HttpGet]
        public JsonResult VerifyTimeAvailability(string date, string start, string end, int? excludeAppointmentId = null)
        {
            if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end))
            {
                return this.Json(new
                {
                    status = DateAndTimeValidationState.Failed.ToString(),
                    text = "Sem informações suficientes",
                }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                var localDateParsed = DateTime.Parse(date).Date;

                if (localDateParsed.Kind != DateTimeKind.Unspecified)
                    throw new ArgumentException("'date' must be expressed in local practice time-zone.", "date");

                AppointmentViewModel viewModel = new AppointmentViewModel
                {
                    LocalDateTime = localDateParsed,
                    Start = start,
                    End = end,
                };

                this.DoDateAndTimeValidation(viewModel, this.GetPracticeLocalNow(), excludeAppointmentId);

                return this.Json(new
                {
                    status = viewModel.DateAndTimeValidationState.ToString(),
                    text = viewModel.TimeValidationMessage,
                }, JsonRequestBehavior.AllowGet);
            }
        }

        private void DoDateAndTimeValidation(AppointmentViewModel viewModel, DateTime localNow, int? excludeAppointmentId)
        {
            if (viewModel.LocalDateTime != viewModel.LocalDateTime.Date)
                throw new ArgumentException("viewModel.Date must be the date alone, without time data.");

            var inconsistencyMessages = new ModelStateDictionary();
            if (!string.IsNullOrEmpty(viewModel.Start) && !string.IsNullOrEmpty(viewModel.End))
            {
                var startTimeLocal = viewModel.LocalDateTime + DateTimeHelper.GetTimeSpan(viewModel.Start);
                var endTimeLocal = viewModel.LocalDateTime + DateTimeHelper.GetTimeSpan(viewModel.End);

                var startTimeUtc = ConvertToUtcDateTime(this.DbPractice, startTimeLocal);
                var endTimeUtc = ConvertToUtcDateTime(this.DbPractice, endTimeLocal);

                var isTimeAvailable = IsTimeAvailableUtc(startTimeUtc, endTimeUtc, this.Doctor.Appointments, excludeAppointmentId);
                if (!isTimeAvailable)
                {
                    inconsistencyMessages.AddModelError(
                        () => viewModel.LocalDateTime,
                        "A data e hora já está marcada para outro compromisso.");
                }
            }

            // Setting the error message to display near the date and time configurations.
            var emptyErrors = new ModelErrorCollection();
            var errorsList = new List<ModelError>();
            errorsList.AddRange(this.ModelState.GetPropertyErrors(() => viewModel.LocalDateTime) ?? emptyErrors);
            errorsList.AddRange(this.ModelState.GetPropertyErrors(() => viewModel.Start) ?? emptyErrors);
            errorsList.AddRange(this.ModelState.GetPropertyErrors(() => viewModel.End) ?? emptyErrors);

            // Flag that tells whether the time and date are valid ot not.
            viewModel.DateAndTimeValidationState =
                errorsList.Any() ? DateAndTimeValidationState.Failed :
                !inconsistencyMessages.IsValid ? DateAndTimeValidationState.Warning :
                DateAndTimeValidationState.Passed;

            // Continue filling error list with warnings.
            errorsList.AddRange(inconsistencyMessages.GetPropertyErrors(() => viewModel.LocalDateTime) ?? emptyErrors);
            errorsList.AddRange(inconsistencyMessages.GetPropertyErrors(() => viewModel.Start) ?? emptyErrors);
            errorsList.AddRange(inconsistencyMessages.GetPropertyErrors(() => viewModel.End) ?? emptyErrors);
            if (errorsList.Any())
            {
                viewModel.TimeValidationMessage = errorsList.First().ErrorMessage;
            }
        }

        [HttpGet]
        public JsonResult GetDatesWithAppointments(int year, int month)
        {
            var localFirst = new DateTime(year, month, 1);
            var localLast = localFirst.AddMonths(1);

            var utcFirst = ConvertToUtcDateTime(this.DbPractice, localFirst);
            var utcLast = ConvertToUtcDateTime(this.DbPractice, localLast);

            var result = (from a in this.db.Appointments
                          where a.Start >= utcFirst && a.End < utcLast
                          select a).ToList()
                .Select(a => ConvertToLocalDateTime(this.DbPractice, a.Start).ToString("'d'dd_MM_yyyy"))
                .Distinct().ToArray();

            return this.Json(result, JsonRequestBehavior.AllowGet);
        }
    }
}
