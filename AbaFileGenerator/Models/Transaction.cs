using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AbaFileGenerator
{
    public class Transaction
    {
        /// <summary>
        /// Bank account name for this transaction. Must be 32 characters or less.
        /// </summary>
        public string AccountName { get; set; }
        /// <summary>
        /// Return the account number as a string. Must be 9 digits or less.
        /// </summary>
        public string AccountNumber { get; set; }
        /// <summary>
        /// Return the bank's BSB for this account. Format is xxx-xxx
        /// </summary>
        public string Bsb { get; set; }
        /// <summary>
        /// transaction amount in cents
        /// </summary>
        public int Amount { get; set; }
        /// <summary>
        /// Return null for a normal transaction or if withholding tax:
        /// </summary>
        public TransactionIndicator? Indicator { get; set; }
        public TransactionCode TransactionCode { get; set; }
        /// <summary>
        /// Description of transaction to appear on recipients bank statement. Reference (narration) that appears on target's bank statement.
        /// </summary>
        public string Reference { get; set; }
        /// <summary>
        /// Name of remitter (appears on target's bank statement; must not be blank but some banks will replace with name fund account is held in).
        /// </summary>
        public string Remitter { get; set; }
        /// <summary>
        /// Amount of tax withholding. Return zero if not withholding any amount. in cents
        /// </summary>
        public int TaxWithholding { get; set; }

        public string Validate()
        {
            if (!new Regex(@"^\d{3}-\d{3}$").IsMatch(Bsb))
            {
                return "Detail record bsb is invalid: " + Bsb + ". Required format is 000-000.";
            }

            if (!new Regex(@"^[\d]{0,9}$").IsMatch(AccountNumber))
            {
                return "Detail record account number is invalid. Must be up to 9 digits only.";
            }

            if (Amount < 0 || Amount.ToString().Length > 10)
            {
                return "Detail record amount is invalid. Must be expressed in cents, as an unsigned integer, no longer than 10 digits.";
            }

            if (AccountName?.Length > 32)
            {
                return "Detail record account name is invalid. Cannot exceed 32 characters.";
            }

            if (!new Regex(@"^[A-Za-z0-9\s+]{0,18}$").IsMatch(Reference))
            {
                return "Detail record reference is invalid: " + Reference + " Must be letters or numbers only and up to 18 characters long.";
            }

            if (!string.IsNullOrEmpty(Remitter) && !new Regex(@"^[A-Za-z\s+]{0,16}$").IsMatch(Remitter))
            {
                return "Detail record remitter is invalid. Must be letters only and up to 16 characters long.";
            }

            if(Indicator == null && TaxWithholding > 0)
            {
                return "Detail record indicator must be W,X or Y if TaxWithholding more than 0";
            }

            if (Indicator != null && TaxWithholding <= 0)
            {
                return "Detail record indicator is invalid. If TaxWithholding more than 0, it's must be W,X or Y";
            }

            return string.Empty;
        }
    }
    
    public enum TransactionIndicator
    {
        /// <summary>
        /// new or varied BSB number or name details
        /// </summary>
        N,
        /// <summary>
        /// dividend paid to a resident of a country where a double tax agreement is in force.
        /// </summary>
        W,
        /// <summary>
        /// dividend paid to a resident of any other country.
        /// </summary>
        X,
        /// <summary>
        /// interest paid to all non-residents.
        /// </summary>
        Y
    }

    public enum TransactionCode
    {
        EXTERNALLY_INITIATED_DEBIT = 13,
        EXTERNALLY_INITIATED_CREDIT = 50,
        AUSTRALIAN_GOVERNMENT_SECURITY_INTEREST = 51,
        FAMILY_ALLOWANCE = 52,
        PAYROLL_PAYMENT = 53,
        PENSION_PAYMENT = 54,
        ALLOTMENT = 55,
        DIVIDEND = 56,
        DEBENTURE_OR_NOTE_INTEREST = 57,
    }
}
