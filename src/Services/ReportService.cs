using System;
using System.Threading.Tasks;
using aspnet_core_api.Models.Reports;
using System.Text.Json;

namespace aspnet_core_api.Services
{
    public interface IReportService
    {
        Task<byte[]> GeneratePdfReport(string reportType, object data);
        Task<byte[]> GenerateExcelReport(string reportType, object data);
        Task<string> GenerateHtmlReport(string reportType, object data);
    }

    public class ReportService : IReportService
    {
        public async Task<byte[]> GeneratePdfReport(string reportType, object data)
        {
            // For now, return a simple PDF placeholder
            // In a real implementation, you would use a library like iTextSharp, PuppeteerSharp, or similar
            var html = await GenerateHtmlReport(reportType, data);

            // Placeholder - in real implementation, convert HTML to PDF
            var bytes = System.Text.Encoding.UTF8.GetBytes($"PDF Report: {reportType}\n\n{html}");
            return bytes;
        }

        public async Task<byte[]> GenerateExcelReport(string reportType, object data)
        {
            // For now, return a simple Excel placeholder
            // In a real implementation, you would use a library like EPPlus, ClosedXML, or similar
            await Task.Delay(1); // Simulate async operation

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes($"Excel Report: {reportType}\n\n{json}");
            return bytes;
        }

        public async Task<string> GenerateHtmlReport(string reportType, object data)
        {
            await Task.Delay(1); // Simulate async operation

            var html = reportType.ToLower() switch
            {
                "sales" => GenerateSalesHtmlReport(data),
                "inventory" => GenerateInventoryHtmlReport(data),
                "customer" => GenerateCustomerHtmlReport(data),
                _ => GenerateGenericHtmlReport(reportType, data)
            };

            return html;
        }

        private string GenerateSalesHtmlReport(object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Sales Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .summary {{ background-color: #f5f5f5; padding: 15px; margin-bottom: 20px; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #4CAF50; color: white; }}
        .total {{ font-weight: bold; background-color: #f0f0f0; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>Sales Report</h1>
        <p>Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>
    
    <div class='summary'>
        <h2>Report Data</h2>
        <pre>{json}</pre>
    </div>
</body>
</html>";
        }

        private string GenerateInventoryHtmlReport(object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Inventory Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .low-stock {{ background-color: #ffe6e6; }}
        .out-of-stock {{ background-color: #ffcccc; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #2196F3; color: white; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>Inventory Report</h1>
        <p>Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>
    
    <div>
        <h2>Inventory Data</h2>
        <pre>{json}</pre>
    </div>
</body>
</html>";
        }

        private string GenerateCustomerHtmlReport(object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Customer Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #FF9800; color: white; }}
        .high-value {{ background-color: #e8f5e8; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>Customer Report</h1>
        <p>Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>
    
    <div>
        <h2>Customer Data</h2>
        <pre>{json}</pre>
    </div>
</body>
</html>";
        }

        private string GenerateGenericHtmlReport(string reportType, object data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>{reportType} Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        pre {{ background-color: #f5f5f5; padding: 15px; border-radius: 5px; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>{reportType} Report</h1>
        <p>Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>
    
    <div>
        <h2>Report Data</h2>
        <pre>{json}</pre>
    </div>
</body>
</html>";
        }
    }
}
