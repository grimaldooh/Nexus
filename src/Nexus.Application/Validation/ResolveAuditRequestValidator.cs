using FluentValidation;
using Nexus.Application.DTOs;

namespace Nexus.Application.Validation;

public class ResolveAuditRequestValidator : AbstractValidator<ResolveAuditRequest>
{
    public ResolveAuditRequestValidator()
    {
        RuleFor(x => x.TransactionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
