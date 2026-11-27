# CIBC PDF Statements and Wealthsimple Reader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import Wealthsimple CSV exports and CIBC credit card PDF statements through the existing import flow.
The PDF import must refuse any statement whose rows don't add up to the statement's own totals.

**Architecture:**
- The Wealthsimple reader is a new `IStatementReader` (CSV).
- `StatementParser` gains content-based routing: `%PDF-` content goes to new `IPdfStatementReader`s, which read a
  positional text model (`PdfText`) built by a PdfPig-based extractor.
- `CibcCreditCardPdfReader` turns `PdfText` into rows plus a `StatementSummary` and self-checks the totals.
- The summary's closing balance is stored on `ImportBatch` and pre-fills the review screen.

**Tech Stack:** .NET 8, MediatR, FluentValidation, EF Core 8 + Npgsql, CsvHelper, **PdfPig 0.1.16 (new)**, xUnit +
FluentAssertions + NSubstitute, Testcontainers, React 18 + react-query v3 + Tailwind.

**Spec:** `docs/superpowers/specs/2026-11-26-cibc-pdf-statements-design.md`, which amends
`docs/superpowers/specs/2026-11-20-statement-import-design.md`. The Wealthsimple half implements Task 20 of
`docs/superpowers/plans/2026-11-20-statement-import.md`, within the limits set below.

## Global Constraints

- Every constraint in `.superpowers/sdd/2026-11-20-statement-import/global-constraints.md` still applies:
  - central package versions
  - Clean Architecture boundaries
  - NSubstitute
  - `decimal(18,2)`
  - CAD only
  - 5 MB limit
  - files never stored
  - user scoping
  - rulings R1, R7, R8 and R9

  The `.csv`-only rule and the unknown-file message are amended below.
- Add `<PackageVersion Include="PdfPig" Version="0.1.16" />` to `Directory.Packages.props`. Reference it **only**
  from `FinSight.Infrastructure` and `FinSight.Infrastructure.Tests`.
- **Real statements stay out of the repo.** Never commit a real PDF, extracted text, merchant, amount, name, address
  or card number. Every fixture is made up and only copies the real structure.
- Exact copy:
  - Unknown file: `Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple, or a CIBC credit card PDF statement.`
  - Validator: `Only .csv or .pdf files are supported.`
  - Extension mismatch: `The file's contents don't match its .pdf/.csv extension.`
  - Unreadable PDF: `The PDF could not be read. Download the statement again from CIBC and retry.`
  - Totals mismatch: `This CIBC statement's transactions don't add up to its totals (off by $X.XX). The statement layout may have changed; nothing was imported.`
    `X.XX` is the absolute difference, formatted `0.00` with the invariant culture.
  - PDF row error: `Page {page}: {message}`. CSV row errors keep `Row {line}: {message}`.
- Error codes:
  - `Import.UnknownFormat` (existing)
  - `Import.FileMismatch`
  - `Import.PdfUnreadable`
  - `Import.TotalsMismatch`
  - `Import.Format` (existing; used for PDF row errors too)
- Commit messages use the `feat:` / `fix:` / `test:` / `refactor:` / `docs:` prefixes and end with
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

**Plan rulings (where the plan settles something the spec left open or got wrong):**
- **P1.** `IStatementParser.Parse` becomes `Parse(Stream content, string fileName)`. The spec says the signature is
  kept, but the extension/content mismatch check needs the file name.
- **P2.** The existing `ImportBatch.PeriodEnd` column stands in for the spec's `StatementPeriodEnd`. When a batch has
  a summary, `PeriodEnd` is set to the summary's `PeriodEnd`. The frontend already pre-fills the statement date from
  `periodEnd`. Only `StatementClosingBalance` is new.
- **P3.** The Wealthsimple reader leaves `ExternalAccountRef` null, because the CSV content carries no account number.
  If there are several Wealthsimple accounts, the existing review-screen account picker handles it.
