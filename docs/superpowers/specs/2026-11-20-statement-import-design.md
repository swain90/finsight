# Statement Import — Design

**Date:** 2026-11-20
**Status:** Draft, awaiting approval
**Roadmap position:** Step 2 of 6 (after build clean-up; before holdings, tax, live connections, AI insights)

## Intent

The owner wants FinSight to be the single place to see their personal finances. Each month they download CSV
statements from their banks, upload them, and immediately see three things:

1. **Spending by category**
2. **Net worth**
3. **How much tax they owe** (the tax estimate itself is step 4; this step must capture the data it needs)

Every number feeds a tax estimate, so correctness matters more than convenience.

## The owner's situation

The owner is **incorporated, sole shareholder**. Money flows like this:

```
Client payments ──► RBC business chequing (CORPORATION) ──┬──► Wealthsimple (personal)
                                                          └──► CIBC savings (personal) ──► cards / lines of credit
```

| Institution | Accounts | Owner |
|---|---|---|
| CIBC | Credit card, personal line of credit (PLC), savings | Personal |
| TD | Credit card | Personal |
| MBNA | Personal line of credit | Personal |
| RBC | Business chequing | **Corporation** |
| Wealthsimple | Cash, TFSA, RRSP, FHSA, Crypto | Personal |

All five institutions export CSV.

**Consequences (agreed in conversation):**
- Client deposits into RBC are **corporate revenue**, not personal income.
- Each transfer from RBC to a personal account is **pay from the corporation**: personal income on the personal side,
  "paid to owner" on the corporate side. Whether it was salary or dividend is *not* decided at import. The T4/T5 in
  step 4 settles that.
- Transfers between personal accounts (card payments, LOC payments, Wealthsimple contributions) are **neither income
  nor spending**.
- The corporation gets its own simple view (revenue, paid to owner, expenses, balance). It is kept out of personal
  spending and budgets.

## Goals

- Upload one CSV at a time, from any of the five institutions. The app detects which institution it came from.
- A **review screen** before anything is saved: rows, suggested categories, duplicates and likely transfers, all visible
  and editable.
- **No duplicates**, even when statement periods overlap or a file is uploaded twice.
- **Transfers matched across accounts**, regardless of upload order, so income and spending are counted once.
- **Month-end balance per account**, entered from the statement at upload, driving net worth and net-worth history.
- **Reconciliation check:** previous balance + this period's transactions vs. the entered closing balance. A mismatch is
  shown as a warning (it usually means missing rows).
- Category corrections are **remembered per merchant** and applied to future imports.
- Personal spending, budgets, insights and anomaly detection **exclude** transfers, investment trades and corporate
  accounts.
- Dashboard shows net worth (personal, and including the corporation), spending by category, and year-to-date pay from
  the corporation.

## Non-goals (this step)

- Investment holdings, positions, cost basis, prices (step 3). Wealthsimple buy/sell rows are imported only as
  "investment trade" activity, excluded from spending.
- Tax calculation (step 4).
- PDF statements, Plaid/Flinks/SnapTrade live connections (step 5).
- AI categorization (step 6). The existing rule + ML.NET categorizer plus remembered merchant rules are used.
- Foreign-currency accounts or conversion. Rows with a non-CAD amount are rejected with a clear message.
- Full corporate bookkeeping (HST, payroll remittances, GL). The corporation view is a simple cash summary.

## Anti-goals (would be a failure even if it "works")

- A wrong total that looks right: any row the importer isn't sure about must be visible, never silently guessed.
- Income or spending double-counted because of transfers or re-uploads.
- Corporate money appearing in personal spending or budgets.
- Uploaded files retained on the server. Only the parsed rows are kept.

## Approaches considered

