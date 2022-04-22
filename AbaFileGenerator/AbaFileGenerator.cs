using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AbaFileGenerator
{
    public class AbaFileGenerator
    {
        #region Const
        const string DESCRIPTIVE_TYPE = "0";
        const string DETAIL_TYPE = "1";
        const string BATCH_TYPE = "7";
        private const char PaddingChar = ' ';
        #endregion

        #region Properties

        /// <summary>
        /// running total of credits in file
        /// </summary>
        public int CreditTotal { get; set; }
        /// <summary>
        /// running total of debit in file
        /// </summary>
        public int DebitTotal { get; set; }
        public int NumberRecords { get; set; }
        /// <summary>
        /// BSB of funds account (formatted 000-000) OR blank (ignored by WPC; APCA specification requires blank).
        /// </summary>
        public string Bsb { get; set; }
        /// <summary>
        /// Account number of fund account (inc leading zeros) OR blank (ignored by WPC; APCA specification requires blank).
        /// </summary>
        public string AccountNumber { get; set; }
        public string BankName { get; set; }
        /// <summary>
        /// The name of the user supplying the aba file. Some banks must match account holder or be specified as "SURNAME Firstname".
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// Appears on recipient's statement as origin of transaction.
        /// </summary>
        public string Remitter { get; set; }
        public string DirectEntryUserId { get; set; }
        public string Description { get; set; }
        /// <summary>
        /// The date transactions are released to all Financial Institutions.
        /// </summary>
        public DateTime ProcessingDate { get; set; } = DateTime.Now;

        #region options
        /// <summary>
        /// Set whether to include the remitter's bank account number and BSB in the descriptive record header.
        /// Defaults to true for historic reasons.Some banks will require you to change this to false.
        /// </summary>
        public bool IncludeAccountNumberInDescriptiveRecord { get; set; } = true;

        /// <summary>
        /// Default is false
        /// </summary>
        public bool IncludeProcessingTime { get; set; }

        public bool IncludeTotalRecord { get; set; } = true;
        #endregion

        /// <summary>
        /// aba file string
        /// </summary>
        public string AbaString { get; set; }

        #endregion

        /// <summary>
        /// Validates that the BSB is 6 digits with a dash in the middle: 123-456
        /// </summary>
        private readonly Regex _bsbRegex = new Regex(@"^\d{3}-\d{3}$");

        public AbaFileGeneratorResult Generate(IEnumerable<Transaction> transactions)
        {
            var result = new AbaFileGeneratorResult();
            if (transactions == null)
            {
                result.FailReason = "No transactions";
                return result;
            }

            var validateResult = validateDescriptiveRecord();
            if (!string.IsNullOrEmpty(validateResult))
            {
                result.FailReason = validateResult;
                return result;
            }
            addAccountDetailsRecord();


            foreach (var item in transactions)
            {
                //Validate the parts of the transaction.
                validateResult = item.Validate();
                if (!string.IsNullOrEmpty(validateResult))
                {
                    result.FailReason = validateResult;
                    return result;
                }
                addTransactionRecord(item);

                if (item.TransactionCode == TransactionCode.EXTERNALLY_INITIATED_DEBIT)
                {
                    DebitTotal += item.Amount;
                }
                else
                {
                    CreditTotal += item.Amount;
                }
            }

            NumberRecords = transactions.Count();

            if (IncludeTotalRecord)
            {
                addTotalRecord();
            }

            result.IsValid = true;
            result.Data = this;
            return result;
        }

        public int GetBatchNetTotal()
        {
            return Math.Abs(CreditTotal - DebitTotal);
        }

        #region Private funcs

        /// <summary>
        /// the first line: Descriptive Record. https://github.com/mjec/aba/blob/master/sample-with-comments.aba
        /// </summary>
        private void addAccountDetailsRecord()
        {
            // Record Type
            var line = DESCRIPTIVE_TYPE;

            if (IncludeAccountNumberInDescriptiveRecord)
            {
                // BSB
                line += PadRight(Bsb, 7, PaddingChar);

                // Account Number
                line += PadLeft(AccountNumber, 9, PaddingChar);

                // Reserved - must be a single blank space
                line += " ";
            }
            else
            {
                // Reserved - must be 17 blank spaces
                line += PadRight("", 17, PaddingChar);
            }

            // Sequence Number
            line += "01";

            // Bank Name
            line += PadRight(BankName, 3, PaddingChar);

            // Reserved - must be seven blank spaces
            line += PadRight("", 7, PaddingChar);

            // User Name
            line += PadRight(UserName, 26, PaddingChar);

            // User ID
            line += PadRight(DirectEntryUserId, 6, PaddingChar);

            // File Description
            line += PadRight(Description, 12, PaddingChar);

            // Processing Date
            line += ProcessingDate.ToString(IncludeProcessingTime ? "ddMMyyHHmm" : "ddMMyy");

            // Reserved - 40 blank spaces
            line += PadRight("", IncludeProcessingTime ? 36 : 40, PaddingChar);

            addLine(line);
        }

        /// <summary>
        /// Add a detail record for each transaction. https://github.com/mjec/aba/blob/master/sample-with-comments.aba
        /// </summary>
        /// <param name="transaction"></param>
        private void addTransactionRecord(Transaction transaction)
        {
            // Record Type
            var line = DETAIL_TYPE;

            // BSB
            line += PadRight(transaction.Bsb, 7, PaddingChar);

            // Account Number
            line += PadLeft(transaction.AccountNumber, 9, '0');

            // Indicator
            line += transaction.Indicator != null ? transaction.Indicator.ToString() : PaddingChar.ToString();

            // Transaction Code
            line += ((int)transaction.TransactionCode).ToString();

            // Transaction Amount
            line += PadLeft(transaction.Amount.ToString(), 10, '0');

            // Account Name
            line += PadRight(transaction.AccountName, 32, PaddingChar);

            // Lodgement Reference
            line += PadRight(transaction.Reference, 18, PaddingChar);

            // Trace BSB - already validated
            line += PadRight(Bsb, 7, PaddingChar);

            // Trace Account Number - already validated
            line += PadLeft(transaction.AccountNumber, 9, PaddingChar);

            // Name of remitter (appears on target's bank statement; must not be blank but some banks will replace with name fund account is held in).
            // Remitter Name - already validated
            var remitter = transaction.Remitter ?? Remitter;
            line += PadRight(remitter, 16, PaddingChar);

            // Withholding amount
            line += PadLeft(transaction.TaxWithholding.ToString(), 8, '0');

            addLine(line);
        }

        /// <summary>
        /// The bottom line, File Total Record. https://github.com/mjec/aba/blob/master/sample-with-comments.aba
        /// </summary>
        private void addTotalRecord()
        {
            var line = BATCH_TYPE;

            // BSB
            line += "999-999";

            // Reserved - must be twelve blank spaces            
            line += PadRight("", 12, PaddingChar);

            // Batch Net Total
            line += PadLeft(GetBatchNetTotal().ToString(), 10, '0');

            // Batch Credits Total
            line += PadLeft(CreditTotal.ToString(), 10, '0');

            // Batch Debits Total
            line += PadLeft(DebitTotal.ToString(), 10, '0');

            // Reserved - must be 24 blank spaces
            line += PadRight("", 24, PaddingChar);

            // Number of records
            line += PadLeft(NumberRecords.ToString(), 6, '0');

            // Reserved - must be 40 blank spaces
            line += PadRight("", 40, PaddingChar);

            addLine(line, false);
        }

        private void addLine(string line, bool crlf = true)
        {
            AbaString += line + (crlf ? "\r\n" : "");
        }

        /// <summary>
        /// Validate the parts of the descriptive record.
        /// </summary>
        /// <returns></returns>
        private string validateDescriptiveRecord()
        {
            if (!string.IsNullOrEmpty(Bsb) && !_bsbRegex.IsMatch(Bsb))
            {
                return "Descriptive record bsb is invalid. Required format is 000-000.";
            }

            if (!string.IsNullOrEmpty(AccountNumber) && !new Regex(@"^[\d]{0,9}$").IsMatch(AccountNumber))
            {
                return "Descriptive record account number is invalid. Must be up to 9 digits only.";
            }

            if (!string.IsNullOrEmpty(BankName) && !new Regex(@"^[A-Z]{3}$").IsMatch(BankName))
            {
                return "Descriptive record bank name must be capital letter abbreviation of length 3.";
            }

            if (!string.IsNullOrEmpty(UserName) && !new Regex(@"^[A-Za-z\s+]{0,26}$").IsMatch(UserName))
            {
                return "Descriptive record user name must be letters only and up to 26 characters long.";
            }

            //if (string.IsNullOrEmpty(Remitter))
            //{
            //    return "Remitter name is required";
            //}

            if (!string.IsNullOrEmpty(DirectEntryUserId) && !new Regex(@"^[\d]{6}$").IsMatch(DirectEntryUserId))
            {
                return "Descriptive record direct entiry user ID is invalid. Must be 6 digits long.";
            }

            if (!string.IsNullOrEmpty(Description) && !new Regex(@"^[A-Za-z\s]{0,12}$").IsMatch(Description))
            {
                return "Descriptive record description is invalid. Must be letters only and up to 12 characters long.";
            }

            return string.Empty;
        }

        private string PadLeft(string input, int numberOfChar, char paddingChar)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty.PadLeft(numberOfChar, paddingChar);
            }
            if (input.Length > numberOfChar)
            {
                return input.Substring(0, numberOfChar).PadLeft(numberOfChar, paddingChar);
            }
            return input.PadLeft(numberOfChar, paddingChar);
        }

        private string PadRight(string input, int numberOfChar, char paddingChar)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty.PadRight(numberOfChar, paddingChar);
            }
            if (input.Length > numberOfChar)
            {
                return input.Substring(0, numberOfChar).PadRight(numberOfChar, paddingChar);
            }
            return input.PadRight(numberOfChar, paddingChar);
        }

        #endregion
    }

    public class AbaFileGeneratorResult
    {
        public bool IsValid { get; set; }
        public string FailReason { get; set; }
        public AbaFileGenerator Data { get; set; }
    }
}
