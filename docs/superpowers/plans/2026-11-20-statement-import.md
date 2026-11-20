# Statement Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the owner upload monthly CSV statements from CIBC, TD, MBNA, RBC and Wealthsimple, review them, and commit them into FinSight. That gives correct spending by category, net worth, and corporation vs. personal income.

**Architecture:** New domain concepts (account owner/institution, transaction kind, balance snapshots, import batches, merchant rules) live in `FinSight.Domain`. Import, matching, reconciliation and reporting logic live in `FinSight.Application` as MediatR handlers plus small pure calculators. CSV parsing lives in `FinSight.Infrastructure` behind one interface, `IStatementParser`, with one `IStatementReader` per institution. The React frontend gets an Import page and updated Dashboard, Accounts and Transactions pages.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8.0.11 + PostgreSQL, MediatR 12.4.1, FluentValidation 11, CsvHelper 33.0.1 (new), xUnit + FluentAssertions + NSubstitute, Testcontainers. Frontend: Vite, React 18, react-query v3, Tailwind.

**Spec:** `docs/superpowers/specs/2026-11-20-statement-import-design.md` (read it before starting any task).

## Global Constraints

- Package versions only in `Directory.Packages.props` (central package management); `.csproj` files carry no `Version`.
- Clean Architecture: Domain references nothing; Application must not reference Infrastructure or Api (enforced by `tests/FinSight.ArchitectureTests`, which must stay green).
- Mocks use NSubstitute, never Moq. Mapping is manual (records), never AutoMapper.
- Money is `decimal`, stored with precision `(18, 2)`; currency via `Money.DefaultCurrency` (`"CAD"`).
- **CAD only.** Any parsed row whose currency is not CAD fails the upload with: `Row {line}: only CAD is supported.`
- Upload limits: `.csv` extension only, max **5 MB** (`5 * 1024 * 1024` bytes).
- Unknown file message, exactly: `Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple.`
- Uploaded files are **never stored**; only parsed rows (`ImportRow.RawLine` capped at 1000 chars).
- Transfer match window: **5 days** (`TransferMatcher.WindowDays = 5`). Description hints only break ties.
- Liabilities (`CreditCard`, `LineOfCredit`, `Mortgage`, `Loan`) store balances as a **positive amount owed**.
- Every handler is scoped to `ICurrentUserService.UserId`; another user's resource returns `Error.NotFound` (404), never 403.
- Commit messages: `feat:` / `fix:` / `test:` / `refactor:` / `docs:` prefixes; end each with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Integration tests need Docker running. If Docker is unavailable, say so in the task report; don't mark that step done.

## Review Focus

1. **The same file uploaded twice, or overlapping statement periods.** Expected: zero new transactions the second time; rows are shown as "already imported" and excluded. Test: Task 11, `Commit_SameFileTwice_SecondCommitAddsNothing`.
2. **Real-world CSV noise:** UTF-8 BOM, CRLF line endings, quoted fields containing commas, blank trailing lines, header-only files. Expected: parsed correctly, or a header-only file fails with `The file contains no transactions.`. Test: Task 6, `Parse_BomCrlfQuotedAndBlankLines_ParsesRows` and `Parse_HeaderOnly_Fails`.
3. **Another user's batch, row or transaction.** Expected: 404, and nothing is changed. Test: Task 12, `GetImport_OtherUsersBatch_Returns404` and `Commit_OtherUsersBatch_Returns404`.
4. **Undoing a batch whose rows were paired with another batch's rows.** Expected: the partner goes back to `PendingTransfer` (excluded from totals), never to a stale pair or `Regular`. Test: Task 11, `Undo_PairedBatch_PartnerBecomesPendingTransfer`.
5. **Two identical transactions on the same day in one file** (two $4.50 coffees). Expected: both kept, and re-importing still adds zero. Test: Task 5, `Compute_IdenticalRowsDifferentOccurrence_DifferentFingerprints`, and Task 11, `Commit_IdenticalSameDayRows_BothKept`.

---

## File Structure

```
src/FinSight.Domain/
  Enums/Enumerations.cs                 (modify) new enums + AccountType/Category values
  Enums/EnumExtensions.cs               (create) IsLiability/IsInvestment/IsRegistered/IsIncome
  Entities/Account.cs                   (modify) owner, institution, ext ref, ImportTransaction, snapshots, liability sign
  Entities/Transaction.cs               (modify) Kind, TransferGroupId, ImportBatchId, ImportFingerprint, pairing
  Entities/User.cs                      (modify) corporate fiscal year end
  Entities/BalanceSnapshot.cs           (create)
  Entities/ImportBatch.cs               (create)
  Entities/ImportRow.cs                 (create)
  Entities/MerchantRule.cs              (create)
  Repositories/IImportBatchRepository.cs, IMerchantRuleRepository.cs, IBalanceSnapshotRepository.cs (create)
  Repositories/ITransactionRepository.cs (modify)

src/FinSight.Application/
  Common/Interfaces/IStatementParser.cs           (create) parser port + ParsedStatement records
  Common/Interfaces/IApplicationDbContext.cs      (modify) new DbSets
  Common/Reporting/PersonalLedger.cs              (create) spending/income rules
  Common/Reporting/NetWorthCalculator.cs          (create)
  Common/Reporting/FiscalYear.cs                  (create)
  Features/Imports/Common/DescriptionNormalizer.cs, ImportFingerprint.cs, TransferMatcher.cs,
                          ReconciliationCalculator.cs, ImportDtos.cs, TransferMatchingService.cs
  Features/Imports/Commands/{CreateImport,UpdateImportRow,CommitImport,DeleteImport}/
  Features/Imports/Queries/{GetImport,GetImports,CheckReconciliation}/
  Features/Transactions/Queries/GetNeedsAttention/, Commands/ResolveTransfer/
  Features/Reports/Queries/{GetNetWorth,GetCorporationSummary,GetPersonalIncome}/
  Features/Settings/{GetSettings,UpdateSettings}/
  Features/Accounts/Commands/UpdateAccount/       (create); CreateAccount (modify)

src/FinSight.Infrastructure/
  Imports/StatementParser.cs, Imports/IStatementReader.cs, Imports/CsvRows.cs
  Imports/Readers/{Cibc,Td,Mbna,Rbc,Wealthsimple}StatementReader.cs   (Tasks 19–20, from samples)
  Persistence/Configurations/{BalanceSnapshot,ImportBatch,ImportRow,MerchantRule}Configuration.cs
  Persistence/Repositories/{ImportBatch,MerchantRule,BalanceSnapshot}Repository.cs
  Persistence/Migrations/*_AddStatementImport.cs
  Persistence/Seeders/DatabaseSeeder.cs           (modify)

src/FinSight.Api/Controllers/{Imports,Reports,Settings}Controller.cs (create); Accounts/Transactions (modify)
src/FinSight.Api/Common/ResultHttpExtensions.cs  (create)

frontend/src/
  types/api.ts, services/api.ts                   (modify)
  pages/ImportPage.tsx                            (create)
  components/imports/{UploadDropzone,ReviewTable,ClosingBalanceForm,ImportHistory}.tsx (create)
  components/transactions/NeedsAttentionPanel.tsx (create)
  pages/{DashboardPage,AccountsPage,TransactionsPage}.tsx, components/layout/Sidebar.tsx, App.tsx (modify)

tests/FinSight.Infrastructure.Tests/              (add to FinSight.sln in Task 6)
```

---

### Task 1: Account owner, institution and new account types

**Files:**
- Modify: `src/FinSight.Domain/Enums/Enumerations.cs`, `src/FinSight.Domain/Entities/Account.cs`
- Create: `src/FinSight.Domain/Enums/EnumExtensions.cs`
- Test: `tests/FinSight.Domain.Tests/Entities/AccountOwnershipTests.cs`