| | Approach | Trade-off |
|---|---|---|
| **A (chosen)** | A dedicated reader per institution, with auto-detection and a review screen | Exact, free, instant, fully testable with fixture files. Needs a small fix if a bank changes its export layout. |
| B | The owner maps columns once per account; the app remembers | Works for any bank without code, but means 11 setups, more UI, and sign conventions (cards, MBNA) are easy to get wrong. |
| C | AI reads any uploaded file | Handles anything incl. PDFs, but costs per upload and can make silent amount errors, which is unacceptable for tax data. |

**Chosen: A.** The owner picked it. The institution set is fixed and small, and exact numbers matter more than
flexibility.

## Design

### 1. Domain changes

**Account**
- `AccountType` gains: `LineOfCredit`, `Tfsa`, `Rrsp`, `Fhsa`, `Crypto`. Liabilities are `CreditCard`,
  `LineOfCredit`, `Mortgage`, `Loan`.
- New `AccountOwner` enum: `Personal` (default), `Corporation`.
- New `Institution` enum: `Cibc`, `Td`, `Mbna`, `Rbc`, `Wealthsimple`, `Other`.
- New optional `ExternalAccountRef` (string, e.g. last 4 digits). Used to route rows when one file holds several
  accounts (RBC), and to auto-pick the account on upload.

**Transaction**
- New `Kind` enum: `Regular`, `Transfer`, `OwnerPay`, `InvestmentTrade`, `PendingTransfer`.
  - `Transfer`: between two personal accounts, or two corporate ones. Excluded from income and spending.
  - `OwnerPay`: one side of a corporation → personal transfer. The personal side counts as personal income (category
    `PayFromCorporation`); the corporate side counts as "paid to owner".
  - `InvestmentTrade`: Wealthsimple buy/sell. Excluded from spending and income.
  - `PendingTransfer`: looks like a transfer but has no partner yet (partner's file not uploaded, or more than one
    candidate). **Excluded from all totals** until resolved, and counted in a visible "N unresolved" badge, so nothing is
    silently guessed. Set when a row has a transfer candidate, a transfer description hint, or comes from a Wealthsimple
    contribution/withdrawal code. Re-evaluated on every commit, so it resolves itself when the other file arrives.
- New `TransferGroupId` (Guid?): links the two sides of a matched transfer or owner pay.
- New `ImportBatchId` (Guid?): which import created it. Used for undo.
- New `ImportFingerprint` (string?): unique per account. See §4.
- New category `PayFromCorporation` (income group).
- A new domain method `Account.ImportTransaction(...)` records a transaction **without** changing `Account.Balance`.
  Imported balances come from balance snapshots, not running totals. The existing `RecordTransaction` (manual entry)
  is unchanged.

**New entities**
- `BalanceSnapshot`: `AccountId`, `AsOf` (date), `Balance` (Money). For liabilities, the balance is stored as a
  positive amount owed. `Account.Balance` is set to the latest snapshot. For **investment accounts (TFSA, RRSP, FHSA,
  Crypto, Investment, Retirement) the snapshot is the total market value** from the statement, so net worth is right
  before step 3. Reconciliation is **skipped** for those types (prices move and trades are excluded).
- **Liability sign convention changes globally**: liability balances are stored positive (amount owed) everywhere, and
  net worth = assets − liabilities. The dashboard and accounts page, which currently sum signed balances, are updated.
- An account with no snapshot uses its current `Balance` in net worth, and has no history points before its first
  snapshot.
- Manual `RecordTransaction` stays available (e.g. cash accounts). On an account with snapshots, the next snapshot
  overrides the balance.
- `ImportBatch`: `UserId`, `Institution`, `FileName`, `Status` (`PendingReview`, `Committed`, `Discarded`), `PeriodStart`,
  `PeriodEnd`, `CreatedAt`, `CommittedAt`.
- `ImportRow`: belongs to a batch. Holds parsed date, description, amount, direction, target account, suggested
  category, `Kind` hint, duplicate flag, excluded flag, and the original raw line (for debugging).
- `MerchantRule`: `UserId`, normalized description key, `Category`. Created when the owner changes a category on review.

### 2. Readers (one per institution)

