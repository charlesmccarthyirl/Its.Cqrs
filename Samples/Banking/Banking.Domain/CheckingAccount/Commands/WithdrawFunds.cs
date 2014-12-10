﻿using Its.Validation;
using Its.Validation.Configuration;
using Microsoft.Its.Domain;

namespace Banking.Domain
{
    public class WithdrawFunds : Command<CheckingAccount>
    {
        public decimal Amount { get; set; }

        public override IValidationRule<CheckingAccount> Validator
        {
            get
            {
                return Validate.That<CheckingAccount>(account => account.DateClosed == null)
                               .WithErrorMessage("You cannot make a withdrawal from a closed account.");
            }
        }

        public override IValidationRule CommandValidator
        {
            get
            {
                return Validate.That<WithdrawFunds>(cmd => cmd.Amount > 0)
                               .WithErrorMessage("You cannot make a withdrawal for a negative amount.");
            }
        }
    }
}