**Interfaces:**
- Produces:
  - `AccountType` gains `LineOfCredit = 9, Tfsa = 10, Rrsp = 11, Fhsa = 12, Crypto = 13`
  - `enum AccountOwner { Personal = 0, Corporation = 1 }`
  - `enum Institution { Other = 0, Cibc = 1, Td = 2, Mbna = 3, Rbc = 4, Wealthsimple = 5 }`
  - `static class AccountTypeExtensions { bool IsLiability(this AccountType); bool IsInvestment(this AccountType); bool IsRegistered(this AccountType); }`
    - Liability: CreditCard, LineOfCredit, Mortgage, Loan.
    - Investment: Investment, Retirement, Tfsa, Rrsp, Fhsa, Crypto.
    - Registered: Tfsa, Rrsp, Fhsa.
  - `Account.Owner`, `Account.Institution`, `Account.ExternalAccountRef` (string?, max 50)
  - `Account.Create(Guid userId, string name, AccountType type, string currency = Money.DefaultCurrency, string? institutionName = null, Institution institution = Institution.Other, AccountOwner owner = AccountOwner.Personal, string? externalAccountRef = null)`
  - `void Account.UpdateDetails(string name, AccountType type, Institution institution, AccountOwner owner, string? externalAccountRef)`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Create_Defaults_PersonalOtherNoRef()
{
    var a = Account.Create(Guid.NewGuid(), "Visa", AccountType.CreditCard);
    a.Owner.Should().Be(AccountOwner.Personal);
    a.Institution.Should().Be(Institution.Other);
    a.ExternalAccountRef.Should().BeNull();
}
[Fact] public void Create_CorporateRbc_StoresOwnerInstitutionRef()
{
    var a = Account.Create(Guid.NewGuid(), "RBC Business", AccountType.Checking, "CAD", "RBC",
        Institution.Rbc, AccountOwner.Corporation, "1234");
    (a.Owner, a.Institution, a.ExternalAccountRef).Should().Be((AccountOwner.Corporation, Institution.Rbc, "1234"));
}
[Fact] public void UpdateDetails_ChangesFields()  // name trimmed, type/institution/owner/ref replaced
[Theory]
[InlineData(AccountType.CreditCard, true)] [InlineData(AccountType.LineOfCredit, true)]
[InlineData(AccountType.Mortgage, true)] [InlineData(AccountType.Loan, true)]
[InlineData(AccountType.Savings, false)] [InlineData(AccountType.Tfsa, false)]
public void IsLiability_MatchesSpec(AccountType t, bool expected) => t.IsLiability().Should().Be(expected);
[Theory]
[InlineData(AccountType.Tfsa, true)] [InlineData(AccountType.Rrsp, true)] [InlineData(AccountType.Fhsa, true)]
[InlineData(AccountType.Crypto, false)] [InlineData(AccountType.Investment, false)]
public void IsRegistered_MatchesSpec(AccountType t, bool expected) => t.IsRegistered().Should().Be(expected);
[Theory]
[InlineData(AccountType.Crypto, true)] [InlineData(AccountType.Retirement, true)] [InlineData(AccountType.Checking, false)]
public void IsInvestment_MatchesSpec(AccountType t, bool expected) => t.IsInvestment().Should().Be(expected);
```

- [ ] **Step 2: Run and confirm failure.** `dotnet test tests/FinSight.Domain.Tests --filter "FullyQualifiedName~AccountOwnershipTests"`. Expected: compile errors (`AccountOwner` not found).
- [ ] **Step 3: Implement** the enums, the extensions and the `Account` changes listed under Interfaces. Existing callers of `Account.Create` keep compiling because the new parameters have defaults.
- [ ] **Step 4: Run all Domain tests.** `dotnet test tests/FinSight.Domain.Tests`. Expected: all pass.
- [ ] **Step 5: Commit.** `feat: add account owner, institution and Canadian account types`

---

### Task 2: Transaction kind, pairing and import recording

**Files:**
- Modify: `src/FinSight.Domain/Enums/Enumerations.cs`, `EnumExtensions.cs`, `Entities/Transaction.cs`, `Entities/Account.cs`
- Test: `tests/FinSight.Domain.Tests/Entities/TransactionKindTests.cs`, `AccountImportTests.cs`

**Interfaces:**
- Consumes: Task 1 enums and `IsLiability()`.
- Produces:
  - `enum TransactionKind { Regular = 0, Transfer = 1, OwnerPay = 2, InvestmentTrade = 3, PendingTransfer = 4 }`
  - `TransactionCategory.PayFromCorporation = 44`
  - `static bool IsIncome(this TransactionCategory)`: true for Salary, FreelanceIncome, InvestmentIncome, GovernmentBenefits, PayFromCorporation.
  - Transaction properties: `Kind`, `TransferGroupId` (Guid?), `ImportBatchId` (Guid?), `ImportFingerprint` (string?, max 64), `IsTransferDismissed` (bool).
  - Transaction methods:
    - `void MarkPaired(Guid transferGroupId, TransactionKind kind, TransactionCategory? category = null)`: throws `DomainException` unless kind is Transfer or OwnerPay.
    - `void MarkPendingTransfer()`
    - `void MarkNotTransfer()`: sets Kind Regular, clears the group, sets `IsTransferDismissed = true`.
    - `void Unpair()`: sets Kind PendingTransfer and clears the group.
  - `Transaction Account.ImportTransaction(string description, Money amount, TransactionType type, DateTimeOffset occurredAt, TransactionCategory category, TransactionKind kind, Guid importBatchId, string fingerprint, string? merchantName = null)`
    - Does **not** change `Balance`.
    - Throws `DomainException` if the account is inactive, the currency differs from `Balance.Currency`, or `kind` is Transfer or OwnerPay (imports start as Regular, InvestmentTrade or PendingTransfer).
  - `Account.RecordTransaction` (manual entry) on a **liability** account: Debit increases `Balance`, Credit decreases it. Assets are unchanged.

- [ ] **Step 1: Write the failing tests**

```csharp
// AccountImportTests
[Fact] public void ImportTransaction_DoesNotChangeBalance()
{
    var a = Account.Create(Guid.NewGuid(), "Visa", AccountType.CreditCard);
    var batch = Guid.NewGuid();
    var tx = a.ImportTransaction("LOBLAWS", Money.CAD(82.15m), TransactionType.Debit, DateTimeOffset.UtcNow,
        TransactionCategory.GroceriesAndFood, TransactionKind.Regular, batch, "fp1");
    a.Balance.Amount.Should().Be(0m);
    (tx.ImportBatchId, tx.ImportFingerprint, tx.Kind).Should().Be((batch, "fp1", TransactionKind.Regular));
}
[Fact] public void ImportTransaction_CurrencyMismatch_Throws()   // USD into a CAD account → DomainException
[Fact] public void ImportTransaction_KindTransfer_Throws()
[Fact] public void RecordTransaction_LiabilityDebit_IncreasesAmountOwed()
{
    var a = Account.Create(Guid.NewGuid(), "Visa", AccountType.CreditCard);
    a.RecordTransaction("Coffee", Money.CAD(5m), TransactionType.Debit, DateTimeOffset.UtcNow);
    a.Balance.Amount.Should().Be(5m);
}
[Fact] public void RecordTransaction_LiabilityCredit_DecreasesAmountOwed()   // debit 100, credit 40 → 60