Interface: `IStatementReader { Institution; bool CanRead(CsvShape); IEnumerable<ParsedRow> Read(stream) }`.
The importer tries each reader's `CanRead` against the header/first rows. Exactly one must match, otherwise the upload
fails with "Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple."

Every reader normalizes to one convention: **from the account holder's point of view, money leaving the account (or a
charge on a card/LOC) is a Debit; money arriving (or a payment to a card/LOC) is a Credit.** Amounts are always
positive.

Expected layouts (**must be verified against real, redacted samples from the owner before the readers are written**):

| Institution | Header | Columns | Notes |
|---|---|---|---|
| CIBC (bank, card, PLC) | none | `date (YYYY-MM-DD), description, debit, credit[, card number]` | One of debit/credit is filled. Card exports add a masked card number. |
| TD credit card | none | `date (MM/DD/YYYY), description, debit, credit, balance` | Debit = charge, credit = payment. |
| MBNA | yes | `Posted Date, Payee, Address, Amount` | Single signed amount: negative = charge, positive = payment. |
| RBC | yes | `Account Type, Account Number, Transaction Date, Cheque Number, Description 1, Description 2, CAD$, USD$` | Signed amount. One file can contain several accounts, routed by account number. A row with only `USD$` is rejected. |
| Wealthsimple | yes | `date, transaction, description, amount, balance, currency` | `transaction` code drives `Kind`: buys/sells → `InvestmentTrade`; dividends/interest → `InvestmentIncome`; contributions/deposits/withdrawals → transfer candidates; fees → `BankFees`. The full code list is confirmed from samples. Crypto account layout to be confirmed. |

### 3. Import flow

```
Upload CSV ──► detect institution ──► parse ──► pick account(s) ──► dedupe check ──► suggest categories
           ──► REVIEW (edit category / exclude row / enter closing balance + statement date)
           ──► COMMIT: insert transactions, snapshot balance, match transfers, learn merchant rules
```

**API (all under `/api/v1/imports`, authenticated, scoped to the current user):**

| Method | Path | Purpose |
|---|---|---|
| POST | `/imports` (multipart: `file`, optional `accountId`) | Parse and create a `PendingReview` batch. Returns the batch with rows. Max 5 MB, `.csv` only. |
| GET | `/imports/{id}` | Fetch a batch (review survives a page refresh). |
| PATCH | `/imports/{id}/rows/{rowId}` | Change a row's category or exclude it. |
| POST | `/imports/{id}/commit` | Body: closing balance + statement end date per account in the batch. Commits. |
| DELETE | `/imports/{id}` | Discard a pending batch, or **undo** a committed one (deletes its transactions and snapshot, un-pairs transfers). |
| GET | `/imports` | Import history. |

**Account selection:** if `accountId` is omitted, the importer picks the account whose `Institution` (and
`ExternalAccountRef`, if present) matches. If it's ambiguous (e.g. CIBC card vs CIBC PLC), the review screen asks.

**Reconciliation (shown on review, before commit):**
`previous snapshot + credits − debits` (sign-flipped for liabilities) vs. the entered closing balance. Mismatch → a
yellow warning with the difference. Commit is still allowed: the first import for an account has no previous snapshot,
and the owner may know why. The warning is stored on the batch.

### 4. Duplicates

`ImportFingerprint = hash(accountId, date, amount, direction, normalized description, n)`, where `n` is the occurrence
index of identical rows *within the same file* (two identical $4.50 coffees on the same day stay two rows). A unique
index on `(AccountId, ImportFingerprint)` makes re-imports idempotent. On review, rows that already exist are shown as
"already imported" and excluded by default.

### 5. Transfer and owner-pay matching

Runs at commit, against **all** of the user's unmatched transactions, so upload order doesn't matter.

A candidate pair is: same amount, opposite direction, different accounts of the same user, dates within **5 days**,
neither side already matched, neither is an `InvestmentTrade`.

