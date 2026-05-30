using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Validation;
using InovaSkill.Importer.Domain.ValueObjects;

namespace InovaSkill.Importer.Infrastructure.Validation;

public sealed class RowValidator : IRowValidator
{
    public ValidationResult Validate(ImportedRow row, FileSchema schema)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(schema.ImportFileTypeCode))
        {
            result.Errors.Add(new ValidationError("ImportFileType", "Unknown schema."));
            return result;
        }

        foreach (var column in schema.Columns)
        {
            var value = row.Get(column.Name);
            if (column.Required && string.IsNullOrWhiteSpace(value))
            {
                result.Errors.Add(new ValidationError(column.Name, "Field is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var ok = column.DataType switch
            {
                ColumnDataType.Int => int.TryParse(value, out _),
                ColumnDataType.Decimal => decimal.TryParse(value, out _),
                ColumnDataType.DateTime => DateTime.TryParse(value, out _),
                ColumnDataType.Email => value.Contains('@'),
                _ => true
            };

            if (!ok)
            {
                result.Errors.Add(new ValidationError(column.Name, $"Invalid {column.DataType} value."));
            }
        }

        return result;
    }
}