// TransactionKindTests
[Fact] public void MarkPaired_OwnerPay_SetsGroupKindCategory()
[Fact] public void MarkPaired_RegularKind_Throws()
[Fact] public void MarkNotTransfer_SetsRegularAndDismissed()
[Fact] public void Unpair_SetsPendingAndClearsGroup()
[Theory] [InlineData(TransactionCategory.PayFromCorporation, true)] [InlineData(TransactionCategory.DiningOut, false)]
public void IsIncome_MatchesSpec(TransactionCategory c, bool expected) => c.IsIncome().Should().Be(expected);
```

- [ ] **Step 2: Run and confirm failure.** `dotnet test tests/FinSight.Domain.Tests --filter "FullyQualifiedName~AccountImportTests|FullyQualifiedName~TransactionKindTests"`. Expected: compile errors.
- [ ] **Step 3: Implement** the Interfaces above. `Transaction.Create` (internal) gains `kind`, `importBatchId` and `fingerprint` parameters with defaults (`Regular`, `null`, `null`).
- [ ] **Step 4: Run Domain and Application tests.** `dotnet test tests/FinSight.Domain.Tests && dotnet test tests/FinSight.Application.Tests`. Expected: all pass. If an existing test asserted a negative credit-card balance, update it to the new convention and say so in the commit message.
- [ ] **Step 5: Commit.** `feat: add transaction kinds, pairing and balance-neutral import recording`

---

### Task 3: Snapshots, import batches, merchant rules and fiscal year (domain)

**Files:**
- Create: `src/FinSight.Domain/Entities/{BalanceSnapshot,ImportBatch,ImportRow,MerchantRule}.cs`, `src/FinSight.Domain/Repositories/{IImportBatchRepository,IMerchantRuleRepository,IBalanceSnapshotRepository}.cs`
- Modify: `Entities/Account.cs`, `Entities/User.cs`, `Enums/Enumerations.cs`, `Repositories/ITransactionRepository.cs`
- Test: `tests/FinSight.Domain.Tests/Entities/{BalanceSnapshotTests,ImportBatchTests,UserFiscalYearTests}.cs`

**Interfaces:**
- Produces:
  - `enum ImportBatchStatus { PendingReview = 1, Committed = 2, Discarded = 3 }`
  - `BalanceSnapshot : BaseEntity`
    - Properties: `AccountId`, `AsOf` (DateOnly), `Balance` (Money), `ImportBatchId` (Guid?).
    - `static BalanceSnapshot Create(Guid accountId, DateOnly asOf, Money balance, Guid? importBatchId)`
  - `Account.LatestSnapshotAsOf` (DateOnly?)
  - `BalanceSnapshot Account.RecordBalanceSnapshot(DateOnly asOf, Money balance, Guid? importBatchId)`: sets `Balance` only when `asOf >= LatestSnapshotAsOf` (or there is none).
  - `void Account.ResetBalanceFromSnapshot(BalanceSnapshot? latest)`: used by undo. A null `latest` sets the balance to zero and clears `LatestSnapshotAsOf`.
  - `ImportBatch : BaseEntity`
    - Properties: `UserId`, `Institution`, `FileName` (max 255), `Status`, `PeriodStart`/`PeriodEnd` (DateOnly?), `CommittedAt`, `ReconciliationWarning` (string?, max 2000), `IReadOnlyCollection<ImportRow> Rows`.
    - `static Create(Guid userId, Institution institution, string fileName)`
    - `void AddRow(ImportRow row)`: also widens PeriodStart/End.
    - `void Commit(string? reconciliationWarning)`, `void Discard()`, `void MarkUndone()`. Wrong-state transitions throw `DomainException`.
  - `ImportRow : BaseEntity`
    - Properties: `ImportBatchId`, `LineNumber`, `AccountId` (Guid?), `OccurredOn` (DateOnly), `Description` (max 500), `MerchantName` (max 200, nullable), `Amount` (Money), `Type`, `Category`, `KindHint`, `IsDuplicate`, `IsExcluded`, `CategoryChangedByUser`, `Fingerprint` (string?, max 64), `RawLine` (max 1000, truncated on create).
    - `static Create(int lineNumber, DateOnly occurredOn, string description, string? merchantName, Money amount, TransactionType type, TransactionCategory category, TransactionKind kindHint, string rawLine)`
    - `void AssignAccount(Guid accountId, string fingerprint, bool isDuplicate)`: a duplicate also sets `IsExcluded = true`.
    - `void SetCategory(TransactionCategory category)`: sets `CategoryChangedByUser = true`.
    - `void SetExcluded(bool excluded)`
  - `MerchantRule : BaseEntity`
    - Properties: `UserId`, `DescriptionKey` (max 200), `Category`.
    - `static Create(Guid userId, string descriptionKey, TransactionCategory category)`, `void Update(TransactionCategory category)`
  - `User.CorporateFiscalYearEndMonth` (default 12) and `User.CorporateFiscalYearEndDay` (default 31)
  - `void User.SetCorporateFiscalYearEnd(int month, int day)`: throws `DomainException` unless it's a valid date in a non-leap year (so Feb 29 is rejected).
  - Repositories:
    - `IImportBatchRepository`: `GetByIdAsync(Guid id, Guid userId, ct)` (includes rows), `GetByUserIdAsync(Guid userId, ct)`, `AddAsync(ImportBatch, ct)`
    - `IMerchantRuleRepository`: `GetByUserIdAsync(Guid userId, ct) -> IReadOnlyList<MerchantRule>`, `AddAsync(MerchantRule, ct)`
    - `IBalanceSnapshotRepository`:
      - `GetByUserIdAsync(Guid userId, ct)`
      - `GetLatestAsync(Guid accountId, DateOnly? onOrBefore, ct)`
      - `GetByImportBatchIdAsync(Guid batchId, ct)`
      - `AddAsync(BalanceSnapshot, ct)`
      - `Remove(BalanceSnapshot)`
  - `ITransactionRepository` gains:
    - `GetExistingFingerprintsAsync(Guid accountId, IReadOnlyCollection<string> fingerprints, ct) -> IReadOnlySet<string>`
    - `GetMatchPoolAsync(Guid userId, DateOnly from, DateOnly to, ct)`: all of the user's transactions in range with Kind Regular or PendingTransfer, `TransferGroupId == null` and `!IsTransferDismissed`, with `Account` included.
    - `GetByImportBatchIdAsync(Guid batchId, ct)`
    - `GetByTransferGroupIdsAsync(IReadOnlyCollection<Guid> groupIds, ct)`
    - `Remove(Transaction)`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void RecordBalanceSnapshot_Newer_UpdatesBalance()
[Fact] public void RecordBalanceSnapshot_Older_KeepsNewerBalance()   // Oct 31 = 500, then Sep 30 = 300 → Balance stays 500
[Fact] public void ResetBalanceFromSnapshot_Null_ZeroesBalance()
[Fact] public void AddRow_WidensPeriod()                             // rows Oct 3 and Oct 28 → PeriodStart Oct 3, PeriodEnd Oct 28
[Fact] public void Commit_FromPending_SetsCommittedAndWarning()
[Fact] public void Commit_Twice_Throws()
[Fact] public void Discard_Committed_Throws()
[Fact] public void MarkUndone_Committed_SetsDiscarded()
[Fact] public void AssignAccount_Duplicate_ExcludesRow()
[Fact] public void Create_LongRawLine_TruncatesTo1000()
[Fact] public void SetCorporateFiscalYearEnd_Feb29_Throws()
[Fact] public void SetCorporateFiscalYearEnd_Jun30_Stores()
[Fact] public void NewUser_FiscalYearEnd_DefaultsToDec31()
```

- [ ] **Step 2: Run and confirm failure.** `dotnet test tests/FinSight.Domain.Tests`. Expected: compile errors.
- [ ] **Step 3: Implement** the entities, the `Account`/`User` members and the repository interfaces.
- [ ] **Step 4: Run tests.** `dotnet test tests/FinSight.Domain.Tests`. Expected: all pass.
- [ ] **Step 5: Commit.** `feat: add balance snapshots, import batches, merchant rules and fiscal year end`

---

### Task 4: Persistence, migration and reseed

**Files:**
- Create: `src/FinSight.Infrastructure/Persistence/Configurations/{BalanceSnapshot,ImportBatch,ImportRow,MerchantRule}Configuration.cs`, `Persistence/Repositories/{ImportBatch,MerchantRule,BalanceSnapshot}Repository.cs`
- Modify: `AccountConfiguration.cs`, `TransactionConfiguration.cs`, `UserConfiguration.cs`, `ApplicationDbContext.cs`, `src/FinSight.Application/Common/Interfaces/IApplicationDbContext.cs`, `Persistence/Repositories/TransactionRepository.cs`, `DependencyInjection/ServiceCollectionExtensions.cs`, `Persistence/Seeders/DatabaseSeeder.cs`
- Create (generated): `Persistence/Migrations/*_AddStatementImport.cs`

**Interfaces:**
- Consumes: Task 3 entities and repository interfaces.
- Produces:
  - DbSets: `BalanceSnapshots`, `ImportBatches`, `ImportRows`, `MerchantRules` on both the context and `IApplicationDbContext`. Repositories are registered as scoped.
  - `TransactionRepository.GetByUserIdAsync` now `.Include(t => t.Account)`, because reporting reads `Account.Owner` and `Type`.

Persistence rules:
- Enums are stored as strings (`HasConversion<string>()`) for the new columns.
- Unique index `(AccountId, ImportFingerprint)`, filtered on `"ImportFingerprint" IS NOT NULL`.
- Indexes on `TransferGroupId`, `ImportBatchId` and `Kind`.
- Index `(AccountId, AsOf)` on snapshots.
- Unique index `(UserId, DescriptionKey)` on merchant rules.
- `ImportRow` cascades from `ImportBatch`. `BalanceSnapshot` cascades from `Account`.

- [ ] **Step 1: Write the failing integration test** `tests/FinSight.Api.IntegrationTests/Tests/PersistenceSmokeTests.cs`: `ImportBatchWithRowsAndSnapshot_RoundTrips`.
  - Arrange: create a user and an account, an `ImportBatch` with 2 rows, and a `BalanceSnapshot`, then save through a scope from the factory.
  - Assert: reloaded with a new scope, the batch has 2 rows with the same amounts, and the snapshot `AsOf` matches.
  - Also `DuplicateFingerprintSameAccount_ThrowsDbUpdateException`.
- [ ] **Step 2: Run and confirm failure.** `dotnet test tests/FinSight.Api.IntegrationTests --filter "FullyQualifiedName~PersistenceSmokeTests"`. Expected: compile errors (no DbSets).
- [ ] **Step 3: Implement** the configurations, DbSets, repositories and DI registrations.
- [ ] **Step 4: Generate the migration.** `dotnet ef migrations add AddStatementImport --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --output-dir Persistence/Migrations`, then `dotnet ef migrations has-pending-model-changes ...`. Expected: "No changes have been made to the model since the last migration."
- [ ] **Step 5: Update the seeder** to the new conventions:
  - CIBC savings and the TD/CIBC cards are `Personal` with the right `Institution`.
  - Add a `Corporation` RBC chequing account.
  - Card balances become positive amounts owed.
  - Seeded transfers use `MarkPaired`.
  - No real data exists, so the local database is reset: `dotnet ef database drop -f ...`, then `dotnet ef database update ...`.
- [ ] **Step 6: Run tests.** Run the integration tests (Docker), then `dotnet test tests/FinSight.ArchitectureTests`. Expected: pass.
- [ ] **Step 7: Commit.** `feat: persist import batches, balance snapshots and merchant rules`

