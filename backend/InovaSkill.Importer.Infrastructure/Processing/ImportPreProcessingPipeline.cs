using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using System.Globalization;

namespace InovaSkill.Importer.Infrastructure.Processing;

public sealed class ImportPreProcessingPipeline(
    IPreProcessorTemplateResolver preProcessorTemplateResolver,
    IImportMappingEngine importMappingEngine,
    IFileTypeDetector fileTypeDetector) : IImportPreProcessingPipeline
{
    public async IAsyncEnumerable<PreProcessedImportRow> ProcessRowsAsync(
        ImportPreProcessingRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var detectedFileTypeCode = request.ImportFileTypeCode;
        var detectionAttempted = !string.IsNullOrWhiteSpace(detectedFileTypeCode);
        var processedRows = 0;
        ImportTemplate? template = null;

        await foreach (var tableRow in request.Rows.WithCancellation(cancellationToken))
        {
            processedRows++;
            var row = new ImportedRow(tableRow.RowNumber, tableRow.Values);

            if (processedRows == 1)
            {
                template = await preProcessorTemplateResolver.ResolveAsync(
                    request.FileName,
                    row.Values.Keys.ToArray(),
                    cancellationToken);

                if (template?.ImportFileType is not null && string.IsNullOrWhiteSpace(detectedFileTypeCode))
                {
                    detectedFileTypeCode = template.ImportFileType.Code;
                    detectionAttempted = true;
                }
            }

            var errors = new List<ImportPreProcessingError>();
            var normalizedRow = row;

            if (template is not null)
            {
                var rawValues = row.Values.ToDictionary(k => k.Key, v => (object?)v.Value, StringComparer.OrdinalIgnoreCase);
                var mapped = importMappingEngine.MapRow(row.RowNumber, rawValues, template);
                foreach (var mapError in mapped.Errors)
                {
                    errors.Add(new ImportPreProcessingError(mapError.RowNumber, mapError.Column, mapError.Message));
                }

                var mappedValues = mapped.StandardValues.ToDictionary(k => k.Key, v => FormatNormalizedValue(v.Value), StringComparer.OrdinalIgnoreCase);
                normalizedRow = new ImportedRow(row.RowNumber, mappedValues);
            }
            else if (!string.IsNullOrWhiteSpace(detectedFileTypeCode))
            {
                normalizedRow = ApplyDefaultAliases(normalizedRow, detectedFileTypeCode);
            }

            if (!detectionAttempted)
            {
                detectedFileTypeCode = fileTypeDetector.DetectCode(normalizedRow.Values);
                detectionAttempted = true;

                if (string.IsNullOrWhiteSpace(detectedFileTypeCode))
                {
                    errors.Add(new ImportPreProcessingError(
                        normalizedRow.RowNumber,
                        "ImportFileType",
                        "Unable to detect file type from header."));

                    yield return new PreProcessedImportRow(normalizedRow, detectedFileTypeCode, errors, ShouldStopProcessing: true);
                    yield break;
                }
            }

            yield return new PreProcessedImportRow(normalizedRow, detectedFileTypeCode, errors, ShouldStopProcessing: false);
        }
    }

    private static string FormatNormalizedValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static ImportedRow ApplyDefaultAliases(ImportedRow row, string importFileTypeCode)
    {
        if (!string.Equals(importFileTypeCode, ImportFileTypeCodes.CustomerList, StringComparison.OrdinalIgnoreCase))
        {
            return row;
        }

        var values = new Dictionary<string, string>(row.Values, StringComparer.OrdinalIgnoreCase);

        if (!values.ContainsKey("customercode") || string.IsNullOrWhiteSpace(values["customercode"]))
        {
            if (values.TryGetValue("cliente", out var customerCodeAlias) && !string.IsNullOrWhiteSpace(customerCodeAlias))
            {
                values["customercode"] = customerCodeAlias;
            }
        }

        if (!values.ContainsKey("name") || string.IsNullOrWhiteSpace(values["name"]))
        {
            if (values.TryGetValue("nome", out var nameAlias) && !string.IsNullOrWhiteSpace(nameAlias))
            {
                values["name"] = nameAlias;
            }
        }

        if (!values.ContainsKey("email") || string.IsNullOrWhiteSpace(values["email"]))
        {
            if (values.TryGetValue("e-mail", out var emailAlias) && !string.IsNullOrWhiteSpace(emailAlias))
            {
                values["email"] = emailAlias;
            }
        }

        return new ImportedRow(row.RowNumber, values);
    }
}