- **P4.** The Wealthsimple code table holds only the codes seen in the samples. Any other code fails the upload,
  naming the code. RRSP, FHSA and crypto samples are still missing, so codes get added once those files exist.

## Review Focus

1. **Bad PDF.** A password-protected or corrupt PDF must give the "could not be read" message, never a 500
   (Task 2: `Parse_CorruptPdf_PdfUnreadable`).
2. **Year boundary.** On a Dec–Jan statement, a December purchase that posts before the period starts must get the
   previous year (Task 3: `Read_DecJanPeriod_AssignsPreviousYearToDecember`).
3. **Row with one date.** One real sample has a charge row with only one date in the date area. It must parse with
   that date and still balance (Task 3: `Read_RowWithSingleDate_UsesThatDate`).
4. **Re-upload.** Uploading the same PDF twice must flag every row as already imported, not double-count it
   (Task 4: `UploadPdf_Twice_SecondBatchAllDuplicates`).
5. **Wealthsimple TFSA rows.** `$0` rows with a ticker in the currency column must be skipped, not fail the file as
   non-CAD (Task 1: `Read_ZeroAmountSecurityEvent_Skipped`).

---

### Task 1: Wealthsimple CSV reader

**Files:**
- Create: `src/FinSight.Infrastructure/Imports/Readers/WealthsimpleStatementReader.cs`
- Create fixtures: `tests/FinSight.Infrastructure.Tests/Imports/Fixtures/wealthsimple-cash.csv` and
  `wealthsimple-tfsa.csv`
- Create tests: `tests/FinSight.Infrastructure.Tests/Imports/Readers/WealthsimpleStatementReaderTests.cs` and
  `tests/FinSight.Infrastructure.Tests/Imports/DetectionTests.cs`
- Modify: `src/FinSight.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` to register the reader as
  `IStatementReader`.
- Modify: the test csproj, so fixtures are copied to the output directory.

**Interfaces:**
- Consumes: `IStatementReader`, `CsvRow`, `ParsedStatementRow`, `StatementFormatException` (all existing).
- Produces: `WealthsimpleStatementReader : IStatementReader` with `Institution => Institution.Wealthsimple`.

**Real layout** (from `samples/`, not committed):
- Header: `"date","transaction","description","amount","balance","currency"`.
- Every field is quoted. There is no BOM and no trailing newline.
- Dates are `yyyy-MM-dd`. Amounts are signed decimals with 1 or 2 decimal places (`-1500.0`, `11.11`).

**Fixture rule:**
- Same structure as the real files, with made-up values.
- Cash fixture covers every chequing code. It includes:
  - a description containing a comma
  - a same-day identical pair
- TFSA fixture covers every investment code, including the zero-amount `LOAN`, `RECALL` and `STKREORG` rows with a
  ticker in the currency column.

**Code table** (the sign of `amount` sets `Type`: negative is a Debit, positive a Credit; `Amount` is the absolute
value):

| Code | KindHint | CategoryHint |
|---|---|---|
| `INT`, `FPLINT` | Regular | `InvestmentIncome` |
| `DIV` | Regular | `InvestmentIncome` |
| `NRT` (non-resident tax) | Regular | `InvestmentIncome` |
| `BUY` | `InvestmentTrade` | none |
| `AFT_IN`, `E_TRFIN`, `E_TRFOUT`, `OBP_OUT` | `PendingTransfer` | none |
| `AFT_OUT` | Regular | none |
| `LOAN`, `RECALL`, `STKREORG` | only ever zero-amount; skipped | — |

- A row whose amount is `0` is skipped, whatever its code.
- An unknown code throws `StatementFormatException(line, $"Unknown Wealthsimple transaction code '{code}'.")`.

