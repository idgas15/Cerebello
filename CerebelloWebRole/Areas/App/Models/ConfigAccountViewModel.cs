﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace CerebelloWebRole.Areas.App.Models
{
    public class ConfigAccountViewModel
    {
        public enum CycleType
        {
            Past = -1,
            Present = 0,
            Future = 1,
        }

        public class BillingCycle
        {
            /// <summary>
            /// Planned date of the billing.
            /// </summary>
            public DateTime PlannedDate { get; set; }

            /// <summary>
            /// Effective date of the payment.
            /// </summary>
            public DateTime EffectiveDate { get; set; }

            /// <summary>
            /// Planned value of the billing.
            /// </summary>
            public decimal PlannedValue { get; set; }

            /// <summary>
            /// Effective value of the payment.
            /// </summary>
            public decimal EffectiveValue { get; set; }

            /// <summary>
            /// Currency of the billing.
            /// </summary>
            public string Currency { get; set; }

            /// <summary>
            /// Indicates whether this is paid already.
            /// </summary>
            public bool IsPaid { get; set; }

            /// <summary>
            /// Whether this billing cycle is in the past, present or future.
            /// </summary>
            public CycleType CycleType { get; set; }
        }

        public class BillingYear
        {
            /// <summary>
            /// The year of this billing group.
            /// </summary>
            public int Year { get; set; }

            /// <summary>
            /// List of billing cycles of this year in reverse date order.
            /// </summary>
            public List<BillingCycle> Cycles { get; set; }

            /// <summary>
            /// Whether this year contains the present billing cycle.
            /// </summary>
            public bool IsPresent { get { return this.Cycles.Any(cy => cy.CycleType == CycleType.Present); } }

            /// <summary>
            /// Whether this year contains only future billing cycles.
            /// </summary>
            public bool IsFuture { get { return this.Cycles.All(cy => cy.CycleType == CycleType.Future); } }

            /// <summary>
            /// Whether this year contains only past billing cycles.
            /// </summary>
            public bool IsPast { get { return this.Cycles.All(cy => cy.CycleType == CycleType.Past); } }
        }

        public enum ContractStatus
        {
            /// <summary>
            /// This is a contract suggestion, that the user can choose to subscribe.
            /// </summary>
            Suggestion,

            /// <summary>
            /// This is a contract that is not active yet, but is scheduled for the future.
            /// </summary>
            Scheduled,

            /// <summary>
            /// This contract is the current one, and it is active.
            /// </summary>
            Active,

            /// <summary>
            /// This contract is the current one, but it is suspended (cannot use but is the current contract).
            /// </summary>
            Suspended,

            /// <summary>
            /// This contract was the current one, but now it is canceled by the user.
            /// </summary>
            Canceled,

            Expired,
        }

        public class Contract
        {
            /// <summary>
            /// Whether this contract is active, or has been canceled.
            /// </summary>
            public ContractStatus Status { get; set; }

            [Display(Name = "Título do plano")]
            public string PlanTitle { get; set; }

            public string Text { get; set; }
            public string Description { get; set; }
            public List<Contract> Additions { get; set; }

            public string UrlIdentifier { get; set; }
        }

        public enum ContractChangeType
        {
            /// <summary>
            /// The destination plan is the same, but is being renewed.
            /// </summary>
            Renovation,

            /// <summary>
            /// The destination plan adds to the current plan.
            /// </summary>
            Upgrade,

            /// <summary>
            /// The destination plan removes from the current plan.
            /// </summary>
            Downgrade,

            /// <summary>
            /// The destination plan is a different plan.
            /// </summary>
            Migration,

            /// <summary>
            /// The destination plan is nothing. Plan will be canceled.
            /// </summary>
            Cancel,
        }

        public class ContractChangeData
        {
            /// <summary>
            /// Type of migration. This will influence the text of the button.
            /// </summary>
            public ContractChangeType Type { get; set; }

            /// <summary>
            /// The contract that is being suggested.
            /// </summary>
            public Contract Contract { get; set; }
        }

        /// <summary>
        /// List of billing informations, grouped by year, and then by billing cycles.
        /// </summary>
        public List<BillingYear> BillingYears { get; set; }

        /// <summary>
        /// List of contracts that are now expired or canceled.
        /// </summary>
        public List<Contract> PastContracts { get; set; }

        /// <summary>
        /// The contract that is active at this moment.
        /// </summary>
        public Contract CurrentContract { get; set; }

        /// <summary>
        /// The contract to which a migration will happen upon the termination of the current contract.
        /// </summary>
        public Contract ScheduledContract { get; set; }

        /// <summary>
        /// List of available migrations.
        /// </summary>
        public List<ContractChangeData> Migrations { get; set; }
    }
}
