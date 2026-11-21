using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Transactions.Commands.ResolveTransfer;

public sealed class ResolveTransferCommandHandler(
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<ResolveTransferCommand, Result>
{
    public async Task<Result> Handle(ResolveTransferCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var tx = await transactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (tx is null || !await accountRepository.BelongsToUserAsync(tx.AccountId, userId, cancellationToken))
            return Result.Failure(Error.NotFound);

        if (tx.TransferGroupId is not null)
            return Result.Failure(Error.Custom("Transfer.AlreadyPaired", "This transaction is already paired."));

        if (request.PartnerId is not { } partnerId)
        {
            tx.MarkNotTransfer();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var notACandidate = Result.Failure(
            Error.Custom("Transfer.NotACandidate", "That transaction can't be paired with this one."));

        // The pool holds only unpaired, undismissed rows with Account loaded, so an already-resolved
        // transaction is simply absent and falls out as "not a candidate".
        var date = DateOnly.FromDateTime(tx.OccurredAt.UtcDateTime);
        var pool = await transactionRepository.GetMatchPoolAsync(
            userId, date.AddDays(-TransferMatcher.WindowDays), date.AddDays(TransferMatcher.WindowDays), cancellationToken);
        var self = pool.FirstOrDefault(t => t.Id == tx.Id);
        var partner = pool.FirstOrDefault(t => t.Id == partnerId);
        if (self is null || partner is null)
            return notACandidate;

        var selfCandidate = MatchCandidates.From(self);
        var partnerCandidate = MatchCandidates.From(partner);
        var candidates = TransferMatcher.FindCandidates(selfCandidate, pool.Select(MatchCandidates.From).ToList());
        if (candidates.All(c => c.TransactionId != partnerId))
            return notACandidate;

        var group = Guid.NewGuid();
        var (selfKind, selfCategory) = TransferMatcher.DecidePair(selfCandidate, partnerCandidate);
        var (partnerKind, partnerCategory) = TransferMatcher.DecidePair(partnerCandidate, selfCandidate);
        self.MarkPaired(group, selfKind, selfCategory);
        partner.MarkPaired(group, partnerKind, partnerCategory);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