---

### Task 5: Description normalizer and import fingerprint

**Files:**
- Create: `src/FinSight.Application/Features/Imports/Common/DescriptionNormalizer.cs`, `ImportFingerprint.cs`
- Test: `tests/FinSight.Application.Tests/Features/Imports/ImportFingerprintTests.cs`

**Interfaces:**
- Produces:
  - `static string DescriptionNormalizer.Normalize(string description)`. Rules, in order:
    1. Uppercase invariant.
    2. Replace every non-letter/digit/space character with a space.
    3. Drop tokens that are all digits **and** at least 3 long (store numbers, references).
    4. Collapse whitespace and trim.
    5. Cap at 200 chars.
  - `static string ImportFingerprint.Compute(Guid accountId, DateOnly date, decimal amount, TransactionType type, string normalizedDescription, int occurrence)`. SHA-256 over `$"{accountId:N}|{date:yyyy-MM-dd}|{amount:0.00}|{type}|{normalizedDescription}|{occurrence}"`, as lowercase hex, first 64 chars.
  - `static IReadOnlyList<int> ImportFingerprint.OccurrenceIndexes(IReadOnlyList<(DateOnly Date, decimal Amount, TransactionType Type, string NormalizedDescription)> rows)`. Zero-based index of each row among earlier identical rows in the same file.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData("Loblaws #1234  Toronto", "LOBLAWS TORONTO")]
[InlineData("TIM HORTONS 0456", "TIM HORTONS")]
[InlineData("E-TRANSFER  ***ab9", "E TRANSFER AB9")]
[InlineData("7-ELEVEN 23", "7 ELEVEN 23")]
public void Normalize_ProducesStableKey(string input, string expected) => DescriptionNormalizer.Normalize(input).Should().Be(expected);
[Fact] public void Compute_SameInputs_SameFingerprint()
[Fact] public void Compute_IdenticalRowsDifferentOccurrence_DifferentFingerprints()
[Fact] public void OccurrenceIndexes_TwoIdenticalCoffees_Returns0And1()   // [A, B, A] → [0, 0, 1]
```

- [ ] **Step 2: Run and confirm failure.** `dotnet test tests/FinSight.Application.Tests --filter "FullyQualifiedName~ImportFingerprintTests"`.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: add description normalizer and import fingerprint`

---

### Task 6: Statement parser port and CSV infrastructure

**Files:**
- Create: `src/FinSight.Application/Common/Interfaces/IStatementParser.cs`, `src/FinSight.Infrastructure/Imports/{IStatementReader,StatementParser,CsvRows}.cs`
- Modify: `Directory.Packages.props` (add `CsvHelper` 33.0.1), `src/FinSight.Infrastructure/FinSight.Infrastructure.csproj`, `FinSight.sln` (add `tests/FinSight.Infrastructure.Tests`), `tests/FinSight.Infrastructure.Tests/FinSight.Infrastructure.Tests.csproj` (central versions + FluentAssertions + project refs; delete `UnitTest1.cs`), `DependencyInjection/ServiceCollectionExtensions.cs`
- Test: `tests/FinSight.Infrastructure.Tests/Imports/StatementParserTests.cs`, with a test-only `FakeReader` in the same folder

**Interfaces:**
- Produces (Application):
  ```csharp
  public interface IStatementParser { Result<ParsedStatement> Parse(Stream csv); }
  public sealed record ParsedStatement(Institution Institution, IReadOnlyList<ParsedStatementRow> Rows);
  public sealed record ParsedStatementRow(int LineNumber, DateOnly Date, string Description, string? MerchantName,
      decimal Amount, TransactionType Type, string Currency, string? ExternalAccountRef,
      TransactionKind KindHint, TransactionCategory? CategoryHint, string RawLine);
  ```
  `Amount` is always positive. `Type` follows the spec convention: money leaving, or a charge on a card/LOC, is a Debit.
- Produces (Infrastructure):
  ```csharp
  public interface IStatementReader {
      Institution Institution { get; }
      bool CanRead(IReadOnlyList<CsvRow> firstRows);          // up to 5 non-blank rows
      IEnumerable<ParsedStatementRow> Read(IReadOnlyList<CsvRow> allRows);
  }
  public sealed record CsvRow(int LineNumber, IReadOnlyList<string> Fields, string RawLine);
  ```
  - `StatementParser(IEnumerable<IStatementReader> readers) : IStatementParser`:
    - Reads all rows with CsvHelper (`HasHeaderRecord = false`, BOM detected, blank lines skipped, fields trimmed).
    - Requires exactly one reader whose `CanRead` is true; otherwise fails with the unknown-file message.
    - Collects rows; if there are none, fails with `The file contains no transactions.`.
    - Any non-CAD row fails with `Row {line}: only CAD is supported.`.
    - A reader exception of type `StatementFormatException(int line, string message)` (Application/Common/Exceptions) becomes `Result.Failure(Error.Custom("Import.Format", $"Row {line}: {message}"))`.
  - Registration: `services.AddScoped<IStatementParser, StatementParser>()`. Readers are registered as `IStatementReader` (none are concrete until Tasks 19–20).

- [ ] **Step 1: Write the failing tests**, using `FakeReader`: header `Date,Description,Amount`, signed amount, ISO dates.

```csharp
[Fact] public void Parse_KnownFile_ReturnsInstitutionAndRows()
[Fact] public void Parse_BomCrlfQuotedAndBlankLines_ParsesRows()   // "\uFEFFDate,Description,Amount\r\n2026-10-03,\"LOBLAWS, TORONTO\",-82.15\r\n\r\n"
                                                                   // → 1 row, Description "LOBLAWS, TORONTO", Debit 82.15, LineNumber 2
[Fact] public void Parse_HeaderOnly_Fails()                        // error description "The file contains no transactions."
[Fact] public void Parse_NoReaderMatches_FailsWithUnknownFileMessage()
[Fact] public void Parse_TwoReadersMatch_FailsWithUnknownFileMessage()
[Fact] public void Parse_UsdRow_FailsNamingRow()                   // "Row 3: only CAD is supported."
[Fact] public void Parse_ReaderFormatException_FailsNamingRow()
```

- [ ] **Step 2: Run and confirm failure.** `dotnet test tests/FinSight.Infrastructure.Tests`.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Run the Infrastructure tests, then the architecture tests. Expected: pass.
- [ ] **Step 5: Commit.** `feat: add statement parser with CSV reading and institution detection`

---

### Task 7: Transfer matcher

**Files:**
- Create: `src/FinSight.Application/Features/Imports/Common/TransferMatcher.cs`
- Test: `tests/FinSight.Application.Tests/Features/Imports/TransferMatcherTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record MatchCandidate(Guid TransactionId, Guid AccountId, AccountOwner Owner, DateOnly Date,
      decimal Amount, TransactionType Type, bool HasTransferHint);
  public sealed record MatchDecision(Guid TransactionId, Guid? PartnerId, TransactionKind Kind, TransactionCategory? Category);
  public static class TransferMatcher {
      public const int WindowDays = 5;
      public static readonly string[] HintWords = ["PAYMENT", "THANK YOU", "TRANSFER", "WEALTHSIMPLE", "E TRANSFER"];
      public static bool HasHint(string normalizedDescription);
      public static IReadOnlyList<MatchCandidate> FindCandidates(MatchCandidate target, IReadOnlyList<MatchCandidate> pool);
      public static IReadOnlyList<MatchDecision> Match(IReadOnlyList<MatchCandidate> pool);
  }
  ```
  - A candidate is a different account, the same amount, the opposite `Type`, and `|date diff| <= WindowDays`.
  - `Match` returns one decision per pool item:
    - **Pair** when A and B are each other's only candidate.
    - **Tie-break:** if A has several candidates but exactly one of them has `HasTransferHint` and that one's only candidate is A, pair them.
    - **Kind:** a Corporation-owned debit paired with a Personal credit gives `OwnerPay` on both sides, with `Category = PayFromCorporation` on the personal credit's decision. Every other pair, including personal → corporation, gives `Transfer`.
    - **Unpaired** with at least one candidate, or with a hint: `PendingTransfer`.
    - **Unpaired** with no candidate and no hint: `Regular`.
  - A transaction is paired at most once per run.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Match_UniqueOppositeWithinWindow_PairsAsTransfer()        // CIBC savings debit 500 Oct 5, Visa credit 500 Oct 6
[Fact] public void Match_CorpDebitToPersonalCredit_IsOwnerPayWithCategory()  // RBC debit 3000 → CIBC credit 3000
[Fact] public void Match_PersonalDebitToCorpCredit_IsTransfer()
[Fact] public void Match_SixDaysApart_NotPaired()                            // and no hint → Regular
[Fact] public void Match_TwoCandidatesNoHint_BothSidesPending()
[Fact] public void Match_TwoCandidatesOneHinted_PairsHinted()
[Fact] public void Match_SameAccount_NotPaired()
[Fact] public void Match_UnpairedWithHint_IsPending()
[Fact] public void Match_EachTransactionPairedOnce()                         // 3 identical credits, 1 debit → ambiguous → all pending
[Theory] [InlineData("PAYMENT THANK YOU", true)] [InlineData("LOBLAWS", false)]
public void HasHint_MatchesSpec(string d, bool expected) => TransferMatcher.HasHint(d).Should().Be(expected);
```

- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: add transfer and owner-pay matcher`

