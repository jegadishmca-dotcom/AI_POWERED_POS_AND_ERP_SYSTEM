using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Finance;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Commands;

public record CreateAccountCommand(
    string AccountCode,
    string Name,
    string AccountType, // ASSET, LIABILITY, EQUITY, REVENUE, EXPENSE
    Guid? ParentAccountId
) : IRequest<Guid>;

public record UpdateAccountCommand(
    Guid Id,
    string Name,
    Guid? ParentAccountId
) : IRequest<bool>;

public record ToggleAccountStatusCommand(Guid Id, bool IsActive) : IRequest<bool>;

public class AccountsCommandsHandler : 
    IRequestHandler<CreateAccountCommand, Guid>,
    IRequestHandler<UpdateAccountCommand, bool>,
    IRequestHandler<ToggleAccountStatusCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public AccountsCommandsHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Accounts.AnyAsync(a => a.AccountCode == request.AccountCode, cancellationToken);
        if (exists) throw new InvalidOperationException($"Account code '{request.AccountCode}' is already registered in the COA.");

        if (request.ParentAccountId.HasValue)
        {
            var parent = await _context.Accounts.FindAsync(new object[] { request.ParentAccountId.Value }, cancellationToken);
            if (parent == null) throw new InvalidOperationException("Parent Account not found.");
        }

        var account = new Account
        {
            AccountCode = request.AccountCode,
            Name = request.Name,
            AccountType = request.AccountType.ToUpper(),
            ParentAccountId = request.ParentAccountId,
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    public async Task<bool> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.FindAsync(new object[] { request.Id }, cancellationToken);
        if (account == null) throw new InvalidOperationException("Account not found.");

        if (request.ParentAccountId.HasValue)
        {
            if (request.ParentAccountId.Value == request.Id) throw new InvalidOperationException("An account cannot be parent to itself.");
            var parent = await _context.Accounts.FindAsync(new object[] { request.ParentAccountId.Value }, cancellationToken);
            if (parent == null) throw new InvalidOperationException("Parent Account not found.");
        }

        account.Name = request.Name;
        account.ParentAccountId = request.ParentAccountId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> Handle(ToggleAccountStatusCommand request, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts.FindAsync(new object[] { request.Id }, cancellationToken);
        if (account == null) throw new InvalidOperationException("Account not found.");

        account.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
