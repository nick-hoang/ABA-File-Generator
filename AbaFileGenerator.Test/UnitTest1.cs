using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using AbaFileGenerator;
using System.Collections.Generic;

namespace AbaFileGenerator.Test
{
    [TestClass]
    public class UnitTest1
    {
        /// <summary>        
        /// FORMAT OF DETAIL RECORD - https://github.com/mjec/aba/blob/master/sample-with-comments.aba
        /// S.P  E.P  LEN T A F NAME                DESCRIPTION
        ///  1    1    1 F - - Record Type         Must be 0 for descriptive record.
        ///  2    8    7 D L S Ext:BSB             BSB of funds account (formatted 000-000) OR blank (ignored by WPC; APCA
        ///                                        specification requires blank).
        ///  9   17    9 C R S Ext:Account Number  Account number of fund account (inc leading zeros) OR blank (ignored by WPC;
        ///                                        APCA specification requires blank).
        /// 18   18    1 F - S Reserved            Must be a single blank space.
        /// 19   20    2 N R Z Sequence Number     Generally 01. Sequence number of file in batch (starting from 01). Batches to
        ///                                        be used where number of detail records exceeds maximum per file of 500.
        /// 21   23    3 A - - Bank Name           Three letter APCA abbreviation for bank (CBA, NAB, ANZ, WPC). See APCA
        ///                                        publication entitled BSB Numbers in Australia.
        /// 24   30    7 F - S Reserved            Must be seven blank spaces.
        /// 31   56   26 B L S User Name           The name of the user supplying the file. Some banks must match account holder
        ///                                        or be specified as "SURNAME Firstname". Must not be blank. APCA says it should
        ///                                        be "User preferred name".
        /// 57   62    6 N R Z DE User ID          Direct Entry user ID where allocated. Required for direct debits. For internet
        ///                                        banking use CBA: 301500, WPC: 037819, ignored by NAB and ANZ. APCA requires this
        ///                                        to be BECS User Identification Number.
        /// 63   74   12 B L S File Description    A description of the contents of the file. Ignored by CBA.
        /// 75   80    6 N - Z Processing Date     Date to process transactions as DDMMYY.
        /// 81   84    4 A L S Ext:Processing Time Time to process transactions as 24 hr HHmm or all spaces. APCA specification
        ///                                        requires blank.
        /// 85  120   36 F - S Reserved            Must be thirty six blank spaces.
        /// </summary>        
        [TestMethod]
        public void TestAccountDetailsRecord()
        {
            var abaGenerator = Generate();
            var abaLines = abaGenerator.AbaString.Split(new[] { "\r\n" }, StringSplitOptions.None);

            Assert.IsTrue(abaLines.Length == 6);
            Assert.IsTrue(abaLines[0][0] == '0');
            Assert.IsTrue(abaLines[1][0] == '1');
            Assert.IsTrue(abaLines[4][0] == '1');
            Assert.IsTrue(abaLines[5][0] == '7');            

            var record = abaLines[0];
            Assert.IsTrue(record.Length == 120);
            if (abaGenerator.IncludeAccountNumberInDescriptiveRecord)
            {
                Assert.IsTrue(abaGenerator.Bsb == record.Substring(2 - 1, 7));
                Assert.IsTrue(abaGenerator.AccountNumber == record.Substring(9 - 1, 9).Trim());
            }
            Assert.IsTrue(abaGenerator.BankName == record.Substring(21 - 1, 3));
            Assert.IsTrue(abaGenerator.UserName == record.Substring(31 - 1, 26).Trim());
            Assert.IsTrue(abaGenerator.DirectEntryUserId == record.Substring(57 - 1, 6));
            Assert.IsTrue(abaGenerator.Description == record.Substring(63 - 1, 12).Trim());
        }

