# CIBC credit card PDF statements: design addendum

Amends `2026-11-20-statement-import-design.md`. That design listed PDF statements as a non-goal. The owner has reversed
this for **CIBC credit cards only** (2026-11-26). Everything not mentioned here stays as the original design says.

## Why

The owner's CIBC Dividend Visa history exists as monthly PDF statements. CIBC sends one of these every month, so they
are the natural thing to upload. Ten 2025–2026 statements are in `samples/` (git-ignored).

## What the samples show

All 10 sample statements came from the same generator ("Ricoh AFP2PDF"). Each one has a real text layer, so no OCR is
needed.

- **Statement period.** The period is printed as `Transactions from September 30 to October 29, 2026`. Transaction
  dates themselves carry no year (`Oct 07`). Periods can cross a year boundary:
  `December 30, 2025 to January 29, 2026`.
- **Sections.** Each section has columns for transaction date, posting date, description, a CIBC spend category (or
  an interest rate), and an amount.

  | Section | Contents | Total line |
  |---|---|---|
  | `Your payments` | Payments to the card, shown as positive amounts | `Total payments` |
  | `Your interest` | Interest charges, plus `INTEREST REVERSAL` adjustments with a `-1.25***` suffix | `Total interest this period` (excludes the `***` adjustments) |
  | `Your new charges and credits` | Purchases are positive; refunds and credits are **negative** | `Total for <card>` |

  The charges section can continue across pages (`(continued)`) and is split into `Card number …` subsections.
- **Row quirks.**
  - A `Ý` marker in front of a description flags a cash-back multiplier.
  - Foreign purchases have an extra line underneath, such as `50.00 USD @ 1.406000000**`. The row's amount is already
    in CAD.
  - Long descriptions sometimes wrap. The two dates can then end up on a different text line from the rest of the
    row.
- **Account summary.** Gives the previous balance, payments, other credits, purchases, cash advances, interest, fees,
  and the total balance.

A throwaway prototype parsed all 10 statements and checked them against the summary:

- 7 balanced to the cent on the first try.
- The other 3 were off because of the two quirks above (wrapped rows, and `***` reversals). Both are handled below.

## Goals

- Upload a CIBC credit card PDF statement through the existing Import page. It goes through the same review, dedupe,
  transfer matching and commit flow as a CSV.
- Never import a PDF whose rows don't add up to the statement's own totals.
- Pre-fill the review screen's closing balance and statement date from the statement.

## Non-goals

- PDFs from any other bank, or other CIBC products (PLC, savings). Those stay CSV.
- Scanned or image-only PDFs, and OCR.
- Reading the CIBC "Dividend Cash Back" rewards balance.
- Using the foreign-currency amounts. The CAD amount is the one that counts.

## Approaches considered

| | Approach | Trade-off |
|---|---|---|
| **A (chosen)** | PdfPig extracts words with their positions. A CIBC-specific reader groups the words into lines and columns, and checks the result against the statement totals. | Pure .NET, free, offline and exact. It breaks loudly, never silently, if CIBC changes the layout. |
| B | Shell out to `pdftotext -layout` and parse the text with regexes | Simple, but it needs Poppler in the container. Its column guessing is what caused the wrapped-row errors in the prototype. |
| C | AI reads the PDF | Already rejected in the original design: per-upload cost and silent amount errors. |

**Chosen: A.**

## Design

### 1. Upload

- The upload now accepts `.csv` **or** `.pdf`. The limit stays at 5 MB, and the file is still never stored.
- The importer goes by the file's content, not its name. A file starting with `%PDF-` goes to the PDF readers;
  anything else goes to the CSV readers as before. If the extension and the content disagree, the upload fails with
  `The file's contents don't match its .pdf/.csv extension.`
- The unknown-file message becomes:
  `Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple, or a CIBC credit card PDF statement.`
- The validator's message becomes `Only .csv or .pdf files are supported.`
- A PDF that can't be opened (corrupt, or password-protected) fails with
  `The PDF could not be read. Download the statement again from CIBC and retry.`
- A PDF with no text layer is treated as unrecognized.

### 2. Parser and reader shape

- `IStatementParser.Parse(Stream content)` keeps its signature and does the content-based routing.
- `ParsedStatement` gains an optional `StatementSummary? Summary`, made of:
  - `PeriodStart` and `PeriodEnd` (both `DateOnly`)
  - `OpeningBalance` and `ClosingBalance`, as positive amounts owed
  - `ExternalAccountRef`, the last 4 digits of the card

  CSV readers leave `Summary` null.
- A new `IPdfStatementReader` in Infrastructure sits alongside `IStatementReader`, with these members:
  - `Institution`
  - `bool CanRead(PdfText)`
  - `ParsedStatement Read(PdfText)`

  `PdfText` holds the document's extracted lines, page by page.
- Detection is the same as for CSV: exactly one PDF reader must claim the file. The only reader for now is
  `CibcCreditCardPdfReader`. It claims a PDF that contains `CIBC`, `Account number`, and a `Transactions from … to …`
  line.

### 3. Text extraction

- Use PdfPig (`PdfPig` 0.1.16, Apache-2.0), referenced from Infrastructure only.
- Take words from each page and group them into lines by baseline, using a small tolerance.
- Within each section, find the column boundaries from that section's header row (`Trans date`, `Post date`,
  `Description`, `Spend Categories` / `Annual interest rate`, `Amount($)`).
- Assign each word to a column by its x-position. This avoids the wrapped-row problem that plain-text extraction
  caused.