- [ ] **Step 1: Write the fixtures and failing tests.**
  - `Read_CashFixture_ParsesExactRows`: asserts every row's `Date`, `Description`, `Amount`, `Type`, `KindHint`,
    `CategoryHint`, `Currency == "CAD"`, `LineNumber`, and `ExternalAccountRef == null`.
  - `Read_MapsEveryCode`: a `[Theory]`, one case per non-zero code in the table, checking the kind and category.
  - `Read_ZeroAmountSecurityEvent_Skipped`: the TFSA fixture yields no row for `LOAN`, `RECALL` or `STKREORG`, and
    the whole fixture goes through `StatementParser` with success.
  - `Read_UnknownCode_ThrowsStatementFormatException`: the message contains the code.
  - `Read_MalformedDate_ThrowsStatementFormatException`.
  - `Read_WrongFieldCount_ThrowsStatementFormatException`.
  - `CanRead_HeaderOnly_True`.
  - `DetectionTests.EachFixture_ClaimedByExactlyOneReader`: a theory over every file in `Fixtures/` against all
    readers registered by `AddInfrastructure`, or against the reader list built the same way.
- [ ] **Step 2: Run** `dotnet test tests/FinSight.Infrastructure.Tests` and confirm the new tests fail.
- [ ] **Step 3: Implement** the reader.
  - `CanRead` matches the header case-insensitively.
  - `Read` validates the field count is 6 and parses with the invariant culture.
- [ ] **Step 4: Run** `dotnet test tests/FinSight.Infrastructure.Tests`. Expected: all pass.
- [ ] **Step 5: Real-file check** (nothing committed). Run every `samples/**/*.csv` Wealthsimple file through
  `StatementParser`, for example in a throwaway test that is not committed. Every file must parse. Report the row
  counts per file.
- [ ] **Step 6: Commit** `feat: add Wealthsimple statement reader`.

---

### Task 2: PDF plumbing and content routing

**Files:**
- Modify: `Directory.Packages.props`; `src/FinSight.Infrastructure/FinSight.Infrastructure.csproj`;
  `tests/FinSight.Infrastructure.Tests/FinSight.Infrastructure.Tests.csproj` (add `PdfPig`).
- Create:
  - `src/FinSight.Infrastructure/Imports/Pdf/PdfText.cs`
  - `src/FinSight.Infrastructure/Imports/Pdf/PdfTextExtractor.cs`
  - `src/FinSight.Infrastructure/Imports/Pdf/IPdfStatementReader.cs`
  - `src/FinSight.Infrastructure/Imports/Pdf/StatementTotalsMismatchException.cs`
- Modify:
  - `src/FinSight.Application/Common/Interfaces/IStatementParser.cs`
  - `src/FinSight.Application/Common/Exceptions/StatementFormatException.cs`
  - `src/FinSight.Infrastructure/Imports/StatementParser.cs`
  - `CreateImportCommandHandler.cs` (pass `request.FileName`)
  - `CreateImportCommandValidator.cs`
  - every existing caller and test of `Parse`
- Test:
  - `tests/FinSight.Infrastructure.Tests/Imports/Pdf/PdfTextExtractorTests.cs`
  - `tests/FinSight.Infrastructure.Tests/Imports/StatementParserTests.cs` (extend it)
  - `tests/FinSight.Infrastructure.Tests/Imports/Pdf/FakePdfReader.cs`
  - `tests/FinSight.Infrastructure.Tests/Imports/Pdf/TestPdf.cs`, a builder helper
  - the validator test, if one exists

**Interfaces (produced):**
```csharp
// Application
public interface IStatementParser { Result<ParsedStatement> Parse(Stream content, string fileName); }
public sealed record ParsedStatement(Institution Institution, IReadOnlyList<ParsedStatementRow> Rows,
    StatementSummary? Summary = null);
public sealed record StatementSummary(DateOnly PeriodStart, DateOnly PeriodEnd, decimal OpeningBalance,
    decimal ClosingBalance, string? ExternalAccountRef);
// StatementFormatException gains: public int? Page { get; }  + ctor (int line, int page, string message)

// Infrastructure.Imports.Pdf
public sealed record PdfWord(string Text, double Left, double Right);
public sealed record PdfLine(int Page, int LineNumber, double Baseline, IReadOnlyList<PdfWord> Words)
{ public string Text => string.Join(' ', Words.Select(w => w.Text)); }
public sealed record PdfText(IReadOnlyList<PdfLine> Lines);
public static class PdfTextExtractor { public static PdfText Extract(byte[] pdf); } // throws PdfUnreadableException
public sealed class PdfUnreadableException(Exception inner) : Exception(null, inner);
public interface IPdfStatementReader { Institution Institution { get; } bool CanRead(PdfText text); ParsedStatement Read(PdfText text); }
public sealed class StatementTotalsMismatchException(decimal difference) : Exception { public decimal Difference { get; } }
```

