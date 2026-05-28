using InovaSkill.Importer.Domain.Entities;
using InovaSkill.Importer.Domain.Enums;
using InovaSkill.Importer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InovaSkill.Importer.Infrastructure.Persistence.Bootstrap;

public static class DbSchemaBootstrapper
{
    public static async Task EnsureProgressColumnsAsync(DbContext db, CancellationToken cancellationToken = default)
    {
        if (db is not ImportDbContext context)
        {
            return;
        }

        await EnsureFileJobsColumnsAsync(context, cancellationToken);
        await EnsureDefaultTemplatesAsync(context, cancellationToken);
    }

    private static async Task EnsureFileJobsColumnsAsync(ImportDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "FileJobs"
            ADD COLUMN IF NOT EXISTS "OriginalFileName" character varying(512) NOT NULL DEFAULT '';
            """,
            cancellationToken);
    }

    private static async Task EnsureDefaultTemplatesAsync(ImportDbContext db, CancellationToken cancellationToken)
    {
        var templates = await db.PreProcessorTemplates
            .Include(x => x.Rules)
            .ToListAsync(cancellationToken);

        var products = UpsertTemplate(
            db,
            templates,
            "default_products_v1",
            "Template Padrão Products",
            FileType.Products,
            "products",
            "sku,name,price");

        var orders = UpsertTemplate(
            db,
            templates,
            "default_orders_v1",
            "Template Padrão Orders",
            FileType.Orders,
            "orders",
            "ordernumber,customeremail,productsku,quantity,orderdate");

        var customers = UpsertTemplate(
            db,
            templates,
            "default_customers_v1",
            "Template Padrão Customers",
            FileType.Customers,
            "customers",
            "name,email");

        UpsertRule(products, "products_extract_weight_value", "extract_regex", true, "{\"source\":\"description\",\"target\":\"weight_value\",\"pattern\":\"(\\\\d+([.,]\\\\d+)?)\\\\s*(kg|g)\",\"group\":\"1\",\"overwrite\":false}", 10);
        UpsertRule(products, "products_extract_weight_unit", "extract_regex", true, "{\"source\":\"description\",\"target\":\"weight_unit\",\"pattern\":\"(\\\\d+([.,]\\\\d+)?)\\\\s*(kg|g)\",\"group\":\"3\",\"overwrite\":false}", 20);
        UpsertRule(products, "products_normalize_weight_value", "normalize_number", true, "{\"column\":\"weight_value\",\"target\":\"weight_value\",\"decimalSeparator\":\",\",\"thousandSeparator\":\".\"}", 30);
        UpsertRule(products, "products_normalize_price", "normalize_number", true, "{\"column\":\"price\",\"target\":\"price\",\"decimalSeparator\":\",\",\"thousandSeparator\":\".\"}", 40);
        UpsertRule(products, "products_convert_weight_kg", "convert_weight_to_kg", true, "{\"valueColumn\":\"weight_value\",\"unitColumn\":\"weight_unit\",\"target\":\"weight_kg\"}", 45);
        UpsertRule(products, "products_validate_required_sku", "validate_required", true, "{\"column\":\"sku\",\"message\":\"sku obrigatório\"}", 100);
        UpsertRule(products, "products_validate_required_name", "validate_required", true, "{\"column\":\"name\",\"message\":\"name obrigatório\"}", 110);
        UpsertRule(products, "products_validate_decimal_price", "validate_decimal", true, "{\"column\":\"price\",\"message\":\"price inválido\"}", 120);

        UpsertRule(orders, "orders_map_orderdate_orderedat", "map_column", true, "{\"from\":\"orderdate\",\"to\":\"orderedat\",\"overwrite\":false}", 5);
        UpsertRule(orders, "orders_normalize_orderdate", "normalize_date", true, "{\"column\":\"orderedat\",\"target\":\"orderedat\",\"formats\":[\"dd/MM/yyyy\",\"yyyy-MM-dd\",\"MM/dd/yyyy\"],\"outputFormat\":\"yyyy-MM-dd\"}", 10);
        UpsertRule(orders, "orders_normalize_quantity", "normalize_number", true, "{\"column\":\"quantity\",\"target\":\"quantity\",\"decimalSeparator\":\",\",\"thousandSeparator\":\".\"}", 20);
        UpsertRule(orders, "orders_validate_required_ordernumber", "validate_required", true, "{\"column\":\"ordernumber\",\"message\":\"ordernumber obrigatório\"}", 100);
        UpsertRule(orders, "orders_validate_email_customer", "validate_email", true, "{\"column\":\"customeremail\",\"message\":\"customeremail inválido\"}", 110);
        UpsertRule(orders, "orders_validate_int_quantity", "validate_int", true, "{\"column\":\"quantity\",\"message\":\"quantity inválido\"}", 120);
        UpsertRule(orders, "orders_validate_datetime_orderedat", "validate_datetime", true, "{\"column\":\"orderedat\",\"message\":\"orderedat inválido\"}", 130);

        UpsertRule(customers, "customers_split_single_column", "split_single_column", true, "{\"column\":\"raw\",\"delimiter\":\";\",\"headers\":[\"name\",\"email\",\"createdat\"],\"overwrite\":false}", 10);
        UpsertRule(customers, "customers_map_email_alias", "map_column", true, "{\"from\":\"e-mail\",\"to\":\"email\",\"overwrite\":false}", 20);
        UpsertRule(customers, "customers_validate_required_name", "validate_required", true, "{\"column\":\"name\",\"message\":\"name obrigatório\"}", 100);
        UpsertRule(customers, "customers_validate_email", "validate_email", true, "{\"column\":\"email\",\"message\":\"email inválido\"}", 110);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static PreProcessorTemplate UpsertTemplate(
        ImportDbContext db,
        IList<PreProcessorTemplate> templates,
        string code,
        string name,
        FileType fileType,
        string fileNamePattern,
        string requiredHeadersCsv)
    {
        var existing = templates.FirstOrDefault(x => x.Code == code);
        if (existing is not null)
        {
            existing.Name = name;
            existing.IsActive = true;
            existing.FileType = fileType;
            existing.FileNamePattern = fileNamePattern;
            existing.RequiredHeadersCsv = requiredHeadersCsv;
            existing.UpdatedAt = DateTime.UtcNow;
            return existing;
        }

        var created = new PreProcessorTemplate
        {
            Code = code,
            Name = name,
            IsActive = true,
            FileType = fileType,
            FileNamePattern = fileNamePattern,
            RequiredHeadersCsv = requiredHeadersCsv,
            ColumnMappingsJson = string.Empty,
            ValidationRulesJson = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Rules = []
        };

        db.PreProcessorTemplates.Add(created);
        templates.Add(created);
        return created;
    }

    private static void UpsertRule(
        PreProcessorTemplate template,
        string name,
        string ruleType,
        bool isEnabled,
        string configJson,
        int sortOrder)
    {
        var existing = template.Rules.FirstOrDefault(x => x.Name == name);
        if (existing is not null)
        {
            existing.RuleType = ruleType;
            existing.IsEnabled = isEnabled;
            existing.ConfigJson = configJson;
            existing.SortOrder = sortOrder;
            return;
        }

        template.Rules.Add(new PreProcessorTemplateRule
        {
            Name = name,
            RuleType = ruleType,
            IsEnabled = isEnabled,
            ConfigJson = configJson,
            SortOrder = sortOrder
        });
    }
}