        /// <summary>        
        /// FORMAT OF DETAIL RECORD - https://github.com/mjec/aba/blob/master/sample-with-comments.aba
        ///
        /// S.P  E.P  LEN T A F NAME                DESCRIPTION
        ///  1    1    1 F - - Record Type         Must be 1 for detail record.
        ///  2    8    7 A L S BSB                 BSB of target account (formatted 000-000).
        ///  9   17    9 A R S Account Number      Account number of target account (inc leading zeros if part of account number).
        /// 18   18    1 A L S Indicator           Must be one of: blank space (nothing indicated), N (this record changes details
        ///                                        of payee as they occured before?), W (this is a dividend payment to a resident
        ///                                        of a country with a double tax agreement), X (this is a dividend payment to a
        ///                                        resident of any other country), Y (this is an interest payment to a non-resident
        ///                                        of Australia). W, X and Y require that a withholding tax amount be specified.
        /// 19   20    2 N R Z Transaction Code    Must be one of: 13 (externally initiated debit), 50 (externally initiated
        ///                                        credit - normally what is required), 51 (Australian Government Security
        ///                                        interest), 52 (Family Allowance), 53 (Payroll payment), 54 (Pension payment),
        ///                                        55 (Allotment), 56 (Dividend), 57 (Debenture or note interest).
        /// 21   30   10 N R Z Transaction Amount  Total amount of this transaction as zero-padded number of cents.
        /// 31   62   32 A L S Account Name        Name target account is held in, normally as "SURNAME First Second Names".
        /// 63   80   18 A L S Lodgement Reference Reference (narration) that appears on target's bank statement.
        /// 81   87    7 A L S Trace BSB           BSB of fund (source) account (formatted 000-000).
        /// 88   96    9 A L S Trace Account Num   Account number of fund (source) account (inc leading zeros if part of number).
        /// 97  112   16 A L S Remitter Name       Name of remitter (appears on target's bank statement; must not be blank but
        ///                                        some banks will replace with name fund account is held in).
        ///113  120    8 N R Z Withholding amount  Amount of withholding tax (if Indicator is W, X or Y) or all zeros. If not zero
        ///                                        then will cause Indicator field to be ignored and tax to be withheld.
        /// </summary>        
        [TestMethod]
        public void TestTransactionRecords()
        {
            var abaGenerator = Generate();
            var abaLines = abaGenerator.AbaString.Split(new[] { "\r\n" }, StringSplitOptions.None);            
            var transactions = GetTransactions();

            for (int i = 1; i < abaLines.Length - 1; i++)
            {
                var record = abaLines[i];
                var transaction = transactions[i - 1];
                Assert.IsTrue(record.Length == 120, "transaction " + i);
                Assert.IsTrue(transaction.Bsb == record.Substring(1, 7), "transaction " + i);
                Assert.IsTrue(transaction.AccountNumber == record.Substring(8, 9).Trim(), "transaction " + i);                
                Assert.IsTrue((transaction.Indicator != null? transaction.Indicator.ToString(): " ")  == record.Substring(18 - 1, 1), "transaction " + i);                
                Assert.IsTrue(((int)transaction.TransactionCode).ToString() == record.Substring(19 - 1, 2), "transaction " + i);
                Assert.IsTrue(transaction.Amount == int.Parse(record.Substring(21 - 1, 10)), "transaction " + i);
                Assert.IsTrue(transaction.AccountName == record.Substring(31 - 1, 32).Trim(), "transaction " + i);
                Assert.IsTrue(transaction.Reference == record.Substring(63 - 1, 18).Trim(), "transaction " + i);
                Assert.IsTrue(abaGenerator.Bsb == record.Substring(81 - 1, 7).Trim(), "transaction " + i);
                Assert.IsTrue(transaction.AccountNumber == record.Substring(88 - 1, 9).Trim(), "transaction " + i);

                var remitter = transaction.Remitter ?? abaGenerator.Remitter;
                Assert.IsTrue(remitter == record.Substring(97 - 1, 16).Trim(), "transaction " + i);
                Assert.IsTrue(transaction.TaxWithholding == int.Parse(record.Substring(113 - 1, 8)), "transaction " + i);
            }
        }

