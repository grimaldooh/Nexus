using FluentValidation;
using Nexus.API.Models;

namespace Nexus.API.Validation;

public class UploadCsvRequestValidator : AbstractValidator<UploadCsvRequest>
{
    public UploadCsvRequestValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.FileName)
            .NotEmpty()
            .Must(name => name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
                          name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                          name.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only .csv, .xlsx, or .xls files are supported.");
        RuleFor(x => x.CarrierCode)
            .NotEmpty()
            .MaximumLength(50);
    }
}