- Exactly one candidate → pair them (shared `TransferGroupId`).
  - Corporation → Personal → both sides `Kind = OwnerPay`; personal side category `PayFromCorporation`.
  - Personal → Corporation → both sides `Kind = Transfer` (owner putting money into the corporation; it does not
    reduce pay). The owner says this has happened once, when the RBC account was opened. Such pairs are listed in
    "Needs attention" so the owner can confirm them.
  - Otherwise → both sides `Kind = Transfer`.
- More than one candidate → `PendingTransfer`, listed on a **"Needs attention"** panel (Transactions page), where the
  owner picks the partner or marks it "not a transfer" (→ `Regular`).
- No candidate but a transfer hint → `PendingTransfer` until a later commit finds the partner, or the owner marks it
  "not a transfer".
- No candidate and no hint → `Regular` (e.g. a payment to someone outside FinSight).
- Every commit re-runs matching over all `PendingTransfer` rows.

Description hints ("PAYMENT", "THANK YOU", "TRANSFER", "WEALTHSIMPLE", "E-TRANSFER") may only break ties between
candidates; they never create a match alone.

### 6. Categorization

Order on review: (1) the owner's `MerchantRule` for this description, (2) the existing rule-based + ML.NET
`ITransactionCategorizationService`, (3) `Uncategorized`. Uncategorized rows are highlighted. When the owner changes a
category on review, a `MerchantRule` is upserted on commit.

### 7. Reporting changes

- One shared query filter for **personal spending**: `Kind = Regular`, account `Owner = Personal`, category not an
  income category. Debits add to their category; **credits (refunds/returns) subtract** from their category. Applied in
  `GetSpendingInsights`, `GetBudgets`, `GetBudgetSummary`, `AnomalyDetectionService`.
- **Personal income** = personal `Regular` credits in income categories + `OwnerPay` credits on personal accounts.
  Investment income inside **registered accounts (TFSA, RRSP, FHSA) is shown separately as "tax-sheltered"** and is
  excluded from the taxable-income figure step 4 will use.
- **Net worth** at a month end = for each account, its latest snapshot on or before that date; assets minus
  liabilities. Two figures: **Personal** and **Including corporation**. Net-worth history = one point per month-end.
- **Corporation summary** (per month / fiscal YTD): revenue (Regular credits on corporate accounts), paid to owner (OwnerPay
  debits), expenses (Regular debits), closing balance.
- The `RecordTransactionCommandHandler` budget increment is unaffected (manual entry only); budgets already derive spend
  from transactions in the summary query.

### 8. Frontend

- **New "Import" page** (sidebar entry): drop zone → detected institution + account picker → review table (date,
  description, amount, category dropdown, status chips: *duplicate*, *likely transfer*, *uncategorized*, exclude
  toggle) → closing balance + statement date → reconciliation result → Commit. Plus an import history list with Undo.

```
┌ Import ───────────────────────────────────────────────────────────┐
│ File: cibc_visa_oct.csv   Detected: CIBC   Account: [CIBC Visa ▾] │
│ ┌──────────┬──────────────────────┬─────────┬──────────────┬─────┐ │
│ │ Oct 03   │ LOBLAWS #1234        │ -82.15  │ Groceries ▾  │     │ │
│ │ Oct 05   │ PAYMENT THANK YOU    │ +500.00 │ —            │ ⇄   │ │
│ │ Oct 05   │ TIM HORTONS          │  -4.50  │ Dining ▾     │ dup │ │
│ │ Oct 09   │ SQ *SOME SHOP        │ -23.00  │ ⚠ Uncat. ▾   │     │ │
│ └──────────┴──────────────────────┴─────────┴──────────────┴─────┘ │
│ Closing balance (owed): [ 1,204.33 ]  As of: [ 2026-10-31 ]        │
│ ✓ Reconciles with previous balance        [Discard]  [Commit 41]  │
└───────────────────────────────────────────────────────────────────┘
```

