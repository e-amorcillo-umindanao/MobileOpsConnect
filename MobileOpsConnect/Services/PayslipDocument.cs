using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MobileOpsConnect.Services
{
    public class PayslipData
    {
        public string EmployeeName { get; set; } = "";
        public string EmployeeEmail { get; set; } = "";
        public string Role { get; set; } = "Employee";
        public string CompanyName { get; set; } = "MobileOps Connect";
        public string PayPeriod { get; set; } = "";
        public DateTime PayDate { get; set; } = DateTime.Now;

        // Earnings
        public decimal BasicSalary { get; set; }
        public decimal Overtime { get; set; }
        public decimal Allowances { get; set; }

        // Deductions
        public decimal Tax { get; set; }
        public decimal SSS { get; set; }
        public decimal PhilHealth { get; set; }
        public decimal PagIbig { get; set; }

        public decimal TotalEarnings => BasicSalary + Overtime + Allowances;
        public decimal TotalDeductions => Tax + SSS + PhilHealth + PagIbig;
        public decimal NetPay => TotalEarnings - TotalDeductions;
    }

    public class PayslipDocument : IDocument
    {
        private readonly PayslipData _data;

        public PayslipDocument(PayslipData data)
        {
            _data = data;
        }

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        void ComposeHeader(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(_data.CompanyName)
                            .FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
                        c.Item().Text("Employee Payslip")
                            .FontSize(14).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(150).AlignRight().Column(c =>
                    {
                        c.Item().Text($"Pay Date: {_data.PayDate:MMM dd, yyyy}")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                        c.Item().Text($"Period: {_data.PayPeriod}")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });

                col.Item().PaddingVertical(8).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
            });
        }

        void ComposeContent(IContainer container)
        {
            container.PaddingVertical(10).Column(col =>
            {
                // Employee Info
                col.Item().Background(Colors.Grey.Lighten4).Padding(12).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Employee Details").Bold().FontSize(11);
                        c.Item().PaddingTop(4).Text($"Name: {_data.EmployeeName}");
                        c.Item().Text($"Email: {_data.EmployeeEmail}");
                        c.Item().Text($"Role: {_data.Role}");
                    });
                });

                col.Item().PaddingVertical(12);

                // Earnings Table
                col.Item().Text("Earnings").Bold().FontSize(12).FontColor(Colors.Green.Darken3);
                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Green.Darken3).Padding(6)
                            .Text("Description").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Green.Darken3).Padding(6).AlignRight()
                            .Text("Amount (₱)").FontColor(Colors.White).Bold();
                    });

                    AddRow(table, "Basic Salary", _data.BasicSalary);
                    AddRow(table, "Overtime", _data.Overtime);
                    AddRow(table, "Allowances", _data.Allowances);

                    table.Cell().ColumnSpan(1).Background(Colors.Green.Lighten4).Padding(6)
                        .Text("Total Earnings").Bold();
                    table.Cell().Background(Colors.Green.Lighten4).Padding(6).AlignRight()
                        .Text($"₱{_data.TotalEarnings:N2}").Bold();
                });

                col.Item().PaddingVertical(12);

                // Deductions Table
                col.Item().Text("Deductions").Bold().FontSize(12).FontColor(Colors.Red.Darken3);
                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Red.Darken3).Padding(6)
                            .Text("Description").FontColor(Colors.White).Bold();
                        header.Cell().Background(Colors.Red.Darken3).Padding(6).AlignRight()
                            .Text("Amount (₱)").FontColor(Colors.White).Bold();
                    });

                    AddRow(table, "Withholding Tax", _data.Tax);
                    AddRow(table, "SSS Contribution", _data.SSS);
                    AddRow(table, "PhilHealth", _data.PhilHealth);
                    AddRow(table, "Pag-IBIG", _data.PagIbig);

                    table.Cell().ColumnSpan(1).Background(Colors.Red.Lighten4).Padding(6)
                        .Text("Total Deductions").Bold();
                    table.Cell().Background(Colors.Red.Lighten4).Padding(6).AlignRight()
                        .Text($"₱{_data.TotalDeductions:N2}").Bold();
                });

                col.Item().PaddingVertical(16);

                // Net Pay
                col.Item().Background(Colors.Blue.Darken3).Padding(14).Row(row =>
                {
                    row.RelativeItem().Text("NET PAY").FontSize(16).Bold().FontColor(Colors.White);
                    row.RelativeItem().AlignRight()
                        .Text($"₱{_data.NetPay:N2}").FontSize(16).Bold().FontColor(Colors.White);
                });

                col.Item().PaddingVertical(16);

                // Note
                col.Item().Text("This is a system-generated payslip from MobileOps Connect ERP. If you have questions about your payslip, please contact your department manager or HR.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1).Italic();
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("MobileOps Connect ERP — ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.Span("Confidential").FontSize(8).Italic().FontColor(Colors.Grey.Medium);
            });
        }

        private static void AddRow(TableDescriptor table, string label, decimal amount)
        {
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(label);
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight()
                .Text($"₱{amount:N2}");
        }
    }
}