- A **logical row** starts at a line whose date columns hold two `Mon dd` dates. The following lines that have no
  dates are continuation lines: either a wrapped description or the `USD @` line. They attach to that row.

### 4. Rows

| Section | Amount sign | Type | KindHint | CategoryHint |
|---|---|---|---|---|
| Payments | + | Credit | `PendingTransfer` (a payment from a bank account) | `DebtPayments` |
| Charges | + | Debit | Regular | from the spend category, see below |
| Charges | − | Credit (refund/credit) | Regular | none |
| Interest | + | Debit | Regular | `BankFees` |
| Interest `***` adjustment | − | Credit | Regular | `BankFees` |

- **Date.** Use the transaction date, which is what the bank side of a payment shows. A date has no year, so take
  the year of `PeriodEnd`. If that would put the date after `PeriodEnd`, use the previous year instead. For example,
  `Dec 29` on a statement ending `Jan 29, 2026` becomes 2025-12-29.
- **Description.** The merchant text, with the `Ý` marker removed. The foreign-currency line is kept in `RawLine`
  only, so merchant rules and fingerprints stay clean.
- **Amounts.** Always positive, with `Currency = "CAD"`.
- **ExternalAccountRef.** The last 4 digits of the card, on every row.
- **Spend category mapping.** CIBC's categories are coarse ("Retail and Grocery" covers both), so only the
  unambiguous ones become hints:
  - `Restaurants` → `DiningOut`
  - `Transportation` → `Transportation`

  Every other CIBC category gives no hint, and the existing categorizer and remembered merchant rules decide.
- **LineNumber.** The 1-based index of the extracted line across the whole document. A PDF error message names the
  page, e.g. `Page 3: …`.

### 5. Self-check (the main safety net)

The reader verifies the statement before returning anything. All of the following must hold to the cent:

1. The sum of payment rows equals `Total payments`.
2. The sum of interest rows, excluding `***` adjustments, equals `Total interest this period`.
3. For each card subsection, the sum of its charge and credit rows equals that card's `Total for <card>`.
4. `Previous balance − payments + charges and credits + interest + adjustments = Total balance`.

If any check fails, the whole upload fails and nothing is imported. The message is:
`This CIBC statement's transactions don't add up to its totals (off by $X.XX). The statement layout may have changed; nothing was imported.`
This is the anti-goal "a wrong total that looks right" turned into a hard stop.

### 6. Review and commit

- When a batch has a `Summary`, the review screen pre-fills that account's closing balance with `ClosingBalance` and
  the statement date with `PeriodEnd`. The owner can still edit both.
- Reconciliation, dedupe and transfer matching work exactly as for CSV.
- The pre-filled values are only defaults, but the review page must still show them after a page refresh. So
  `ImportBatch` gains nullable `StatementClosingBalance` and `StatementPeriodEnd` columns (with a migration), and the
  import DTO returns them.
- Account routing uses the existing `Institution` + `ExternalAccountRef` match. If the owner's CIBC Visa account has
  `ExternalAccountRef = 1234`, the PDF lands there automatically.

### 7. Frontend

- The file picker accepts `.csv,.pdf`.
- The copy on the Import page mentions CIBC credit card PDF statements.
- The review page pre-fills the closing balance and statement date when the batch has them.

### 8. Testing

- **Real statements stay out of the repo.** No real PDF, extracted text, merchant, amount or card number is
  committed.
- **Line-level parser tests.** These feed made-up `PdfText` lines that reproduce the real structure:
  - sections, a `(continued)` page, two card subsections
  - `Ý`, a wrapped description, a `USD @` line, a refund
  - an interest charge and a `***` reversal
  - a Dec→Jan period
  - one test per self-check failure

  They assert every row's date, description, amount, type, hints and reference.
- **One extraction test.** Builds a small PDF in-memory with PdfPig's `PdfDocumentBuilder`, laid out like the real
  statement (header columns at real-ish x-positions), and parses it end to end.
- **Parser routing tests:**
  - `%PDF-` content goes to the PDF reader
  - an extension/content mismatch is rejected
  - a corrupt PDF gives the "could not be read" message
  - a PDF with no matching reader gives the unknown-file message
- **Real-file check (not committed).** Every PDF in `samples/` passes the self-check, and the row counts match the
  statement.

## Decided by the owner

- Add PDF support for CIBC credit card statements (2026-11-26).

## Calls made by Claude (open to correction)

- CIBC credit card PDFs only. Other CIBC products stay CSV.
- Use the transaction date, not the posting date.
- Only `Restaurants` and `Transportation` map to category hints.
- A self-check failure fails the whole upload rather than producing a warning.
- The foreign-currency detail stays in `RawLine` and is not added to the description.
- The statement's closing balance and date are stored on the batch and used to pre-fill the review screen.

## Related: Wealthsimple findings (plan Task 20)

Recorded here because they came out of the same sample review:

- The Wealthsimple TFSA CSV contains share-lending and reorganisation rows (`LOAN`, `RECALL`, `STKREORG`). These have
  a `0.0` amount and a ticker in the currency column. The Wealthsimple reader skips rows whose amount is zero, so a
  TFSA file isn't rejected as non-CAD.
- Codes seen so far:
  - Chequing: `INT`, `AFT_IN`, `AFT_OUT`, `E_TRFIN`, `E_TRFOUT`, `OBP_OUT`
  - TFSA: `BUY`, `DIV`, `NRT`, `FPLINT`, `LOAN`, `RECALL`, `STKREORG`