---

### Task 8: Reconciliation calculator

**Files:**
- Create: `src/FinSight.Application/Features/Imports/Common/ReconciliationCalculator.cs`
- Test: `tests/FinSight.Application.Tests/Features/Imports/ReconciliationCalculatorTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record ReconciliationResult(bool Skipped, string? SkipReason, decimal? Expected, decimal? Difference, bool IsBalanced);
  public static class ReconciliationCalculator {
      public static ReconciliationResult Check(AccountType type, decimal? previousBalance,
          IEnumerable<(decimal Amount, TransactionType Type)> rows, decimal closingBalance);
  }
  ```
  - Investment types skip with reason `"Investment account: market value changes with prices."`.
  - No previous balance skips with reason `"First import for this account."`.
  - Assets: `expected = prev + credits − debits`. Liabilities: `expected = prev + debits − credits`.
  - `Difference = closing − expected`. `IsBalanced` when `|Difference| < 0.005m`.

- [ ] **Step 1: Write the failing tests:**
  - `Check_AssetBalanced`: prev 1000, credit 200, debit 50, closing 1150 → balanced.
  - `Check_LiabilityBalanced`: prev 300, debit 82.15, credit 300, closing 82.15 → balanced.
  - `Check_Mismatch_ReportsDifference`: expected 1150, closing 1100 → difference −50.
  - `Check_Tfsa_Skipped`
  - `Check_NoPrevious_Skipped`
- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: add statement reconciliation calculator`

---

### Task 9: Create import (upload → pending review)

**Files:**
- Create: `src/FinSight.Application/Features/Imports/Common/ImportDtos.cs`, `Features/Imports/Commands/CreateImport/{CreateImportCommand,CreateImportCommandValidator,CreateImportCommandHandler}.cs`
- Test: `tests/FinSight.Application.Tests/Features/Imports/CreateImportCommandHandlerTests.cs`, `CreateImportCommandValidatorTests.cs`

**Interfaces:**
- Consumes:
  - `IStatementParser` (Task 6)
  - `DescriptionNormalizer` and `ImportFingerprint` (Task 5)
  - `TransferMatcher.HasHint` (Task 7)
  - The repositories from Task 3
  - `ITransactionCategorizationService.Categorize(merchant, description)`
- Produces:
  ```csharp
  public sealed record CreateImportCommand(string FileName, long Length, Stream Content, Guid? AccountId) : IRequest<Result<ImportBatchDto>>;
  public sealed record ImportBatchDto(Guid Id, string Institution, string FileName, string Status,
      DateOnly? PeriodStart, DateOnly? PeriodEnd, string? ReconciliationWarning,
      IReadOnlyList<ImportAccountDto> Accounts, IReadOnlyList<ImportRowDto> Rows);
  public sealed record ImportAccountDto(Guid AccountId, string Name, string Type, bool IsLiability, bool IsInvestment,
      decimal? PreviousBalance, DateOnly? PreviousBalanceAsOf);
  public sealed record ImportRowDto(Guid Id, int LineNumber, Guid? AccountId, DateOnly Date, string Description,
      decimal Amount, string Type, string Category, string KindHint, bool IsDuplicate, bool IsExcluded);
  public sealed record ImportBatchSummaryDto(Guid Id, string Institution, string FileName, string Status,
      DateOnly? PeriodStart, DateOnly? PeriodEnd, int RowCount, DateTimeOffset CreatedAt, DateTimeOffset? CommittedAt);
  public static class ImportDtoMapper { public static ImportBatchDto ToDto(ImportBatch batch, IReadOnlyList<Account> accounts, IReadOnlyDictionary<Guid, BalanceSnapshot?> latestSnapshots); }
  ```
  - `ImportBatchDto.Accounts` lists every account a row is assigned to, plus the user's accounts at the detected institution, so the review screen can offer the right choices.
- Validator:
  - `FileName` ends with `.csv` (case-insensitive): `"Only .csv files are supported."`.
  - `Length` is greater than 0 and at most 5 MB: `"File must be 5 MB or smaller."`.
- Handler order:
  1. Auth check, then parse.
  2. If `AccountId` is given, it must belong to the user, else `Error.NotFound`.
  3. Otherwise pick candidates: the user's active accounts with `Institution == parsed.Institution`; when a row has `ExternalAccountRef`, keep the accounts whose `ExternalAccountRef` the row's ref ends with. Assign when exactly one matches; otherwise leave `AccountId` null.
  4. Normalize descriptions, compute occurrences and fingerprints for assigned rows, and look up existing fingerprints to set duplicates.
  5. Category: `MerchantRule` by normalized key, then the reader's `CategoryHint`, then the categorizer.
  6. KindHint: the reader's `KindHint`. If that is `Regular` and `TransferMatcher.HasHint(normalized)`, use `PendingTransfer`.
  7. Save the batch; return the DTO.

- [ ] **Step 1: Write the failing tests** (parser, repositories and categorizer substituted):
  - `Handle_SingleMatchingAccount_AssignsAndFingerprints`
  - `Handle_ExistingFingerprint_MarksDuplicateAndExcluded`
  - `Handle_TwoAccountsSameInstitution_LeavesUnassigned`
  - `Handle_ExternalRefDisambiguates`: rows with ref "1234" go to the account with ref "1234".
  - `Handle_AccountIdOfOtherUser_ReturnsNotFound`
  - `Handle_MerchantRuleBeatsCategorizer`
  - `Handle_PaymentThankYou_KindHintPendingTransfer`
  - `Handle_ParserFailure_ReturnsFailureAndSavesNothing`
  - Validator tests: `.txt` rejected, 0 bytes rejected, 5 MB + 1 byte rejected, exactly 5 MB accepted.
- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: create import batches from uploaded statements`

---

### Task 10: Review operations (get, list, edit row, reconciliation preview)

**Files:**
- Create:
  - `Features/Imports/Queries/GetImport/{GetImportQuery,GetImportQueryHandler}.cs`
  - `Features/Imports/Queries/GetImports/...`
  - `Features/Imports/Queries/CheckReconciliation/...`
  - `Features/Imports/Commands/UpdateImportRow/{Command,Validator,Handler}.cs`
- Test: `tests/FinSight.Application.Tests/Features/Imports/ReviewOperationsTests.cs`

**Interfaces:**
- Consumes: Task 9 DTOs and mapper; Task 8 calculator; Task 5 fingerprint.
- Produces:
  - `GetImportQuery(Guid ImportId) : IRequest<Result<ImportBatchDto>>`
  - `GetImportsQuery() : IRequest<Result<IReadOnlyList<ImportBatchSummaryDto>>>`: newest first.
  - `UpdateImportRowCommand(Guid ImportId, Guid RowId, TransactionCategory? Category, bool? IsExcluded, Guid? AccountId) : IRequest<Result<ImportRowDto>>`
    - Only allowed while `PendingReview`, else `Error.Custom("Import.NotPending", "This import has already been committed or discarded.")`.
    - Assigning an account computes the fingerprint and the duplicate flag. The account must belong to the user.
  - `CheckReconciliationQuery(Guid ImportId, Guid AccountId, decimal ClosingBalance) : IRequest<Result<ReconciliationResult>>`: uses the account's latest snapshot as the previous balance and the batch's non-excluded rows for that account.

- [ ] **Step 1: Write the failing tests:**
  - `GetImport_OtherUser_NotFound`
  - `GetImports_NewestFirst`
  - `UpdateRow_Category_SetsChangedByUser`
  - `UpdateRow_AssignAccount_ComputesFingerprintAndDuplicate`
  - `UpdateRow_CommittedBatch_Fails`
  - `CheckReconciliation_UsesLatestSnapshotAndIncludedRowsOnly`
- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: add import review queries and row edits`

---

### Task 11: Commit, matching service and undo

**Files:**
- Create:
  - `Features/Imports/Common/TransferMatchingService.cs`
  - `Features/Imports/Commands/CommitImport/{Command,Validator,Handler}.cs`
  - `Features/Imports/Commands/DeleteImport/{Command,Handler}.cs`
- Test: `tests/FinSight.Application.Tests/Features/Imports/{CommitImportCommandHandlerTests,DeleteImportCommandHandlerTests,TransferMatchingServiceTests}.cs`; `tests/FinSight.Api.IntegrationTests/Features/Imports/ImportFlowTests.cs` (the Review Focus 1, 4 and 5 tests live here and run against a real database)

**Interfaces:**
- Consumes: Tasks 2, 3, 5, 7, 8, 9.
- Produces:
  - `ITransferMatchingService.RunAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct)`, registered scoped in Application DI. It:
    - loads `GetMatchPoolAsync(userId, from.AddDays(-5), to.AddDays(5))`
    - builds `MatchCandidate`s, with `HasTransferHint` from the normalized description
    - applies the decisions: `MarkPaired` for pairs, with a shared new `Guid` per pair; `MarkPendingTransfer` or set back to Regular otherwise. It never touches rows with `IsTransferDismissed`.
  - `CommitImportCommand(Guid ImportId, IReadOnlyList<ClosingBalanceInput> Balances) : IRequest<Result<ImportBatchDto>>` with `ClosingBalanceInput(Guid AccountId, decimal ClosingBalance, DateOnly AsOf)`. The handler:
    1. Fails `Import.UnassignedRows` ("Assign an account to every row before committing.") if any included row has no account.
    2. Fails `Import.MissingBalance` ("Enter a closing balance for {account name}.") if an account with included rows has no balance input.
    3. Calls `Account.ImportTransaction` for each included row, with `Kind = KindHint`.
    4. Calls `RecordBalanceSnapshot` per balance input.
    5. Joins the reconciliation warnings (`"{account}: off by {difference:C}"`) for unbalanced, non-skipped accounts.
    6. Upserts a `MerchantRule` for each row with `CategoryChangedByUser`.
    7. Commits the batch, saves, runs matching over the batch period, and saves again.
  - `DeleteImportCommand(Guid ImportId) : IRequest<Result>`:
    - Pending: `Discard()`.
    - Committed:
      - Load the batch's transactions and their partners via `TransferGroupId`, and call `Unpair()` on partners outside the batch.
      - Remove the batch's transactions and snapshots.
      - For each affected account, `ResetBalanceFromSnapshot(latest remaining)`.
      - `MarkUndone()` and save.
    - Discarded: `Error.Custom("Import.AlreadyDiscarded", "This import was already removed.")`.

- [ ] **Step 1: Write the failing unit tests:**
  - `Commit_UnassignedRow_Fails`
  - `Commit_MissingBalance_Fails`
  - `Commit_ImportsIncludedRowsOnly`
  - `Commit_RecordsSnapshotAndBalance`
  - `Commit_UnbalancedAccount_StoresWarning`
  - `Commit_CategoryChanged_UpsertsMerchantRule`
  - `Commit_RunsMatchingOverPeriod`
  - `Delete_Pending_Discards`
  - `Delete_Committed_RemovesTransactionsAndSnapshotsAndResetsBalance`
  - `Matching_DismissedRowsUntouched`
- [ ] **Step 2: Write the failing integration tests** in `ImportFlowTests`, using a `TestStatementReader` registered through the factory's `ConfigureTestServices`. It reads the same `Date,Description,Amount` format as Task 6's `FakeReader`, with `Institution.Other`.
  - `Commit_SameFileTwice_SecondCommitAddsNothing`
  - `Commit_IdenticalSameDayRows_BothKept`
  - `Commit_PartnerFileLater_PairsPendingTransfer`: savings file first (row is PendingTransfer), card file second, so both become Transfer.
  - `Undo_PairedBatch_PartnerBecomesPendingTransfer`
- [ ] **Step 3: Run and confirm failure.** Run the Application tests, then the integration tests with `--filter "FullyQualifiedName~ImportFlowTests"`.
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run tests.** Expected: pass.
- [ ] **Step 6: Commit.** `feat: commit and undo imports with transfer matching`

---

### Task 12: Imports API

**Files:**
- Create: `src/FinSight.Api/Controllers/ImportsController.cs`, `src/FinSight.Api/Common/ResultHttpExtensions.cs`
- Test: `tests/FinSight.Api.IntegrationTests/Features/Imports/ImportsEndpointTests.cs`

**Interfaces:**
- Consumes: the commands and queries from Tasks 9–11.
- Produces:
  - `IActionResult ControllerBase.ToActionResult<T>(Result<T> result, Func<T, IActionResult> onSuccess)` and `ToActionResult(Result)`. Error mapping: `NotFound` → 404, `Unauthorized` → 401, anything else → 400 `ProblemDetails { Title = Code, Detail = Description }`.
  - Endpoints:

    | Endpoint | Behaviour |
    |---|---|
    | `POST /api/v1/imports` | multipart `file`, optional `accountId`; `[RequestSizeLimit(6 * 1024 * 1024)]`; returns 201 + `ImportBatchDto` |
    | `GET /api/v1/imports` | |
    | `GET /api/v1/imports/{id}` | |
    | `PATCH /api/v1/imports/{id}/rows/{rowId}` | body `{ category?, isExcluded?, accountId? }` |
    | `GET /api/v1/imports/{id}/reconciliation?accountId=&closingBalance=` | |
    | `POST /api/v1/imports/{id}/commit` | body `{ balances: [{ accountId, closingBalance, asOf }] }` |
    | `DELETE /api/v1/imports/{id}` | 204 |

- [ ] **Step 1: Write the failing integration tests:**
  - `Upload_ValidCsv_Returns201WithRows`
  - `Upload_TxtFile_Returns400`
  - `Upload_UnknownLayout_Returns400WithMessage`
  - `GetImport_OtherUsersBatch_Returns404`
  - `Commit_OtherUsersBatch_Returns404`
  - `FullFlow_UploadPatchCommitDelete_Succeeds`
- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: expose imports API`

---

### Task 13: Needs-attention transfers

**Files:**
- Create: `Features/Transactions/Queries/GetNeedsAttention/...`, `Features/Transactions/Commands/ResolveTransfer/...`
- Modify: `src/FinSight.Api/Controllers/TransactionsController.cs`; the existing transaction DTO(s) returned by `GetTransactions` gain `string Kind`
- Test: `tests/FinSight.Application.Tests/Features/Transactions/NeedsAttentionTests.cs`; integration `TransactionsEndpointTests` (+2 tests)

**Interfaces:**
- Consumes: `TransferMatcher.FindCandidates`, `Transaction.MarkPaired/MarkNotTransfer`.
- Produces:
  - `GetNeedsAttentionQuery() : IRequest<Result<IReadOnlyList<NeedsAttentionItemDto>>>` with:
    - `NeedsAttentionItemDto(Guid TransactionId, Guid AccountId, string AccountName, DateOnly Date, string Description, decimal Amount, string Type, IReadOnlyList<TransferCandidateDto> Candidates)`
    - `TransferCandidateDto(Guid TransactionId, string AccountName, DateOnly Date, string Description, decimal Amount)`
  - `ResolveTransferCommand(Guid TransactionId, Guid? PartnerId) : IRequest<Result>`:
    - Null partner: `MarkNotTransfer`.
    - Otherwise the partner must be one of `FindCandidates`, else `Error.Custom("Transfer.NotACandidate", "That transaction can't be paired with this one.")`.
    - The kind follows Task 7's rule.
  - Endpoints: `GET /api/v1/transactions/needs-attention`, `POST /api/v1/transactions/{id}/resolve-transfer` with body `{ partnerId? }`.

- [ ] **Step 1: Write the failing tests:**
  - `NeedsAttention_ListsPendingWithCandidates`
  - `Resolve_NullPartner_MarksRegularDismissed`
  - `Resolve_ValidPartnerCorpToPersonal_OwnerPay`
  - `Resolve_NonCandidate_Fails`
  - `Resolve_OtherUsersTransaction_NotFound`
- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: list and resolve unmatched transfers`

---

### Task 14: Personal spending and income rules in existing reports

**Files:**
- Create: `src/FinSight.Application/Common/Reporting/PersonalLedger.cs`
- Modify:
  - `Features/Insights/Queries/GetSpendingInsights/GetSpendingInsightsQueryHandler.cs`
  - `Features/Budgets/Queries/GetBudgets/GetBudgetsQueryHandler.cs`
  - `Features/Budgets/Queries/GetBudgetSummary/GetBudgetSummaryQueryHandler.cs`
  - `src/FinSight.Infrastructure/Services/ML/AnomalyDetectionService.cs`
- Test: `tests/FinSight.Application.Tests/Common/PersonalLedgerTests.cs`; update the existing budget and insight handler tests

**Interfaces:**
- Consumes: `Transaction.Account` (loaded), `Kind`, `IsIncome()`, `IsRegistered()`.
- Produces:
  ```csharp
  public static class PersonalLedger {
      public static bool IsPersonalSpending(Transaction t);    // Kind Regular && Account.Owner Personal && !Category.IsIncome()
      public static decimal SpendingAmount(Transaction t);     // Debit → +Amount, Credit (refund) → −Amount
      public static bool IsTaxableIncome(Transaction t);       // personal && Credit && ((Regular && IsIncome && !(InvestmentIncome && registered)) || OwnerPay)
      public static bool IsTaxShelteredIncome(Transaction t);  // personal && Regular && Credit && InvestmentIncome && Account.Type.IsRegistered()
  }
  ```
  - All four handlers use `IsPersonalSpending` with `SpendingAmount` for spending.
  - Spending insights' income figure uses `IsTaxableIncome`.
  - Anomaly detection considers only `IsPersonalSpending` debits.

- [ ] **Step 1: Write the failing tests:**
  - `IsPersonalSpending_ExcludesTransferOwnerPayTradePending`
  - `IsPersonalSpending_ExcludesCorporateAccount`
  - `SpendingAmount_RefundSubtracts`
  - `IsTaxableIncome_OwnerPayCreditCounts`
  - `IsTaxableIncome_TfsaDividendExcluded`
  - `IsTaxShelteredIncome_RrspInterestCounts`
  - `BudgetSummary_RefundReducesCategorySpent`: debit 100 + refund credit 30 → spent 70.
  - `SpendingInsights_ExcludesTransfers`
- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement.** Update existing tests whose transactions lack a loaded `Account` by building them through `Account.RecordTransaction` or `ImportTransaction`, which sets the navigation.
- [ ] **Step 4: Run all Application and integration tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: exclude transfers and corporate activity from personal spending`