**Extraction rules:**
- Use PdfPig's `PdfDocument.Open(bytes)` and `page.GetWords()`.
- Group words into a line when their baselines (`BoundingBox.Bottom`) are within **2.0 pt**.
- Order lines top to bottom, and words within a line left to right.
- `LineNumber` is 1-based across the whole document.
- Any PdfPig exception, an encrypted document, or zero pages becomes a `PdfUnreadableException`.

**Routing rules in `StatementParser.Parse(content, fileName)`:**
- Read the first 5 bytes. `%PDF-` means PDF, anything else means CSV.
- If the extension is `.pdf` but the content is CSV, or the other way round: `Import.FileMismatch`.
- PDF path:
  - Extract the text, then require exactly one `IPdfStatementReader.CanRead`; otherwise `Import.UnknownFormat`.
  - A PDF with no words is unknown format.
  - `StatementFormatException` with a `Page` becomes `Import.Format` with `Page {page}: {message}`.
  - `StatementTotalsMismatchException` becomes `Import.TotalsMismatch` with the exact copy.
  - `PdfUnreadableException` becomes `Import.PdfUnreadable`.
  - The existing positive-amount, empty and CAD-only checks run on PDF rows too.
- CSV path: unchanged, apart from the new unknown-file copy.
- The constructor takes `IEnumerable<IStatementReader>` **and** `IEnumerable<IPdfStatementReader>`. Register no PDF
  reader yet; Task 3 adds one.

- [ ] **Step 1: Write the failing tests.**
  - `TestPdf.Build(params (int page, double x, double y, string text)[])`: a helper that uses PdfPig's
    `PdfDocumentBuilder` with Helvetica to place text at exact positions and return bytes.
  - `Extract_GroupsWordsOnSameBaselineIntoOneLine`: text at the same y but out of x order comes back as one line,
    in x order.
  - `Extract_SplitsLinesAndNumbersAcrossPages`.
  - `Extract_Garbage_ThrowsPdfUnreadable`.
  - Parser tests:
    - `Parse_PdfContent_RoutesToPdfReader`, using `FakePdfReader`.
    - `Parse_PdfExtensionCsvContent_FileMismatch`.
    - `Parse_CsvExtensionPdfContent_FileMismatch`.
    - `Parse_CorruptPdf_PdfUnreadable`: bytes are `%PDF-1.4` followed by junk.
    - `Parse_PdfNoReader_UnknownFormatWithNewCopy`.
    - `Parse_PdfReaderTotalsMismatch_TotalsMismatchCopy`: asserts `off by $1.50` for a difference of `-1.5m`.
    - `Parse_PdfRowFormatError_PageMessage`.
  - Validator tests:
    - `.pdf` is accepted.
    - `.txt` is rejected with `Only .csv or .pdf files are supported.`
- [ ] **Step 2: Run** `dotnet test tests/FinSight.Infrastructure.Tests tests/FinSight.Application.Tests` and confirm the
  new tests fail.
- [ ] **Step 3: Implement** the interfaces and rules above. Update every `Parse(` call site, including the test doubles
  in the integration tests.
- [ ] **Step 4: Run** `dotnet test FinSight.sln` with Docker running. Expected: everything passes, including the
  architecture tests (PdfPig must not leak into Application).
- [ ] **Step 5: Commit** `feat: route PDF uploads to PDF statement readers`.

