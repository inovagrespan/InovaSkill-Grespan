using InovaSkill.Importer.Application.Abstractions;
using InovaSkill.Importer.Application.Validation;
using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.ValueObjects;
using System.Globalization;

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
                ColumnDataType.Decimal => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
                ColumnDataType.DateTime => DateTime.TryParse(value, out _),
                ColumnDataType.Email => value.Contains('@'),
                _ => true
            };

            if (!ok)
            {
                result.Errors.Add(new ValidationError(column.Name, $"Invalid {column.DataType} value."));
                continue;
            }

            if (column.DataType == ColumnDataType.Decimal &&
                column.Precision is not null &&
                column.Scale is not null &&
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue) &&
                !FitsPrecision(decimalValue, column.Precision.Value, column.Scale.Value))
            {
                result.Errors.Add(new ValidationError(column.Name, $"Value exceeds numeric({column.Precision},{column.Scale}) limit."));
            }
        }

        ValidateSalesInvoiceComputedValues(row, schema, result);

        return result;
    }

    private static void ValidateSalesInvoiceComputedValues(ImportedRow row, FileSchema schema, ValidationResult result)
    {
        if (!string.Equals(schema.ImportFileTypeCode, ImportFileTypeCodes.SalesInvoice, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!decimal.TryParse(row.Get("quantity"), NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) ||
            !decimal.TryParse(row.Get("unitprice"), NumberStyles.Number, CultureInfo.InvariantCulture, out var unitPrice))
        {
            return;
        }

        var totalAmount = Math.Abs(quantity * unitPrice);
        if (!FitsPrecision(totalAmount, precision: 18, scale: 2))
        {
            result.Errors.Add(new ValidationError("totalamount", "Calculated total amount exceeds numeric(18,2) limit."));
        }
    }

    private static bool FitsPrecision(decimal value, int precision, int scale)
    {
        var limit = DecimalPower(precision - scale);
        var rounded = decimal.Round(Math.Abs(value), scale, MidpointRounding.AwayFromZero);
        return rounded < limit;
    }

    private static decimal DecimalPower(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}