---

### Task 15: Net worth, corporation summary, personal income and settings

**Files:**
- Create:
  - `src/FinSight.Application/Common/Reporting/{NetWorthCalculator,FiscalYear}.cs`
  - `Features/Reports/Queries/{GetNetWorth,GetCorporationSummary,GetPersonalIncome}/...`
  - `Features/Settings/{GetSettings,UpdateSettings}/...`
  - `src/FinSight.Api/Controllers/{ReportsController,SettingsController}.cs`
- Test: `tests/FinSight.Application.Tests/Common/{NetWorthCalculatorTests,FiscalYearTests}.cs`, `tests/FinSight.Application.Tests/Features/Reports/ReportQueriesTests.cs`, integration `ReportsEndpointTests.cs`

**Interfaces:**
- Consumes: `IBalanceSnapshotRepository.GetByUserIdAsync`, `PersonalLedger`, `AccountTypeExtensions`, the `User` fiscal-year fields.
- Produces:
  ```csharp
  public sealed record AccountBalanceSeries(Guid AccountId, AccountOwner Owner, bool IsLiability, decimal CurrentBalance,
      IReadOnlyList<(DateOnly AsOf, decimal Balance)> Snapshots);
  public static class NetWorthCalculator {
      public static (decimal Personal, decimal IncludingCorporation) Current(IReadOnlyList<AccountBalanceSeries> accounts);
      public static (decimal Personal, decimal IncludingCorporation)? At(DateOnly date, IReadOnlyList<AccountBalanceSeries> accounts);
  }
  public static class FiscalYear { public static (DateOnly Start, DateOnly End) Range(int endMonth, int endDay, int endingYear);
                                   public static int EndingYearContaining(DateOnly date, int endMonth, int endDay); }
  public sealed record NetWorthDto(decimal Personal, decimal IncludingCorporation, string Currency, IReadOnlyList<NetWorthPointDto> History);
  public sealed record NetWorthPointDto(DateOnly MonthEnd, decimal Personal, decimal IncludingCorporation);
  public sealed record CorporationSummaryDto(DateOnly FiscalYearStart, DateOnly FiscalYearEnd, decimal Revenue, decimal PaidToOwner,
      decimal Expenses, decimal ClosingBalance, IReadOnlyList<CorporationMonthDto> Months);
  public sealed record CorporationMonthDto(int Year, int Month, decimal Revenue, decimal PaidToOwner, decimal Expenses);
  public sealed record PersonalIncomeDto(int Year, decimal PayFromCorporation, decimal OtherTaxableIncome, decimal TaxShelteredInvestmentIncome);
  public sealed record SettingsDto(int CorporateFiscalYearEndMonth, int CorporateFiscalYearEndDay);
  ```
  - **Net worth:** `Current` uses `CurrentBalance` for every account. `At(date)` uses each account's latest snapshot on or before `date`, skips accounts with none, and returns null when no account has one. Liabilities subtract. Personal excludes Corporation accounts.
  - **History:** the last `Months` month-ends (default 12), skipping null points.
  - **Corporation summary:** corporation accounts only:
    - Revenue = Regular credits.
    - PaidToOwner = OwnerPay debits.
    - Expenses = Regular debits.
    - Transfers are excluded.
    - ClosingBalance = the sum of corporation account balances.
    - The fiscal year comes from the user setting; the default is the one containing today.
  - **Personal income:** for a calendar year, using `PersonalLedger`; PayFromCorporation = OwnerPay credits.
  - Endpoints:
    - `GET /api/v1/reports/net-worth?months=12`
    - `GET /api/v1/reports/corporation?fiscalYear=`
    - `GET /api/v1/reports/personal-income?year=`
    - `GET /api/v1/settings`
    - `PUT /api/v1/settings` with body `{ corporateFiscalYearEndMonth, corporateFiscalYearEndDay }`. Invalid input → 400 with the domain message.

- [ ] **Step 1: Write the failing tests:**
  - `Current_AssetsMinusLiabilities_PersonalExcludesCorp`: savings 5000, Visa owed 1200, TFSA 20000, RBC corp 30000 → Personal 23800, Including 53800.
  - `At_UsesLatestSnapshotOnOrBefore`
  - `At_AccountWithoutSnapshot_Skipped`
  - `At_NoSnapshots_ReturnsNull`
  - `Range_Dec31_2026`: Jan 1 2026–Dec 31 2026.
  - `Range_Jun30_2026`: Jul 1 2025–Jun 30 2026.
  - `EndingYearContaining_Jun30_Aug2026_Is2027`
  - `CorporationSummary_ExcludesPersonalToCorpTransfer`
  - `PersonalIncome_SplitsPayOtherSheltered`
  - `UpdateSettings_Feb29_Returns400` (integration)
- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run tests.** Expected: pass.
- [ ] **Step 5: Commit.** `feat: add net worth, corporation summary, personal income and settings`

---

### Task 16: Accounts API and page (owner, institution, new types)

**Files:**
- Create: `Features/Accounts/Commands/UpdateAccount/{Command,Validator,Handler}.cs`
- Modify:
  - `Features/Accounts/Commands/CreateAccount/*` (add `Institution`, `Owner`, `ExternalAccountRef`)
  - `Queries/GetAccounts/*`: `AccountDto` gains `string Institution, string Owner, string? ExternalAccountRef, bool IsLiability, bool IsInvestment`
  - `src/FinSight.Api/Controllers/AccountsController.cs`: `PUT /api/v1/accounts/{id}`
  - `frontend/src/types/api.ts`, `frontend/src/services/api.ts`, `frontend/src/pages/AccountsPage.tsx`
- Test: `tests/FinSight.Application.Tests/Features/Accounts/UpdateAccountCommandHandlerTests.cs`; integration `AccountsEndpointTests` (+2)

**Interfaces:**
- Produces:
  - `UpdateAccountCommand(Guid AccountId, string Name, AccountType Type, Institution Institution, AccountOwner Owner, string? ExternalAccountRef) : IRequest<Result>`. Another user's account → NotFound.
  - Frontend: `accountsApi.update(id, payload)`.
  - Accounts page:
    - Create and edit forms gain institution, owner (Personal/Corporation) and external-ref fields, plus the new type options.
    - Liability balances are labelled "owed".
    - The total uses `assets − liabilities`.
    - A "Corporation fiscal year end" month/day field calls `PUT /settings`.

- [ ] **Step 1: Write the failing tests:**
  - `Update_ChangesOwnerAndInstitution`
  - `Update_OtherUsersAccount_NotFound`
  - Integration: `CreateAccount_WithOwnerAndInstitution_RoundTrips`, `UpdateAccount_Returns204`
- [ ] **Step 2: Run and confirm failure.**
- [ ] **Step 3: Implement** the backend, then the frontend.
- [ ] **Step 4: Verify.** Run the backend tests; then `cd frontend && npx tsc --noEmit && npx vite build`. Expected: no errors. Then a manual check against the dev API (`npm run dev`): create a Corporation RBC account, edit it, and confirm the totals.
- [ ] **Step 5: Commit.** `feat: manage account owner, institution and Canadian account types`

---

### Task 17: Import page

**Files:**
- Create: `frontend/src/pages/ImportPage.tsx`, `frontend/src/components/imports/{UploadDropzone,ReviewTable,ClosingBalanceForm,ImportHistory}.tsx`
- Modify: `frontend/src/types/api.ts` (Import DTO types mirroring Task 9/10 records), `frontend/src/services/api.ts` (`importsApi`), `frontend/src/App.tsx` (route `import`), `frontend/src/components/layout/Sidebar.tsx` ("Import" link)

**Interfaces:**
- Consumes: the Task 12 endpoints.
- Produces: `importsApi = { upload(file, accountId?), list(), get(id), updateRow(id, rowId, patch), reconciliation(id, accountId, closingBalance), commit(id, balances), remove(id) }`.

