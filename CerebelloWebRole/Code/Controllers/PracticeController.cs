﻿using System.Diagnostics;
using System.Linq;
using System.Web.Mvc;
using Cerebello.Model;
using System;
using CerebelloWebRole.Code.Notifications;

namespace CerebelloWebRole.Code
{
    public abstract class PracticeController : CerebelloController
    {
        /// <summary>
        /// Consultório atual
        /// </summary>
        public Practice DbPractice { get; set; }

        /// <summary>
        /// Converts the specified UTC date and time for the location of the current practice.
        /// </summary>
        /// <param name="practice"> </param>
        /// <param name="utcDateTime"></param>
        /// <returns></returns>
        public static DateTime ConvertToLocalDateTime(Practice practice, DateTime utcDateTime)
        {
            if (practice == null) throw new ArgumentNullException("practice");

            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(practice.WindowsTimeZoneId);
            var result = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZoneInfo);
            return result;
        }

        /// <summary>
        /// Converts the specified date and time at the location of the current practice to UTC.
        /// </summary>
        /// <param name="practice"> </param>
        /// <param name="practiceDateTime"></param>
        /// <returns></returns>
        public static DateTime ConvertToUtcDateTime(Practice practice, DateTime practiceDateTime)
        {
            if (practice == null) throw new ArgumentNullException("practice");

            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(practice.WindowsTimeZoneId);
            return DateTimeHelper.ConvertToUtcDateTime(practiceDateTime, timeZoneInfo);
        }

        public DateTime GetPracticeLocalNow()
        {
            return ConvertToLocalDateTime(this.DbPractice, DateTimeHelper.UtcNow);
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            // setting up user
            Debug.Assert(this.DbUser != null);

            // setting up practice
            var practiceName = this.RouteData.Values["practice"] as string;
            var controllerName = this.RouteData.Values["controller"] as string;
            var actionName = this.RouteData.Values["action"] as string;

            var practice = this.db.Users
                .Where(u => u.Id == this.DbUser.Id && u.Practice.UrlIdentifier == practiceName)
                .Select(u => u.Practice)
                .SingleOrDefault();

            if (practice == null)
            {
                filterContext.Result = new HttpUnauthorizedResult();
                return;
            }

            this.DbPractice = practice;
            this.ViewBag.Practice = practice;
            this.ViewBag.PracticeName = practice.Name;

            // Redirect to VerifyPracticeAndEmail, if the practice has not been verified yet.
            if (practice.VerificationDate == null)
            {
                filterContext.Result = this.RedirectToAction("CreateAccountCompleted", "Authentication", new { area = "", practice = practiceName, mustValidateEmail = true });
                return;
            }

            // Setting a common ViewBag value.
            this.ViewBag.LocalNow = this.GetPracticeLocalNow();

            // discover the appointments that have already been polled and sent to the client
            this.ViewBag.AlreadyPolledMedicalAppointments =
                NewAppointmentNotificationsHelper.GetNewMedicalAppointmentNotifications(this.db, this.DbPractice.Id, this.DbUser.Id,
                                                                                 this.Url, true);
            this.ViewBag.AlreadyPolledGenericAppointments =
                NewAppointmentNotificationsHelper.GetNewGenericAppointmentNotifications(this.db, this.DbPractice.Id, this.DbUser.Id,
                                                                                this.Url, true);

            // discover the notifications that have already been polled and sent to to the client
            this.ViewBag.AlreadyPolledNotifications = NotificationsHelper.GetNotifications(this.db, this.DbUser.Id, this, true);
        }

        public virtual bool IsSelfUser(User user)
        {
            return false;
        }
    }
}
