﻿using System;
using System.Linq;
using System.Web.Mvc;
using Cerebello.Model;
using CerebelloWebRole.Areas.App.Models;
using CerebelloWebRole.Code;

namespace CerebelloWebRole.Areas.App.Controllers
{
    public class ExamResultsController : DoctorController
    {
        public static ExaminationResultViewModel GetViewModel(ExaminationResult examResult, Func<DateTime, DateTime> toLocal)
        {
            return new ExaminationResultViewModel
                {
                    Id = examResult.Id,
                    PatientId = examResult.PatientId,
                    Text = examResult.Text,
                    MedicalProcedureCode = examResult.MedicalProcedureCode,
                    MedicalProcedureName = examResult.MedicalProcedureName,
                    ExaminationDate = toLocal(examResult.ExaminationDate),
                    ReceiveDate = toLocal(examResult.ReceiveDate),
                };
        }

        public ActionResult Details(int id)
        {
            var practiceId = this.DbUser.PracticeId;

            var examResult = this.db.ExaminationResults
                .Where(r => r.Id == id)
                .First(r => r.Patient.Doctor.Users.FirstOrDefault().PracticeId == practiceId);

            return this.View(GetViewModel(examResult, this.GetToLocalDateTimeConverter()));
        }

        [HttpGet]
        public ActionResult Create(int patientId, string newKey, int? y, int? m, int? d)
        {
            return this.Edit(null, patientId, y, m, d);
        }

        [HttpPost]
        public ActionResult Create(ExaminationResultViewModel[] examResults)
        {
            return this.Edit(examResults);
        }

        [HttpGet]
        public ActionResult Edit(int? id, int? patientId, int? y, int? m, int? d)
        {
            ExaminationResultViewModel viewModel;

            if (id != null)
            {
                var practiceId = this.DbUser.PracticeId;

                var modelObj = this.db.ExaminationResults
                    .Where(r => r.Id == id)
                    .FirstOrDefault(r => r.Patient.Doctor.Users.FirstOrDefault().PracticeId == practiceId);

                // todo: if modelObj is null, we must tell the user that this object does not exist.

                viewModel = GetViewModel(modelObj, this.GetToLocalDateTimeConverter());
            }
            else
            {
                viewModel = new ExaminationResultViewModel
                    {
                        Id = null,
                        PatientId = patientId,
                        ExaminationDate = null,
                        ReceiveDate = DateTimeHelper.CreateDate(y, m, d) ?? this.GetPracticeLocalNow(),
                    };
            }

            return this.View("Edit", viewModel);
        }

        [HttpPost]
        public ActionResult Edit(ExaminationResultViewModel[] examResults)
        {
            var formModel = examResults.Single();

            ExaminationResult dbObject;

            if (formModel.Id == null)
            {
                dbObject = new ExaminationResult
                {
                    CreatedOn = this.GetUtcNow(),
                    PatientId = formModel.PatientId.Value,
                    PracticeId = this.DbUser.PracticeId,
                };

                this.db.ExaminationResults.AddObject(dbObject);
            }
            else
            {
                var practiceId = this.DbUser.PracticeId;

                dbObject = this.db.ExaminationResults
                    .Where(r => r.Id == formModel.Id)
                    .FirstOrDefault(r => r.Patient.Doctor.Users.FirstOrDefault().PracticeId == practiceId);

                // If modelObj is null, we must tell the user that this object does not exist.
                if (dbObject == null)
                    return View("NotFound", formModel);

                // Security issue... must check current user practice against the practice of the edited objects.
                var currentUser = this.DbUser;
                if (currentUser.PracticeId != dbObject.Patient.Doctor.Users.First().PracticeId)
                    return View("NotFound", formModel);
            }

            if (this.ModelState.IsValid)
            {
                dbObject.Patient.IsBackedUp = false;
                dbObject.Text = formModel.Text;
                dbObject.MedicalProcedureName = formModel.MedicalProcedureName;
                dbObject.MedicalProcedureCode = formModel.MedicalProcedureId.HasValue
                    ? this.db.SYS_MedicalProcedure.Where(mp => mp.Id == formModel.MedicalProcedureId).Select(mp => mp.Code).FirstOrDefault()
                    : formModel.MedicalProcedureCode;
                dbObject.ExaminationDate = this.ConvertToUtcDateTime(formModel.ExaminationDate.Value);
                dbObject.ReceiveDate = this.ConvertToUtcDateTime(formModel.ReceiveDate.Value);

                this.db.SaveChanges();

                return this.View("Details", GetViewModel(dbObject, this.GetToLocalDateTimeConverter()));
            }

            return this.View("Edit", formModel);
        }

        /// <summary>
        /// 
        /// Requisitos:
        ///     
        ///     1 - The given exam-result should stop existing after this action call.
        ///         In case of success, it should return a JsonDeleteMessage
        ///         with success = true
        ///     
        ///     2 - In case the given exam-result doesn't exist,
        ///         it should return a JsonDeleteMessage with success = false and the text property
        ///         informing that the object does not exist.
        ///     
        ///     3 - In case the given exam-result doesn't belong to the current user practice,
        ///         it should return a JsonDeleteMessage with success = false and the text property
        ///         informing that the object does not exist.
        /// </summary>
        [HttpGet]
        public JsonResult Delete(int id)
        {
            try
            {
                var practiceId = this.DbUser.PracticeId;

                var modelObj = this.db.ExaminationResults
                    .Where(r => r.Id == id)
                    .FirstOrDefault(r => r.Patient.Doctor.Users.FirstOrDefault().PracticeId == practiceId);

                // If exam does not exist, return message telling this.
                if (modelObj == null)
                {
                    return this.Json(
                        new JsonDeleteMessage { success = false, text = "O resultado de exame não existe." },
                        JsonRequestBehavior.AllowGet);
                }

                this.db.ExaminationResults.DeleteObject(modelObj);
                this.db.SaveChanges();
                return this.Json(
                    new JsonDeleteMessage { success = true },
                    JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return this.Json(
                    new JsonDeleteMessage { success = false, text = ex.Message },
                    JsonRequestBehavior.AllowGet);
            }
        }
    }
}