        /// <summary>
        /// FORMAT OF BAtCH CONTROL RECORD - https://github.com/mjec/aba/blob/master/sample-with-comments.aba
        ///
        /// S.P  E.P  LEN T A F NAME                DESCRIPTION
        ///  1    1    1 F - - Record Type         Must be 7 for batch control record.
        ///  2    8    7 F - - BSB                 Must be "999-999".
        ///  9   20   12 F - S Reserved            Must be twelve blank spaces.
        /// 21   30   10 N R Z Batch Net Total     Total of credits minus total of debits in batch as zero-padded number of cents.
        /// 31   40   10 N R Z Batch Credits Total Total of credits in batch as zero-padded number of cents. Some banks permit
        ///                                        this to be ignored by placing all zeros or all spaces.
        /// 41   50   10 N R Z Batch Debits Total  Total of debits in batch as zero-padded number of cents. Some banks permit
        ///                                        this to be ignored by placing all zeros or all spaces.
        /// 51   74   24 F - S Reserved            Must be twenty four blank spaces.
        /// 75   80    6 N R Z Number of records   Must be the total number of detail records in the batch, zero-padded.
        /// 81  120   40 F - S Reserved            Must be forty blank spaces.
        /// </summary> 
        [TestMethod]
        public void TestBatchControlRecord()
        {
            var abaGenerator = Generate();
            var abaLines = abaGenerator.AbaString.Split(new[] { "\r\n" }, StringSplitOptions.None);            
            var record = abaLines[abaLines.Length - 1];

            Assert.IsTrue(record.Length == 120);
            Assert.IsTrue(record.Substring(2 - 1, 7) == "999-999");
            Assert.IsTrue(record.Substring(9 - 1, 12).Trim() == "");            
            Assert.IsTrue(abaGenerator.GetBatchNetTotal() == int.Parse(record.Substring(21 - 1, 10)));
            Assert.IsTrue(abaGenerator.CreditTotal == int.Parse(record.Substring(31 - 1, 10)));
            Assert.IsTrue(abaGenerator.DebitTotal == int.Parse(record.Substring(41 - 1, 10)));
            Assert.IsTrue(record.Substring(51 - 1, 24).Trim() == "");
            Assert.IsTrue(abaGenerator.NumberRecords == int.Parse(record.Substring(75 - 1, 6)));
            Assert.IsTrue(record.Substring(81 - 1, 40).Trim() == "");
        }

        #region Private funcs

        private AbaFileGenerator Generate()
        {
            var generator = GetAccountDetails();            
            var result = generator.Generate(GetTransactions());
            Assert.IsTrue(result.IsValid, result.FailReason);
            return result.Data;
        }

        private AbaFileGenerator GetAccountDetails()
        {
            return new AbaFileGenerator
            {
                Bsb = "012-012", //ANZANZ E Trade Support
                AccountNumber = "12345678",
                BankName = "CBA",
                UserName = "Some name",
                Remitter = "From some guy",
                DirectEntryUserId = "999999",
                Description = "Payroll",
                IncludeAccountNumberInDescriptiveRecord = false
            };
        }

        /// <summary>
        /// 0123-123 12345678 01CBA       Some name                 999999Payroll     150714
        /// 1234-456   098765 130000000345John Smith                      A direct debit    123-12312345678                 00000000
        /// 1123-456    67832 500000008765Mary Jane                       For dinner        123-12312345678                 00000000
        /// 1098-765    84736 530000007546Borris Becker                   Your salary       123-12312345678                 00000000
        /// 1888-888123456789 530000123456Some Dude                       Your salary       123-12312345678                 00000000
        /// 7999-999            000013942200001397670000000345                        000004
        ///
        /// 0067-102 12341234 01CBA       Smith John Allan          301500ABA Test    0704131530
        /// 1062-692 43214321 500000000001Smith Joan Emma                 ABA Test CR       067-102 12341234Mr John Smith   00000000
        /// 7999-999            000000000100000000010000000000                        000001
        /// </summary>
        /// <returns></returns>
        public List<Transaction> GetTransactions()
        {
            var transactions = new List<Transaction>();
            var item1 = new Transaction {
                AccountName = "John Smith",
                AccountNumber = "098765",
                Bsb = "012-105",
                Amount = 345,
                TransactionCode = TransactionCode.EXTERNALLY_INITIATED_DEBIT,
                Reference = "A direct debit",
                Indicator = TransactionIndicator.X,
                TaxWithholding = 12
            };
            transactions.Add(item1);

            var item2 = new Transaction
            {
                AccountName = "Mary Jane",
                AccountNumber = "67832",
                Bsb = "012-105",
                Amount = 8765,
                TransactionCode = TransactionCode.EXTERNALLY_INITIATED_CREDIT,
                Reference = "For dinner"
            };
            transactions.Add(item2);

            var item3 = new Transaction
            {
                AccountName = "Borris Becker",
                AccountNumber = "84736",
                Bsb = "012-172",
                Amount = 7546,
                TransactionCode = TransactionCode.PAYROLL_PAYMENT,
                Reference = "Your salary"
            };
            transactions.Add(item3);

            var item4 = new Transaction
            {
                AccountName = "Some Dude",
                AccountNumber = "123456789",
                Bsb = "012-276",
                Amount = 123456,
                TransactionCode = TransactionCode.PAYROLL_PAYMENT,
                Reference = "Your salary"
            };
            transactions.Add(item4);            

            return transactions;
        }

        #endregion
    }
}
