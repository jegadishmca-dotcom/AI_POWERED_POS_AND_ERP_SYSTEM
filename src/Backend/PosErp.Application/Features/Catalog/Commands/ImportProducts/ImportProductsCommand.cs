using MediatR;
using Microsoft.AspNetCore.Http;
using PosErp.Application.Interfaces;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;

namespace PosErp.Application.Features.Catalog.Commands.ImportProducts;

public record ImportProductsCommand(IFormFile File) : IRequest<string>;

public class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, string>
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public ImportProductsCommandHandler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<string> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            throw new ArgumentException("Empty file.");
        }

        // Save file temporarily for background processing
        var tempPath = Path.GetTempFileName();
        using (var stream = new FileStream(tempPath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream, cancellationToken);
        }

        // Enqueue background job (Hangfire)
        var jobId = _backgroundJobClient.Enqueue<ICsvImportService>(x => x.ProcessProductCsvAsync(tempPath));

        return $"Import started successfully. Job ID: {jobId}";
    }
}

// Background service interface (implementation would handle the Transaction and Validation)
public interface ICsvImportService
{
    Task ProcessProductCsvAsync(string filePath);
}