- **Accounts page:** add/edit institution, owner (Personal/Corporation), new account types, external ref.
- **Dashboard:** net worth card (Personal / Including corp) with monthly history chart; spending by category (using the
  new filter); YTD pay from corporation; corporation summary card.
- **Transactions page:** "Needs attention" panel for ambiguous transfers; `Kind` shown as a chip.

### 9. Testing

- **Readers:** one fixture CSV per institution/layout (built from the owner's redacted samples) → exact parsed rows,
  including sign conventions, date formats, quoted commas, blank lines, trailing totals.
- **Detection:** each fixture is claimed by exactly one reader; unknown files are rejected.
- **Fingerprint:** re-importing the same file adds zero rows; identical same-day rows are both kept.
- **Matching:** unique match, ambiguous match, corp↔personal → OwnerPay, out-of-window, upload in either order, undo
  un-pairs.
- **Reconciliation:** asset and liability sign handling, first import with no prior snapshot.
- **Reporting:** spending excludes transfers/trades/corporate; net worth with mixed assets and liabilities.
- **Integration:** upload → review → commit → dashboard totals, via the existing Testcontainers setup.
- **Architecture tests** keep passing: readers live in Infrastructure behind an Application interface.

## Decided by the owner

- Approach A (per-institution readers).
- The RBC account is the corporation's; transfers from it to personal accounts are personal income (salary vs dividend
  settled later by T4/T5).
- The outputs that matter: spending by category, net worth, tax owed.
- Personal → corporation transfers are rare (once, to open the account): treated as a transfer, not a pay reduction.
- Corporate fiscal year ends December 31.
- Redacted samples will be supplied later; readers are built last.

## Calls made by Claude (open to correction)

- Month-end balance is entered by hand at upload rather than computed. Most of these CSVs carry no balance, and
  running totals drift.
- Transfer window of 5 days; description text only breaks ties.
- Ambiguous transfers go to a "Needs attention" list rather than being guessed.
- Uploaded files are not stored; parsed rows are.
- Merchant rules remember category corrections; AI categorization waits for step 6.
- Non-CAD rows are rejected for now.
- Corporation view is a simple cash summary, not bookkeeping.
- Probable-but-unpaired transfers are `PendingTransfer` and excluded from totals until resolved, rather than counted.
- Refunds on cards reduce spending in their category.
- Registered-account investment income is tax-sheltered and kept out of taxable income.
- Investment-account snapshots are total market value; reconciliation is skipped for them.
- Liabilities are stored as positive amounts owed; the database is reset (no real data exists).

## Fiscal year

The corporation's fiscal year ends **December 31** (owner's best understanding). It is stored as a user setting
(`CorporateFiscalYearEnd`, month/day, default Dec 31) so it can be changed without code, and the corporation summary's
YTD follows it. Personal totals always use the calendar year.

## Sample files and build order

The owner will supply one redacted CSV per account into `samples/` (git-ignored). Until they arrive:
- Everything except the five concrete readers can be built and tested: domain changes, import batches, review/commit
  API, fingerprinting, matching, reconciliation, reporting, frontend. Use a simple test-only reader to drive them.
- The readers and detection rules are written **last**, from the samples, with fixtures derived from them. If the
  samples aren't in place when that task starts, stop and ask the owner for them rather than writing from the layouts
  in §2.

## Existing data

There is no real data yet: the database only holds seed data. The new schema ships as a fresh migration and the
database is **reset and reseeded**. The seeder is updated to the new conventions (positive liabilities, `Institution`,
`Owner`). No data-migration task. The existing `Investment` and `Retirement` types remain for accounts that aren't
TFSA/RRSP/FHSA.

## Left to the builder

- Exact Wealthsimple transaction-code mapping and crypto layout, from the owner's samples (see Sample files and build order).
- CSV library choice (e.g. CsvHelper) and normalization rules for descriptions.
- Migration and index details; storage of `ImportRow` raw lines (size cap).
- Frontend component structure, within the existing React Query + Tailwind patterns.