---

### Task 3: CIBC credit card PDF reader

**Files:**
- Create: `src/FinSight.Infrastructure/Imports/Readers/CibcCreditCardPdfReader.cs`; register it as
  `IPdfStatementReader`.
- Test:
  - `tests/FinSight.Infrastructure.Tests/Imports/Readers/CibcCreditCardPdfReaderTests.cs`, using synthetic `PdfText`
  - `tests/FinSight.Infrastructure.Tests/Imports/Pdf/CibcPdfEndToEndTests.cs`, using a `TestPdf`-built document

**Interfaces:**
- Consumes: `PdfText`, `PdfLine`, `PdfWord`, `IPdfStatementReader`, `StatementSummary`,
  `StatementTotalsMismatchException`, and `StatementFormatException(line, page, message)` (Task 2).
- Produces: `CibcCreditCardPdfReader : IPdfStatementReader` with `Institution.Cibc`.

**Real layout facts** (measured on the samples; positions in PDF points from the left edge):
- Header words:
  - `Trans`/`date` at x≈36.7
  - `Post`/`date` at x≈77–79
  - `Description` at x≈118.8
  - `Spend Categories` (charges) or `Annual interest rate` (interest) at x≈377–505
  - `Amount($)` at x≈504
- Amounts are right-aligned and end at x≈575.7.
- All words of one row share a baseline, but the content stream emits them column by column. This is why grouping
  must be by baseline, not by stream order.
- Dates are `Mmm dd` (`Oct 07`), split into two words.
- Section starts: `Your payments`, `Your interest`, `Your new charges and credits` (also `… (continued)` on later
  pages).
- Section totals:
  - `Total payments $1,000.00`
  - `Total interest this period $0.00`
  - `Total for <card number> $2,000.00`
- Card subsections start with `Card number 4500 XXXX XXXX 1234`.
- Summary lines on page 1:
  - `Previous balance $1,500.00`
  - `Total balance = $2,500.00`
  - `Account number 4500 XXXX XXXX 1234`
- Period line: `Transactions from September 30 to October 29, 2026`. It can also read
  `Transactions from December 30, 2025 to January 29, 2026`, and the samples also contain the variant
  `December 30 , 2025`, with a space before the comma.
- Row decorations:
  - A leading cash-back marker (`Ý` in pdftotext output; PdfPig may decode it as another single non-ASCII glyph) is
    stripped.
  - Foreign purchases have a following line `50.00 USD @ 1.406000000**`, whose words sit left of the amount column.
- Interest reversals: amounts like `-1.25***`. These are not in `Total interest this period`, but they are in
  `Total balance`.
- One real charge row has **only one** date word pair in the date area.

**Parsing rules:**
- The **amount column** is any word whose `Left >= amountHeader.Left - 10` and that matches
  `^-?[\d,]+\.\d{2}(\*{3})?$`.
- A **row anchor** is a line inside a section that has an amount-column word and at least one `Mmm dd` pair left of
  the `Description` header.
  - With two pairs, the first is the transaction date.
  - With one pair, that date is used.
- A **continuation line** is a line in a section with no amount-column word and no date. Its words are appended to
  the previous row's `RawLine` only.
- `Total …`, header and `Card number` lines are not rows.
- **Description** is the anchor's words between the date columns and the category/rate column, joined by single
  spaces, with a leading one-character non-ASCII token removed.
- Row types and hints follow spec §4:

  | Section | Sign | Type | KindHint | CategoryHint |
  |---|---|---|---|---|
  | Payments | + | Credit | `PendingTransfer` | `DebtPayments` |
  | Charges | + | Debit | Regular | `Restaurants` → `DiningOut`, `Transportation` → `Transportation`, otherwise null |
  | Charges | − | Credit | Regular | null |
  | Interest | + | Debit | Regular | `BankFees` |
  | Interest `***` | − | Credit | Regular | `BankFees` |

