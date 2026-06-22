using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IApprovalWorkflowService
{
    Task<bool> RequiresApprovalAsync(Guid storeId, string requestType, decimal amount, CancellationToken cancellationToken);
    Task<Guid?> SubmitForApprovalAsync(Guid storeId, string requestType, Guid targetId, decimal amount, Guid requestedBy, CancellationToken cancellationToken);
    Task<bool> ApproveStepAsync(Guid requestId, Guid actionedBy, string? comments, CancellationToken cancellationToken);
}

public class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly IApplicationDbContext _context;

    public ApprovalWorkflowService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> RequiresApprovalAsync(Guid storeId, string requestType, decimal amount, CancellationToken cancellationToken)
    {
        var limits = await _context.ApprovalLimits
            .FirstOrDefaultAsync(l => l.StoreId == storeId && l.RequestType == requestType, cancellationToken);

        if (limits == null)
        {
            // Seed a default limit if not present (using Head Office default limits)
            var hoStoreId = Guid.Parse("00000000-0000-0000-0000-000000000000");
            limits = await _context.ApprovalLimits
                .FirstOrDefaultAsync(l => l.StoreId == hoStoreId && l.RequestType == requestType, cancellationToken);

            if (limits == null)
            {
                if (requestType == "SUPPLIER_PAYMENT" || requestType == "JOURNAL_ADJUSTMENT" || requestType == "ASSET_PURCHASE")
                {
                    limits = new ApprovalLimit
                    {
                        StoreId = storeId,
                        RequestType = requestType,
                        ManagerLimit = 25000.00m,
                        OwnerLimit = 100000.00m
                    };
                    _context.ApprovalLimits.Add(limits);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    return true;
                }
            }
        }

        return amount > limits.ManagerLimit;
    }

    public async Task<Guid?> SubmitForApprovalAsync(Guid storeId, string requestType, Guid targetId, decimal amount, Guid requestedBy, CancellationToken cancellationToken)
    {
        var limits = await _context.ApprovalLimits
            .FirstOrDefaultAsync(l => l.StoreId == storeId && l.RequestType == requestType, cancellationToken);

        if (limits == null)
        {
            var hoStoreId = Guid.Parse("00000000-0000-0000-0000-000000000000");
            limits = await _context.ApprovalLimits
                .FirstOrDefaultAsync(l => l.StoreId == hoStoreId && l.RequestType == requestType, cancellationToken);
        }

        decimal managerLimit = limits?.ManagerLimit ?? 25000.00m;
        decimal ownerLimit = limits?.OwnerLimit ?? 100000.00m;

        if (amount <= managerLimit)
        {
            return null;
        }

        var request = new ApprovalRequest
        {
            StoreId = storeId,
            RequestType = requestType,
            TargetId = targetId,
            Amount = amount,
            RequestedById = requestedBy,
            Status = "PENDING"
        };

        _context.ApprovalRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        // Level 1: Manager approval is always required if amount > managerLimit
        var step1 = new ApprovalRequestStep
        {
            ApprovalRequestId = request.Id,
            Level = 1,
            RoleName = "Manager",
            Status = "PENDING"
        };
        _context.ApprovalRequestSteps.Add(step1);

        // Level 2: Owner approval is also required if amount > ownerLimit
        if (amount > ownerLimit)
        {
            var step2 = new ApprovalRequestStep
            {
                ApprovalRequestId = request.Id,
                Level = 2,
                RoleName = "Owner",
                Status = "PENDING"
            };
            _context.ApprovalRequestSteps.Add(step2);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return request.Id;
    }

    public async Task<bool> ApproveStepAsync(Guid requestId, Guid actionedBy, string? comments, CancellationToken cancellationToken)
    {
        var request = await _context.ApprovalRequests
            .Include(r => r.Steps)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null) throw new InvalidOperationException("Approval request not found.");
        if (request.Status != "PENDING") throw new InvalidOperationException("Approval request is already resolved.");

        // Get actioner's role
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == actionedBy, cancellationToken);
        if (user == null) throw new InvalidOperationException("User not found.");

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == user.RoleId, cancellationToken);
        if (role == null) throw new InvalidOperationException("User role not found.");

        // Find the lowest pending step
        var nextStep = request.Steps
            .Where(s => s.Status == "PENDING")
            .OrderBy(s => s.Level)
            .FirstOrDefault();

        if (nextStep == null)
        {
            throw new InvalidOperationException("No pending steps found for this approval request.");
        }

        // Validate role matches required role (Owner can act as Manager, but not vice versa, unless exact match is required.
        // Let's enforce that if role is Owner, they can approve Manager steps as well, which is common in ERPs.)
        bool isAuthorized = string.Equals(role.Name, nextStep.RoleName, StringComparison.OrdinalIgnoreCase) 
            || (string.Equals(role.Name, "Owner", StringComparison.OrdinalIgnoreCase));

        if (!isAuthorized)
        {
            throw new InvalidOperationException($"Authorized role '{nextStep.RoleName}' is required. Acting user has role '{role.Name}'.");
        }

        // Approve this step
        nextStep.Status = "APPROVED";
        nextStep.ActionedById = actionedBy;
        nextStep.ActionedAt = DateTime.UtcNow;
        nextStep.Comments = comments;

        // Check if there are any remaining pending steps
        var remainingPending = request.Steps.Any(s => s.Status == "PENDING" && s.Id != nextStep.Id);
        if (!remainingPending)
        {
            // All steps approved
            request.Status = "APPROVED";
            request.ActionedById = actionedBy;
            request.ActionedAt = DateTime.UtcNow;
            request.Comments = comments;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return request.Status == "APPROVED";
    }
}