Behaviour:
- **Layout:** follows the spec §8 sketch.
- **Upload:** drop zone or file picker, `.csv` only. On success the review loads; on 400 the `ProblemDetails.detail` is shown verbatim.
- **Account selection:** a header with the detected institution. Each unassigned row, or a whole-batch account picker when every row is unassigned, offers only the `ImportBatchDto.Accounts` choices.
- **Review table:**
  - Columns: date, description, signed amount (debits shown negative), category dropdown (all `TransactionCategory` names), status chips (`duplicate`, `⇄ likely transfer` for PendingTransfer, `uncategorized` highlighted), exclude toggle.
  - Each edit calls `updateRow`.
- **Closing balance:** one input and an "as of" date per account (default `PeriodEnd`). Liability accounts are labelled "Amount owed".
- **Reconciliation:** 400 ms after typing, call `reconciliation` and show ✓ balanced, ⚠ off by $X, or the skip reason.
- **Commit button:** shows the count of included rows. It is disabled until every included row has an account and every account has a balance. On success it shows a summary, any reconciliation warning, and a link to Transactions.
- **History:** a list with Undo (calls `remove` after a confirmation, built as an in-page dialog rather than `window.confirm`) and a Discard button for pending batches.

- [ ] **Step 1: Implement** the API client and types, then the components and the page.
- [ ] **Step 2: Verify the build.** `cd frontend && npx tsc --noEmit && npx vite build`. Expected: no type errors.
- [ ] **Step 3: Manual check** with the API running and a fixture CSV:
  - upload → review → change one category → enter balance → commit;
  - upload the same file again and confirm all rows are "duplicate";
  - undo the first import.
- [ ] **Step 4: Commit.** `feat: add statement import page`

---

### Task 18: Dashboard and Transactions updates

**Files:**
- Create: `frontend/src/components/transactions/NeedsAttentionPanel.tsx`
- Modify: `frontend/src/pages/DashboardPage.tsx`, `frontend/src/pages/TransactionsPage.tsx`, `frontend/src/services/api.ts` (`reportsApi`, `transactionsApi.needsAttention`, `transactionsApi.resolveTransfer`), `frontend/src/types/api.ts`

**Interfaces:**
- Consumes: the Task 13 and Task 15 endpoints.
- Produces: `reportsApi = { netWorth(months?), corporation(fiscalYear?), personalIncome(year?) }`.

Behaviour:
- **Dashboard:**
  - Replace the signed-sum net worth (`DashboardPage.tsx:27`) with a net worth card from `reportsApi.netWorth()`: Personal (large), Including corporation (small), and a 12-month line chart (recharts) of both.
  - Spending by category keeps its source endpoint, which now applies the personal rules.
  - Add a "Pay from corporation (YTD)" figure from `personalIncome`.
  - Add a corporation card: revenue, paid to owner, expenses and balance for the fiscal year.
  - Add an "N unresolved transfers" badge linking to Transactions when `needsAttention` is non-empty.
- **Transactions page:**
  - The `NeedsAttentionPanel` lists each item with its candidates as buttons ("Pair with …") and a "Not a transfer" button. Each calls `resolveTransfer`, then refetches the panel and the transactions.
  - Each transaction row shows a Kind chip for non-Regular kinds (Transfer, Owner pay, Investment trade, Pending transfer).

- [ ] **Step 1: Implement.**
- [ ] **Step 2: Verify the build.** `cd frontend && npx tsc --noEmit && npx vite build`. Expected: no errors.
- [ ] **Step 3: Manual check** with seeded data:
  - the net worth card shows Personal < Including corporation;
  - resolving a pending transfer removes it from the panel and the badge count drops.
- [ ] **Step 4: Commit.** `feat: show net worth, corporation summary and unresolved transfers`

---

### Task 19: CIBC, TD and MBNA readers (requires owner samples)

**Precondition:** `samples/` contains the owner's redacted exports for CIBC (savings, card, PLC), TD (card) and MBNA (PLC). **If any is missing, stop and ask the owner for it. Don't write a reader from the spec's §2 table alone.**

**Files:**
- Create: `src/FinSight.Infrastructure/Imports/Readers/{Cibc,Td,Mbna}StatementReader.cs`, `tests/FinSight.Infrastructure.Tests/Imports/Fixtures/{cibc-savings,cibc-card,cibc-plc,td-card,mbna-plc}.csv`, `tests/FinSight.Infrastructure.Tests/Imports/Readers/{Cibc,Td,Mbna}StatementReaderTests.cs`, `tests/FinSight.Infrastructure.Tests/Imports/DetectionTests.cs`
- Modify: `DependencyInjection/ServiceCollectionExtensions.cs` (register the readers)

**Interfaces:**
- Consumes: `IStatementReader`, `CsvRow`, `ParsedStatementRow`, `StatementFormatException` (Task 6).
- Produces: three `IStatementReader` implementations, with `Institution` set to `Cibc`, `Td` and `Mbna`.

Fixture rule: each fixture copies the sample's **structure exactly** (header or no header, column count and order, date format, quoting, sign convention, trailing rows) with **made-up** dates, merchants, amounts and card numbers. Never copy a real row. Each fixture has at least: one purchase, one refund or credit, one payment, one description containing a comma, and one same-day identical pair.

- [ ] **Step 1: Inspect the samples** (`head -5 samples/*`) and write the fixtures.
- [ ] **Step 2: Write the failing tests.** Per reader:
  - `Read_Fixture_ParsesExactRows`, asserting every row's date, description, amount, `Type` (purchase = Debit, payment/refund = Credit) and `LineNumber`.
  - `Read_MalformedDate_ThrowsStatementFormatException`.
  - `DetectionTests.EachFixture_ClaimedByExactlyOneReader`, a theory over every fixture file and all registered readers.
- [ ] **Step 3: Run and confirm failure.** `dotnet test tests/FinSight.Infrastructure.Tests`.
- [ ] **Step 4: Implement** the three readers.
- [ ] **Step 5: Run tests.** Expected: pass.
- [ ] **Step 6: Real-file check** (not committed): upload each real sample through the Import page and confirm the counts and totals match the bank statement.
- [ ] **Step 7: Commit.** `feat: add CIBC, TD and MBNA statement readers`

---

### Task 20: RBC and Wealthsimple readers (requires owner samples)

**Precondition:** the RBC business chequing export and every Wealthsimple export (cash, TFSA, RRSP, FHSA, crypto) are in `samples/`. **If any is missing, stop and ask the owner for it.**

**Files:**
- Create: `src/FinSight.Infrastructure/Imports/Readers/{Rbc,Wealthsimple}StatementReader.cs`, fixtures `rbc-chequing.csv`, `wealthsimple-{cash,tfsa,rrsp,fhsa,crypto}.csv`, tests `{Rbc,Wealthsimple}StatementReaderTests.cs`
- Modify: `DependencyInjection/ServiceCollectionExtensions.cs`, `DetectionTests.cs` (it picks up the new fixtures automatically if it enumerates the folder)

**Interfaces:**
- Produces:
  - `RbcStatementReader`:
    - `Institution.Rbc`.
    - Sets `ExternalAccountRef` from the account-number column (last 4 digits).
    - A row with only a `USD$` amount gets `Currency = "USD"`, so the parser rejects it.
  - `WealthsimpleStatementReader`:
    - `Institution.Wealthsimple`.
    - Maps the transaction code to `KindHint`/`CategoryHint`: buy/sell → `InvestmentTrade`; dividend/interest → `CategoryHint = InvestmentIncome`; contribution/deposit/withdrawal → `PendingTransfer`; fee → `CategoryHint = BankFees`.
    - The code table is written from the actual codes found in the samples.
    - If a crypto layout differs, it gets its own reader class.

Fixture rule: same as Task 19. The RBC fixture includes two different account numbers, to test routing.

- [ ] **Step 1: Inspect the samples and write the fixtures.**
- [ ] **Step 2: Write the failing tests:**
  - `Rbc_Read_SetsExternalRefPerRow`
  - `Rbc_Read_SignedAmountsToTypes`
  - `Rbc_UsdOnlyRow_CurrencyUsd`
  - `Wealthsimple_Read_MapsEveryCodeInFixture`: a theory over each code seen.
  - `Wealthsimple_UnknownCode_ThrowsStatementFormatException`: the message names the code.
  - Detection theory still passes.
- [ ] **Step 3: Run and confirm failure.**
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run tests.** Run all test projects. Expected: pass.
- [ ] **Step 6: Real-file check** (not committed). Upload each real sample:
  - the RBC rows land on the Corporation account;
  - the Wealthsimple contributions pair with the CIBC or RBC transfers;
  - the dashboard pay-from-corporation figure matches the RBC → personal transfers.
- [ ] **Step 7: Commit.** `feat: add RBC and Wealthsimple statement readers`