- **Year:** use `PeriodEnd.Year`. If the resulting date is later than `PeriodEnd`, use `PeriodEnd.Year - 1`.
- Every row gets `Currency = "CAD"` and `ExternalAccountRef` = the last 4 digits of `Account number`.
- `MerchantName` stays null.
- `RawLine` is the anchor text plus any continuation text, capped at 1000 characters.
- **Summary:**
  - `PeriodStart` and `PeriodEnd` come from the period line.
  - `OpeningBalance` is `Previous balance`; `ClosingBalance` is `Total balance`.
  - `ExternalAccountRef` is the last 4 digits.
- **Self-check:** these are evaluated in order, and the first failure throws
  `StatementTotalsMismatchException(computed - printed)`:
  1. The sum of payment rows equals `Total payments`.
  2. The sum of interest rows without `***` equals `Total interest this period`.
  3. For each card subsection, the signed sum of its charge rows equals its `Total for`.
  4. `Previous balance - payments + Σcharges(signed) + Σinterest(signed, including ***)` equals `Total balance`.

  A statement with no `Your interest` section has zero interest. A missing `Previous balance`, `Total balance` or
  period line throws `StatementFormatException(line: 1, page: 1, "Statement summary not found.")`.
- **CanRead:** the document text contains `CIBC`, a line starting `Account number`, and a `Transactions from … to …`
  line.

- [ ] **Step 1: Write the failing tests.** Use a small builder in the test file that makes `PdfLine`s at given x
  positions using the real column positions above, so every test reads like a mini statement. Use made-up merchants,
  amounts and card `4500 XXXX XXXX 1234`.
  - `Read_BasicStatement_ParsesExactRowsAndSummary`. Rows: 2 payments, 3 charges (one `Restaurants`), 1 refund,
    1 interest charge. Assert every field of every row, plus the summary.
  - `Read_ContinuedSectionAcrossPages_AllRowsParsed`.
  - `Read_TwoCardSubsections_EachTotalChecked`.
  - `Read_CashBackMarker_StrippedFromDescription`.
  - `Read_ForeignCurrencyLine_InRawLineNotDescription`.
  - `Read_InterestReversalTripleStar_CreditExcludedFromInterestTotalIncludedInBalance`.
  - `Read_DecJanPeriod_AssignsPreviousYearToDecember`: period `December 30, 2025 to January 29, 2026` (also with
    `December 30 , 2025`); a `Dec 27` trans / `Dec 30` post charge becomes 2025-12-27; `Jan 05` becomes 2026-01-05.
  - `Read_RowWithSingleDate_UsesThatDate`.
  - `Read_NoInterestSection_Balances`.
  - `Read_PaymentsTotalWrong_ThrowsMismatchWithDifference`. Write one each for interest, the card total and the
    balance equation (4 tests, or a theory).
  - `Read_MissingSummary_ThrowsFormatException`.
  - `CanRead_CibcStatement_True` and `CanRead_OtherPdf_False`.
  - End to end: `CibcPdf_BuiltDocument_ParsesThroughStatementParser`. Build a 2-page PDF with `TestPdf` at the real
    positions, with the rows emitted column by column (all dates, then all descriptions, then all amounts) to mimic
    the real content-stream order. Assert it parses through `StatementParser` with the expected row count and
    summary.
- [ ] **Step 2: Run** `dotnet test tests/FinSight.Infrastructure.Tests` and confirm the new tests fail.
- [ ] **Step 3: Implement** the reader.
- [ ] **Step 4: Run** `dotnet test tests/FinSight.Infrastructure.Tests`. Expected: all pass.
- [ ] **Step 5: Real-file check** (nothing committed). Run every `samples/**/*.pdf` statement through `StatementParser`
  with a throwaway, uncommitted test or console snippet. Expected:
  - All 10 CIBC statements succeed, which means the self-check passed.
  - The Wealthsimple `CRM2` PDF gets `Import.UnknownFormat`.

  Report each file's row count and the summary's closing balance. If a real file fails, fix the reader (adding a
  made-up test that reproduces the structure), not the check.
- [ ] **Step 6: Commit** `feat: add CIBC credit card PDF statement reader`.

---

### Task 4: Store the statement balance, pre-fill review, and accept PDFs in the UI

**Files:**
- Modify:
  - `src/FinSight.Domain/Entities/ImportBatch.cs`
  - its EF configuration
  - a new migration, `AddImportBatchStatementBalance`
  - `CreateImportCommandHandler.cs`
  - `ImportDtos.cs`, plus the `ImportDtoLoader` mapping
- Frontend:
  - `frontend/src/types/index.ts`
  - `frontend/src/components/imports/UploadDropzone.tsx`
  - `frontend/src/pages/ImportPage.tsx`
- Test:
  - a domain test for the new method
  - `tests/FinSight.Api.IntegrationTests/.../ImportsEndpointTests.cs`, or a new `PdfImportTests.cs`, using the real
    `CibcCreditCardPdfReader` with a `TestPdf`-style document (copy the builder into the integration test helpers, or
    reference it)

**Interfaces:**
- Consumes: `ParsedStatement.Summary` (Task 2), and the CIBC reader (Task 3) for the integration test.
- Produces:
  - `ImportBatch.StatementClosingBalance` (`decimal?`, precision 18,2).
  - `ImportBatch.ApplyStatementSummary(DateOnly periodStart, DateOnly periodEnd, decimal closingBalance)`. It sets
    `StatementClosingBalance`, sets `PeriodEnd = periodEnd`, and sets
    `PeriodStart = min(existing PeriodStart, periodStart)`. The handler calls it **after** all rows are added (ruling
    P2).
  - `ImportBatchDto` gains `decimal? StatementClosingBalance` as its last parameter.
  - Frontend `ImportBatch` gains `statementClosingBalance: number | null`.

- [ ] **Step 1: Write the failing tests.**
  - Domain: `ApplyStatementSummary_SetsClosingBalanceAndPeriodEnd`. Rows dated Sep 26–Oct 29 with a summary of
    Sep 30–Oct 29 give `PeriodStart` Sep 24 and `PeriodEnd` Oct 29.
  - Integration:
    - `UploadPdf_CibcStatement_CreatesBatchWithClosingBalance`: POST a built PDF named `statement.pdf`. Assert 200,
      institution `Cibc`, the row count, `statementClosingBalance`, and `periodEnd`. The account with
      `Institution.Cibc`, `ExternalAccountRef "1234"` and type `CreditCard` is auto-picked.
    - `UploadPdf_Twice_SecondBatchAllDuplicates`: commit the first batch, upload again, and every row is flagged
      already imported and excluded.
    - `UploadCsv_StatementClosingBalanceNull`.
- [ ] **Step 2: Run** and confirm the new tests fail.
- [ ] **Step 3: Implement** the backend changes. Generate the migration with
  `dotnet ef migrations add AddImportBatchStatementBalance` (using the existing tool manifest and the same
  project/startup flags as earlier migrations).
- [ ] **Step 4: Implement** the frontend changes.
  - Dropzone: `accept=".csv,.pdf,text/csv,application/pdf"`, client-side check `.csv` or `.pdf` with the validator
    copy, and helper text `CSV exports from CIBC, TD, MBNA, RBC or Wealthsimple, or a CIBC credit card PDF statement.`
  - `ImportPage`: when a new entry is initialised and `batch.statementClosingBalance != null`, set
    `value: String(batch.statementClosingBalance)`. `asOf` already comes from `periodEnd`. Never overwrite a value
    the user has typed.
- [ ] **Step 5: Verify.**
  - `dotnet test FinSight.sln` (Docker running): all pass.
  - `cd frontend && npx tsc --noEmit && npx vite build`: no errors.
  - Then `git checkout -- frontend/dist` and remove any new untracked `frontend/dist/assets/*` files from the build.
- [ ] **Step 6: Commit** `feat: pre-fill statement balance from PDF statements and accept PDF uploads`.
